# 21 — لوحة الإدارة (Admin Area في Onyx.Web)

## 1. ماذا تفعل

منطقة MVC باسم `Admin` داخل Onyx.Web، بـ MVC + JavaScript خفيف. **لا تلمس قاعدة البيانات أبدًا**: كل قراءة/كتابة عبر عملاء API مكتوبين (`IHttpClientFactory` + المرونة). الـ API هي نقطة الإنفاذ الوحيدة للصلاحيات؛ الواجهة **تُخفي** فقط ما لا يملكه المستخدم.

## 2. البنية

| المكوّن | الملف | الدور |
|---|---|---|
| المصادقة BFF | [Authentication/](../../../src/Onyx.Web/Authentication/) | انظر [01-identity-auth-AR.md](01-identity-auth-AR.md) |
| الغلاف | [AdminShellFilter](../../../src/Onyx.Web/Areas/Admin/AdminShellFilter.cs)، [AdminShellModel](../../../src/Onyx.Web/Areas/Admin/AdminShellModel.cs) | يحمّل الاسم والقائمة المفلترة بالصلاحيات وعدد غير المقروء **قبل** الإجراء (فتُعالج الجلسة الملغاة بـ `AdminSessionEndedFilter` لا أثناء العرض) |
| القائمة | [AdminNavigation](../../../src/Onyx.Web/Areas/Admin/Navigation/AdminNavigation.cs) | 28 عنصرًا؛ `VisibleTo(permissions)` يُخفي ما يحتاج صلاحية غير محمولة |
| أساس شاشات CMS | [CmsControllerBase](../../../src/Onyx.Web/Areas/Admin/Cms/CmsControllerBase.cs) | `[Area("Admin")]`، `[Authorize]`؛ يترجم نتائج الـ API: 403 → «ممنوع الوصول»، 404 → غير موجود، API غير متاحة → 503، غير ذلك → رسالة لمرة واحدة (`onyx.notice`/`onyx.error` في TempData) |
| خيارات الواجهة | [CmsOptions](../../../src/Onyx.Web/Areas/Admin/Cms/CmsOptions.cs) | قوائم القيم المعروضة (أنواع المحتوى، الحالات…) — يجب أن تطابق الـ enums في Domain |
| محرر الكتل | [BlockTextFormat](../../../src/Onyx.Web/Areas/Admin/Cms/BlockTextFormat.cs) | نص عادي ↔ كتل منظمة (`##`، `- `، `1. `، `![alt](media:GUID "caption")`، `[[cta:GUID]]`، `**strong**`، `*em*`، `[text](content:GUID \| /path \| https://…)`) — **لا HTML** |
| QR للمصادِق | [AuthenticatorQrCode](../../../src/Onyx.Web/Areas/Admin/AuthenticatorQrCode.cs) | SVG data URI لعنوان `otpauth://totp` فقط (حزمة `Net.Codecrete.QrCodeGenerator` معتمدة لـ Web فقط) |
| عملاء الـ API | [ApiClients/](../../../src/Onyx.Web/ApiClients/) | `OnyxApiClientBase` (المكان الوحيد لبناء طلبات HTTP)، `ApiResult<T>` (لا استثناء للأخطاء المتوقعة؛ `IsUnavailable`)، `AccessTokenHandler`، `RequestContextHandler` (X-Correlation-ID، Accept-Language، X-Forwarded-For) |
| الأمان | [Security/](../../../src/Onyx.Web/Security/) | `SecurityHeadersMiddleware` (CSP بـ nonce لكل طلب، كل شيء مستضاف ذاتيًا، استثناءات Turnstile/GA4 لكل استجابة، **الإدارة لا تُخزَّن مؤقتًا**)، `SensitivePageAttribute` (no-referrer + no-store لصفحات الرموز)، `AllowsTurnstileAttribute`؛ Antiforgery عام (`AutoValidateAntiforgeryTokenAttribute`، كوكي `__Host-onyx.af`) |

## 3. الشاشات الموجودة (متحكمات Admin)

