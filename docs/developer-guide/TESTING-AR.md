# دليل الاختبارات والتحقق

> الحالة عند الالتزام `7271ad6`: **1070 حالة اختبار** (633 دالة `[Fact]/[Theory]`؛ الـ Theory تتوسع لعدة حالات)، 0 فاشلة، 0 متخطاة عند ضبط PostgreSQL المحلي.
> التوزيع: Domain 170 · Application 158 · Architecture 37 · Web 143 · API.IntegrationTests 562.

## 1. مشاريع الاختبار ودور كل منها

| المشروع | ماذا يختبر | يحتاج قاعدة بيانات؟ | البنية المساعدة |
|---|---|---|---|
| [Onyx.Domain.Tests](../../tests/Onyx.Domain.Tests/) | قواعد الكيانات والحراس (الإثبات، اعتماد AI، الحد الأدنى للصناعات، الحجز المزدوج، الـ slug، الموافقة…) | لا | لا شيء — اختبارات وحدات صرفة |
| [Onyx.Application.Tests](../../tests/Onyx.Application.Tests/) | قواعد الخدمات النقية (جدولة المهام، التراجع، توليد ICS/CSV، حساب الفترات الزمنية، تطبيع البحث، قواعد الأخبار والتحليلات، كتالوج الصلاحيات) | لا | — |
| [Onyx.ArchitectureTests](../../tests/Onyx.ArchitectureTests/) | حدود الاعتماديات (CLAUDE.md §5) على مستوى المشروع والنوع، وعزل حزم Google/RSS | لا | NetArchTest |
| [Onyx.Web.Tests](../../tests/Onyx.Web.Tests/) | الموقع العام + لوحة الإدارة عبر `WebApplicationFactory` مع API مزيّفة | لا | [OnyxWebFactory](../../tests/Onyx.Web.Tests/Infrastructure/OnyxWebFactory.cs)، [FakeOnyxApi](../../tests/Onyx.Web.Tests/Infrastructure/FakeOnyxApi.cs) |
| [Onyx.API.IntegrationTests](../../tests/Onyx.API.IntegrationTests/) | الـ API الحقيقية + الـ Worker + EF + PostgreSQL حقيقي بترحيلات حقيقية | **نعم** | [PostgresFixture](../../tests/Onyx.API.IntegrationTests/Infrastructure/PostgresFixture.cs)، [OnyxApiFactory](../../tests/Onyx.API.IntegrationTests/Infrastructure/OnyxApiFactory.cs)، [WorkerTestHost](../../tests/Onyx.API.IntegrationTests/Worker/WorkerTestHost.cs) |

## 2. قاعدة بيانات الاختبار (قواعد صارمة)

- المصدر الوحيد للاتصال هو متغير البيئة `ONYX_TEST_POSTGRES`، ويجب أن يشير إلى قاعدة الصيانة `postgres` بدور مخصص `NOSUPERUSER CREATEDB`. خطوات الإعداد في [development-setup.md §2–3](../development-setup.md).
- [PostgresFixture](../../tests/Onyx.API.IntegrationTests/Infrastructure/PostgresFixture.cs) (Assembly fixture) ينشئ **قاعدتين مؤقتتين** لكل تشغيل:
  - قاعدة التطبيق: تُرحَّل بـ `MigrateAsync` (الترحيلات الحقيقية — لا `EnsureCreated`).
  - قاعدة «Scratch» لنموذج اختبار المعترضات فقط ([InterceptorTestDbContext](../../tests/Onyx.API.IntegrationTests/Persistence/InterceptorTestDbContext.cs))، وهي الاستثناء الوحيد المسموح لـ `EnsureCreated` (CLAUDE.md §19).
