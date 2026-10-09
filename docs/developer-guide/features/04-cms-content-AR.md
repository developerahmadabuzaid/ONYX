# 04 — نظام إدارة المحتوى: دورة النشر والمراجعات والمعاينة والدعوات للإجراء

## 1. ماذا تفعل الميزة

CMS ثنائي اللغة لأنواع محتوى تشترك في نواة واحدة (دورة حياة، مراجعات، slug، SEO): `ServicePillar`، `Service`، `Industry`، `Page`، `Project`، `CaseStudy`، `Insight`، `Topic`، `GlossaryTerm`، `Integration` ([ContentType](../../../src/Onyx.Domain/Content/ContentType.cs)).

**مبادئ ملزمة في الكود:**
- **نسخة عمل منفصلة عن النسخة الحية.** الموقع العام يخدم لقطة المراجعة المنشورة (`LiveRevisionId`) ولا يقرأ `content_translations` أبدًا. أي تعديل يعيد نسخة العمل إلى Draft بينما تبقى الصفحة الحية كما هي.
- **الاعتماد والنشر خطوتان وصلاحيتان منفصلتان** (`Content.Approve` ≠ `Content.Publish`).
- **الأجسام ليست HTML**: كتل منظمة نصًا عاديًا ([ContentBlock](../../../src/Onyx.Domain/Content/ContentBlock.cs)) تُرمَّز عند العرض → لا مسار XSS مخزن.
- **كل لغة تُكتب بشكل مستقل** ولا يُنشر شيء إلا والـ `ar` و`en` كاملتان (`SupportedLanguages.All`).
- **الروابط لا تنكسر**: تغيّر الـ slug يُنشئ تحويل 301 تلقائيًا، والمحتوى المسحوب يجيب 410 حتى يقرر محرر (لا تحويل للصفحة الرئيسية أبدًا).

## 2. دورة الحياة

```
          UpdateTranslation/UpdateSeo/UpdateSettings (أي تعديل → Draft، تُمسح الموافقة والتحقق من الإثبات)
                 │
Draft ──SubmitForReview──► PendingReview ──Approve──► Approved ──Publish──► Published (حي)
  ▲                          │ Reject(note)              │  └──Schedule(t)──► Scheduled ──(Worker عند t)──► Published
  └──────────────────────────┘                           │                     │ CancelSchedule / FailSchedule
                                                          ◄─────────────────────┘
Published ──Unpublish(resolution)──► Draft (غير حي) ؛ ScheduleUnpublish(t) → Worker يسحبه بـ PendingDecision
أي حالة ──Archive(resolution)──► Archived ──RestoreFromArchive──► Draft
محتوى كان حيًا ثم سُحب ──ResolveOffline(Gone | RedirectToContent)──► قرار نهائي لروابطه
```

`ContentStatus` يصف **نسخة العمل**؛ «حي أم لا» سؤال منفصل: `IsLive = LiveRevisionId != null && Status != Archived`. عنصر منشور يُحرَّر الآن حالته Draft لكنه ما زال حيًا.

حراس Domain لا يمكن تجاوزها من أي مسار (محرر، API، Worker) — `ContentItem.PublicationGuard()`:
- محتوى إثبات (`Project`، `CaseStudy`، `Integration`) بلا `ProofVerification` → `Content.ProofNotVerified`.
- `Insight` بأصل `AiAssisted` بلا موافقة بشرية مسجلة (`ApprovedById`، `ApprovedAtUtc`، `ApprovedRevisionId`) → `Content.AiApprovalRequired`.
- وفي `CheckPublishable`: ترجمات ناقصة → `Content.IncompleteTranslations`؛ صفحة صناعة دون الحد الأدنى (ملخص + 3 أقسام H2 لكل منها كتلة غير عنوان، في كل لغة) → `Content.IndustryMinimum`.

## 3. الملفات والأصناف

