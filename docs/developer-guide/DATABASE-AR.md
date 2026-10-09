# قاعدة البيانات — الكيانات والجداول والترحيلات

> المصدر الحاكم: [ApplicationDbContext.cs](../../src/Onyx.Infrastructure/Persistence/ApplicationDbContext.cs)، الإعدادات في [Persistence/Configurations/](../../src/Onyx.Infrastructure/Persistence/Configurations/)، واللقطة [ApplicationDbContextModelSnapshot.cs](../../src/Onyx.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs).
> العدد عند `7271ad6`: **68 جدولًا** و12 ترحيلًا. قاعدة PostgreSQL مخصصة لـ Onyx؛ لا SQL Server إطلاقًا.

## 1. اصطلاحات ثابتة (لا تكسرها)

| الاصطلاح | التنفيذ | لماذا |
|---|---|---|
| أسماء snake_case لكل جدول/عمود/مفتاح/فهرس | `ApplyOnyxConventions` → [SnakeCaseNaming.ToSnakeCase](../../src/Onyx.Infrastructure/Persistence/Conventions/SnakeCaseNaming.cs) يُطبَّق **آخرًا** في `OnModelCreating` | تسمية PostgreSQL أصيلة دون حزمة خارجية |
| معرّفات UUIDv7 | [Entity](../../src/Onyx.Domain/Common/Entity.cs) يولّد `Guid.CreateVersion7()` في المُنشئ | يمكن الإشارة للكيان قبل حفظه (أحداث، تدقيق)، وترتيب زمني يحسّن محلية الفهارس. القيم غير القابلة للتخمين (رموز) تُولَّد عشوائيًا منفصلة |
| الوقت `timestamptz` UTC | `DateTimeOffset` + `TimeProvider`؛ مصانع مثل `AuditLog.Record` ترفض offset غير صفري | |
| التزامن المتفائل | خاصية `uint Version` مربوطة بعمود النظام `xmin` في PostgreSQL على الكيانات المحرَّرة إداريًا (ContentItem، CallToAction، ProviderConnection، AnalyticsSyncState…) | التعارض → `ConcurrencyConflictException` → HTTP 409 |
| أعمدة `jsonb` | `HasJsonColumn<T>()` في [JsonColumnExtensions](../../src/Onyx.Infrastructure/Persistence/JsonColumnExtensions.cs) (مقارنة بالقيمة المسلسلة، enums كنص) | أجسام المحتوى، النصوص المحلية، لقطات المراجعات، حمولة الـ outbox |
| الحذف الناعم | أي كيان ينفذ `ISoftDeletable` يأخذ فلتر استعلام عام `!IsDeleted` تلقائيًا (`ApplySoftDeleteFilters`)، و`AuditFieldsInterceptor` يحوّل الحذف إلى تعديل | |
| enums كنص | تُخزن بالاسم؛ «أضف قيمًا جديدة فقط ولا تُعِد الترقيم/التسمية» (تعليقات `AuditAction`، `NotificationType`، `ContentType`…) | إعادة التسمية تكسر صفوفًا موجودة |
| جدول تاريخ الترحيلات | `__ef_migrations_history` (`ApplicationDbContext.MigrationsHistoryTable`) | |
| الامتدادات | `pg_trgm` يُفعَّل بالترحيل (`HasPostgresExtension("pg_trgm")`) للبحث | أي امتداد جديد يحتاج موافقة (CLAUDE.md §19) |

ترجمة أخطاء القيود الفريدة إلى تعارضات مفهومة داخل `ApplicationDbContext.SaveChangesAsync`:

| القيد | يتحول إلى | المستهلك |
|---|---|---|
| `DbUpdateConcurrencyException` (xmin) | `ConcurrencyConflictException` | كل الخدمات → 409 |
| `FeaturedProjectConfiguration.SlotIndex` / `ContentIndex` | تعارض «featured projects» | `ContentProofService.SetFeaturedAsync` |
| `ix_consultation_bookings_confirmed_slot` | تعارض برمز `BookingService.SlotTakenCode` | `BookingService` → «الموعد غير متاح» |
| `ix_news_items_original_url_hash`، `ix_news_sources_feed_url`، فهارس slug للأخبار | تعارض «news item/feed/slug» | الاستيعاب يتخطى في المحاولة التالية |

