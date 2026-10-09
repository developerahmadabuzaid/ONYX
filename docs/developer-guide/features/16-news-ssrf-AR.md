# 16 — مركز الأخبار، قراءة الخلاصات، وSafeOutboundHttp (الحماية من SSRF)

## 1. ماذا تفعل الميزة

**مركز الأخبار العام** (`/ar/آخر-الأخبار/…`، `/en/news/…`): أخبار من مصادر **مسموح بها صراحة**، بملخصات Onyx الأصلية الموجزة لكل لغة مع الإسناد الكامل (الناشر، وقت المصدر إن وُجد، رابط الأصل، وسم «ملخص Onyx»).

**قواعد ملزمة في الكود:**
- يُخزن **الوصف الوصفي + ملخص Onyx فقط**؛ نص المقال وصوره لا تُخزن (الوصف المقتطف يُعالج مؤقتًا ولا يُحفظ).
- **لا شيء عام بلا عنوان وملخص Onyx بكل لغات الموقع** (`News.SummaryRequired`).
- المصدر الجديد يبدأ **محظورًا**؛ السماح يتطلب ملاحظة ترخيص/شروط مراجَعة (`News.LicensingNoteRequired`). حذف الملاحظة يعيد حظره. سجلات التدقيق لا تحوي رابط الخلاصة (قد يحمل مفتاح ناشر).
- **النشر التلقائي** فقط إن: ملخصات مولّدة من الخدمة الخارجية (غير معدّلة) + المصدر مسموح ومضبوط على النشر التلقائي + لا عنصر مشابه حديث. وإلا يبقى `Held` مع سبب (`SummaryUnavailable`، `SummaryRejected`، `AutoPublishOff`، `SimilarItem`).
- **حالة الإطلاق:** خدمة الملخصات غير مهيأة (`NotConfiguredNewsSummaryService`) ⇒ كل عنصر يُستوعب يبقى **Held**، والمحررون يكتبون الملخص وينشرون يدويًا.
- العنصر المنشور `noindex, follow` افتراضيًا (`Indexable = false`) ما لم يعلّمه محرر قابلًا للفهرسة.
- المحذوف يبقى «شاهد قبر» برابطه (لا يُعاد استيعابه) ويفقد ملخصاته. الـ slug ثابت بعد أن أصبح العنصر عامًا.
- التصنيف بقواعد (كلمات مفتاحية مطبّعة بنفس مُطبِّع البحث)، **بلا AI**.

**SafeOutboundHttp**: الطريق الوحيد لجلب رابط خارجي يقدمه زائر أو مسؤول (CLAUDE.md §8).

## 2. دورة حياة العنصر

```
Ingest → Held ──(EditTranslation × كل اللغات + Publish)──► Published ──Unpublish──► Unpublished
   │  └──(ملخص مولّد صالح + مصدر auto + لا مشابه)──► AutoPublish ──► Published
   └─ Hide (من Held/Published/Unpublished) ──► Hidden
أي حالة ──Delete──► Deleted (شاهد قبر)
```

## 3. الملفات

