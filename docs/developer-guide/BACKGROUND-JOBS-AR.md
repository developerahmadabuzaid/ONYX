# المهام الخلفية والـ Outbox — Onyx.Worker

> المصدر الحاكم: [src/Onyx.Infrastructure/BackgroundJobs/](../../src/Onyx.Infrastructure/BackgroundJobs/) و[src/Onyx.Worker/Program.cs](../../src/Onyx.Worker/Program.cs) و[WorkerServiceCollectionExtensions.cs](../../src/Onyx.Infrastructure/WorkerServiceCollectionExtensions.cs).
> القرار المعماري: `BackgroundService` مدمجة + جداول قاعدة بيانات بعقود إيجار (leases). **لا Hangfire ولا Quartz.NET** (CLAUDE.md §3، §6).

## 1. ما الذي يعمل داخل Onyx.Worker

`Program.cs` في الـ Worker يبني `WebApplication` فقط ليعرض فحوص صحة داخلية (`/health/live`، `/health/ready`) على `Worker:HealthUrls` (افتراضيًا `http://127.0.0.1:5290`، أي loopback). لا يوجد أي سطح HTTP عام آخر.

يسجّل `AddWorkerProcessing` ثلاث خدمات مستضافة (Hosted Services) تشترك جميعها في حلقة [PollingBackgroundService](../../src/Onyx.Infrastructure/BackgroundJobs/PollingBackgroundService.cs):

| الخدمة المستضافة | ما تستدعيه | ماذا تعالج |
|---|---|---|
| `OutboxDispatcherService` | `OutboxProcessor.ProcessBatchAsync` | أحداث المجال المخزنة في `outbox_messages` |
| `EmailDispatcherService` | `EmailProcessor.ProcessBatchAsync` | الرسائل المنتظرة في `email_messages` |
| `JobSchedulerService` | `JobScheduler.SynchronizeDefinitionsAsync` ثم `RunDueJobsAsync` | المهام المجدولة `IScheduledJob` |

**سلوك الحلقة** (`PollingBackgroundService.ExecuteAsync`): ما دام هناك عمل (`RunOnceAsync` أعاد > 0) تستمر فورًا؛ وإلا تنام `Worker:PollIntervalSeconds` (افتراضي 5 ثوانٍ). أي استثناء غير متوقع يُسجَّل ثم تُستأنف الحلقة — لا يسقط المضيف أبدًا.

في الـ Worker يكون `ICurrentUser` = `SystemCurrentUser` (UserId = null، UserName = "system")، و`IRequestContext` = `SystemRequestContext` (بلا IP ولا correlation id). لذلك تظهر سجلات التدقيق الصادرة من الـ Worker بمستخدم فارغ.

## 2. خيارات الـ Worker (`Worker` في الإعدادات)

[WorkerOptions.cs](../../src/Onyx.Infrastructure/BackgroundJobs/WorkerOptions.cs) — مُتحقَّق منها عند الإقلاع:

| الخيار | الافتراضي | المدى | الأثر |
|---|---|---|---|
| `PollIntervalSeconds` | 5 | 1–300 | النوم عند الخمول |
| `LeaseSeconds` | 120 | 10–3600 | مدة قفل الرسالة/المهمة **وهي أيضًا مهلة تنفيذ المهمة** |
| `OutboxBatchSize` | 50 | 1–500 | |
| `EmailBatchSize` | 20 | 1–200 | |
| `MaxOutboxAttempts` | 10 | 1–50 | بعدها Dead-letter |
| `MaxEmailAttempts` | 8 | 1–50 | بعدها `DeadLettered` |
| `RetryBaseDelaySeconds` | 30 | | أساس التراجع الأُسّي |
| `RetryMaxDelayMinutes` | 60 | | سقف التراجع |
| `ProcessedOutboxRetentionDays` | 14 | 1–365 | تنظيف تشغيلي (لا بيانات شخصية) |
| `JobRunRetentionDays` | 30 | 1–365 | |

التراجع: [RetryBackoff.Delay(attempt, base, max)](../../src/Onyx.Application/Jobs/RetryBackoff.cs) = `base × 2^(attempt-1)` بسقف `max` (الأس محدود بـ 30 لتجنّب الفيضان).

## 3. المهام المجدولة المسجلة (7 مهام في الإنتاج)

> الاسم مخزَّن في `job_definitions.name` — **لا تُعِد تسمية مهمة موجودة أبدًا** (تعليق `IScheduledJob.Name`). الجدول الافتراضي يُستخدم فقط عند أول تسجيل؛ تعديل `DefaultSchedule` في الكود لا يغيّر صفًا موجودًا (`ON CONFLICT (name) DO NOTHING`).