| الطبقة | المسار | المحتوى |
|---|---|---|
| Domain | [src/Onyx.Domain/Content/](../../../src/Onyx.Domain/Content/) | `ContentItem` (الجذر والانتقالات والحراس)، `ContentTranslation` (نسخة عمل لغة + SEO)، `ContentRevision` (لقطة غير قابلة للتغيير)، `ContentPath`، `ContentPreviewToken`، `CallToAction`، `CtaDefault`، `ContentRelation`، `ContentBodyRules`، `Slug`، `TextNormalization`، الأحداث `ContentPublicationChanged` و`PublicUrlsChanged` |
| Application | [src/Onyx.Application/Content/](../../../src/Onyx.Application/Content/) | انظر الجدول §5 |
| Infrastructure | [Persistence/Repositories/Content/](../../../src/Onyx.Infrastructure/Persistence/Repositories/Content/)، [Persistence/Configurations/Content/](../../../src/Onyx.Infrastructure/Persistence/Configurations/Content/) | المستودعات (`ContentRepository`، `ContentRevisionRepository`، `ContentPathRepository`…) والإعدادات |
| API | [AdminContentController](../../../src/Onyx.API/Controllers/Admin/AdminContentController.cs)، [AdminCtasController](../../../src/Onyx.API/Controllers/Admin/AdminCtasController.cs)، [ContentController](../../../src/Onyx.API/Controllers/ContentController.cs) | |
| Web Admin | [Areas/Admin/Controllers/ContentController.cs](../../../src/Onyx.Web/Areas/Admin/Controllers/ContentController.cs)، [Cms/BlockTextFormat.cs](../../../src/Onyx.Web/Areas/Admin/Cms/BlockTextFormat.cs)، [Cms/CmsOptions.cs](../../../src/Onyx.Web/Areas/Admin/Cms/CmsOptions.cs)، [CtasController](../../../src/Onyx.Web/Areas/Admin/Controllers/CtasController.cs) | محرر نصي للكتل (لا HTML) |
| Web عام | [PageController](../../../src/Onyx.Web/Controllers/PageController.cs)، [PreviewController](../../../src/Onyx.Web/Controllers/PreviewController.cs)، [Content/](../../../src/Onyx.Web/Content/) | العرض |
| Worker | [ContentSchedulingJob](../../../src/Onyx.Infrastructure/BackgroundJobs/ContentSchedulingJob.cs) | `every:1m` |

## 4. التدفق من الطرف إلى الطرف

### 4.1 التحرير

```
Admin UI (BlockTextFormat يحوّل النص إلى كتل؛ أخطاء التنسيق تُعرض محليًا دون استدعاء API)
 → PUT /api/v1/admin/content/{id}/translations/{lang} [Content.Edit]
 → ContentAdministrationService.UpdateTranslationAsync
     ContentAccess.ForEditAsync(id, version): نطاق النوع ({Type}.Edit) + SetExpectedVersion (xmin)
     ContentMapping.ToBlocks → Slug.Normalize → SlugTakenAsync(lang, slugScope, slug, excludeId)
     ContentItem.UpdateTranslation → BeginEdit(): Draft + مسح الموافقة + مسح ProofVerification
     SaveEditAsync → ContentRevisionRecorder.RecordAsync(kind = Edited)   (لا يُخزّن إن طابقت التجزئة آخر مراجعة)
     UnitOfWork.SaveChanges → تدقيق تلقائي على ContentTranslation (Body/Faqs مستثناة)
```

### 4.2 النشر — المسار الوحيد: `ContentPublisher.PublishAsync`

يستخدمه المحرر (`ContentWorkflowService.PublishAsync`) والـ Worker (`ContentSchedulingService.PublishDueAsync`) معًا:

1. `ContentQualityGate.EvaluateAsync` → أي خطأ يمنع (`Content.QualityGate` مع قائمة الأخطاء).
2. تحميل المراجعة المعتمدة وفك لقطتها.
3. `ContentPathService.ComputeAsync` → المسار لكل لغة (قسم مترجم أو مسار الأب + slug). الأب يجب أن يكون حيًا (`Content.ParentNotLive`)، والـ slug ليس محجوزًا (`ContentRouteSegments.Reserved`).
4. `ContentItem.Publish` أو `PublishScheduled` (الحراس أعلاه) → `LiveRevisionId = ApprovedRevisionId` + حدث `ContentPublicationChanged(true)`.
5. `ContentPathService.ApplyAsync`: المسار القديم يتقاعد (يبقى مملوكًا للعنصر ويحوَّل 301 للجديد)، يُرفض مسار يملكه عنصر آخر (`Content.PathTaken`) أو له قاعدة تحويل (`Content.PathHasRedirect`)؛ تدقيق `ContentSlugChanged`.
6. `RefreshDescendantsAsync`: نقل الأبناء الحيين تحت الرابط الجديد (حتى `MaxHierarchyDepth = 6`).
7. لكل لغة تغيّرت بصمتها (`PublicPageFingerprint`) → `RecordLiveContentModified` (يقود `lastmod`) وتضاف للروابط المتغيرة.
8. `AnnouncePublicUrls` → حدث `PublicUrlsChanged` (بحث + IndexNow).
9. `MediaUsageIndexer.RebuildAsync` (استخدامات الوسائط الحية والعمل).
10. تدقيق `ContentPublished`. المستدعي يلتزم الوحدة.