- الأسماء بنمط `onyx_test_{yyyyMMddHHmmss}_{random8}` وتُحذف في النهاية بـ `DROP DATABASE … WITH (FORCE)`.
- [PostgresTestDatabaseGuard](../../tests/Onyx.API.IntegrationTests/TestDatabase/PostgresTestDatabaseGuard.cs) يرفض: دور superuser، دورًا بلا CREATEDB، أي قاعدة غير `postgres` في سلسلة الاتصال، أي اسم لا يطابق النمط، وأي قاعدة لم ينشئها هذا التشغيل.
- غياب `ONYX_TEST_POSTGRES` ⇒ اختبارات قاعدة البيانات **تُتخطى** (Skipped) ولا تنجح زيفًا (`PostgresFixture.RequireDatabase()` → `Assert.SkipUnless`). إعداد غير آمن ⇒ يفشل التشغيل بصوت عالٍ.
- الاختبارات التي تكتب على القاعدة المشتركة في مجموعة `[Collection("Database")]` غير متوازية ([DatabaseCollection](../../tests/Onyx.API.IntegrationTests/Infrastructure/DatabaseCollection.cs))؛ وتصفّر الجداول بـ `ITestDatabase.ResetAsync`.
- **ممنوع** أن تلمس الاختبارات قاعدة التطوير أو الإنتاج، وممنوع مزوّد EF In-Memory.

## 3. أوامر التشغيل والتحقق

المشغّل هو **Microsoft.Testing.Platform** (مضبوط في [global.json](../../global.json)) مع xUnit v3.

```powershell
# بناء كامل بلا تحذيرات (TreatWarningsAsErrors مفعّل في Directory.Build.props)
dotnet build Onyx.slnx --no-incremental

# كل الاختبارات
dotnet test Onyx.slnx

# مشروع واحد
dotnet test tests/Onyx.Domain.Tests/Onyx.Domain.Tests.csproj

# صنف أو دالة (يدعم *) — متحقق منهما على هذا المستودع
dotnet test tests/Onyx.Domain.Tests/Onyx.Domain.Tests.csproj --filter-class "*SlugAndBodyRulesTests"
dotnet test tests/Onyx.Application.Tests/Onyx.Application.Tests.csproj --filter-method "*Delay_is_capped"

# فحص الحزم الضعيفة (مباشرة وانتقالية) — مطلوب قبل كل التزام مرحلة
dotnet list Onyx.slnx package --vulnerable --include-transitive

# انحراف نموذج EF عن الترحيلات (يجب أن يقول: No changes have been made to the model since the last migration.)
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/Onyx.Infrastructure --startup-project src/Onyx.Infrastructure
```

> ملاحظة: `has-pending-model-changes` يعمل مع `--startup-project src/Onyx.Infrastructure` (حيث `DesignTimeDbContextFactory`). استخدام `Onyx.API` كمشروع بدء يفشل لأنه لا يرجع إلى `Microsoft.EntityFrameworkCore.Design`.

### 3.1 قائمة التحقق قبل الالتزام (ممارسة المراحل السابقة)

1. `dotnet build Onyx.slnx --no-incremental` → 0 تحذيرات، 0 أخطاء.
2. `dotnet test Onyx.slnx` مع `ONYX_TEST_POSTGRES` مضبوطًا → 0 فاشلة، 0 متخطاة.
3. فحص الحزم الضعيفة → لا شيء.
4. فحص انحراف EF → لا تغييرات معلّقة.
5. مراجعة `git status` و`git diff --cached` بحثًا عن أسرار أو ملفات مولّدة (`bin/`، `obj/`، `TestResults/`، user-secrets، `.env`).

## 4. خريطة الميزات → ملفات الاختبار

