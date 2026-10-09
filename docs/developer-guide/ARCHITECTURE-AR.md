# المعمارية — الطبقات وحدود الاعتماديات

> مرجع التصميم الكامل: [docs/architecture.md](../architecture.md). هذا الملف يصف **ما هو مبني فعلًا** عند الالتزام `7271ad6` وكيف تُفرض الحدود في الكود والاختبارات.

## 1. الصورة العامة

```
                        ┌──────────────────────────────┐
  المتصفح ──HTTPS──►    │ Onyx.Web  (MVC + Admin Area)  │  BFF: كوكي مشفّر يحمل الرموز
                        │  - الموقع العام /ar /en        │  لا قاعدة بيانات، لا Domain
                        │  - لوحة الإدارة /admin         │
                        └──────────────┬───────────────┘
                                       │ HTTPS REST (عملاء مكتوبون فقط)
                                       ▼  Bearer JWT للإدارة · X-Forwarded-For · X-Correlation-ID
                        ┌──────────────────────────────┐
                        │ Onyx.API  (Controllers v1)    │  نقطة الإنفاذ الوحيدة للصلاحيات
                        └──────────────┬───────────────┘
                                       │ Application services (Result<T>)
          ┌────────────────────────────┼─────────────────────────────┐
          ▼                            ▼                             ▼
   Onyx.Application  ──────►  Onyx.Domain          Onyx.Infrastructure (EF Core, Identity,
   (حالات الاستخدام، المنافذ)   (الكيانات، الحراس،      المستودعات، المعترضات، المحوّلات،
                               الأحداث)               SafeOutboundHttp، Google، MailKit…)
                                                              │
                                                              ▼
                                                   PostgreSQL (قاعدة Onyx المخصصة)
                                                              ▲
                        ┌──────────────────────────────┐      │
                        │ Onyx.Worker (BackgroundService)│─────┘  outbox · البريد · المهام المجدولة
                        └──────────────────────────────┘          (لا سطح HTTP عام)
```

مونوليث معياري واحد بمضيفين خلفيين (API وWorker) + Web. **لا خدمات مصغّرة.** النشر محمول بلا ارتباط بمزوّد استضافة.

## 2. المشاريع ومراجعها الفعلية (من ملفات csproj)

| المشروع | يرجع إلى | الحزم الرئيسية | لا يجوز أبدًا |
|---|---|---|---|
| [Onyx.Domain](../../src/Onyx.Domain/) | — | — | أي مشروع، EF، ASP.NET، Identity، SDK مزوّد |
| [Onyx.Contracts](../../src/Onyx.Contracts/) | — | — | أي مشروع أو حزمة غير BCL/DataAnnotations |
| [Onyx.Application](../../src/Onyx.Application/) | Domain، Contracts | FluentValidation (+DI) | Infrastructure/API/Web/Worker، EF، `Microsoft.AspNetCore.*`، أي SDK مزوّد |
| [Onyx.Infrastructure](../../src/Onyx.Infrastructure/) | Application، Domain | Npgsql EF، EF Relational/Design، Identity EF، JwtBearer، MailKit، System.ServiceModel.Syndication، Google.Apis.* | API/Web/Worker |
| [Onyx.API](../../src/Onyx.API/) | Application، Contracts، Infrastructure (**جذر التركيب فقط** — `Program`) | Asp.Versioning، OpenApi، SwaggerUI (Development)، Serilog | Web/Worker |
| [Onyx.Worker](../../src/Onyx.Worker/) | Application، Infrastructure (**جذر التركيب فقط**) | Serilog | API/Web |
| [Onyx.Web](../../src/Onyx.Web/) | Contracts | Http.Resilience، Net.Codecrete.QrCodeGenerator | Domain/Application/Infrastructure/API/Worker، EF، Npgsql |

> ملاحظة: `HtmlSanitizer` في قائمة الحزم المعتمدة في CLAUDE.md لكنه **غير مثبت** حاليًا — لا يوجد محتوى HTML مخزن (الأجسام كتل نصية منظمة).

