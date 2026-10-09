# 17 — التحليلات (GA4) وأداء البحث (Search Console) وقمع العملاء

## 1. ماذا تفعل الميزة

- **مزامنة لا استدعاءات حية:** الـ Worker يجلب مجاميع يومية من GA4 Data API وSearch Console إلى جداول محلية؛ لوحات الإدارة تقرأ المحلي فقط. كل مقياس يذكر مصدره ونطاقه الزمني.
- **غير نشط حتى الإعداد:** بدون معرّف الخاصية/الموقع ومفتاح حساب الخدمة، يبقى المزوّد `NotConfigured`، تُتخطى المهام (`Skipped`)، وتعرض الإدارة الحالة **لا أصفارًا**. لا مزوّد مزيّف مسجّل في الإنتاج (الاختبارات تسجّل مزيّفاتها).
- **قمع الطرف الأول** (من بيانات Onyx نفسها، إسناد Stage 8 هو المرجع): Inquiry → Lead → Contacted → Qualified → Proposal → Won/Lost، مجمّعًا حسب المصدر/الوسيط/الحملة/صفحة الهبوط/الخدمة/السوق/اللغة. **لا ربط زائف بين GA4 والـ Leads** — مجموعتا بيانات منفصلتان، تُقارنان فقط تجميعيًا بمفتاح مشترك (حملة/صفحة هبوط).
- **أحداث التحويل** في Web: `inquiry_submitted` و`consultation_booking_confirmed` فقط — بلا بيانات شخصية، مرة واحدة (TempData)، فقط مع موافقة التحليلات، **بلا تتبع من الخادم**.
- **إشارات حتمية** (قواعد بعتبات ظاهرة) لا تغيّر شيئًا تلقائيًا ولا تخترع مقاييس.
- Search Console: يحترم تأخر البيانات (3 أيام افتراضيًا)، بيانات «final» ونوع «web»، والروابط العربية والإنجليزية منفصلة.

## 2. الملفات

| الطبقة | المسار | المحتوى |
|---|---|---|
| Domain | [src/Onyx.Domain/Analytics/](../../../src/Onyx.Domain/Analytics/) | `WebAnalyticsDailyMetric` (DailyTotal/Acquisition/ConversionEvent)، `SearchPerformanceDailyMetric` (DailyTotal/Page/Query/CountryDevice)، `AnalyticsSyncState` (`Start`، `Succeed`، `Fail`، `IsSyncing`، xmin)، `AnalyticsDimensions` (`(other)`، `(not set)`، تنظيف، تجزئة SHA-256) |
| Application | [src/Onyx.Application/Analytics/](../../../src/Onyx.Application/Analytics/) | المنافذ `IWebAnalyticsProvider`، `ISearchPerformanceProvider`؛ `AnalyticsSyncService`؛ `AnalyticsReportService`؛ `AnalyticsIntegrationService`؛ `AnalyticsSignalRules`؛ `AnalyticsSettings`؛ `ConversionEvents`؛ `AnalyticsPaths`؛ `AnalyticsProviderErrors` |
| Infrastructure | [src/Onyx.Infrastructure/Analytics/](../../../src/Onyx.Infrastructure/Analytics/) | `GoogleAnalyticsDataProvider` (`analytics.ga4`)، `GoogleSearchConsoleProvider` (`search.gsc`)، `GoogleServiceAccount`، `GoogleErrors.Map`، `GoogleAnalyticsOptions` (`Analytics:Google`)، `AnalyticsSyncOptions` (`Analytics:Sync`)؛ المستودعات في [Repositories/Analytics/](../../../src/Onyx.Infrastructure/Persistence/Repositories/Analytics/) |
| Worker | `WebAnalyticsSyncJob` (`daily@05:00`)، `SearchPerformanceSyncJob` (`daily@05:30`)، `AnalyticsJobOutcome` | |
| API | [AdminAnalyticsController](../../../src/Onyx.API/Controllers/Admin/AdminAnalyticsController.cs)، [AdminSearchPerformanceController](../../../src/Onyx.API/Controllers/Admin/AdminSearchPerformanceController.cs)، [AdminAnalyticsIntegrationsController](../../../src/Onyx.API/Controllers/Admin/AdminAnalyticsIntegrationsController.cs) — كلها `no-store` | |
| Web | [Analytics/](../../../src/Onyx.Web/Analytics/) (`AnalyticsTag`، `AnalyticsTagFilter`، `ConversionEvent`، `NoAnalyticsAttribute`)، `Views/Shared/_AnalyticsTag.cshtml`، [SecurityHeadersMiddleware](../../../src/Onyx.Web/Security/SecurityHeadersMiddleware.cs)، Admin [AnalyticsController](../../../src/Onyx.Web/Areas/Admin/Controllers/AnalyticsController.cs) | |

