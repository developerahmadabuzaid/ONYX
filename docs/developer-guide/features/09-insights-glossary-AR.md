# 09 — الرؤى والمواضيع والمسرد والمؤلفون

## 1. ماذا تفعل الميزة

- **الرؤى** (`ContentType.Insight`): مقال أو دليل (`InsightKind`)، بأصل `Human` أو `AiAssisted` (`InsightOrigin`)، ومؤلف حقيقي اختياري (وإلا تُنسب للمؤسسة).
  - المحتوى `AiAssisted` **يبقى موسومًا للأبد** (`Content.OriginDowngrade`) — حتى استعادة مراجعة بشرية قديمة لا تُزيل الوسم.
  - لا يُنشر ولا يُجدول إلا بعد موافقة بشرية مسجلة (`Content.AiApprovalRequired`)؛ الـ Worker ينشر المجدول فقط ما دامت الموافقة قائمة.
  - البيانات المنظمة `Article` مسموحة للرؤى فقط.
- **المواضيع** (`Topic`): صفحات تجمّع الرؤى؛ صفحة الموضوع تعرض رؤاه الأحدث أولًا (`GET content/listing?section=insights&topic=…`).
- **المسرد** (`GlossaryTerm`) لا يحتاج تحقق إثبات؛ **التكاملات** (`Integration`) حاملة للإثبات (انظر [08](08-proof-work-AR.md)).
- **المؤلفون** (`Author`): أشخاص حقيقيون بأسماء لكل لغة (+ مسمى وظيفي) و**ملاحظة موافقة إلزامية** على النسب العلني. لا شخصيات مختلقة. لا يُحذف مؤلف منسوب لمحتوى (`Author.Credited`) — يُعطَّل بدلًا من ذلك.

## 2. الملفات

| المسار | الدور |
|---|---|
| [ContentItem.UpdateInsightSettings](../../../src/Onyx.Domain/Content/ContentItem.cs)، [InsightKind](../../../src/Onyx.Domain/Content/InsightKind.cs)، [InsightOrigin](../../../src/Onyx.Domain/Content/InsightOrigin.cs) | قواعد الأصل |
| [Author.cs](../../../src/Onyx.Domain/Insights/Author.cs) | الكيان (`ConsentNote` غير مُدقق بالقيمة) |
| [AuthorService.cs](../../../src/Onyx.Application/Insights/AuthorService.cs) | `IAuthorService`: `ListAsync`، `CreateAsync`، `UpdateAsync`، `DeleteAsync` (تدقيق `AuthorDeleted`) |
| [ContentAdministrationService.ApplyInsightSettingsAsync](../../../src/Onyx.Application/Content/ContentAdministrationService.cs) | يُستدعى من `UpdateSettingsAsync`؛ يقبل مؤلفًا موجودًا ونشطًا فقط (`Content.InvalidAuthor`) |
| [PublicContentBuilder.cs](../../../src/Onyx.Application/Content/PublicContentBuilder.cs) | السطر التعريفي: المؤلف ما دام موجودًا ونشطًا، وإلا المؤسسة |
| API: [AdminAuthorsController](../../../src/Onyx.API/Controllers/Admin/AdminAuthorsController.cs) (`Insights.View`/`Insights.Edit`) | |
| Web: [AuthorsController (Admin)](../../../src/Onyx.Web/Areas/Admin/Controllers/AuthorsController.cs)، صفحات الرؤى عبر `PageController` | الإفصاح عن AI والتواريخ والسطر التعريفي |

## 3. صلاحيات

نطاق النوع: `Insight` و`Topic` يتبعان `Insights.View/Edit`؛ `GlossaryTerm` يتبع `Glossary.*`. دور `Marketing` يصوغ ويُقدّم الرؤى للمراجعة لكنه لا يعتمد ولا ينشر ولا يمس أنواعًا أخرى (اختبار `Marketing_can_draft_insights_but_not_approve_publish_or_touch_other_types`).

## 4. الاختبارات

- [InsightsApiTests](../../../tests/Onyx.API.IntegrationTests/Content/InsightsApiTests.cs) — 8 اختبارات: الوسم والموافقة، الـ Worker لا ينشر رؤية AI مجدولة إن سُحبت الموافقة، المؤلفون الحقيقيون الموافقون، صلاحيات Marketing، المواضيع، المسرد والتكاملات، `Article` للرؤى فقط، الاستعادة تحفظ الوسم.
- [WorkProofInsightRulesTests](../../../tests/Onyx.Domain.Tests/Content/WorkProofInsightRulesTests.cs)، [WorkAndInsightRulesTests](../../../tests/Onyx.Application.Tests/Content/WorkAndInsightRulesTests.cs).
- [WorkAndInsightsSiteTests](../../../tests/Onyx.Web.Tests/WorkAndInsightsSiteTests.cs) — `Insights_show_the_byline_dates_ai_disclosure_and_article_data`.

## 5. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| حقول الرؤية (kind/origin/author) | `ContentSnapshot` (توافق رجوعي)، `PublicPageFingerprint` (هي جزء من البصمة)، `PublicContentBuilder`، `ContentRevisionService.RestoreAsync` (حفظ الوسم) |
| قاعدة موافقة AI | `ContentItem.PublicationGuard` + `ContentQualityGate` (`Insight.AiAssisted`)، CLAUDE.md §2 و§9 — **لا تضعفها** |
| حذف المؤلفين | `AuthorRepository.IsCreditedAsync` + FK `Restrict` من `content_items.author_id` |

## 6. الحالة

- **منفّذ:** كل ما سبق.
- **مؤجّل (Phase 2):** «محرك المحتوى بالذكاء الاصطناعي» (بحث → اقتراح → مسودتان → مراجعة). صلاحيات `ContentEngine.*` تُستخدم حاليًا لتقويم المحتوى فقط ([20-knowledge-intelligence-AR.md](20-knowledge-intelligence-AR.md)). لا يوجد توليد نصوص في هذا المستودع؛ مفتاح `ai.text` محجوز وغير مهيأ.