### 4.3 السحب والأرشفة — `ContentPublisher.TakeOfflineAsync`

يرفض إن كان للعنصر أبناء أحياء (`Content.HasLiveChildren`). هدف التحويل يجب أن يكون محتوى حيًا مختلفًا (`Content.RedirectTargetNotLive`). بعد الانتقال: إعادة بناء الاستخدامات، إعلان الروابط الحالية، تدقيق `ContentUnpublished`/`ContentArchived`، حدث `ContentPublicationChanged(false)`.

### 4.4 القراءة العامة — `PublicContentService.ResolveAsync(language, path)`

حتى 6 قفزات مع كشف الحلقات:
- مسار مملوك لعنصر حي وحالي من القفزة الأولى → `Content` (يُبنى بـ `PublicContentBuilder`).
- مسار قديم لعنصر → `FollowItemAsync` (تحويل واحد مباشر إلى المسار الحالي، أو 410/التحويل المختار إن كان العنصر غير حي).
- قاعدة `Redirect` نشطة: `Gone` → 410؛ هدف محتوى → اتبعه؛ هدف مسار → تحويل إن كان المسار الحالي لعنصر حي، وإلا تابع القفز.
- غير ذلك → `NotFound`.

`PublicContentBuilder` يحوّل الروابط الداخلية إلى المسارات الحالية (الروابط لمحتوى غير حي تصبح نصًا)، يضع نص alt سياقيًا، يبني فتات الخبز، المحتوى المرتبط الحي، الدعوة للإجراء المحلولة، والبيانات المنظمة التي يدعمها المحتوى المرئي فقط.

## 5. الخدمات والدوال المفتاحية