| الاسم الثابت | الصنف | الجدول الافتراضي | ما تفعله | متى تُعيد `Skipped` | متى تفشل (تُطلق استثناء) |
|---|---|---|---|---|---|
| `operational-cleanup` | [OperationalCleanupJob](../../src/Onyx.Infrastructure/BackgroundJobs/OperationalCleanupJob.cs) | `daily@03:00` | يحذف رسائل outbox المسلَّمة الأقدم من `ProcessedOutboxRetentionDays`، وعلامات `processed_events` بنفس الحد، و`job_runs` المنتهية الأقدم من `JobRunRetentionDays`. لا يمس الرسائل الميتة (Dead-letter). | لا تتخطى أبدًا | خطأ قاعدة بيانات |
| `content-scheduling` | [ContentSchedulingJob](../../src/Onyx.Infrastructure/BackgroundJobs/ContentSchedulingJob.cs) | `every:1m` | ينشر المحتوى المجدول المستحق (`IContentSchedulingService.PublishDueAsync`) ويُلغي نشر ما حان وقت إزالته (`UnpublishDueAsync`). كل عنصر في نطاق DI ووحدة عمل منفصلة. فشل النشر → `HandleFailedPublicationAsync` (يعيد العنصر إلى Approved ويُشعر المحررين). تعارض التزامن يُتجاهل ويُعاد تقييمه في التشغيل التالي. | «Nothing is due.» | لا تفشل بسبب عنصر واحد؛ تعدّ الإخفاقات في الرسالة |
| `retention-enforcement` | [RetentionEnforcementJob](../../src/Onyx.Infrastructure/BackgroundJobs/RetentionEnforcementJob.cs) | `daily@02:30` | `IRetentionService.EnforceAsync` — يطبّق كل سياسة احتفاظ مفعّلة بدُفعات تُلتزم كل منها منفردة. | لا توجد سياسة مفعّلة بفترة | خطأ غير متوقع |
| `search-index-reconcile` | [SearchIndexReconcileJob](../../src/Onyx.Infrastructure/BackgroundJobs/SearchIndexReconcileJob.cs) | `every:30m` | `SearchIndexer.ReconcileAsync` + `NewsSearchIndexer.ReconcileAsync`: يملأ الفهرس أول مرة ويُصلح ما فات الأحداث. | الفهرس محدَّث | |
| `news-ingestion` | [NewsIngestionJob](../../src/Onyx.Infrastructure/BackgroundJobs/NewsIngestionJob.cs) | `every:15m` | لكل مصدر مستحق: `NewsIngestionService.IngestSourceAsync` في نطاق ومعاملة مستقلين، ضمن ميزانية زمنية (`News:RunBudgetMinutes`) وميزانية ملخصات (`NewsSummaryBudget`). ثم `ReportSummaryServiceAsync`. | لا مصدر مستحق | **فقط** إذا فشلت كل المصادر التي جُرّبت |
| `analytics-ga4-sync` | [WebAnalyticsSyncJob](../../src/Onyx.Infrastructure/BackgroundJobs/WebAnalyticsSyncJob.cs) | `daily@05:00` | `AnalyticsSyncService.SyncWebAnalyticsAsync` | المزوّد غير مهيأ أو معطَّل | حالة `Failed` من المزامنة → `AnalyticsJobOutcome.From` يرمي `InvalidOperationException` بالرمز الآمن فقط |
| `analytics-gsc-sync` | [SearchPerformanceSyncJob](../../src/Onyx.Infrastructure/BackgroundJobs/SearchPerformanceSyncJob.cs) | `daily@05:30` | `AnalyticsSyncService.SyncSearchPerformanceAsync` | نفس القاعدة | نفس القاعدة |

> الأسماء `analytics-ga4-sync` و`analytics-gsc-sync` ثوابت في `AnalyticsIntegrationService.WebAnalyticsJob` و`SearchPerformanceJob` ([src/Onyx.Application/Analytics/](../../src/Onyx.Application/Analytics/)).
> اختبار [JobSchedulingTests](../../tests/Onyx.API.IntegrationTests/Worker/JobSchedulingTests.cs) يتوقع **8** تعريفات في بيئة الاختبار (السبع أعلاه + مهمة اختبار `TestJob` بجدول `every:10m`). أي مهمة جديدة تتطلب تحديث هذا العدد والتأكيدات.

### 3.1 صيغة الجدول (In-house)

