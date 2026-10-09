# 20 — المعرفة المعتمدة، خريطة الكلمات المفتاحية، العناقيد، التوصيات، التقويم، المنافسون، هوية العلامة

> كل ما في هذا الملف **داخلي** (لوحة الإدارة فقط). اختبارات المعمارية تمنع متحكمات الـ API العامة وصفحات Web العامة من الوصول إلى `Intelligence` (`Public_api_controllers_never_reach_competitor_or_intelligence_data`، `The_public_site_never_reaches_competitor_or_intelligence_data`).

## 1. المبدأ المشترك

طبقة «ذكاء تحريري» **قائمة على القواعد والأدلة**، لا تخترع مقاييس ولا تغيّر المحتوى تلقائيًا:
- المقاييس الكمية مقبولة **فقط** من مزوّد متصل ومع الفترة (`EvidenceItem` — مصادر ≥ 10 هي مزوّدو بيانات). وإلا «بيانات غير كافية».
- التوصيات تُقترح → يعتمدها/يرفضها إنسان (`Seo.Approve`) → التطبيق يعدّل **نسخة العمل فقط** (Draft) فتمر بالمراجعة والنشر العاديين.
- الصياغة ترابطية لا سببية (`ChangeAssessment`: «مرتبط بتحسّن»، «لا تغيّر واضح»…).
- منافذ AI معرّفة **بلا تنفيذ**: `ITextGenerationService`، `IImageGenerationService` (لا مزوّد معتمد).

## 2. المكوّنات

