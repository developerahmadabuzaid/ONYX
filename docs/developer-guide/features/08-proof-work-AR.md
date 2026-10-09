# 08 — الأعمال والإثبات (المشاريع، دراسات الحالة، التكاملات، عناصر الإثبات، المشاريع المميزة)

## 1. ماذا تفعل الميزة

تطبيق **سياسة الإثبات** (CLAUDE.md §2.1): كل إثبات عام حقيقي وموثّق. في الكود:
- `Project`، `CaseStudy`، `Integration` أنواع «حاملة للإثبات» (`ContentItem.IsProofBearing`). لا تُنشر ولا تُجدول إلا بتحقق مسجّل (`ProofVerification`: من، متى، المصدر، إذن العميل).
- **أي تعديل يمسح التحقق** (`ContentItem.BeginEdit`)؛ الإثبات الحي لا يمكن إلغاء تحققه قبل سحبه (`Content.RevokeLive`).
- دراسة الحالة تنتمي لمشروع (`ParentId` إلزامي) لكنها تعيش في قسم واحد `work/case-studies` (نطاق slug واحد).
- **عناصر الإثبات** (`ProofItem`): قدرة، مقياس (قيمة + تاريخ صحتها)، شهادة (اسم المقتبس + إذن العميل إلزامي). تظهر فقط إن كانت موثقة **ومنشورة** وصفحتها حية؛ أي تعديل يعيدها مسودة غير موثقة.
- **المشاريع المميزة**: حتى 3 خانات على الرئيسية، فقط لمشروع حي وموثق؛ الحد مفروض بقيود فريدة في القاعدة حتى مع الطلبات المتزامنة.
- صفحات الخدمة/الصناعة: رابط سياقي واحد فقط لمشروع أو دراسة حالة (`Relation.OneWorkLink`).
- لا أرقام أو عملاء أو شهادات مختلقة في أي مكان؛ القسم يُبنى ويُخفى إن لم يوجد محتوى.

## 2. الملفات

| المسار | الدور |
|---|---|
| [ContentItem.cs](../../../src/Onyx.Domain/Content/ContentItem.cs) | `VerifyProof`، `RevokeProofVerification`، `PublicationGuard` |
| [src/Onyx.Domain/Proof/](../../../src/Onyx.Domain/Proof/) | `ProofVerification.Create`، `ProofItem`، `ProofItemKind`، `ProofItemStatus`، `FeaturedProject` (`MaxFeatured = 3`)، `ClientPermission` |
| [ContentProofService.cs](../../../src/Onyx.Application/Proof/ContentProofService.cs) | التحقق والإلغاء والتمييز |
| [ProofItemService.cs](../../../src/Onyx.Application/Proof/ProofItemService.cs) | دورة عناصر الإثبات |
| [FeaturedProjectConfiguration](../../../src/Onyx.Infrastructure/Persistence/Configurations/Content/FeaturedProjectConfiguration.cs) | `SlotIndex` و`ContentIndex` الفريدان |
| API: [AdminContentProofController](../../../src/Onyx.API/Controllers/Admin/AdminContentProofController.cs)، [AdminProofItemsController](../../../src/Onyx.API/Controllers/Admin/AdminProofItemsController.cs)، `GET content/featured-projects`، `GET content/listing?section=work…` | |
| Web: [ProofItemsController (Admin)](../../../src/Onyx.Web/Areas/Admin/Controllers/ProofItemsController.cs)، [HomeController](../../../src/Onyx.Web/Controllers/HomeController.cs) (FeaturedWork)، [PageController](../../../src/Onyx.Web/Controllers/PageController.cs) | |

## 3. الدوال المفتاحية

