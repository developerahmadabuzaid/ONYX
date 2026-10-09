# 06 — SEO والتحويلات وخرائط الموقع وIndexNow

## 1. ماذا تفعل الميزة

- إعدادات SEO مستقلة لكل لغة لكل عنصر ([SeoProfile](../../../src/Onyx.Domain/Content/SeoProfile.cs)): كلمة رئيسية وثانوية، نية البحث، meta، canonical، robots، أنواع البيانات المنظمة، Open Graph، السوق المستهدف، حالة العمل وملاحظات. **لا مقاييس** — الأداء يأتي فقط من مزوّدين متصلين.
- تحويلات تلقائية لكل slug سابق (عبر `content_paths`) + **قواعد يدوية** للمسارات غير المملوكة لمحتوى ([Redirect](../../../src/Onyx.Domain/Content/Redirect.cs)): 301 لهدف داخلي فقط أو 410 صريح. لا روابط خارجية، لا تحويل تلقائي للرئيسية، لا سلاسل.
- خرائط موقع لكل لغة + فهرس، `robots.txt` يمنع كل شيء قبل الإطلاق.
- hreflang/canonical: فقط الصفحات القابلة للفهرسة وذاتية الـ canonical تشارك.
- IndexNow (Bing وآخرون؛ ليس Google): غير نشط حتى يُهيأ ويُطلق الموقع.
- بيانات Organization المنظمة: خلف بوابة تحقق (مفتاح المالك + شعار رسمي + موقع الهوية = نفس أصل الموقع).

## 2. الملفات

| المسار | الدور |
|---|---|
| [ContentTranslation.UpdateSeo / Validate](../../../src/Onyx.Domain/Content/ContentTranslation.cs) | التحقق: canonical مسار موقع آمن فقط، السوق رمز ISO من حرفين، حدود الأطوال، أنواع بيانات منظمة معروفة |
| [Redirect.cs](../../../src/Onyx.Domain/Content/Redirect.cs) | `IsSafePath`، يرفع `PublicUrlsChanged` عند الإنشاء/التعديل/`AnnounceRemoval` |
| [RedirectAdministrationService.cs](../../../src/Onyx.Application/Content/RedirectAdministrationService.cs) | إدارة القواعد اليدوية |
| [PublicPageFingerprint.cs](../../../src/Onyx.Application/Content/PublicPageFingerprint.cs) | ما يحرّك `lastmod` |
| [PublicContentBuilder.cs](../../../src/Onyx.Application/Content/PublicContentBuilder.cs) | hreflang والبيانات المنظمة المسموحة |
| [src/Onyx.Application/Seo/](../../../src/Onyx.Application/Seo/) | `ISearchEngineNotifier`، `PublicUrlsChangedHandler`، `OrganizationProfileService` |
| [src/Onyx.Infrastructure/Seo/](../../../src/Onyx.Infrastructure/Seo/) | `IndexNowNotifier`، `IndexNowOptions`، `PublicSiteOptions` (`Site`)، `SeoOptions` |
| Web: [SitemapController](../../../src/Onyx.Web/Controllers/SitemapController.cs)، [SearchEngineVerificationController](../../../src/Onyx.Web/Controllers/SearchEngineVerificationController.cs)، [Content/SeoContextExtensions.cs](../../../src/Onyx.Web/Content/SeoContextExtensions.cs)، [Content/StructuredDataBuilder.cs](../../../src/Onyx.Web/Content/StructuredDataBuilder.cs)، [RedirectsController (Admin)](../../../src/Onyx.Web/Areas/Admin/Controllers/RedirectsController.cs) | |

## 3. نقاط الدخول

| السطح | المسار |
|---|---|
| API إدارة | `PUT admin/content/{id}/translations/{lang}/seo` (`Seo.Edit`)؛ `admin/redirects` (`Redirects.Manage`) |
| API عام | `GET content/sitemap?language`، `GET news/sitemap?language`، `GET organization`، `GET search-engine-verification/indexnow-key` |
| Web | `/robots.txt`، `/sitemap.xml`، `/sitemaps/{lang}.xml`، `/indexnow-key.txt`؛ Admin `admin/redirects` |
| حدث | `PublicUrlsChanged` → `PublicUrlsChangedHandler` (Worker) |

## 4. التدفقات