| المكوّن | Domain | Application | الصلاحيات (API) | ملاحظات |
|---|---|---|---|---|
| **المعرفة المعتمدة** | `KnowledgeEntry` (Draft → Approved → Retired؛ أي تعديل يعيد المعتمد إلى Draft؛ الاعتماد يتطلب مصدرًا مسجلًا `Knowledge.SourceRequired`)، `KnowledgeCategory`، `ContentGovernanceRules` (قواعد كتابة غير قابلة للتعديل) | [KnowledgeService](../../../src/Onyx.Application/Knowledge/KnowledgeService.cs)، [CompanyKnowledgeSource](../../../src/Onyx.Application/Knowledge/CompanyKnowledgeSource.cs) (`GetApprovedFactsAsync` — المعتمد فقط بلغة معينة) | View / Edit / **Approve** منفصلة | لا شيء مزروع مسبقًا. تدقيق `KnowledgeApproved`/`KnowledgeRetired` |
| **طلبات المعلومات** | `InformationRequest` (Open → Answered/Dismissed؛ الإجابة تربط مدخل معرفة معتمد) | `KnowledgeService.RaiseRequestAsync/ResolveRequestAsync` | View يرفع؛ Approve يحسم | بديل «التخمين» عند نقص حقيقة |
| **خريطة الكلمات المفتاحية** | `KeywordTarget` (كلمة → نية → لغة → سوق → محتوى → عنقود → حالة؛ **كلمة واحدة لكل لغة وسوق لهدف واحد** = منع التنافس الداخلي؛ لا حجم ولا صعوبة ولا ترتيب) | [KeywordMapService](../../../src/Onyx.Application/Intelligence/KeywordMapService.cs) (`CheckConflictsAsync` يغطي المحتوى والخريطة، يتجاهل الحالة والصفحة نفسها) | `Content.View` قراءة؛ `Seo.Edit` كتابة | بوابة الجودة تستخدمها (`Seo.KeywordMapConflict`، `Seo.KeywordCannibalization`) |
| **العناقيد** | `ContentCluster` + `ContentClusterMember` (ركيزة واحدة كحد أقصى `Cluster.PillarExists`) | [ContentClusterService](../../../src/Onyx.Application/Content/ContentClusterService.cs) — ملاحظات تغطية من الرسم البياني نفسه | `Content.View` / `Seo.Edit` | |
| **توصيات SEO** | `SeoRecommendation` (Proposed → Approved/Rejected → Applied؛ Superseded)، `ProposedFieldChange`، `EvidenceItem`، `SeoChangeObservation` | [SeoRecommendationService](../../../src/Onyx.Application/Intelligence/SeoRecommendationService.cs): `ProposeAsync` (توصيات المحرر **لا تحمل مقاييس** — `Manual_recommendations_cannot_carry_metrics…`)، `ApproveAsync`/`RejectAsync`، `ApplyAsync` (حقول مفردة مدعومة فقط ⇒ مراجعة `RecommendationApplied` على نسخة العمل؛ غير ذلك `Recommendation.ManualApply`)، `MarkAppliedAsync` | Content.View / Seo.Edit / **Seo.Approve** | تدقيق `SeoRecommendationApproved/Rejected/Applied` |
| **تحليل الروابط الداخلية** | — | [InternalLinkAnalyzer](../../../src/Onyx.Application/Intelligence/InternalLinkAnalyzer.cs): روابط مكسورة، صفحات يتيمة، فرص بالكلمات، نصوص رابط ملتبسة، روابط مفرطة؛ `GenerateRecommendationsAsync` يحوّلها لتوصيات بأدلة دون تكرار | Content.View / Seo.Edit | |
| **حالة القدرات والصحة** | `ContentPerformanceSnapshot` (يكتبها **فقط** مزامنة التحليلات)، `ContentHealth` | [IntelligenceStatusService](../../../src/Onyx.Application/Intelligence/IntelligenceStatusService.cs): `GetCapabilitiesAsync` (كل مصدر بحالة اتصاله الحقيقية)، `GetContentProfileAsync`، `AssessHealth` (فترتا Search Console على الأقل وإلا InsufficientData؛ عتبة تراجع 25%) | Content.View | |
| **توقيت النشر** | — | [PublishingTimeAdvisor](../../../src/Onyx.Application/Intelligence/PublishingTimeAdvisor.cs) | Content.View | **لا يقترح وقتًا** دون بيانات نشاط زوار حقيقية: الجواب الصادق «بيانات غير كافية» (توقيت جمهور، ليس عامل ترتيب) |
| **تقويم المحتوى** | `ContentCalendarEntry` (Proposed → Accepted/Rejected → InPreparation (ربط مسودة) → Completed؛ الوقت المقترح يذكر أساسه) | [ContentCalendarService](../../../src/Onyx.Application/Intelligence/ContentCalendarService.cs) | ContentEngine.View/Review/**Approve** | تدقيق `CalendarEntryDecided`. AI لا يجدول ولا ينشر |
| **المنافسون** | `CompetitorReference` (نطاق مجرد، أسواق، لغات، مواضيع) | [CompetitorService](../../../src/Onyx.Application/Intelligence/CompetitorService.cs) | Competitors.View/Manage | **بحث داخلي فقط**؛ لا زحف ولا نسخ ولا عرض عام (CLAUDE.md §2.2) |
| **هوية العلامة** | `BrandKit` (يبدأ فارغًا؛ الشعار الرسمي وسيط مرفوع، لا يُولّد شعار بديل) | [BrandKitService](../../../src/Onyx.Application/Brand/BrandKitService.cs) (`MissingItems` يبلّغ الناقص ولا يملؤه)، [BrandedImagePolicy](../../../src/Onyx.Application/Brand/BrandedImagePolicy.cs) | BrandKit.View/Manage | تدقيق `BrandKitUpdated`؛ موقع الهوية يغذي بوابة بيانات Organization |

## 3. الملفات الإضافية

- Domain: [src/Onyx.Domain/Knowledge/](../../../src/Onyx.Domain/Knowledge/)، [src/Onyx.Domain/Intelligence/](../../../src/Onyx.Domain/Intelligence/)، [src/Onyx.Domain/Brand/](../../../src/Onyx.Domain/Brand/).
- API: `AdminKnowledgeController`، `AdminKeywordsController`، `AdminClustersController`، `AdminSeoRecommendationsController`، `AdminIntelligenceController`، `AdminContentCalendarController`، `AdminCompetitorsController`، `AdminBrandKitController`، و`GET admin/content/{id}/intelligence/{lang}` — انظر [API-ROUTES-AR.md §3.4](../API-ROUTES-AR.md).
- Web Admin: `KnowledgeController`، `KeywordsController`، `ClustersController`، `RecommendationsController`، `IntelligenceController`، `CalendarController`، `CompetitorsController`، `BrandKitController` في [Areas/Admin/Controllers/](../../../src/Onyx.Web/Areas/Admin/Controllers/).

## 4. الاختبارات

- [GovernanceRulesTests](../../../tests/Onyx.Domain.Tests/Content/GovernanceRulesTests.cs) — المقاييس من مزوّدين متصلين بفترة فقط، الأدلة للتوصيات المبنية عليها، منع تهريب المقاييس، المعرفة تتطلب مصدرًا، الهوية فارغة وترفض القيم غير الصالحة، التقويم اقتراحات يقررها إنسان.
- [ContentGovernanceTests](../../../tests/Onyx.Application.Tests/Content/ContentGovernanceTests.cs) — صور الجسم بلا علامة تجارية، الشعار المفقود = «يحتاج معلومات»، الصحة «بيانات غير كافية» دون فترتين، التراجع كملاحظة بمصدرها.
- [IntelligenceServicesApiTests](../../../tests/Onyx.API.IntegrationTests/Content/IntelligenceServicesApiTests.cs) — تعارضات الكلمات، تقرير الروابط وعدم تكرار المقترحات، التطبيق ينشئ مسودة ولا ينشر، التطبيق اليدوي، توقيت النشر، المنافسون.
- [CmsAdministrationApiTests](../../../tests/Onyx.API.IntegrationTests/Content/CmsAdministrationApiTests.cs) — المعرفة والهوية.

## 5. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| حقل جديد قابل للتطبيق من توصية | `SeoRecommendationService.ApplicableFields` + منطق `ApplyAsync` + `ContentItem.UpdateSeo/UpdateTranslation` |
| مصدر أدلة جديد | `EvidenceSource` (إلحاق؛ ≥ 10 للمزوّدين) + `IntelligenceStatusService` (قائمة المصادر ومفاتيح المزوّدين) |
| ربط مزوّد LLM | يحتاج موافقة؛ نفّذ `ITextGenerationService` في Infrastructure؛ الإدخال من `CompanyKnowledgeSource` + `ContentGovernanceRules` فقط؛ لا أسرار ولا PII؛ المخرجات غير موثوقة وتُراجع دائمًا |
| أي عرض عام لهذه البيانات | **ممنوع** دون موافقة المالك — يكسر اختبارات المعمارية |

## 6. الحالة

- **منفّذ:** كل ما سبق كأدوات تحريرية داخلية بالقواعد.
- **غير متصل:** كل مصادر البيانات الخارجية ما عدا GA4/Search Console بعد إعدادهما؛ منفذا النص/الصور بلا تنفيذ.
- **Phase 2:** محرك المحتوى بالذكاء الاصطناعي، مساعد SEO.