## 2. تاريخ الترحيلات

كلها في [src/Onyx.Infrastructure/Persistence/Migrations/](../../src/Onyx.Infrastructure/Persistence/Migrations/). تُطبَّق من خط النشر فقط (لا ترحيل تلقائي في الإنتاج — CLAUDE.md §19).

| الترحيل | المرحلة | ما يضيفه (ملخص) |
|---|---|---|
| `20261003175930_InitialFoundation` | 1–2 | `audit_logs`، `outbox_messages` |
| `20261003212055_WorkerMessagingNotifications` | 2 | `email_messages`، `job_definitions`، `job_runs`، `admin_notifications`، `notification_read_states`، `provider_connections`؛ وأعمدة عقد الإيجار في `outbox_messages` (`locked_by`، `locked_until_utc`، `next_attempt_at_utc`، `dead_lettered_at_utc`) |
| `20261003215351_IdentityAndSessions` | 3 | جداول Identity (`users`، `roles`، `user_roles`، `user_claims`، `user_logins`، `user_tokens`، `role_claims`)، `role_permissions`، `user_sessions`، `refresh_tokens`، `login_challenges`، `data_protection_keys` |
| `20261003222707_CookieConsentReceipts` | 3–4 | `cookie_consent_receipts` |
| `20261004114859_CmsContentFoundation` | 6 | 23 جدولًا: `content_items`، `content_translations`، `content_revisions`، `content_paths`، `redirects`، `content_preview_tokens`، `call_to_actions`، `cta_defaults`، `content_relations`، `content_clusters`، `content_cluster_members`، `media_assets`، `media_usages`، `brand_kits`، `knowledge_entries`، `information_requests`، `keyword_targets`، `competitor_references`، `seo_recommendations`، `seo_change_observations`، `content_performance_snapshots`، `content_calendar_entries`، `email_template_copies` |
| `20261004210857_SeoFoundationHardening` | 6 | عمود واحد: `content_translations.live_content_modified_at_utc` (يقود `lastmod` في خريطة الموقع) |
| `20261005100026_WorkProofInsights` | 7 | `featured_projects`، `proof_items`، `authors`؛ وأعمدة `content_items`: `author_id`، `insight_kind`، `origin`، `proof_verification` |
| `20261005112251_Stage8LeadsAndBooking` | 8 | `inquiries`، `consent_records`، `leads`، `lead_notes`، `lead_activities`، `booking_settings`، `availability_rules`، `availability_exceptions`، `consultation_bookings`، `processed_events`؛ وعمود `email_messages.attachments` (لملف ‎.ics) |
| `20261005182913_PrivacyOperations` | 9 | `data_subject_requests`، `retention_policies`؛ وعمود `erased_at_utc` في `leads` و`inquiries` |
| `20261007114401_SiteSearch` | 10 | `search_documents` (tsvector + GIN + `pg_trgm`) |
| `20261007135602_NewsHub` | 11 | `news_sources`، `news_categories`، `news_category_translations`، `news_items`، `news_item_translations` |
| `20261007172319_AnalyticsAggregates` | 12 | `web_analytics_daily_metrics`، `search_performance_daily_metrics`، `analytics_sync_states` (إضافي فقط) |

> لمحتوى كل ترحيل بالضبط اقرأ ملف `*.cs` الخاص به؛ الجدول أعلاه يُلخّص الجداول الجديدة فقط.

## 3. الجداول حسب المجال

العمود «الكيان» هو صنف Domain (أو Infrastructure للجداول التشغيلية). «فريد» = الفهارس الفريدة الفعلية من اللقطة.

### 3.1 البنية التحتية والتشغيل