[JobSchedule.cs](../../src/Onyx.Application/Jobs/JobSchedule.cs):
- `every:<n>s|m|h` — فاصل ثابت، أدناه 10 ثوانٍ وأقصاه 7 أيام.
- `daily@HH:mm` — مرة يوميًا **بتوقيت UTC** (ليس توقيت الرياض). مثال: `daily@05:00` = 08:00 بتوقيت الرياض.
- `GetNextOccurrence(after)` يعيد الموعد التالي **بعد** اللحظة المعطاة حصرًا.

### 3.2 دورة حياة تشغيل مهمة — `JobScheduler`

```
JobSchedulerService (كل دورة)
  └─ SynchronizeDefinitionsAsync()  (مرة واحدة بنجاح؛ يُعاد إن فشل)
       INSERT … ON CONFLICT (name) DO NOTHING   ← آمن متعدد النسخ
  └─ RunDueJobsAsync()
       ClaimDueAsync(): UPDATE job_definitions SET locked_by, locked_until_utc
                        WHERE id IN (SELECT … is_enabled AND next_run_at_utc <= now
                                     AND (lease فارغ أو منتهٍ) ORDER BY next_run_at_utc
                                     LIMIT 10 FOR UPDATE SKIP LOCKED) RETURNING *
       لكل مهمة مُطالَب بها → RunAsync(definition)
           1. إدراج JobRun بحالة Running (التزام فوري)
           2. نطاق DI جديد + CancellationToken مربوط بمهلة = Lease
           3. job.ExecuteAsync() → Succeeded/Skipped
              استثناء → Failed + رسالة "Type: Message" (أو "Timed out.") + إشعار JobFailed
              إيقاف الـ Worker → Failed "Interrupted by Worker shutdown." دون تقديم الجدول
           4. FinishRunAsync (ExecuteUpdateAsync)
           5. ReleaseAsync: next_run = schedule.GetNextOccurrence(finishedAt)
              (جدول غير صالح في القاعدة → +1 ساعة + إشعار JobFailed)
              التحديث «مُسيَّج» بـ locked_by = هذا الـ Worker
```

- **حصرية التنفيذ:** `FOR UPDATE SKIP LOCKED` + عقد إيجار مع `WorkerIdentity.InstanceId` (`Machine/PID/Guid`). نسخة قديمة فقدت عقدها لا تستطيع إتمام العمل لأن كل تحديث مشروط بـ `LockedBy == owner`.
- **إشعار الفشل:** `NotificationType.JobFailed`، خطورة Warning، صلاحية `System.Jobs.View`، معامل `job`.
- **تشغيل فوري يدوي:** [JobTrigger.RequestRunAsync(jobName)](../../src/Onyx.Infrastructure/BackgroundJobs/JobTrigger.cs) يضبط `next_run_at_utc = now` لمهمة مفعّلة فقط ويعيد `false` إن لم تكن مسجلة/مفعّلة. لا يُنفذ أي شيء داخل الـ API. المستخدم الوحيد حاليًا: `AnalyticsIntegrationService.RequestSyncAsync`.

### 3.3 قواعد كتابة مهمة جديدة

1. نفّذ `IScheduledJob` في `Onyx.Infrastructure/BackgroundJobs/` (أو في Application إن لم تحتج بنية تحتية)، وفوّض العمل لخدمة Application.
2. اجعلها **Idempotent**: قد تُعاد بعد انهيار أو انتهاء عقد.
3. أعد `JobOutcome.Skipped(reason)` عندما لا يوجد عمل أو المزوّد غير مهيأ؛ ارمِ استثناءً للفشل الحقيقي.
4. سجّلها في `AddWorkerProcessing` بـ `services.AddScoped<IScheduledJob, …>()`.
5. حدّث `JobSchedulingTests` (العدد + التأكيد على الاسم والجدول) وهذا الملف.
6. اختر جدولًا بتوقيت UTC، وتذكّر أن `LeaseSeconds` هو المهلة القصوى للتنفيذ.

## 4. الـ Outbox وأحداث المجال

### 4.1 الكتابة (داخل نفس المعاملة)