**الحزم المعتمدة (Infrastructure فقط):** Google.Apis.Auth 1.77.0، Google.Apis.AnalyticsData.v1beta 1.76.0.4269، Google.Apis.SearchConsole.v1 1.75.0.4234. اختبارات المعمارية تمنع أي نوع Google/Newtonsoft خارج `Onyx.Infrastructure.Analytics`. نطاقات قراءة فقط (`AnalyticsReadonly`، `WebmastersReadonly`).

## 3. المزامنة — `AnalyticsSyncService.SyncWebAnalyticsAsync` / `SyncSearchPerformanceAsync`

1. غير مهيأ أو معطّل → `Skipped` (ويُسجَّل NotConfigured على `provider_connections`).
2. النافذة — `Window(today, delayDays, lastSynced, settings)`: أول مرة `InitialDays` (28) حتى آخر يوم نهائي؛ لاحقًا يُعاد تحميل آخر `RefreshDays` (3)؛ بحد `MaxDaysPerRun` (31) — الفجوة الطويلة تُلحق على عدة تشغيلات. التأخير: GA4 يوم واحد، Search 3 أيام.
3. الجلب بمهلة `FetchTimeoutSeconds`، حد صفوف `MaxRowsPerReport` (25,000).
4. لكل يوم وتقرير: أكبر `MaxRowsPerDay` (500) صف، والباقي يُجمع في `(other)`؛ المستخدمون غير قابلين للجمع عبر الأيام (يُخزنون على `DailyTotal` فقط).
5. ربط المسار بعنصر CMS (`ContentId` → null إن لم يكن صفحة عامة حالية؛ لا `Guid.Empty`).
6. **استبدال الأيام المتزامنة في معاملة واحدة** (حذف + إضافة) ⇒ إعادة المحاولة لا تُكرر.
7. لقطات `ContentPerformanceSnapshot` أسبوعية (الإثنين–الأحد) **للأسابيع الكاملة فقط**، تُستبدل بشكل متكرر الأمان.
8. الفشل → `Fail(code)` على الحالة + `ReportFailureAsync` للاتصال (حدث `IntegrationFailed` مرة عند الانتقال) + `Failed` → المهمة ترمي برمز آمن فقط. **آخر بيانات جيدة تبقى ظاهرة.**

رموز الأخطاء الآمنة: `NotConfigured`، `Unauthorized` (يشمل المفتاح غير الصالح/المنتهي)، `QuotaExceeded`، `Timeout`، `Unavailable`، `InvalidResponse` — لا تُعرض رسائل Google ولا الأسرار.

## 4. التقارير — `AnalyticsReportService`

| الطريقة | الصلاحية | الأقسام (مختصر) |
|---|---|---|
| `OverviewAsync`، `TrafficAsync`، `LandingPagesAsync`، `CampaignsAsync`، `ConversionsAsync`، `ContentAsync`، `FunnelAsync` | `Analytics.View` | يومي الويب، المصدر/الوسيط، الحملات، الأجهزة/الدول، صفحات الهبوط، التحويلات، قمع الطرف الأول وأعداده، مقارنات تجميعية |
| `SearchOverviewAsync`، `SearchQueriesAsync`، `SearchPagesAsync`، `SearchSignalsAsync` | `SearchPerformance.View` | يومي البحث، الاستعلامات، الصفحات، الدول/الأجهزة، الإشارات |
| `StatusAsync(providerKey, hasDataInPeriod, ct)` | — | الحالة: `NotConfigured` / `Disabled` / `Failed` / `Ok` / `NoData` + علامة `stale` (آخر نجاح أقدم من 48 ساعة) + `syncing` + نطاق التغطية + آخر رمز فشل + `truncated` |
| `PeriodAsync` | — | الفترة المطلوبة أو آخر 28 يومًا بها بيانات؛ ≤ 366 يومًا؛ لا مستقبل |

**الإشارات** (`AnalyticsSignalRules`): CTR منخفض (< 1% مع ≥ 200 ظهور)، تغيّر النقرات ≥ 25% (مع ≥ 20 نقرة سابقة)، فرص الاستعلامات (متوسط الموقع 8–20 مع ≥ 100 ظهور)، زيارات بلا استفسارات (≥ 100)، ظهور بلا استفسارات، لا ظهور. حتى 50 صفًا لكل إشارة. كل إشارة تعرض أرقامها وفترتها.

`AnalyticsIntegrationService.RequestSyncAsync(providerKey)` → `IJobTrigger.RequestRunAsync` (يجعل المهمة مستحقة الآن) + تدقيق `AnalyticsSyncRequested` → 202. `Analytics.JobUnavailable` إن لم يسجّل الـ Worker المهمة بعد.

## 5. وسم GA4 في الموقع العام