| الخدمة | الدوال (كلها `Task<Result<…>>` مع `CancellationToken`) | ملاحظات |
|---|---|---|
| [ContentAdministrationService](../../../src/Onyx.Application/Content/ContentAdministrationService.cs) (`IContentAdministrationService`) | `CreateAsync`، `GetAsync`، `ListAsync`، `UpdateTranslationAsync`، `UpdateSeoAsync`، `UpdateSettingsAsync`، `DeletePermanentAsync`، `ListRelationsAsync`، `AddRelationAsync`، `RemoveRelationAsync`، `SuggestRelatedAsync` | الإنشاء يسجل مراجعة `Created`. نقل العنصر يتحقق من شكل الأب، عدم النقل تحت أحد الأحفاد، والعمق (ارتفاع الشجرة الفرعية + مستوى الأب ≤ 6). `UpdateSettings` يرفض تحويل صفحة حية إلى `TaxonomyOnly`. العلاقات: صفحات الخدمة/الصناعة ترتبط بمشروع أو دراسة حالة **واحدة** فقط (`Relation.OneWorkLink`) |
| | `DeletePermanentAsync` | يتطلب كتابة معرّف العنصر للتأكيد، غير حي، بلا أبناء، ليس هدف تحويل، وقرار URL (`Gone`/`RedirectToContent`) إن كانت له روابط. يحوّل كل مساراته إلى قواعد `Redirect` دائمة قبل الحذف، يزيل الاستخدامات وعضوية العناقيد، تدقيق `ContentDeletedPermanently` |
| [ContentWorkflowService](../../../src/Onyx.Application/Content/ContentWorkflowService.cs) (`IContentWorkflowService`) | `SubmitForReviewAsync`، `ApproveAsync`، `RejectAsync`، `PublishAsync`، `ScheduleAsync`، `CancelScheduleAsync`، `ScheduleUnpublishAsync`، `CancelUnpublishScheduleAsync`، `UnpublishAsync`، `ArchiveAsync`، `RestoreFromArchiveAsync`، `ResolveOfflineAsync`، `GetQualityReportAsync`، `RecommendPublishingTimeAsync` | كل انتقال: `ForEditAsync` (نطاق + نسخة) → انتقال Domain → تدقيق Publishing → حفظ. الاعتماد والجدولة يمران ببوابة الجودة. المنطقة الزمنية للجدولة يجب أن تكون معروفة للنظام |
| [ContentPublisher](../../../src/Onyx.Application/Content/ContentPublisher.cs) | `PublishAsync(item, scheduled, ct)`، `TakeOfflineAsync(item, resolution, redirectToId, archive, scheduled, ct)`، `AnnounceCurrentUrlsAsync`، `ValidateTargetAsync` | لا يحفظ؛ المستدعي يلتزم |
| [ContentPathService](../../../src/Onyx.Application/Content/ContentPathService.cs) | `ComputeAsync`، `ApplyAsync`، `RefreshDescendantsAsync`، `NormalizePath` (NFC، بلا شرطات طرفية) | |
| [ContentSchedulingService](../../../src/Onyx.Application/Content/ContentSchedulingService.cs) | `ListDuePublicationsAsync`، `ListDueUnpublicationsAsync`، `PublishDueAsync`، `UnpublishDueAsync`، `HandleFailedPublicationAsync` | Idempotent (عنصر عولج مسبقًا → نجاح بلا عمل). الفشل → `FailSchedule` (إلى Approved) + تدقيق + إشعار `ContentPublishingFailed` (`Content.Publish`). دفعة = `Content:SchedulerBatchSize` |
| [ContentRevisionService](../../../src/Onyx.Application/Content/ContentRevisionService.cs) | `ListAsync`، `GetAsync`، `CompareAsync`، `RestoreAsync` | الاستعادة تنسخ مراجعة قديمة إلى نسخة العمل كمراجعة جديدة (`Restored`) ولا تعيد كتابة التاريخ؛ تُرفض إن اختفى الأب أو أصبح الـ slug مأخوذًا؛ الأصل `AiAssisted` لا يُخفض؛ تدقيق `ContentRevisionRestored` |
| [ContentPreviewService](../../../src/Onyx.Application/Content/ContentPreviewService.cs) | `CreateLinkAsync`، `GetPreviewAsync`، `CanViewMediaAsync` | رمز 256-bit عشوائي (base64url) لكل لغة، التجزئة فقط مخزنة، عمر `Content:PreviewTokenMinutes` (افتراضي 30)؛ يُعرض بنفس الباني مع معاينة SEO؛ تدقيق `ContentPreviewLinkCreated` |
| [ContentQualityGate](../../../src/Onyx.Application/Content/ContentQualityGate.cs) | `EvaluateAsync(item, ct)` → `QualityReport` | انظر §6 |
| [PublicContentService](../../../src/Onyx.Application/Content/PublicContentService.cs) | `ResolveAsync`، `GetSitemapAsync`، `ListAsync` (أقسام: work، case-studies، insights، topics، glossary، integrations؛ 20 لكل صفحة)، `GetFeaturedProjectsAsync` | |
| [CallToActionService](../../../src/Onyx.Application/Content/CallToActionService.cs)، [CtaResolver](../../../src/Onyx.Application/Content/CtaResolver.cs) | `ResolveAsync(itemCtaId, pillarCtaId, type, lang, ct)` | ترتيب الحل: تجاوز العنصر → افتراضي الركيزة → افتراضي النوع → الافتراضي العام؛ أول CTA نشط له نص بهذه اللغة. WhatsApp مرفوض في Domain (`Cta.WhatsAppDisabled`)؛ هدف URL يجب أن يكون مسار موقع آمن أو https |
| [ContentAuthorizer](../../../src/Onyx.Application/Content/ContentAuthorizer.cs)، [ContentAccess](../../../src/Onyx.Application/Content/ContentAccess.cs) | `CheckRead`، `CheckEdit`، `ForReadAsync`، `ForEditAsync` | نطاق النوع: `Insight` و`Topic` يتبعان `Insights.*` |
| [ContentRevisionRecorder](../../../src/Onyx.Application/Content/ContentRevisionRecorder.cs) | `RecordAsync(item, kind, note, restoredFrom, ct)` | يزيد `CurrentRevisionNumber` (فيتحرك الـ xmin دائمًا) |