| الطبقة | المسار | المحتوى |
|---|---|---|
| Domain | [src/Onyx.Domain/News/](../../../src/Onyx.Domain/News/) | `NewsSource` (`Create`، `Update`، `Allow`، `Block`، `IsDue`، `RecordSuccess`، `RecordFailure` بتراجع `interval × 2^(n−1)` حتى 24 ساعة، أول فشل يرفع `NewsSourceFailing`)، `NewsItem` (`Ingest`، `ApplyGeneratedSummaries`، `EditTranslation`، `Publish`، `AutoPublish`، `KeepHeld`، `Hide`، `Unpublish`، `Delete`، `SetIndexable`، `ChangeCategory`)، `NewsItemTranslation` (عنوان ≤ 200، ملخص 40–700)، `NewsCategory`، `NewsUrl` (`Parse`، `Check`، `Canonicalize` بلا معاملات تتبّع، `Hash`) |
| Application | [src/Onyx.Application/News/](../../../src/Onyx.Application/News/) | `NewsIngestionService`، `NewsClassifier`، `NewsSummaryChecks` (لغة واحدة لكل لغة موقع، أطوال، نص عادي، لا روابط، **لا 8 كلمات متتالية منسوخة من المصدر**)، `NewsSummaryBudget`، `NewsSlugAllocator`، `NewsAdministrationService`، `NewsSourceAdministrationService`، `NewsCategoryAdministrationService`، `PublicNewsService`، `NewsSearchIndexer`، `INewsFeedReader`، `INewsSummaryService` |
| Infrastructure | [News/](../../../src/Onyx.Infrastructure/News/)، [Outbound/](../../../src/Onyx.Infrastructure/Outbound/) | `SyndicationNewsFeedReader` (الوحيد الذي يستخدم `System.ServiceModel.Syndication` — اختبار معمارية)، `FeedText`، `NotConfiguredNewsSummaryService`، `NewsOptions`؛ `SafeOutboundHttpClient`، `OutboundAddressPolicy`، `RobotsTxtPolicy`/`RobotsTxtRules`، `SafeOutboundHttpOptions` |
| Worker | [NewsIngestionJob](../../../src/Onyx.Infrastructure/BackgroundJobs/NewsIngestionJob.cs) | `every:15m` |
| API | [NewsController](../../../src/Onyx.API/Controllers/NewsController.cs)، [AdminNewsController](../../../src/Onyx.API/Controllers/Admin/AdminNewsController.cs)، [AdminNewsSourcesController](../../../src/Onyx.API/Controllers/Admin/AdminNewsSourcesController.cs)، [AdminNewsCategoriesController](../../../src/Onyx.API/Controllers/Admin/AdminNewsCategoriesController.cs) | |
| Web | [NewsHubController](../../../src/Onyx.Web/Controllers/NewsHubController.cs)، [News/NewsPages.cs](../../../src/Onyx.Web/News/NewsPages.cs)، Admin `NewsItemsController`/`NewsSourcesController`/`NewsCategoriesController` | |

## 4. تدفق الاستيعاب — `NewsIngestionService.IngestSourceAsync(sourceId, budget, ct)`

لكل مصدر مستحق (نطاق ومعاملة مستقلان):
1. **جلب** عبر `INewsFeedReader.ReadAsync(feedUrl, format, maxEntries)` → `RobotsTxtPolicy.IsAllowedAsync` أولًا (ملف مفقود 4xx = مسموح؛ خطأ خادم/تعذّر = «لاحقًا» لا «مسموح»، مخزّن ساعة لكل أصل) → `SafeOutboundHttpClient.GetAsync` → تحليل XML بلا DTD ولا resolver وبحد أحرف. الفشل رمز آمن من `NewsFeedErrors` (Blocked، Timeout، TooLarge، TooManyRedirects، HttpStatus، RobotsDisallowed، Malformed، Unreachable) → `source.RecordFailure`.
2. **تطبيع**: رابط قانوني، عنوان، الناشر **من إعداد المصدر** (لا من نص الخلاصة)، وقت المصدر (تاريخ مستقبلي > يوم لا يُوثق؛ المفقود يبقى مفقودًا).
3. **إزالة التكرار**: تجزئة الرابط القانوني (أي حالة بما فيها المحذوف) + مفتاح عنقود العنوان المطبّع داخل `News:SimilarWindowHours` (72).
4. **تصنيف** (`NewsClassifier.Classify`)؛ بلا تطابق → الفئة الافتراضية للمصدر.
5. **ملخص** إن بقيت ميزانية (`News:MaxSummariesPerRun`) ولم تُبلغ الخدمة «غير مهيأة» في هذا التشغيل؛ مهلة `SummaryTimeoutSeconds`؛ `NewsSummaryChecks.Verify`.
6. **نشر تلقائي** أو `KeepHeld(reason)`؛ تدقيق `NewsItemAutoPublished`.

`NewsIngestionJob` يوقف بدء مصادر جديدة بعد `RunBudgetMinutes` (4)، ويفشل التشغيل فقط إن فشلت كل المصادر المُجرّبة. `ReportSummaryServiceAsync` يحدّث حالة مزوّد `ai.news-summary` مرة لكل تشغيل.

## 5. SafeOutboundHttpClient — القواعد (متحقق منها في التعليقات والاختبارات)

