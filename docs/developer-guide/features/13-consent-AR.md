# 13 — موافقة الكوكيز وموافقة النماذج

## 1. ماذا تفعل الميزة

**موافقة الكوكيز (الموقع العام):** ثلاث فئات — Necessary (ضمنية) / Analytics / Marketing.
- الاختيار يُحفظ في كوكي `onyx.consent` بصيغة `{policyVersion}.{a}{m}.{consentId}` (تقرؤه [ConsentState](../../../src/Onyx.Web/Consent/ConsentState.cs))، **و** إيصال على الخادم (`CookieConsentReceipt`) عبر الـ API.
- **تغيير `Consent:PolicyVersion`** يجعل كل الاختيارات السابقة «بلا قرار» فيعود الشريط للظهور.
- السكربتات غير الضرورية تُحمّل **فقط** بعد الموافقة المطابقة (`AnalyticsAllowed`/`MarketingAllowed`). وسم GA4: انظر [17-analytics-AR.md](17-analytics-search-console-AR.md).
- سحب/رفض موافقة التحليلات يحذف كوكيز GA (`_ga…`) للمضيف ولنطاقه، وكوكيز الإسناد `onyx.ft`/`onyx.lt`.
- الإيصال مجهول: معرّف موافقة عشوائي فقط، والـ IP يُخزن **فقط** كـ HMAC-SHA256 إن ضُبط `Privacy:IpHashKey` (وإلا لا يُخزن إطلاقًا).
- الاختيار يُحترم حتى لو فشل تخزين الإيصال (اختبار `The_choice_is_honoured_even_when_the_receipt_cannot_be_stored`).

**موافقة النماذج:** `ConsentRecord` لكل استفسار — موافقة التواصل (إلزامية) وموافقة التسويق (اختيارية، منفصلة، قابلة للسحب) + `ConsentVersion` (نص الإشعار الذي رآه الزائر). التفاصيل في [10-leads-crm-AR.md](10-leads-crm-AR.md).

> الشريط ليس ادعاء امتثال قانوني (CLAUDE.md §10).

## 2. الملفات

| المسار | الدور |
|---|---|
| [ConsentController (Web)](../../../src/Onyx.Web/Controllers/ConsentController.cs) | `POST /{lang}/consent` (antiforgery): إيصال API + الكوكي (`Consent:CookieLifetimeDays`، افتراضي 180) + حذف كوكيز التحليلات عند عدم الموافقة؛ العودة فقط لمسار في نفس اللغة |
| [ConsentState](../../../src/Onyx.Web/Consent/ConsentState.cs)، [ConsentOptions](../../../src/Onyx.Web/Configuration/ConsentOptions.cs) | قراءة الكوكي ونسخة السياسة |
| [CookieConsentReceipt](../../../src/Onyx.Domain/Privacy/CookieConsentReceipt.cs) | الكيان (لا ينفذ `IAuditable`) |
| [CookieConsentService](../../../src/Onyx.Application/Privacy/CookieConsentService.cs) + `RecordCookieConsentRequestValidator` | `RecordAsync` → `Result<Guid>` |
| [HmacIpAddressHasher](../../../src/Onyx.Infrastructure/Privacy/HmacIpAddressHasher.cs)، [PrivacyOptions](../../../src/Onyx.Infrastructure/Privacy/PrivacyOptions.cs) | تجزئة الـ IP (المفتاح Base64 ≥ 32 بايت إن ضُبط) |
| [ConsentsController (API)](../../../src/Onyx.API/Controllers/ConsentsController.cs) | `POST /api/v1/consents` — مجهول، `strict`، 202 |
| [AttributionCookies](../../../src/Onyx.Web/Attribution/AttributionCookies.cs) | كوكيز الإسناد تُحذف دون موافقة التحليلات |

## 3. الاختبارات

- [ConsentTests (Web)](../../../tests/Onyx.Web.Tests/ConsentTests.cs) — الشريط حتى الاختيار، احترام الاختيار عند فشل الإيصال، antiforgery، رفض روابط العودة خارج اللغة.
- [ConsentApiTests](../../../tests/Onyx.API.IntegrationTests/Privacy/ConsentApiTests.cs) — إيصال مجهول، اختيار جديد يضيف إيصالًا ولا يكتب فوقه، رفض الإيصالات غير الصالحة.
- [PrivacyOptionsTests](../../../tests/Onyx.API.IntegrationTests/Privacy/PrivacyOptionsTests.cs) — بلا مفتاح لا IP؛ مع مفتاح تجزئة فقط.
- [AnalyticsSiteTests](../../../tests/Onyx.Web.Tests/AnalyticsSiteTests.cs) — لا وسم ولا أصل Google في CSP دون موافقة؛ السحب يحذف كوكيز GA.

## 4. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| فئة كوكيز جديدة | صيغة الكوكي في `ConsentState.Format/Read`، `RecordCookieConsentRequest`، الكيان + ترحيل، الشريط في `_PublicLayout`، الاحتفاظ |
| سكربت طرف ثالث جديد | يجب أن يُشرط بالموافقة + CSP لكل استجابة فقط (نمط `AnalyticsTagFilter`) — لا تُضعف CSP العامة |
| نص سياسة الكوكيز | ارفع `Consent:PolicyVersion` |

## 5. الحالة

- **منفّذ:** كل ما سبق.
- **غير منفّذ عمدًا:** Google Consent Mode v2 (قرار مالك)؛ لا سكربتات تسويق حاليًا رغم وجود فئة Marketing.
