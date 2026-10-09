# 07 — مكتبة الوسائط

## 1. ماذا تفعل الميزة

رفع الصور وتخزينها وعرضها بأمان:
- النوع يُحدَّد من **توقيع البايتات** لا من الاسم ولا من الترويسة.
- تُزال البيانات الوصفية (EXIF/XMP/IPTC/تعليقات/مقاطع خاصة — قد تحوي موقع GPS) وأي بايتات بعد نهاية الصورة (حمولات polyglot). ملفات تعريف الألوان والحركة تُحفظ.
- JPEG صارم (SOI + رأس إطار + scan واحد على الأقل + EOI وإلا رفض). **SVG وكل الصيغ الأخرى مرفوضة**. المسموح: JPEG، PNG، WebP، GIF.
- التخزين تحت مفتاح عشوائي يولده الخادم `yyyy/MM/{guid}.{ext}`؛ اسم الملف الأصلي للعرض فقط.
- الملف المتطابق (SHA-256) يُخزن مرة واحدة.
- لا حذف لوسيط مستخدم (في نسخة عمل أو نسخة حية أو كشعار الهوية).
- الوصول العام فقط لوسيط تستخدمه صفحة حية، أو نسخة عمل يمنحها رمز معاينة صالح، أو الشعار الرسمي حين تكون بيانات Organization موثقة.

## 2. الملفات

| المسار | الدور |
|---|---|
| [src/Onyx.Domain/Media/](../../../src/Onyx.Domain/Media/) | `MediaAsset` (`Create` يتحقق من صيغة المفتاح، `UpdateTexts`، `MarkDeleted` يرفع `MediaAssetDeleted`، `SafeDisplayName`)، `MediaUsage`، `MediaUsageContext` (Body، FeaturedImage، OpenGraphImage، BrandLogo) |
| [MediaService.cs](../../../src/Onyx.Application/Media/MediaService.cs) | `IMediaService` |
| [MediaUsageIndexer.cs](../../../src/Onyx.Application/Media/MediaUsageIndexer.cs) | يعيد بناء استخدامات عنصر (عمل/حي) |
| [MediaStorageCleanupHandler.cs](../../../src/Onyx.Application/Media/MediaStorageCleanupHandler.cs) | حذف الملف عبر الـ outbox |
| [ImageInspector.cs](../../../src/Onyx.Infrastructure/Media/ImageInspector.cs) | `IImageInspector` — تحليل بنيوي داخلي بلا حزمة صور |
| [LocalFileMediaStorage.cs](../../../src/Onyx.Infrastructure/Media/LocalFileMediaStorage.cs) | `IMediaStorage` — مفاتيح محققة ومحصورة داخل الجذر (لا path traversal) |
| [MediaOptions.cs](../../../src/Onyx.Infrastructure/Media/MediaOptions.cs) | قسم `Media` |
| API: [AdminMediaController](../../../src/Onyx.API/Controllers/Admin/AdminMediaController.cs)، [MediaController](../../../src/Onyx.API/Controllers/MediaController.cs) | |
| Web: [MediaController](../../../src/Onyx.Web/Controllers/MediaController.cs)، [MediaLibraryController (Admin)](../../../src/Onyx.Web/Areas/Admin/Controllers/MediaLibraryController.cs) | بروكسي من نفس الأصل |

## 3. التدفقات

**الرفع** — `POST /api/v1/admin/media` (`Media.Upload`، حد الطلب 11 MB):
`MediaService.UploadAsync(stream, fileName, ct)` → قراءة محدودة بـ `Media:MaxUploadBytes` (`Media.TooLarge`) → `ImageInspector.InspectAndClean` (أبعاد ≤ 8000، بكسلات ≤ 40M، إطارات GIF ≤ 1000) → SHA-256؛ موجود مسبقًا → يُعاد نفس الأصل → حفظ الملف ثم الصف + تدقيق `MediaUploaded`؛ فشل الحفظ في القاعدة → حذف الملف المخزن.