| الجدول | الكيان | فريد / ملاحظات |
|---|---|---|
| `audit_logs` | [AuditLog](../../src/Onyx.Domain/Auditing/AuditLog.cs) | إلحاق فقط؛ لا تحديث ولا حذف عبر التطبيق |
| `outbox_messages` | [OutboxMessage](../../src/Onyx.Infrastructure/Persistence/Outbox/OutboxMessage.cs) | قفل بعقد (`locked_by`، `locked_until_utc`)، `dead_lettered_at_utc` |
| `processed_events` | [ProcessedEvent](../../src/Onyx.Infrastructure/Persistence/Outbox/ProcessedEvent.cs) | `ix_processed_events_key` (Key) — يمنع ازدواج آثار المعالجات |
| `job_definitions` / `job_runs` | [JobDefinition](../../src/Onyx.Infrastructure/BackgroundJobs/JobDefinition.cs) / [JobRun](../../src/Onyx.Infrastructure/BackgroundJobs/JobRun.cs) | `ix_job_definitions_name` |
| `email_messages` | `EmailMessage` ([Infrastructure/Email](../../src/Onyx.Infrastructure/Email/)) | الحالة Pending/Sent/DeadLettered؛ تخضع للاحتفاظ |
| `admin_notifications` / `notification_read_states` | [AdminNotification](../../src/Onyx.Domain/Notifications/AdminNotification.cs) / [NotificationReadState](../../src/Onyx.Domain/Notifications/NotificationReadState.cs) | `read_state` → Cascade مع الإشعار |
| `provider_connections` | [ProviderConnection](../../src/Onyx.Domain/Providers/ProviderConnection.cs) | `ix_provider_connections_provider_key`؛ لا أسرار |
| `data_protection_keys` | `DataProtectionKeyRecord` | حلقة مفاتيح Data Protection للـ API (رموز 2FA المشفرة) |

### 3.2 الهوية

| الجدول | الكيان | علاقات/فريد |
|---|---|---|
| `users`، `roles`، `user_roles`، `user_claims`، `user_logins`، `user_tokens`، `role_claims` | `ApplicationUser`، `ApplicationRole` (Identity) | `user_name_index`، `role_name_index`؛ Cascade إلى المستخدم/الدور |
| `role_permissions` | `RolePermission` | → `roles` (Cascade) |
| `user_sessions` | `UserSession` | → `users` (Cascade) |
| `refresh_tokens` | `RefreshToken` | → `user_sessions` (Cascade)؛ `ix_refresh_tokens_token_hash` (التجزئة فقط) |
| `login_challenges` | `LoginChallenge` | → `users` (Cascade)؛ `ix_login_challenges_token_hash` |

### 3.3 المحتوى (CMS) والإثبات والرؤى