ترتيب المعترضات في `AddInfrastructure` مهم ومقصود:
1. [AuditFieldsInterceptor](../../src/Onyx.Infrastructure/Persistence/Interceptors/AuditFieldsInterceptor.cs) — يختم `CreatedAtUtc/ById` و`UpdatedAtUtc/ById`، ويحوّل حذف `ISoftDeletable` إلى تعديل.
2. [AuditLogInterceptor](../../src/Onyx.Infrastructure/Persistence/Interceptors/AuditLogInterceptor.cs) — يكتب `AuditLog` لكل تغيير على `IAuditable`.
3. [DomainEventOutboxInterceptor](../../src/Onyx.Infrastructure/Persistence/Interceptors/DomainEventOutboxInterceptor.cs) — يحوّل كل `IDomainEvent` مرفوع إلى صف `OutboxMessage` (النوع = الاسم الكامل للصنف، الحمولة = JSON). تُمسح الأحداث من الكيانات **بعد نجاح الحفظ فقط**؛ فشل الحفظ يفصل الرسائل المعلّقة فلا تكرار ولا ضياع.

### 4.2 التسليم — [OutboxProcessor](../../src/Onyx.Infrastructure/BackgroundJobs/OutboxProcessor.cs)

- يطالب بدفعة بـ `FOR UPDATE SKIP LOCKED` (غير معالَجة، غير ميتة، `next_attempt_at_utc` حان، عقد فارغ أو منتهٍ) مرتبة بـ `occurred_at_utc`.
- يحلّ النوع عبر [DomainEventTypeRegistry](../../src/Onyx.Infrastructure/BackgroundJobs/DomainEventTypeRegistry.cs) — **قائمة سماح** من تجميعة Domain فقط؛ لا يُمرَّر اسم من قاعدة البيانات إلى `Type.GetType`. نوع مجهول → Dead-letter فورًا.
- يفكّ الحمولة ويستدعي كل معالج مسجّل عبر [DomainEventDispatcher](../../src/Onyx.Infrastructure/BackgroundJobs/DomainEventDispatcher.cs) في نطاق DI جديد.
- نجاح → `processed_at_utc`. فشل → `attempt_count+1`، `last_error` (حتى 4000 حرف)، `next_attempt_at_utc` بالتراجع الأسي. عند بلوغ `MaxOutboxAttempts` → `dead_lettered_at_utc` + إشعار `OutboxMessageFailed` (Critical، `System.Jobs.View`).
- **التسليم At-least-once**: يجب أن تكون المعالجات Idempotent. المعالجات ذات الأثر الجانبي (رسائل بريد، إشعارات) تستخدم [IProcessedEventLog.TryBeginAsync(key)](../../src/Onyx.Application/Abstractions/Messaging/IProcessedEventLog.cs): تضيف علامة `processed_events` في نفس وحدة العمل، ومفتاح فريد يمنع التزام تسليمين متزامنين.

### 4.3 خريطة الأحداث → المعالجات (متحقق منها بالبحث في الكود)

| الحدث (Domain) | يرفعه | المعالج (Application) | الأثر |
|---|---|---|---|
| `ContentPublicationChanged(ContentItemId, IsLive)` | `ContentItem.GoLive` / `ContentItem.Unpublish` | [SearchIndexOnPublication](../../src/Onyx.Application/Search/SearchIndexOnPublication.cs) | إعادة فهرسة/إزالة المستند من فهرس البحث |
| `PublicUrlsChanged(SourceId, Paths)` | `ContentItem.AnnouncePublicUrls`، `Redirect.Update`، `Redirect.AnnounceRemoval` | [SearchIndexOnUrlChanges](../../src/Onyx.Application/Search/SearchIndexOnUrlChanges.cs)، [PublicUrlsChangedHandler](../../src/Onyx.Application/Seo/PublicUrlsChangedHandler.cs) | تحديث روابط الفهرس؛ إبلاغ IndexNow (غير نشط حتى يُهيأ) |
| `InquirySubmitted(InquiryId, LeadId)` | `Inquiry` (مصنع الإنشاء) | [InquirySubmittedHandler](../../src/Onyx.Application/Leads/InquirySubmittedHandler.cs) | بريد للزائر + بريد داخلي + إشعار NewLead |
| `BookingConfirmed(BookingId, LeadId, RescheduledFromId)` | `ConsultationBooking` | [BookingConfirmedHandler](../../src/Onyx.Application/Booking/BookingConfirmedHandler.cs) | بريد تأكيد مع ‎.ics + بريد داخلي + إشعار |
| `BookingCancelled(BookingId, LeadId, ByVisitor)` | `ConsultationBooking.Cancel…` | [BookingCancelledHandler](../../src/Onyx.Application/Booking/BookingCancelledHandler.cs) | بريد إلغاء + إشعار |
| `MediaAssetDeleted(MediaAssetId, StorageKey)` | `MediaAsset.MarkDeleted` | [MediaStorageCleanupHandler](../../src/Onyx.Application/Media/MediaStorageCleanupHandler.cs) | حذف الملف من التخزين بعد التزام حذف الصف |
| `NewsItemPublicationChanged(NewsItemId)` | عدة انتقالات في `NewsItem` | [SearchIndexOnNewsPublication](../../src/Onyx.Application/News/SearchIndexOnNewsPublication.cs) | فهرسة/إزالة الخبر |
| `NewsSourceFailing(NewsSourceId, SourceName)` | `NewsSource` عند بدء الفشل | [NewsSourceFailingHandler](../../src/Onyx.Application/News/NewsSourceFailingHandler.cs) | إشعار `NewsIngestionFailed` |
| `ProviderConnectionFailed(ProviderConnectionId, ProviderKey)` | `ProviderConnection.RecordFailure` (عند **الانتقال** إلى Error فقط) | [ProviderConnectionFailedHandler](../../src/Onyx.Application/Providers/ProviderConnectionFailedHandler.cs) | إشعار `IntegrationFailed` (Critical، `ProviderConnections.Manage`) |