**الفرض بالاختبارات** ([tests/Onyx.ArchitectureTests/](../../tests/Onyx.ArchitectureTests/)):
- [ProjectReferenceRulesTests](../../tests/Onyx.ArchitectureTests/ProjectReferenceRulesTests.cs): المراجع المسموحة فقط؛ Domain/Contracts بلا حزم؛ Application بلا ASP.NET؛ لا حزم مزوّدين حيث تُمنع.
- [TypeDependencyRulesTests](../../tests/Onyx.ArchitectureTests/TypeDependencyRulesTests.cs): على مستوى الأنواع (حتى لا تُستغل المراجع الانتقالية)؛ API وWorker يستخدمان Infrastructure من `Program` فقط؛ متحكمات API العامة وصفحات Web العامة لا تصل إلى `Intelligence` أو إدارة الأخبار؛ حزمة RSS في `Infrastructure.News` فقط؛ أنواع Google/Newtonsoft في `Infrastructure.Analytics` فقط.

## 3. تدفق الطلب القياسي

```
Controller (API)  — رفيع: [HasPermission]/[AllowAnonymous]، async + CancellationToken، يعيد ProblemDetails
  → Application service  — تحقق FluentValidation، قواعد التنسيق، تدقيق صريح، Result/Result<T>
     → Domain entity  — الانتقالات والحراس (لا يمكن تجاوزها)، رفع الأحداث
     → Repository interface (Application) → implementation (Infrastructure) → ApplicationDbContext → PostgreSQL
  → IUnitOfWork.SaveChangesAsync  — معاملة واحدة: البيانات + audit_logs + outbox_messages (+ email_messages)
Worker لاحقًا: outbox → معالجات الأحداث (بريد، إشعارات، فهرسة، IndexNow، حذف ملفات)
```

- المتحكمات لا تستخدم `DbContext` ولا المستودعات مباشرة.
- كيانات Domain لا تعبر حدود الـ API؛ كل الإدخال/الإخراج DTOs من Contracts.
- لا `IRepository<T>` عام ولا تسريب `IQueryable`.
- الأخطاء المتوقعة `Result` (لا استثناءات)؛ `ConcurrencyConflictException` للتزامن (→ 409).

## 4. جذور التركيب (Composition roots)

| المضيف | الملف | ما يسجّله |
|---|---|---|
| API | [Program.cs](../../src/Onyx.API/Program.cs) | `AddApplication` + `AddInfrastructure` + `AddOnyxIdentity` + `RequireTurnstileInProduction` + `AddOnyxApi`؛ أوامر المشغّل قبل تشغيل المضيف |
| Worker | [Program.cs](../../src/Onyx.Worker/Program.cs) | `AddApplication` + `AddInfrastructure` + `AddWorkerProcessing` |
| Web | [Program.cs](../../src/Onyx.Web/Program.cs) → [WebServiceCollectionExtensions](../../src/Onyx.Web/Configuration/WebServiceCollectionExtensions.cs)، [WebApplicationExtensions](../../src/Onyx.Web/Configuration/WebApplicationExtensions.cs) | التوطين، BFF، العملاء المكتوبون، المرشحات العامة (antiforgery، انتهاء الجلسة، عدم توفر الـ API، وسم التحليلات) |

نقاط التسجيل في الطبقات: [Onyx.Application/DependencyInjection.cs](../../src/Onyx.Application/DependencyInjection.cs) (`AddApplication` + مسح المعالجات تلقائيًا)، [Onyx.Infrastructure/DependencyInjection.cs](../../src/Onyx.Infrastructure/DependencyInjection.cs) (`AddInfrastructure` → `AddContent`، `AddSeo`، `AddLeads`، `AddNews`، `AddAnalytics`)، [IdentityServiceCollectionExtensions](../../src/Onyx.Infrastructure/IdentityServiceCollectionExtensions.cs) (API فقط)، [WorkerServiceCollectionExtensions](../../src/Onyx.Infrastructure/WorkerServiceCollectionExtensions.cs) (Worker فقط).

**تفصيل مهم:** `IDataSubjectRequestService` مسجّل مع الهوية (API فقط) لأنه يمحو حسابات الإدارة؛ الـ Worker لا يملكه.

## 5. المنافذ (Ports) والمحوّلات

