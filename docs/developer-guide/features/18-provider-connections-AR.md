# 18 — اتصالات المزوّدين الخارجيين (Provider Connections)

## 1. ماذا تفعل الميزة

سجل **حالة تشغيلية** لكل مزوّد خارجي (ناقل البريد، GA4، Search Console، خدمة الملخصات، IndexNow…) **بلا أي أسرار** — بيانات الاعتماد تعيش في مخزن الأسرار فقط. لا تُعرض بيانات كأنها متاحة ما لم يعمل المزوّد فعلًا.

**الحالات** ([ProviderConnectionStatus](../../../src/Onyx.Domain/Providers/ProviderConnectionStatus.cs)): `NotConfigured → Configured → Active → Error / Disabled`.

| الانتقال | الدالة | ملاحظات |
|---|---|---|
| إنشاء | `ProviderConnection.Register(key, category)` | يبدأ `NotConfigured` |
| بيانات اعتماد مفقودة | `MarkNotConfigured()` | لا يمس `Disabled` |
| بيانات موجودة غير مُثبتة | `MarkConfigured()` | من `NotConfigured` فقط |
| نجاح | `RecordSuccess(now)` | → `Active`، يمسح آخر خطأ؛ يُتجاهل إن كان معطلًا |
| فشل | `RecordFailure(now, error)` | → `Error`، خطأ ≤ 500 حرف بلا أسرار؛ **حدث `ProviderConnectionFailed` فقط عند الانتقال إلى Error** (لا إغراق) |
| تعطيل/تفعيل | `Disable()` / `Enable()` | التفعيل يعيد إلى `NotConfigured` |

`IsUsable` = Configured أو Active أو Error.

## 2. مفاتيح المزوّدين ([ProviderKeys](../../../src/Onyx.Application/Providers/ProviderKeys.cs) — لا تُعِد تسمية القيم)

| المفتاح | الاستخدام الحالي |
|---|---|
| `email.smtp`، `email.pickup-directory` | `EmailProcessor` يبلّغ حالة الناقل |
| `analytics.ga4`، `search.gsc` | مزامنة التحليلات (Stage 12) |
| `ai.news-summary` | خدمة ملخصات الأخبار (غير مهيأة) |
| `seo.indexnow` | IndexNow |
| `ai.text`، `ai.image`، `seo.onyx-platform`، `seo.keywords`، `seo.serp`، `analytics.behavior` | **محجوزة**؛ تظهر في حالة قدرات الذكاء التحريري كـ «غير متصل» — لا محوّلات لها |

## 3. الملفات

| المسار | الدور |
|---|---|
| [ProviderConnection.cs](../../../src/Onyx.Domain/Providers/ProviderConnection.cs) | الكيان (`IAuditable`، فئة Integrations، xmin) |
| [ProviderConnectionService.cs](../../../src/Onyx.Application/Providers/ProviderConnectionService.cs) | `IProviderConnectionService`: `ReportNotConfiguredAsync`، `ReportSuccessAsync`، `ReportFailureAsync` — ينشئ الصف عند أول تقرير ويحفظ |
| [ProviderConnectionAdministrationService.cs](../../../src/Onyx.Application/Providers/ProviderConnectionAdministrationService.cs) | `ListAsync`، `DisableAsync`، `EnableAsync` (تدقيق `ProviderConnectionDisabled/Enabled` مع المفتاح)، `QueueEmailTestAsync` (بريد `system.provider-test` إلى عنوان المسؤول الحالي؛ تدقيق `ProviderTestRequested`) |
| [ProviderConnectionFailedHandler.cs](../../../src/Onyx.Application/Providers/ProviderConnectionFailedHandler.cs) | إشعار `IntegrationFailed` (Critical، `ProviderConnections.Manage`) |
| API: [AdminProviderConnectionsController](../../../src/Onyx.API/Controllers/Admin/AdminProviderConnectionsController.cs)، [AdminAnalyticsIntegrationsController](../../../src/Onyx.API/Controllers/Admin/AdminAnalyticsIntegrationsController.cs) | `ProviderConnections.Manage` (صلاحية مميزة ⇒ 2FA) |
| Web: [AnalyticsController.Integrations](../../../src/Onyx.Web/Areas/Admin/Controllers/AnalyticsController.cs) | شاشة حالة تكاملات التحليلات فقط؛ **لا شاشة عامة لكل الاتصالات** (الـ API فقط) |

## 4. الاختبارات

- [ProviderConnectionTests](../../../tests/Onyx.Domain.Tests/Providers/ProviderConnectionTests.cs) — البداية، النجاح، الحدث عند الانتقال فقط، التعطيل يتجاهل التقارير، اقتطاع الأخطاء.
- [ProviderConnectionConcurrencyTests](../../../tests/Onyx.API.IntegrationTests/Worker/ProviderConnectionConcurrencyTests.cs) — `Version` = `xmin`؛ رفض التعديلات المتزامنة.
- [AdminOperationsApiTests](../../../tests/Onyx.API.IntegrationTests/Identity/AdminOperationsApiTests.cs) (`Provider_connections_can_be_listed_disabled_enabled_and_tested_with_audit`)، [OutboxProcessingTests](../../../tests/Onyx.API.IntegrationTests/Worker/OutboxProcessingTests.cs) (`Provider_failure_notifies_once_through_the_outbox_and_is_audited`).

## 5. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| مزوّد جديد | أضف مفتاحًا في `ProviderKeys` و(إن لزم) فئة في `ProviderCategory` (إلحاق)؛ بلّغ الحالة من الكود الذي يستخدمه عبر `IProviderConnectionService` (بأفضل جهد، في نطاق منفصل كما في `EmailProcessor.ReportProviderAsync`) |
| رسالة الخطأ المخزنة | **لا رسائل استثناءات خام** (قد تحوي أسرارًا) — رموز/أوصاف آمنة فقط |
| عرض الحالة في الإدارة | لا تُظهر أرقامًا من مزوّد غير `Active`/`Ok` (CLAUDE.md §11) |

## 6. الحالة

- **منفّذ:** السجل، التقارير من البريد والتحليلات والأخبار وIndexNow، التعطيل/التفعيل، اختبار البريد.
- **لا يوجد:** «اختبار اتصال» عام لكل مزوّد (طلب مزامنة التحليلات يعمل كاختبار لها)، ولا تخزين لإعدادات غير سرية في الصف حاليًا.