تسجيل المعالجات تلقائي: `AddDomainEventHandlers` في [Onyx.Application/DependencyInjection.cs](../../src/Onyx.Application/DependencyInjection.cs) يمسح التجميعة عن كل صنف ينفذ `IDomainEventHandler<>`.

## 5. إرسال البريد — [EmailProcessor](../../src/Onyx.Infrastructure/BackgroundJobs/EmailProcessor.cs)

| الحالة | السلوك |
|---|---|
| الناقل غير مهيأ (`IEmailSender.IsConfigured = false`) | لا يطالب بأي رسالة؛ تبقى `Pending`. يُبلَّغ `ProviderConnection` بـ NotConfigured **مرة واحدة** لكل عملية. |
| نجاح الإرسال | `Status = Sent`، `SentAtUtc`، تصفير العقد. ثم `ReportSuccessAsync` للمزوّد. |
| `EmailTransportException` بنوع `ProviderUnavailable` | تأجيل **كل** الرسائل المتبقية في الدفعة دون استهلاك محاولات (لا «عاصفة» رسائل ميتة)، و`ReportFailureAsync`. |
| رفض خاص بالرسالة | محاولة + تراجع أسي؛ عند `MaxEmailAttempts` → `DeadLettered` + إشعار `EmailDeliveryFailed` (Warning، `System.Jobs.View`). يُخزَّن وصف آمن فقط (لا رسائل استثناء المزوّد). |

اختيار الناقل في `AddWorkerProcessing` حسب `Email:Transport`: `Smtp` → `SmtpEmailSender` (MailKit)، `PickupDirectory` → `PickupDirectoryEmailSender` (للتطوير)، غير ذلك → `UnconfiguredEmailSender`. التفاصيل في [features/12-email-notifications-AR.md](features/12-email-notifications-AR.md).

## 6. استكشاف الأخطاء

| العَرَض | أين تنظر |
|---|---|
| مهمة لا تعمل | `job_definitions`: هل `is_enabled`؟ ما `next_run_at_utc`؟ هل `locked_until_utc` في المستقبل (Worker آخر يملكها)؟ |
| مهمة تفشل باستمرار | `job_runs.message` (حتى 500 حرف) + إشعار JobFailed + سجلات Serilog «Scheduled job {JobName} failed» |
| حدث لم يُعالَج | `outbox_messages`: `attempt_count`، `last_error`، `dead_lettered_at_utc` |
| بريد لم يصل | `email_messages.status` و`last_error`؛ وحالة `provider_connections` لمفتاح `email.smtp`/`email.pickup-directory` |
| تغيير جدول مهمة موجودة | عدّل صف `job_definitions.schedule` (لا يكفي تغيير الكود). لا توجد واجهة إدارة لذلك حاليًا — غير مؤكد — يحتاج مراجعة. |

## 7. الاختبارات ذات الصلة

| الاختبار | ما يتحقق منه |
|---|---|
| [tests/Onyx.API.IntegrationTests/Worker/](../../tests/Onyx.API.IntegrationTests/Worker/) | الجدولة، عقود الإيجار، الـ outbox، البريد (انظر [TESTING-AR.md](TESTING-AR.md)) |
| [JobSchedulingTests](../../tests/Onyx.API.IntegrationTests/Worker/JobSchedulingTests.cs) | تسجيل التعريفات مرة واحدة متعدد النسخ، العدد 8، أسماء وجداول كل مهمة |
| `Onyx.Application.Tests` (JobSchedule/RetryBackoff) | تحليل صيغ الجدول والتراجع |