| الجدول | الكيان | علاقات / فريد |
|---|---|---|
| `content_items` | [ContentItem](../../src/Onyx.Domain/Content/ContentItem.cs) | `ix_content_items_key` (حيث `key IS NOT NULL`)؛ FK ذاتي `parent_id` و`offline_redirect_to_id` (Restrict)؛ `author_id` → authors (Restrict)؛ `featured_image_id` → media (Restrict)؛ `primary_cta_id` → CTAs (SetNull) |
| `content_translations` | [ContentTranslation](../../src/Onyx.Domain/Content/ContentTranslation.cs) | (ContentItemId, LanguageCode) فريد؛ **(LanguageCode, SlugScope, Slug) فريد** = تفرد الـ slug لكل لغة ونطاق؛ Cascade مع العنصر |
| `content_revisions` | [ContentRevision](../../src/Onyx.Domain/Content/ContentRevision.cs) | (ContentItemId, Number) فريد؛ لقطة `jsonb` غير قابلة للتعديل |
| `content_paths` | [ContentPath](../../src/Onyx.Domain/Content/ContentPath.cs) | (LanguageCode, Path) فريد — المسار ملك عنصر واحد للأبد؛ (ContentItemId, LanguageCode) فريد حيث `is_current` — مسار حالي واحد لكل لغة |
| `redirects` | [Redirect](../../src/Onyx.Domain/Content/Redirect.cs) | (LanguageCode, FromPath) فريد؛ `target_content_item_id` (Restrict) |
| `content_preview_tokens` | [ContentPreviewToken](../../src/Onyx.Domain/Content/ContentPreviewToken.cs) | `token_hash` فريد (SHA-256 فقط) |
| `call_to_actions` / `cta_defaults` | [CallToAction](../../src/Onyx.Domain/Content/CallToAction.cs) / [CtaDefault](../../src/Onyx.Domain/Content/CtaDefault.cs) | `key` فريد؛ `cta_defaults.content_type` فريد (null = الافتراضي العام) |
| `content_relations` | [ContentRelation](../../src/Onyx.Domain/Content/ContentRelation.cs) | (From, To, Kind) فريد؛ Cascade للطرفين |
| `content_clusters` / `content_cluster_members` | [ContentCluster](../../src/Onyx.Domain/Content/ContentCluster.cs) / `ContentClusterMember` | (ClusterId, ContentItemId) فريد |
| `media_assets` / `media_usages` | [src/Onyx.Domain/Media/](../../src/Onyx.Domain/Media/) | `sha256` و`storage_key` فريدان؛ الاستخدام (Media, Item, Lang, Context, IsLive) فريد؛ `media_asset_id` Restrict (لا حذف لوسائط مستخدمة) |
| `brand_kits` | [src/Onyx.Domain/Brand/](../../src/Onyx.Domain/Brand/) | `official_logo_media_id` → media (Restrict) |
| `featured_projects` | [FeaturedProject](../../src/Onyx.Domain/Proof/) | `slot` فريد و`content_item_id` فريد — الحد الأقصى 3 مُطبَّق بالجدول نفسه |
| `proof_items` | [ProofItem](../../src/Onyx.Domain/Proof/) | → content_items (Cascade) |
| `authors` | [Author](../../src/Onyx.Domain/Insights/) | |
| `email_template_copies` | [EmailTemplateCopy](../../src/Onyx.Domain/Messaging/EmailTemplateCopy.cs) | (TemplateKey, LanguageCode) فريد |

### 3.4 المعرفة والذكاء التحريري (داخلي)

| الجدول | الكيان | علاقات / فريد |
|---|---|---|
| `knowledge_entries` | [src/Onyx.Domain/Knowledge/](../../src/Onyx.Domain/Knowledge/) | |
| `information_requests` | نفس المجلد | → content_items وknowledge_entries (SetNull) |
| `keyword_targets` | [src/Onyx.Domain/Intelligence/](../../src/Onyx.Domain/Intelligence/) | (LanguageCode, NormalizedKeyword, TargetMarket) فريد؛ → cluster/content (SetNull) |
| `competitor_references` | نفس المجلد | `domain` فريد |
| `seo_recommendations` / `seo_change_observations` | نفس المجلد | الملاحظات → التوصية (Cascade) |
| `content_performance_snapshots` | نفس المجلد | → content_items (Cascade)؛ تُملأ أسبوعيًا من مزامنة التحليلات |
| `content_calendar_entries` | نفس المجلد | → cluster/linked content/target service (SetNull) |

### 3.5 العملاء المحتملون والحجز

| الجدول | الكيان | علاقات / فريد |
|---|---|---|
| `inquiries` | [Inquiry](../../src/Onyx.Domain/Leads/) | → leads (Restrict)؛ غير قابل للتعديل بعد الإنشاء |
| `consent_records` | `ConsentRecord` | `inquiry_id` فريد (سجل موافقة واحد لكل استفسار)؛ → leads (Restrict) |
| `leads` / `lead_notes` / `lead_activities` | `Lead` وتوابعه | `owner_id` → users (SetNull)؛ الملاحظات والأنشطة Cascade |
| `booking_settings` | [src/Onyx.Domain/Booking/](../../src/Onyx.Domain/Booking/) | `key` فريد (صف إعدادات واحد) |
| `availability_rules` / `availability_exceptions` | نفس المجلد | `availability_exceptions.date` فريد |
| `consultation_bookings` | `ConsultationBooking` | **`start_utc` فريد حيث `status = 'Confirmed'`** = منع الحجز المزدوج على مستوى القاعدة؛ → inquiries/leads (Restrict) |