- http(s) فقط (https عند الطلب)، بلا بيانات اعتماد، المنافذ الافتراضية، اسم DNS عام (لا IP حرفي، لا أسماء داخلية مثل localhost/*.local/*.internal).
- حل DNS **مرة واحدة لكل اتصال**؛ **كل** العناوين المحلولة يجب أن تكون عامة (`OutboundAddressPolicy.IsPublic`: يحظر loopback، الخاص RFC1918/ULA، CGNAT، link-local بما فيه 169.254.169.254، multicast، التوثيق، المحجوز؛ IPv4 وIPv6؛ عناوين IPv6 المعيّنة لـ IPv4 تُقيَّم بجزئها IPv4، وNAT64/6to4/Teredo مرفوضة كليًا)؛ ثم الاتصال **بالعنوان المُدقق نفسه** ⇒ لا DNS rebinding.
- بلا proxy، بلا كوكيز، بلا اعتماد؛ الشهادات تُتحقق عاديًا.
- التحويلات تُتبع يدويًا، كل هدف يُعاد تحققه، حتى `MaxRedirects` (3)، ولا تخفيض https→http.
- مهلة كلية (`TimeoutSeconds` 20، اتصال 10)، سقف الجسم بعد فك الضغط (`MaxResponseBytes` 2 MB — يكشف قنابل الضغط).
- `User-Agent` معرّف (`OnyxVanguard-NewsReader/1.0`) — رمز المنتج يُطابق مجموعات robots.txt (RFC 9309).
- السجل: المضيف والحالة والحجم والمدة فقط — **لا مسار ولا query ولا جسم**.

## 6. الاختبارات

- Domain: [NewsItemTests](../../../tests/Onyx.Domain.Tests/News/NewsItemTests.cs)، [NewsSourceTests](../../../tests/Onyx.Domain.Tests/News/NewsSourceTests.cs)، [NewsUrlTests](../../../tests/Onyx.Domain.Tests/News/NewsUrlTests.cs).
- Application: [NewsRulesTests](../../../tests/Onyx.Application.Tests/News/NewsRulesTests.cs) — التصنيف، تغطية اللغات، رفض النسخ والروابط والترميز، توقف الميزانية عند «غير مهيأ».
- Integration: [NewsIngestionTests](../../../tests/Onyx.API.IntegrationTests/News/NewsIngestionTests.cs) (Held دون خدمة، النشر التلقائي فقط من مصادر auto، فشل/مهلة/نسخ ⇒ Held، عزل المصادر وإشعار واحد، المحظورة لا تُجلب، المشابهة تُحجز)، [NewsEditorialApiTests](../../../tests/Onyx.API.IntegrationTests/News/NewsEditorialApiTests.cs)، [NewsSourcesApiTests](../../../tests/Onyx.API.IntegrationTests/News/NewsSourcesApiTests.cs).
- SSRF: [SafeOutboundHttpTests](../../../tests/Onyx.API.IntegrationTests/Outbound/SafeOutboundHttpTests.cs) — 12 اختبارًا (العناوين الخاصة، الإجابات المختلطة، **DNS rebinding**، التحويل لوجهات محظورة، حد السلسلة، فرض https، قنابل الضغط، المهلات، robots.txt)؛ [FeedParserTests](../../../tests/Onyx.API.IntegrationTests/Outbound/FeedParserTests.cs) (DTD/الكيانات الخارجية/قنابل الكيانات مرفوضة).
- Web: [NewsSiteTests](../../../tests/Onyx.Web.Tests/NewsSiteTests.cs).
- Architecture: `The_feed_parsing_package_is_used_only_by_infrastructure`، `Inside_infrastructure_only_the_news_feed_reader_parses_feeds`، `Public_api_controllers_never_reach_news_administration`.

## 7. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| **أي ميزة جديدة تجلب رابطًا خارجيًا** (أدوات Phase 2) | **يجب** أن تستخدم `SafeOutboundHttpClient` + `RobotsTxtPolicy` للزحف؛ لا `HttpClient` مباشر لروابط المستخدمين؛ أضف اختبارات SSRF |
| `OutboundAddressPolicy` | `SafeOutboundHttpTests` كاملة |
| ربط خدمة ملخصات حقيقية | سجّل محوّلًا لـ `INewsSummaryService` (يستبدل `TryAddScoped` الافتراضي)؛ لا أسرار ولا PII في الطلب؛ مخرجاته تمر بـ `NewsSummaryChecks` دائمًا؛ مفتاح المزوّد `ai.news-summary` |
| قواعد النشر التلقائي | `NewsItem.AutoPublish` + `NewsIngestionTests` |
| الفهرسة/خريطة الموقع | `PublicNewsService.GetSitemapAsync`، `NewsSearchIndexer`، `NewsHubController` (`NoIndex`) |

## 8. الحالة

- **منفّذ:** المصادر، الاستيعاب، إزالة التكرار، التصنيف، التحرير، النشر اليدوي، المركز العام، البحث، خرائط الموقع.
- **غير مهيأ:** خدمة الملخصات (وكيل AI خارجي — لا يُبنى داخل هذا المستودع؛ «No AI Agent»). النتيجة: لا نشر تلقائي فعليًا حتى يُربط محوّل.