**قاعدة تحويل يدوية** — `RedirectAdministrationService.CreateAsync`: لغة مدعومة، مسار آمن (بلا `:`/`?`/`#`/`\`/مسافات/مقاطع نقطية)، لا قاعدة موجودة لنفس المسار (`Redirect.Exists`)، **المسار ليس مملوكًا لمحتوى** حاليًا أو تاريخيًا (`Redirect.ManagedByContent`)، الهدف داخلي، والاتباع من الهدف لا يعود للمصدر (`Redirect.Loop`). اللغة ومسار المصدر غير قابلين للتغيير (`Redirect.Immutable`). تدقيق `RedirectCreated/Changed/Deleted` + حدث `PublicUrlsChanged`.

**خريطة الموقع** — `PublicContentService.GetSitemapAsync(lang)` يعيد الصفحات الحية القابلة للفهرسة ذاتية الـ canonical مع البدائل؛ `SitemapController.Language` يجمعها مع مدخلات الأخبار ويكتب روابط مطلقة مرمّزة. قبل الإطلاق: `X-Robots-Tag: noindex, nofollow`.

**robots.txt** — `Site:AllowIndexing = false` → `Disallow: /`. بعد الإطلاق: يمنع `/admin`، `/{lang}/preview`، `/{lang}/language`، `/media/*?preview=` ويضيف `Sitemap:`.

**IndexNow** — `IndexNowNotifier.IsActive = Site:AllowIndexing && Site:PublicBaseUrl https && IndexNow:Key صالح (8–128 حرفًا/رقمًا/شرطة)`. يرسل روابط هذا المضيف فقط، مطلقة ومرمّزة، بدفعات حتى 10,000. 429/5xx → `SearchEngineNotificationDeferredException` فيعيد الـ outbox المحاولة بتراجع؛ أخطاء الإعداد تُبلَّغ دون إعادة لا نهائية. لا يُسجَّل المفتاح.

**Organization** — `OrganizationProfileService.GetVerifiedAsync` يعيد null ما لم: `Seo:OrganizationStructuredDataEnabled = true` + شعار الهوية موجود كوسيط + موقع الهوية = نفس أصل `Site:PublicBaseUrl`. الاسم ثابت `"Onyx Vanguard Solutions"`. لا يُملأ شيء ناقص.

## 5. البيانات المنظمة

المحرر يختار من `WebPage | BreadcrumbList | Service | FaqPage | Article`؛ تُخرج فقط ما يدعمه المحتوى المرئي، وبوابة الجودة تمنع: `FaqPage` بلا أسئلة مرئية، `Service` على غير خدمة، `Article` على غير رؤية. العنوان H1 دائمًا عنوان الترجمة (الكتل H2–H4 فقط).

## 6. الاختبارات

- [SeoFoundationApiTests](../../../tests/Onyx.API.IntegrationTests/Content/SeoFoundationApiTests.cs) — `lastmod` يتحرك فقط بتغيّر مرئي في تلك اللغة؛ حقول التخطيط الداخلي لا تحركه؛ الصفحات المعاد توجيه canonical لها لا تشارك في hreflang؛ noindex بلا بدائل؛ إعلان الروابط المتغيرة بدقة؛ Organization/IndexNow غير متاحين افتراضيًا.
- [SearchEngineActivationApiTests](../../../tests/Onyx.API.IntegrationTests/Content/SearchEngineActivationApiTests.cs)، [IndexNowNotifierTests](../../../tests/Onyx.API.IntegrationTests/Seo/IndexNowNotifierTests.cs).
- [CmsAdministrationApiTests](../../../tests/Onyx.API.IntegrationTests/Content/CmsAdministrationApiTests.cs) (`Manual_redirects_reject_external_targets_and_loops`)، [SlugAndBodyRulesTests](../../../tests/Onyx.Domain.Tests/Content/SlugAndBodyRulesTests.cs) (`Redirects_never_target_external_sites_or_the_homepage_by_default`).
- Web: [SeoFoundationSiteTests](../../../tests/Onyx.Web.Tests/SeoFoundationSiteTests.cs)، [ContentSiteTests](../../../tests/Onyx.Web.Tests/ContentSiteTests.cs) (`Robots_blocks_everything_before_launch`، `Redirect_targets_can_never_leave_the_site`).

## 7. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| حقل SEO جديد | `SeoProfile`، `ContentTranslation.UpdateSeo/Validate/Seo`، `ContentSnapshot`، `PublicPageFingerprint` (هل هو مرئي؟)، `ContentMapping.ToSeo`، DTO في Contracts، `ContentSeoForm` في Web، ترحيل |
| قواعد hreflang | `PublicContentBuilder` (الدالة الموثقة «Indexable and self-canonical»)، `GetSitemapAsync`، `SeoContextExtensions` في Web، `SeoFoundationRulesTests.Only_indexable_self_canonical_pages_take_part_in_hreflang` |
| `Site:AllowIndexing` | يجب أن يكون متطابقًا في Web وAPI وWorker (تعليق `PublicSiteOptions`) |
| حالات `ResolveAsync` | `PageController.Show` في Web يعتمد على قيم `Kind` النصية (`Content`، `Redirect`، `Gone`، `NotFound`) |

## 8. مثال

**لنقل صفحة إلى رابط جديد دون كسر القديم:** غيّر الـ slug في CMS ثم اعتمد وانشر — `ContentPathService.ApplyAsync` يتقاعد المسار القديم ويحوّله 301 مباشرة للجديد ويعلن الرابطين. لا تُنشئ قاعدة يدوية (ستُرفض بـ `Redirect.ManagedByContent`).

## 9. الحالة

- **منفّذ:** كل ما سبق.
- **غير نشط حتى الإعداد/الإطلاق:** IndexNow، بيانات Organization، الفهرسة (robots/noindex).
- **ليس جزءًا من هذا المستودع:** تكامل منصة Onyx SEO (`ISeoAnalysisEngine` — Phase 2؛ مفتاح المزوّد `seo.onyx-platform` محجوز فقط).