## 6. بوابة الجودة (قواعد، لا درجات)

لا تنتج رقمًا ولا تتنبأ بترتيب. `CanPublish = لا أخطاء`.

| الخطورة | الرموز |
|---|---|
| **Error** (يمنع الاعتماد والنشر) | `Lifecycle.Archived`، `Content.NoPage`، `Translation.Missing`، `Translation.Incomplete`، `Industry.ContentMinimum`، `Proof.NotVerified`، `Slug.Conflict`، `Path.Taken`، `Path.RedirectConflict`، `Link.Broken`، `Media.Missing`، `Cta.Missing`، `Schema.FaqWithoutVisibleFaqs`، `Schema.ServiceOnNonService`، `Schema.ArticleOnNonInsight` |
| **Warning** | `Content.EmptyBody`، `Cta.PrimaryMissing`، `Headings.SkippedLevel`، `Link.TargetNotLive`، `Links.Excessive` (>100)، `Media.AltMissing`، `Seo.CanonicalOverride`، `Seo.DuplicateMetaTitle`، `Seo.KeywordCannibalization`، `Seo.KeywordMapConflict`، `Seo.MetaDescriptionLength` (50–160)، `Seo.MetaDescriptionMissing`، `Seo.MetaTitleLong` (>60)، `Seo.MetaTitleMissing`، `Seo.NoIndex`، `Slug.Long` (>60) |
| **Recommendation** | `Headings.Duplicate`، `Headings.None`، `Insight.AiAssisted`، `Insight.OrganizationByline`، `Link.SelfLink`، `Links.NoInternalLinks`، `Schema.FaqNoRichResult`، `Seo.NoPrimaryKeyword`، `Seo.NoSearchIntent`، `Seo.NoSocialImage` |

## 7. قاعدة البيانات

`content_items`، `content_translations`، `content_revisions`، `content_paths`، `content_preview_tokens`، `call_to_actions`، `cta_defaults`، `content_relations`، `media_usages`. قيود حاسمة: تفرد (اللغة، نطاق الـ slug، الـ slug)؛ تفرد (اللغة، المسار) للأبد؛ مسار حالي واحد لكل (عنصر، لغة). التفاصيل في [DATABASE-AR.md §3.3](../DATABASE-AR.md).

نطاق الـ slug: `ContentItem.BuildSlugScope(type, parentId)` = `"{Type}:{parent:N}"` للعناصر ذات الأب (ما عدا دراسة الحالة: قسم واحد مشترك) وإلا اسم النوع.

## 8. الأحداث والمهام والبحث وSEO

- `ContentPublicationChanged` → فهرسة البحث ([15-search-AR.md](15-search-AR.md)).
- `PublicUrlsChanged` → تحديث مسارات الفهرس + IndexNow ([06-seo-redirects-AR.md](06-seo-redirects-AR.md)).
- `content-scheduling` كل دقيقة ([BACKGROUND-JOBS-AR.md](../BACKGROUND-JOBS-AR.md)).
- لا بيانات شخصية في المحتوى عادة؛ ملاحظات التحقق من الإثبات قد تسمي جهات اتصال (`[NotAudited]`).

## 9. الاختبارات