| الشاشة | المتحكم | الصلاحية المطلوبة للعرض في القائمة | ملف الميزة |
|---|---|---|---|
| لوحة البداية | `DashboardController` (`/admin`) | — | — |
| الإشعارات | `NotificationsController` | — | [12](12-email-notifications-AR.md) |
| العملاء المحتملون، الاستفسارات | `LeadsController`، `InquiriesController` | Leads.View، Inquiries.View | [10](10-leads-crm-AR.md) |
| الحجوزات، التوفر | `BookingsController`، `AvailabilityController` | Bookings.View، Availability.Manage | [11](11-booking-AR.md) |
| طلبات الخصوصية، الاحتفاظ | `PrivacyRequestsController`، `RetentionController` | Privacy.Manage | [14](14-privacy-retention-AR.md) |
| التحليلات، أداء البحث، التكاملات | `AnalyticsController` | Analytics.View، SearchPerformance.View، ProviderConnections.Manage | [17](17-analytics-search-console-AR.md) |
| المحتوى، المؤلفون | `ContentController`، `AuthorsController`، `ProofItemsController` | Content.View، Insights.View | [04](04-cms-content-AR.md)، [08](08-proof-work-AR.md)، [09](09-insights-glossary-AR.md) |
| الأخبار، المصادر، الفئات | `NewsItemsController`، `NewsSourcesController`، `NewsCategoriesController` | News.View، NewsSources.Manage | [16](16-news-ssrf-AR.md) |
| الوسائط، التحويلات، الدعوات للإجراء | `MediaLibraryController`، `RedirectsController`، `CtasController` | Media.View، Redirects.Manage، Ctas.View | [07](07-media-AR.md)، [06](06-seo-redirects-AR.md)، [04](04-cms-content-AR.md) |
| العناقيد، الكلمات، التوصيات، الذكاء، التقويم، المنافسون، المعرفة، الهوية | `ClustersController`… `BrandKitController` | Content.View / ContentEngine.View / Competitors.View / Knowledge.View / BrandKit.View | [20](20-knowledge-intelligence-AR.md) |
| قوالب البريد | `EmailTemplatesController` | EmailTemplates.Manage | [12](12-email-notifications-AR.md) |
| الحساب | `AccountController` | — | [01](01-identity-auth-AR.md) |

## 4. ما ليس له شاشة (الـ API فقط)

إدارة المستخدمين والأدوار وعرض الصلاحيات، سجل التدقيق، اتصالات المزوّدين العامة (ما عدا تكاملات التحليلات) واختبار البريد، عرض/إدارة المهام الخلفية.

## 5. الاختبارات

[AdminAuthenticationTests](../../../tests/Onyx.Web.Tests/AdminAuthenticationTests.cs)، [AdminCmsTests](../../../tests/Onyx.Web.Tests/AdminCmsTests.cs)، [AdminLeadsTests](../../../tests/Onyx.Web.Tests/AdminLeadsTests.cs)، [AdminPrivacyTests](../../../tests/Onyx.Web.Tests/AdminPrivacyTests.cs)، [AnalyticsSiteTests](../../../tests/Onyx.Web.Tests/AnalyticsSiteTests.cs)، [NewsSiteTests](../../../tests/Onyx.Web.Tests/NewsSiteTests.cs)، [WorkAndInsightsSiteTests](../../../tests/Onyx.Web.Tests/WorkAndInsightsSiteTests.cs) — كلها تستخدم [FakeOnyxApi](../../../tests/Onyx.Web.Tests/Infrastructure/FakeOnyxApi.cs) لمحاكاة الـ API.

## 6. خريطة أثر التغيير — إضافة شاشة إدارة

1. نقطة نهاية API بصلاحية صريحة (انظر [API-ROUTES-AR.md §5](../API-ROUTES-AR.md)).
2. دالة في العميل المكتوب المناسب (`IAdmin…ApiClient` + تنفيذه)؛ إن كان عميلًا جديدًا سجّله في `WebServiceCollectionExtensions` مع `AddHttpMessageHandler<AccessTokenHandler>()`.
3. متحكم يرث `CmsControllerBase` (أو `[Area("Admin")]` + `[Authorize]`) + عرض Razor بلا CSS نهائي (Stage 5 مؤجلة).
4. عنصر في `AdminNavigation` بالصلاحية + مفاتيح `Nav.*` في ملفي `.resx` (ar/en).
5. نماذج POST مع antiforgery؛ تمرير `Version` للتزامن المتفائل.
6. اختبار Web بـ `FakeOnyxApi` + اختبار 401/403 في الـ API.

## 7. الحالة

- **منفّذ:** كل ما في الجدول §3، بقوالب دلالية وظيفية **بلا تصميم بصري** (التصميم بعد Stage 12 وبموافقة المالك فقط — CLAUDE.md).