| الميزة | Domain | Application | API Integration | Web |
|---|---|---|---|---|
| الهوية/الجلسات/2FA | — | `Identity/IdentityPolicyTests` | `Identity/*` (Authentication، SessionToken، EnrollmentBinding، PasswordAndInvitation، OperatorTwoFactorRecovery، Bootstrap، IdentityPrimitives) | `AdminAuthenticationTests`، `AuthenticatorQrCodeTests`، `TokenLinkTests` |
| الصلاحيات والأدوار | — | `Authorization/PermissionCatalogueTests` | `Identity/AuthorizationTests`، `Identity/AdministrationGuardTests` | `LocalizationAndHandlerUnitTests` (إخفاء عناصر القائمة) |
| التدقيق | `Auditing/AuditLogTests` | — | `Persistence/PersistenceInterceptorTests`، `Identity/AdminOperationsApiTests` | — |
| CMS ودورة النشر والمراجعات | `Content/ContentItemLifecycleTests`، `SlugAndBodyRulesTests`، `PublicUrlChangeTests` | `Content/ContentGovernanceTests`، `SeoFoundationRulesTests` | `Content/ContentWorkflowApiTests`، `ContentHierarchyApiTests`، `CmsAdministrationApiTests` | `AdminCmsTests`، `BlockTextFormatTests`، `ContentSiteTests` |
| SEO والتحويلات وIndexNow | `Content/SlugAndBodyRulesTests`، `PublicUrlChangeTests` | `Content/SeoFoundationRulesTests` | `Content/SeoFoundationApiTests`، `SearchEngineActivationApiTests`، `Seo/IndexNowNotifierTests` | `SeoFoundationSiteTests`، `ContentSiteTests` |
| التوطين والتوجيه | — | — | — | `PublicSiteTests`، `LocalizationAndHandlerUnitTests` |
| الوسائط | — | — | `Media/ImageSanitizationTests`، `Content/MediaDeletionApiTests`، `CmsAdministrationApiTests` | `ContentSiteTests` (بروكسي الوسائط) |
| الأعمال والإثبات | `Content/WorkProofInsightRulesTests` | `Content/WorkAndInsightRulesTests` | `Content/WorkAndProofApiTests` | `WorkAndInsightsSiteTests` |
| الرؤى والمؤلفون والمسرد | `Content/WorkProofInsightRulesTests` | `Content/WorkAndInsightRulesTests` | `Content/InsightsApiTests` | `WorkAndInsightsSiteTests` |
| الاستفسارات والعملاء المحتملون والإسناد | `Leads/InquiryAndConsentTests`، `LeadTests`، `AttributionTests` | `Booking/IcsAndCsvTests` (CSV) | `Leads/InquiryApiTests`، `LeadAdministrationApiTests`، `LeadNotificationWorkerTests`، `TurnstileNotConfiguredApiTests`، `AdminSignInTurnstileApiTests` | `LeadFormsSiteTests`، `AdminLeadsTests` |
| الحجز | `Booking/BookingTests` | `Booking/SlotCalculatorTests`، `IcsAndCsvTests` | `Leads/BookingApiTests` | `LeadFormsSiteTests`، `AdminLeadsTests` |
| البريد والإشعارات | `Notifications/AdminNotificationTests` | `Email/EmailRequestValidatorTests`، `Notifications/NotificationServiceTests`، `Content/ContentGovernanceTests` (نصوص القوالب) | `Worker/EmailProcessingTests`، `PickupDirectoryEmailSenderTests`، `NotificationVisibilityTests` | `AdminAuthenticationTests` (صفحة الإشعارات) |
| موافقة الكوكيز | — | — | `Privacy/ConsentApiTests` | `ConsentTests`، `AnalyticsSiteTests` |
| الخصوصية والاحتفاظ والتصدير والمحو | `Privacy/PrivacyRulesTests` | `Privacy/DataSubjectExportBuilderTests` | `Privacy/*` (Retention، DataSubjectExport، DataSubjectErasure، PrivacyOptions) | `AdminPrivacyTests` |
| البحث | — | `Search/SearchTextNormalizerTests` | `Search/SiteSearchApiTests` | `SiteSearchTests` |
| الأخبار وSSRF | `News/NewsItemTests`، `NewsSourceTests`، `NewsUrlTests` | `News/NewsRulesTests` | `News/*`، `Outbound/SafeOutboundHttpTests`، `Outbound/FeedParserTests` | `NewsSiteTests` |
| التحليلات وSearch Console | `Analytics/AnalyticsAggregateTests` | `Analytics/AnalyticsRulesTests` | `Analytics/*` | `AnalyticsSiteTests` |
| اتصالات المزوّدين | `Providers/ProviderConnectionTests` | — | `Identity/AdminOperationsApiTests`، `Worker/ProviderConnectionConcurrencyTests`، `Worker/OutboxProcessingTests` | — |
| الـ Worker/المهام/الـ Outbox | — | `Jobs/JobScheduleTests`، `RetryBackoffTests` | `Worker/*` | — |
| المعرفة والكلمات المفتاحية والعناقيد | `Content/GovernanceRulesTests` | `Content/ContentGovernanceTests` | `Content/IntelligenceServicesApiTests`، `CmsAdministrationApiTests` | — |
| بنية HTTP (أخطاء، معدلات، ترويسات، CORS) | `Common/*` | — | `Http/*` | `PublicSiteTests` (CSP nonce) |
| حدود المعمارية | — | — | — | `Onyx.ArchitectureTests/*` |