| الدالة | الصلاحية (API + خدمة) | السلوك |
|---|---|---|
| `ContentProofService.VerifyAsync(id, VerifyProofRequest, ct)` | `Proof.Verify` + نطاق قراءة النوع | `ClientPermission` = `NotApplicable`/`Granted`؛ `Granted` يتطلب ملاحظة الإذن؛ المصدر 1–1000 حرف. تدقيق `ProofVerified` **بدون الملاحظات** (قد تسمي جهات اتصال) |
| `ContentProofService.RevokeAsync` | `Proof.Verify` | يفشل للمحتوى الحي؛ تدقيق `ProofVerificationRevoked` |
| `ContentProofService.SetFeaturedAsync(id, SetFeaturedRequest, ct)` | `Projects.Publish` | مشروع فقط، تطابق النسخة، حي وموثق (`Featured.NotEligible`)، أول خانة فارغة 1..3 (`Featured.Full`)؛ تعارض متزامن → 409 عبر ترجمة القيد في `ApplicationDbContext`. تدقيق `ProjectFeatured`/`ProjectUnfeatured` |
| `ProofItemService.CreateAsync/UpdateAsync` | API: `Content.Edit` + نطاق تحرير النوع الأب؛ الشهادات تتطلب `Testimonials.Create/Edit` | الأب مشروع أو دراسة حالة فقط؛ الصفحة والنوع غير قابلين للتغيير (`Proof.Immutable`)؛ تعديل عنصر منشور يسحبه (تدقيق `ProofItemUnpublished` بسبب "edited") |
| `ProofItemService.VerifyAsync` | `Proof.Verify` | الشهادة تتطلب `ClientPermission.Granted` (`Proof.ClientPermissionRequired`) |
| `ProofItemService.PublishAsync/UnpublishAsync` | API على مستوى المتحكم: `Content.View` فقط؛ **الخدمة** تفرض `Content.Publish` للعناصر العادية و`Testimonials.Publish` للشهادات، مع نطاق تحرير النوع | النشر يتطلب تحققًا؛ إن كانت الصفحة حية → `RecordLiveContentModified` للغات العنصر + إعلان الروابط (يتحرك `lastmod` ويُبلَّغ IndexNow) |
| `ProofItemService.DeleteAsync` | `Content.Edit` (+ `Testimonials.Delete` للشهادات) | تدقيق `ProofItemDeleted` |
| `PublicContentBuilder` (الإثبات المنشور الموثق بلغة الصفحة) | — | ما يُعرض للعامة |

## 4. قاعدة البيانات

`content_items.proof_verification` (jsonb، `[NotAudited]`)، `proof_items` (Cascade مع العنصر)، `featured_projects` (`slot` فريد، `content_item_id` فريد، Cascade).

## 5. الاختبارات

- [WorkProofInsightRulesTests](../../../tests/Onyx.Domain.Tests/Content/WorkProofInsightRulesTests.cs) — لا نشر بلا تحقق، أي تعديل يمسح التحقق، التحقق يحتاج مصدرًا وإذنًا مسجلًا، دراسة الحالة تنتمي لمشروع، خانات 1–3، الشهادات تتطلب إذن العميل.
- [WorkAndProofApiTests](../../../tests/Onyx.API.IntegrationTests/Content/WorkAndProofApiTests.cs) — 11 اختبارًا أهمها: `At_most_three_live_verified_projects_can_be_featured_even_under_concurrent_requests`، `Proof_items_appear_publicly_only_when_verified_and_published_and_leave_on_edit`، `Service_pages_link_to_at_most_one_project_or_case_study`، `Testimonials_need_the_testimonial_permissions`.
- [WorkAndInsightsSiteTests](../../../tests/Onyx.Web.Tests/WorkAndInsightsSiteTests.cs) — صفحات المشاريع تعرض فقط ما تعيده الـ API، الرئيسية تُخفي الأعمال المميزة إن لم توجد.

## 6. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| نوع إثبات جديد | `ContentItem.IsProofType`، `CmsOptions.ProofTypes` في Web، `ContentQualityGate.CheckProofAndProvenance` |
| `FeaturedProject.MaxFeatured` | قيد الخانات + `PublicContentService.GetFeaturedProjectsAsync` + CLAUDE.md §2.2 (الحد 3 قرار عمل) |
| قواعد صلاحيات عناصر الإثبات | `ProofItemService.Allowed`/`LoadAsync`، `AdminProofItemsController`، `WorkAndProofApiTests` |
| حقل جديد في `ProofVerification` | سجل jsonb متوافق رجوعيًا، وتذكّر عدم تدقيق الملاحظات |

## 7. مثال

**إضافة شهادة عميل:** محرر بـ `Testimonials.Create` + `Projects.Edit` ينشئ العنصر → شخص بـ `Proof.Verify` يسجل المصدر و`Granted` مع ملاحظة الإذن → شخص بـ `Testimonials.Publish` + `Projects.Edit` ينشره → يظهر فقط إن كان المشروع حيًا وبلغة مكتوبة فيها الشهادة.

## 8. الحالة

- **منفّذ:** كل ما سبق.
- **محتوى:** المشاريع الستة المؤكدة (CLAUDE.md §2.2) تُدخل كمحتوى عبر CMS بعد التحقق — لا شيء مزروع في القاعدة. حدود الادعاءات في `docs/architecture.md` §9.