**الحذف** — `MediaService.DeleteAsync`: أي استخدام → `Media.InUse` (409)؛ شعار الهوية → `Media.BrandLogo`؛ وإلا `MarkDeleted()` + حذف الصف + تدقيق `MediaDeleted` في معاملة واحدة، ثم الـ Worker يحذف الملف من حدث `MediaAssetDeleted` (يُعاد عند الفشل، ويصبح Dead-letter مع إشعار إن استمر). حذف ملف غير موجود ينجح.

**العرض العام** — المتصفح → `GET /media/{id}?preview=…` (Onyx.Web) → `GET /api/v1/media/{id}?previewToken=…` → `OpenPublicAsync`. Web يمرّر **أنواع الصور الأربعة فقط**، ويضع `Cache-Control: no-store` لطلبات المعاينة (وإلا `public, max-age=86400` افتراضيًا). الـ API تعيد ETag = SHA-256.

**الاستخدامات** — `MediaUsageIndexer.RebuildAsync(contentItemId, workingCopy, live, ct)` يُستدعى من `ContentPublisher` عند النشر/السحب ومن حفظ التعديلات؛ المراجع: الصورة البارزة، صورة OG لكل لغة، كتل `Image` في الجسم.

## 4. الإعدادات

`Media:StorageRoot` (خارج أي web root، ومشمول بالنسخ الاحتياطي)، `MaxUploadBytes` (10 MB)، `MaxWidth`/`MaxHeight` (8000)، `MaxPixels` (40M)، `MaxFrames` (1000). جذر التخزين يُتحقق منه عند أول استخدام (فقط المضيف الذي يخدم الوسائط يحتاجه).

## 5. الاختبارات

- [ImageSanitizationTests](../../../tests/Onyx.API.IntegrationTests/Media/ImageSanitizationTests.cs) — 11 اختبارًا لكل صيغة (حفظ الإطارات والتوقيت، إزالة EXIF/التعليقات/ما بعد النهاية، رفض الملفات المشوهة وSVG).
- [MediaDeletionApiTests](../../../tests/Onyx.API.IntegrationTests/Content/MediaDeletionApiTests.cs) — إعادة محاولة حذف الملف حتى يختفي؛ الوسائط المستخدمة لا تُحذف.
- [CmsAdministrationApiTests](../../../tests/Onyx.API.IntegrationTests/Content/CmsAdministrationApiTests.cs) — التحقق بالمحتوى ورفض المتنكر، الوسائط غير المستخدمة ليست عامة.
- [ContentSiteTests](../../../tests/Onyx.Web.Tests/ContentSiteTests.cs) (`Media_is_proxied_only_for_images_and_never_cached_in_previews`).

## 6. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| صيغة جديدة | `ImageInspector` (محلل بنيوي جديد)، `MediaAsset.StorageKeyPattern`، `AllowedTypes` في Web `MediaController`، `ImageSanitizationTests` — ولا تسمح بـ SVG دون تنظيف معتمد (CLAUDE.md §14) |
| مخزن سحابي | نفّذ `IMediaStorage` جديدًا وسجّله بدل `LocalFileMediaStorage` — لا تغيير في Domain/CMS |
| مكان جديد يستخدم وسيطًا | أضف `MediaUsageContext` وغطّه في `MediaUsageIndexer.References` وإلا سيُسمح بحذف وسيط مستخدم |
| قواعد الوصول العام | `MediaService.OpenPublicAsync`، `ContentPreviewService.CanViewMediaAsync`، `OrganizationProfileService` |

## 7. الحالة

- **منفّذ:** كل ما سبق.
- **مؤجّل (يحتاج حزمة صور معتمدة):** إعادة الترميز والأحجام المتجاوبة. لا فيديو ولا مستندات (`MediaKind` = `Image` فقط).