### 3.6 الخصوصية والبحث والأخبار والتحليلات

| الجدول | الكيان | علاقات / فريد |
|---|---|---|
| `cookie_consent_receipts` | [CookieConsentReceipt](../../src/Onyx.Domain/Privacy/) | مجهول؛ لكل اختيار صف جديد |
| `data_subject_requests` | `DataSubjectRequest` | |
| `retention_policies` | `RetentionPolicy` | `category` فريد |
| `search_documents` | `SearchDocument` ([Infrastructure/Search](../../src/Onyx.Infrastructure/Search/)) | (ContentItemId, LanguageCode) فريد؛ يحوي المحتوى المنشور العام فقط |
| `news_sources` | [src/Onyx.Domain/News/](../../src/Onyx.Domain/News/) | `feed_url` فريد؛ `default_category_id` (Restrict) |
| `news_categories` / `news_category_translations` | نفس المجلد | `key` فريد؛ (Lang, Slug) و(Category, Lang) فريدان |
| `news_items` / `news_item_translations` | نفس المجلد | `original_url_hash` فريد (منع التكرار)؛ (Lang, Slug) و(Item, Lang) فريدان؛ source/category Restrict |
| `web_analytics_daily_metrics` | [WebAnalyticsDailyMetric](../../src/Onyx.Domain/Analytics/) | (ProviderKey, Date, Kind, DimensionHash) فريد؛ `content_item_id` SetNull |
| `search_performance_daily_metrics` | `SearchPerformanceDailyMetric` | نفس مفتاح التفرد؛ SetNull |
| `analytics_sync_states` | `AnalyticsSyncState` | `provider_key` فريد؛ xmin |

## 4. سلوكيات الحذف المهمة (OnDelete)

- **Cascade** من `content_items` إلى: الترجمات، المراجعات، المسارات، رموز المعاينة، العلاقات، أعضاء العناقيد، استخدامات الوسائط، `featured_projects`، `proof_items`، لقطات الأداء. أي حذف دائم لعنصر محتوى يمحو كل ذلك — لذلك `DeletePermanentAsync` يتطلب تأكيدًا ويحافظ على إجابة الروابط القديمة (اختبار `Permanent_deletion_needs_confirmation_and_keeps_former_urls_answering`).
- **Restrict** يمنع حذف: وسائط مستخدمة (`media_usages`، `featured_image_id`، صور OG، شعار الهوية)، مؤلف مرتبط بمحتوى، عنصر أب، هدف تحويل، مصدر/فئة أخبار عليها عناصر، Lead له استفسارات/حجوزات.
- **SetNull** للمراجع الاختيارية (CTA أساسي، مالك Lead، روابط التقويم والتوصيات والكلمات، محتوى صفوف التحليلات).

## 5. قواعد تعديل المخطط

1. عدّل الكيان في Domain والإعداد في `Configurations/…` (مساحة الاسم يجب أن تبدأ بـ `Onyx.Infrastructure.Persistence.Configurations.Application` ليُلتقط تلقائيًا — انظر `ApplyConfigurationsFromAssembly` في `OnModelCreating`). **ملاحظة:** إعدادات التحليلات موجودة فعليًا في مجلد `Configurations/Analytics/` لكن بمساحة الاسم `…Configurations.Application`.
2. أنشئ الترحيل **بعد موافقة المالك** فقط (CLAUDE.md §19): `dotnet ef migrations add <Name> --project src/Onyx.Infrastructure --startup-project src/Onyx.Infrastructure`.
3. ترحيلات إضافية (additive) قدر الإمكان؛ لا `EnsureCreated` للتطبيق.
4. شغّل `has-pending-model-changes` وكل اختبارات التكامل (تُطبق الترحيلات الحقيقية على قاعدة مؤقتة).
5. إن أضفت بيانات شخصية: أضف فئة احتفاظ (`RetentionCategory`، إلحاق فقط) وغطِّها في [PersonalDataStore](../../src/Onyx.Infrastructure/Privacy/PersonalDataStore.cs) للتصدير والمحو.
6. حدّث هذا الملف.