| المنفذ (Application) | التنفيذ الحالي (Infrastructure ما لم يُذكر) | الحالة |
|---|---|---|
| `IUnitOfWork` | `ApplicationDbContext` | فعّال |
| `ICurrentUser`، `IRequestContext` | `HttpCurrentUser`/`HttpRequestContext` (API)، `SystemCurrentUser`/`SystemRequestContext` (Worker) | فعّال |
| `IAuditTrail`، `IAuditLogQueries` | `AuditTrail`، `AuditLogQueries` | فعّال |
| `IEmailOutbox`، `IEmailTemplateRenderer`، `IEmailSender` | `EmailOutbox`، `EmailTemplateRenderer`، `SmtpEmailSender`/`PickupDirectoryEmailSender`/`UnconfiguredEmailSender` | الناقل غير مهيأ افتراضيًا |
| `ICaptchaVerifier` | `TurnstileCaptchaVerifier` | يفشل مغلقًا دون سر؛ إلزامي في الإنتاج |
| `IMediaStorage`، `IImageInspector` | `LocalFileMediaStorage`، `ImageInspector` | فعّال |
| `IAvailabilityProvider`، `IBookingCalendarProvider` | `InternalAvailabilityProvider`، `InternalBookingCalendarProvider` (Application) | داخلي |
| `ISiteSearchService` | `PostgresSiteSearchService` | فعّال |
| `ISearchEngineNotifier` | `IndexNowNotifier` | غير نشط حتى الإعداد والإطلاق |
| `INewsFeedReader` | `SyndicationNewsFeedReader` (عبر `SafeOutboundHttpClient`) | فعّال |
| `INewsSummaryService` | `NotConfiguredNewsSummaryService` | **غير مهيأ** (وكيل AI خارجي لاحقًا) |
| `IWebAnalyticsProvider`، `ISearchPerformanceProvider` | `GoogleAnalyticsDataProvider`، `GoogleSearchConsoleProvider` | **غير مهيأ** حتى بيانات الاعتماد |
| `IJobTrigger` | `JobTrigger` | فعّال |
| `IIpAddressHasher` | `HmacIpAddressHasher` | لا يخزن IP دون مفتاح |
| `ITextGenerationService`، `IImageGenerationService` | **لا تنفيذ** | منافذ مستقبلية |
| `ISeoAnalysisEngine`، `ISecurityPostureEngine`، `IOpportunityEngine`، `IAdPlatformProvider` | **غير موجودة في الكود** | Phase 2/3 (موصوفة في CLAUDE.md فقط) |

## 6. Onyx.Web كـ BFF

- لا يملك قاعدة بيانات؛ كل البيانات من الـ API.
- العملاء المكتوبون (`OnyxApiClientBase`) هم المكان **الوحيد** لبناء طلبات HTTP؛ الإدارة تضيف `AccessTokenHandler`.
- الرموز داخل كوكي `__Host-onyx.admin` المشفّر (Data Protection) — لا تصل لـ JavaScript أبدًا.
- الـ API لا تسجّل CORS: المتصفح لا يستدعيها مباشرة.

## 7. الأحداث والاتساق

نمط **Transactional Outbox**: الأحداث تُكتب في `outbox_messages` في نفس معاملة التغيير (لا كتابة مزدوجة)، والـ Worker يسلّمها «مرة على الأقل» ⇒ معالجات متكررة الأمان (`IProcessedEventLog`). خريطة الأحداث كاملة في [BACKGROUND-JOBS-AR.md §4.3](BACKGROUND-JOBS-AR.md).

## 8. أين أضع كودًا جديدًا؟

| النوع | المكان |
|---|---|
| قاعدة عمل/حارس | كيان Domain (دالة سلوك تعيد `Result`) |
| حالة استخدام/تنسيق | خدمة Application في مجلد الميزة + مدقق FluentValidation |
| استعلام/كتابة قاعدة بيانات | واجهة مستودع في Application + تنفيذ في `Infrastructure/Persistence/Repositories/<Feature>/` |
| تكامل خارجي | منفذ في Application + محوّل في Infrastructure (+ موافقة حزمة) |
| نقطة نهاية | متحكم API + DTOs في Contracts |
| صفحة | متحكم/عرض Web + عميل API مكتوب |
| عمل طويل/دوري | `IScheduledJob` أو معالج حدث في الـ Worker |