يُحقن `_AnalyticsTag.cshtml` في `<head>` فقط إذا: موافقة التحليلات **و** `Analytics:Ga4MeasurementId` مضبوط (`^G-[A-Z0-9]{4,20}$`) **و** الصفحة مؤهلة. `AnalyticsTagFilter` (مرشح عام) يضع علامة في `HttpContext.Items`، و`SecurityHeadersMiddleware` يضيف أصول Google إلى CSP **لتلك الاستجابة فقط** — بلا `unsafe-inline`/`unsafe-eval` (السكربت بـ nonce). صفحات الإدارة والمعاينة والرموز موسومة `[NoAnalytics]`. موقع الصفحة = المسار + معاملات `utm_*` فقط؛ المُحيل = مسار (نفس الموقع) أو أصل (غيره). Consent Mode v2 **غير مفعّل** (قرار مالك).

## 6. قاعدة البيانات

`web_analytics_daily_metrics`، `search_performance_daily_metrics` (مفتاح فريد: المزوّد، التاريخ، النوع، تجزئة البُعد؛ `content_item_id` SetNull)، `analytics_sync_states` (`provider_key` فريد). ترحيل `AnalyticsAggregates` إضافي. الاحتفاظ: فئة `AnalyticsAggregates` (حذف فقط، **بلا فترة مسبقة**). لا بيانات شخصية.

## 7. الاختبارات

- [AnalyticsAggregateTests](../../../tests/Onyx.Domain.Tests/Analytics/AnalyticsAggregateTests.cs)، [AnalyticsRulesTests](../../../tests/Onyx.Application.Tests/Analytics/AnalyticsRulesTests.cs).
- [AnalyticsSyncTests](../../../tests/Onyx.API.IntegrationTests/Analytics/AnalyticsSyncTests.cs) — التخطي بلا إعداد وبلا أصفار، الحدود والتكرار الآمن، فصل اللغات واللقطات الأسبوعية، `(other)` والاقتطاع، «لا بيانات» مقابل الفشل مع إبقاء آخر بيانات، المهلة والتعطيل.
- [AnalyticsReportsApiTests](../../../tests/Onyx.API.IntegrationTests/Analytics/AnalyticsReportsApiTests.cs) — الصلاحيات و`no-store`، حدود الفترات، القمع، المقارنة التجميعية فقط، طلب المزامنة مُدقق، الاحتفاظ.
- [SearchSignalsTests](../../../tests/Onyx.API.IntegrationTests/Analytics/SearchSignalsTests.cs)، [GoogleAdapterTests](../../../tests/Onyx.API.IntegrationTests/Analytics/GoogleAdapterTests.cs) (لا اتصال بـ Google: تحقق الإعدادات، غير مهيأ لا يتصل، مفتاح غير صالح ⇒ Unauthorized دون تسريب، ربط الأخطاء).
- [AnalyticsSiteTests (Web)](../../../tests/Onyx.Web.Tests/AnalyticsSiteTests.cs) — 11 اختبارًا للموافقة وCSP والأحداث والإدارة.
- [TypeDependencyRulesTests](../../../tests/Onyx.ArchitectureTests/TypeDependencyRulesTests.cs) — عزل Google.

## 8. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| بُعد/تقرير GA4 جديد | `GoogleAnalyticsDataProvider` + `WebAnalyticsBatch` + `WebAnalyticsReportKind` (إلحاق) + `IAnalyticsMetricRepository` + `AnalyticsReportService` + ترجمات العرض + ترحيل إن تغير المخطط |
| حدث تحويل جديد | `ConversionEvents` (Application) + `ConversionEvent` (Web) + `AnalyticsTag.Script` + مصدر الحدث (Controller) + `AnalyticsSiteTests.Only_known_conversion_events_and_inquiry_types_can_be_emitted` |
| ربط GA4 بالـ Leads (client id custom dimension) | **قرار مالك معلّق** — لا تنفذه |
| مزوّد بديل (غير Google) | نفّذ المنفذين في Infrastructure واستبدل `TryAddSingleton`؛ لا أنواع مزوّد في Application |
| `AnalyticsSettings` | قيم تقنية يؤكدها المالك عند التفعيل — عبر `Analytics:Sync` |

## 9. الحالة

- **منفّذ:** كل ما سبق.
- **غير مهيأ:** GA4 وSearch Console (يحتاجان: `Analytics:Google:Ga4PropertyId`، `SearchConsoleSiteUrl`، مفتاح حساب الخدمة في مخزن الأسرار، و`Analytics:Ga4MeasurementId` في Web).
- **مؤجّل:** منصات الإعلانات (`IAdPlatformProvider` — Phase 3)، أحداث الأدوات (Phase 2)، Consent Mode v2، بُعد client id.