| الملف | ما يتحقق منه |
|---|---|
| [ContentItemLifecycleTests](../../../tests/Onyx.Domain.Tests/Content/ContentItemLifecycleTests.cs) | الانتقالات، العودة إلى Draft مع إبقاء الحي، الحد الأدنى للصناعة، شكل الأب، حل السحب |
| [SlugAndBodyRulesTests](../../../tests/Onyx.Domain.Tests/Content/SlugAndBodyRulesTests.cs)، [PublicUrlChangeTests](../../../tests/Onyx.Domain.Tests/Content/PublicUrlChangeTests.cs) | تطبيع الـ slug، الروابط الآمنة، إعلان الروابط |
| [ContentWorkflowApiTests](../../../tests/Onyx.API.IntegrationTests/Content/ContentWorkflowApiTests.cs) | فصل الصلاحيات، نطاق النوع، المسودات لا تصل للعام، تفرد الـ slug، التحويل المباشر للمسار الحالي، 410، الأرشيف، الجدولة، المعاينة، الاستعادة، 409 للنسخة القديمة، الحذف الدائم |
| [ContentHierarchyApiTests](../../../tests/Onyx.API.IntegrationTests/Content/ContentHierarchyApiTests.cs) | العمق الأقصى ونقل الأحفاد |
| [ContentGovernanceTests](../../../tests/Onyx.Application.Tests/Content/ContentGovernanceTests.cs)، [SeoFoundationRulesTests](../../../tests/Onyx.Application.Tests/Content/SeoFoundationRulesTests.cs) | المقارنة بين المراجعات، البصمة، رموز المعاينة |
| [AdminCmsTests](../../../tests/Onyx.Web.Tests/AdminCmsTests.cs)، [BlockTextFormatTests](../../../tests/Onyx.Web.Tests/BlockTextFormatTests.cs)، [ContentSiteTests](../../../tests/Onyx.Web.Tests/ContentSiteTests.cs) | المحرر، تنسيق الكتل، العرض العام والتحويلات والمعاينة |

## 10. خريطة أثر التغيير

| إذا غيّرت… | راجع |
|---|---|
| انتقالًا في `ContentItem` | `ContentWorkflowService` (الدالة المقابلة)، `ContentPublisher`، `ContentSchedulingService`، `CmsOptions.Statuses` في Web، `ContentItemLifecycleTests`، `ContentWorkflowApiTests` |
| `PublicationGuard` أو `CheckPublishable` | `ContentQualityGate.CheckProofAndProvenance`/`CheckCompleteness` (لعرض الخطأ قبل الرفض)، اختبارات الإثبات والرؤى |
| بنية `ContentSnapshot` | **التوافق مع اللقطات القديمة** (`WorkAndInsightRulesTests.Older_snapshots_without_insight_fields_still_deserialize`)، `PublicPageFingerprint`، `ContentRevisionService.CompareAsync`، `PublicContentBuilder` |
| `PublicPageFingerprint` | `lastmod` وإعلانات IndexNow؛ `SeoFoundationRulesTests`، `SeoFoundationApiTests.Lastmod_moves_only_when_the_page_in_that_language_visibly_changes` |
| `ContentRouteSegments` (Contracts) | `ContentPathService.ComputeAsync`، `SectionRoutes` و`PageController` في Web، `LeadPages`/`SearchPages`/`NewsPages`، **روابط منشورة** — تغيير قسم يكسر الروابط القائمة ما لم تُضف تحويلات |
| `ContentBodyRules` أو أنواع الكتل | `BlockTextFormat` في Web، `ContentMapping.ToBlocks`، `PublicContentBuilder`، `MediaUsageIndexer`، `InternalLinkAnalyzer` |
| `MaxHierarchyDepth` | `ContentAdministrationService.ValidateParentAsync`، `ContentPathService.RefreshAsync`، `ContentHierarchyApiTests` |

## 11. أمثلة عملية

- **لإضافة نوع محتوى جديد:** أضف قيمة في نهاية `ContentType`؛ حدّث `ContentItem.ValidateParentShape`، `ContentAuthorizer.ViewPermission/EditPermission` (وصلاحيات جديدة في `Permissions`)، `ContentPathService.ComputeAsync` (القسم)، `ContentRouteSegments` (+ `Reserved`)، `PublicContentService.SectionTypes` إن كان له قائمة، `CmsOptions.ContentTypes`، ونصوص الترجمة. ثم الاختبارات.
- **لإضافة قاعدة جودة:** أضف `findings.Add(new(QualitySeverity.X, "Code.Name", "…"))` في الدالة المناسبة داخل `ContentQualityGate`. إن كانت Error فهي تمنع النشر فورًا لكل المحتوى الموجود الذي يخالفها — ناقش ذلك أولًا.
- **لتغيير مدة رابط المعاينة:** `Content:PreviewTokenMinutes` في الإعدادات (`ContentOptions`).

## 12. الحالة

- **منفّذ بالكامل:** كل ما سبق.
- **غير منفّذ:** «Page builder» حر (مرفوض بالتصميم — CLAUDE.md §6)، تصميم بصري للصفحات العامة (Stage 5 مؤجلة؛ القوالب الحالية دلالية بلا تنسيق).
