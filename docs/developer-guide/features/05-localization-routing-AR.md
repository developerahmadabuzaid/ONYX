# 05 — التوطين والتوجيه ثنائي اللغة

## 1. ماذا تفعل الميزة

- **العربية افتراضية.** `/` → 302 إلى `/ar/` ما لم يختر الزائر لغة صراحة (كوكي `onyx.lang`). لا تحويل حسب `Accept-Language`؛ بدلًا من ذلك اقتراح قابل للإغلاق.
- **روابط مترجمة بالكامل:** مقاطع عربية + slugs عربية (`/ar/الخدمات/حلول-الذكاء-الاصطناعي`) ومقاطع إنجليزية + slugs إنجليزية (`/en/services/ai-solutions`).
- **لغة الصفحة العامة = قيمة `{lang}` في المسار فقط.** لغة واجهة الإدارة = تفضيل المسؤول ثم اختيار الزائر ثم الافتراضي.
- تُخرج الروابط مرمّزة UTF-8 (percent-encoded) وتُطبَّع NFC، والنص العربي يُخرج حروفًا لا كيانات `&#x…;`.

## 2. الملفات

| المسار | الدور |
|---|---|
| [ContentRouteSegments.cs](../../../src/Onyx.Contracts/Content/ContentRouteSegments.cs) | **المصدر المشترك** للمقاطع المترجمة لأقسام المحتوى والمقاطع المحجوزة — يستخدمه الـ API (بناء المسارات) وWeb (التوجيه) |
| [LanguageCatalog.cs](../../../src/Onyx.Web/Localization/LanguageCatalog.cs) | اللغات المهيأة (قسم الإعدادات `Localization`، صنف `WebLocalizationOptions`)، الاتجاه، الافتراضي، اسم كوكي الاختيار |
| [LanguageRouteConstraint.cs](../../../src/Onyx.Web/Localization/LanguageRouteConstraint.cs) | قيد `{lang:onyxlang}`: لغة غير مهيأة → 404 |
| [LocalizedRouteTable.cs](../../../src/Onyx.Web/Localization/LocalizedRouteTable.cs) | جدول الصفحات البرمجية ذات المقطع المترجم (نماذج التحويل، البحث، الأخبار)؛ يرفض تسجيل مقطع لصفحتين |
| [LocalizedRouteTransformer.cs](../../../src/Onyx.Web/Localization/LocalizedRouteTransformer.cs) | `/{lang}/{segment}/{**slug}` → متحكم مسجّل، وإلا → `PageController.Show` (محتوى CMS) |
| [OnyxRequestCultureProvider.cs](../../../src/Onyx.Web/Localization/OnyxRequestCultureProvider.cs) | اختيار الثقافة |
| [LanguageSuggestion.cs](../../../src/Onyx.Web/Localization/LanguageSuggestion.cs) | اقتراح لغة من `Accept-Language` إن لم يوجد اختيار صريح |
| [SitePaths.cs](../../../src/Onyx.Web/Localization/SitePaths.cs) | ترميز المسارات والروابط المطلقة |
| [SeoContext.cs](../../../src/Onyx.Web/Localization/SeoContext.cs) | بيانات canonical/hreflang/robots/OG/JSON-LD لكل طلب |
| [HomeController](../../../src/Onyx.Web/Controllers/HomeController.cs)، [LanguageController](../../../src/Onyx.Web/Controllers/LanguageController.cs)، [PageController](../../../src/Onyx.Web/Controllers/PageController.cs) | |
| [SupportedLanguages.cs](../../../src/Onyx.Application/Localization/SupportedLanguages.cs) | اللغات في الـ API (`ar`، `en`) — الشرط لاكتمال النشر |
| الموارد: `src/Onyx.Web/Resources/SharedResource.ar.resx` و`SharedResource.en.resx` | نصوص الواجهة (المفاتيح متطابقة في الملفين) |

## 3. خريطة المقاطع (متحقق منها)

| القسم | ar | en |
|---|---|---|
| الخدمات (الركائز) | `الخدمات` | `services` |
| القطاعات | `القطاعات` | `industries` |
| الأعمال (المشاريع) | `أعمالنا` | `work` |
| دراسات الحالة | `أعمالنا/دراسات-الحالة` | `work/case-studies` |
| المعرفة (الرؤى) | `المعرفة` | `insights` |
| المواضيع | `المعرفة/مواضيع` | `insights/topics` |
| المصطلحات | `المصطلحات` | `glossary` |
| التكاملات | `التكاملات` | `integrations` |
| نماذج التحويل | `ابدأ-مشروعك`، `احجز-استشارة`، `تواصل-معنا`، `إدارة-الحجز` | `start-a-project`، `book-a-consultation`، `contact`، `manage-booking` |
| البحث | `بحث` | `search` |
| الأخبار | `آخر-الأخبار` | `news` |

الخدمات الفرعية والصفحات تعيش تحت رابط الأب. المقاطع المحجوزة لا يمكن أن تكون slug صفحة عليا: `ContentRouteSegments.Reserved` (يشمل أيضًا `preview`، `language`، `consent`، `media`، `sitemap(s)`، `admin`، `api`، `error`، `robots`).