أسماء الدوال نفسها وصفية (مثل `Unverified_proof_cannot_be_published_through_the_api`)؛ كل ملف ميزة في [features/](features/) يسرد الدوال الأهم مع ما تتحقق منه.

## 5. كيف تكتب اختبارًا جديدًا في المكان الصحيح

| ما تغيّره | أين تضيف الاختبار |
|---|---|
| قاعدة/حارس في كيان Domain | `Onyx.Domain.Tests/<Feature>/` — بلا قاعدة بيانات |
| منطق خالص في Application (حساب، تطبيع، تنسيق) | `Onyx.Application.Tests/<Feature>/` |
| نقطة نهاية API جديدة | `Onyx.API.IntegrationTests/<Feature>/` + اختبار 401 (مجهول) و403 (بلا صلاحية) — إلزامي (CLAUDE.md §18). `AuthorizationTests.Every_endpoint_declares_its_authorization_explicitly` يكشف أي إجراء نسي سمة التفويض |
| معالج حدث أو مهمة Worker | `Onyx.API.IntegrationTests/Worker/` أو مجلد الميزة باستخدام `WorkerTestHost` و`ManualTimeProvider` |
| صفحة Web أو سلوك Admin UI | `Onyx.Web.Tests/` مع `FakeOnyxApi.On(method, path, …)` لمحاكاة ردود الـ API |
| حزمة جديدة أو مرجع مشروع | `Onyx.ArchitectureTests/` (أضف قاعدة عزل كما فُعل لـ Google وRSS) |

**نصائح عملية:**
- الوقت قابل للتحكم: `ManualTimeProvider` في Persistence وWeb. تقديم الوقت ساعة كاملة يُبطل رمز الوصول (10 دقائق) — قدّم دقائق أو سجّل الدخول من جديد بعد التقديم.
- الأحداث لا تُعالَج تلقائيًا في اختبارات الـ API؛ شغّل `OutboxProcessor`/`EmailProcessor` عبر `WorkerTestHost` لترى الرسائل والإشعارات.
- Turnstile في الاختبارات: [FakeTurnstile](../../tests/Onyx.API.IntegrationTests/Leads/FakeTurnstile.cs)؛ والتحقق الحقيقي يفشل مغلقًا دون سر.
- مزوّدا Google في الاختبارات مزيّفان (`FakeWebAnalytics`، `FakeSearch` في [AnalyticsApiTestBase](../../tests/Onyx.API.IntegrationTests/Analytics/AnalyticsApiTestBase.cs))؛ لا يوجد أي اتصال حقيقي بـ Google في الاختبارات.