## 4. التدفق

```
GET /  → HomeController.Root: Cache-Control: private, no-store؛ Vary: Cookie → 302 /{choice|ar}/
GET /ar → 301 /ar/   (رابط واحد لكل صفحة)
GET /{lang}/language?return=… → LanguageController.Switch: كوكي onyx.lang (سنة، HttpOnly، Secure، Lax)،
                                 العودة فقط لمسار محلي في اللغة المختارة (لا open redirect)
GET /{lang}/{path}
  → LocalizedRouteTransformer:
       المقطع الأول مسجّل في LocalizedRouteTable → المتحكم البرمجي (LeadPage، SiteSearch، NewsHub…)
       وإلا → PageController.Show(lang, path)
            مسار ينتهي بـ "/" → 301 بدونه
            SectionRoutes.Match → قائمة القسم (noindex إن كانت فارغة؛ canonical لنفسها)
            وإلا IPublicContentApiClient.ResolveAsync → 200 / 301 (LocalRedirectPermanent، مرمّز) / 410 / 404
```

## 5. الدوال المفتاحية

| الدالة | السلوك |
|---|---|
| `LocalizedRouteTable.Register(LocalizedRoute)` | يرمي `InvalidOperationException` إن كان المقطع (بعد NFC) مستخدمًا لصفحة أخرى — يمنع التعارض عند الإقلاع |
| `LocalizedRouteTable.PathFor(key, language, params slugs)` | مسار جذري مرمّز للروابط الصادرة |
| `OnyxRequestCultureProvider.DetermineProviderCultureResult` | `/admin…` → claim `lang` ثم كوكي الاختيار؛ غير ذلك → قيمة المسار؛ الاحتياط = الافتراضي |
| `LanguageSuggestion.Suggest(httpContext, currentLanguage)` | لا اقتراح إن وُجد كوكي الاختيار |
| `SitePaths.Encode(path)` | تطبيع NFC وترميز كل مقطع؛ يحفظ الشرطة المائلة الختامية للصفحة الرئيسية |
| `ContentPathService.NormalizePath` (API) | النظير في الـ API |
| `Slug.Normalize` (Domain) | NFC، حذف التشكيل والتطويل، أرقام عربية → غربية، أحرف عربية/a–z/0–9 وشرطات مفردة؛ حد 80 (موصى 60) |

## 6. الاختبارات

- [PublicSiteTests](../../../tests/Onyx.Web.Tests/PublicSiteTests.cs) — التحويل للعربية افتراضيًا، تجاهل Accept-Language، احترام الاختيار، `lang`/`dir`، الحروف العربية لا الكيانات، 404 مترجم، تبديل اللغة الآمن.
- [LocalizationAndHandlerUnitTests](../../../tests/Onyx.Web.Tests/LocalizationAndHandlerUnitTests.cs) — الترميز، أشكال التطبيع، منع تكرار المقطع، المحوّل.
- [SeoFoundationSiteTests](../../../tests/Onyx.Web.Tests/SeoFoundationSiteTests.cs) — الشكل الوحيد لرابط الرئيسية، عدم تخزين التحويل المعتمد على الكوكي.
- [SlugAndBodyRulesTests](../../../tests/Onyx.Domain.Tests/Content/SlugAndBodyRulesTests.cs) — تطبيع الـ slug.

## 7. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| إضافة لغة | `SupportedLanguages.All` (API)، `Localization:Languages` (Web)، كل `ContentRouteSegments`، `LeadPages`/`SearchPages`/`NewsPages`، ملف `.resx` جديد، قوالب البريد، `CmsOptions.Languages`؛ **كل المحتوى المنشور سيصبح غير قابل للاعتماد حتى تكتمل اللغة الجديدة** (`CheckPublishable`) |
| تغيير مقطع قسم | الروابط المنشورة تنكسر — أضف قواعد `Redirect` أو مسارات تاريخية؛ حدّث `Reserved` |
| صفحة برمجية جديدة بمقطع مترجم | سجّلها في `LocalizedRouteTable` (في `WebServiceCollectionExtensions`) وأضف مقاطعها إلى `ContentRouteSegments.Reserved` حتى لا يأخذها محرر |
| سلوك الصفحة الرئيسية `/` | يجب أن يبقى `no-store` و`Vary: Cookie` |

## 8. مثال

**لإضافة صفحة «من نحن» برمجية بمسار مترجم:** لا تفعل — صفحات المحتوى تُنشأ في CMS (`ContentType.Page`) وتأخذ مسارها من الـ slug. الصفحات البرمجية فقط للسلوك المُدار بالكود (نماذج، بحث، أدوات) وفق CLAUDE.md §6.

## 9. الحالة

- **منفّذ:** كل ما سبق.
- **مؤجّل:** صفحات الأسواق (Phase 3)؛ اللغة والسوق بُعدان منفصلان ولا توجد نسخ موقع لكل دولة.
