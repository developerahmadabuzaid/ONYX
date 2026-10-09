# 15 — البحث في الموقع (PostgreSQL)

## 1. ماذا تفعل الميزة

بحث عام لكل لغة على **المحتوى المنشور العام فقط**، مجمّع حسب النوع (الخدمات، القطاعات، الأعمال، الرؤى، الأخبار، المسرد، التكاملات، الصفحات).

- **الفهرس** (`search_documents`): صف لكل صفحة حية ولكل خبر منشور قابل للفهرسة، لكل لغة. تُفهرس الصفحة في لغة فقط إن كانت حية، لها صفحة، لقطتها الحية تحوي اللغة، لها رابط حالي، **وقابلة للفهرسة في تلك اللغة** (بلا noindex وcanonical لنفسها) — نفس قاعدة خريطة الموقع. النص العام فقط (عنوان، ملخص، جسم ≤ 100,000 حرف، أسئلة، slug)؛ **لا** كلمات SEO الداخلية ولا ملاحظات.
- **المحرك:** `websearch_to_tsquery` على متجه موزون مُجذَّع (`arabic`/`english`: العنوان A، الملخص B، الجسم C) + متجه `simple` غير مُجذَّع للأسماء والعلامات (مع مطابقة بادئة لآخر كلمة) + تشابه trigram على العناوين (`word_similarity ≥ 0.4`) للأخطاء الإملائية. الترتيب: `ts_rank_cd` × وزن المجموعة + مكافأة الأسماء + 0.3 × تشابه العنوان؛ الأخبار تُضعَّف بالقِدم (نصف عمر 30 يومًا).
- **مُطبِّع واحد مشترك** للفهرسة والاستعلام: NFKC؛ حذف التشكيل والتطويل؛ (أ إ آ ٱ → ا، ة → ه، ى → ي، ؤ → و، ئ → ي)؛ الأرقام العربية/الفارسية → غربية؛ الترقيم → مسافات؛ أحرف صغيرة. للعربية تُضاف «الصيغة العارية» (بلا و/ف + ال) لأن مُجذِّع Snowball العربي لا يزيلها.
- الاستعلام نصٌّ فقط: يُبقى فقط علامات الاقتباس للعبارات والسالب للاستبعاد؛ كل القيم معاملات مربوطة. الاستعلامات لا تُخزن ولا تُسجَّل. 2–100 حرف، 5 نتائج لكل مجموعة.

## 2. الملفات

| المسار | الدور |
|---|---|
| [SearchTextNormalizer.cs](../../../src/Onyx.Application/Search/SearchTextNormalizer.cs) | `Normalize`، `ForIndex`، `BareForm`، `ParseQuery` |
| [SearchIndexer.cs](../../../src/Onyx.Application/Search/SearchIndexer.cs) | `ReindexAsync(contentItemId, ct)`، `ReconcileAsync(ct)` (حتى 500 صفحة لكل تشغيل) |
| [NewsSearchIndexer.cs](../../../src/Onyx.Application/News/NewsSearchIndexer.cs) | فهرسة الأخبار (المجموعة `News`) |
| [SearchIndexOnPublication.cs](../../../src/Onyx.Application/Search/SearchIndexOnPublication.cs)، [SearchIndexOnUrlChanges.cs](../../../src/Onyx.Application/Search/SearchIndexOnUrlChanges.cs)، [SearchIndexOnNewsPublication.cs](../../../src/Onyx.Application/News/SearchIndexOnNewsPublication.cs) | الفهرسة المدفوعة بالأحداث (Worker) |
| [SiteSearchQueryService.cs](../../../src/Onyx.Application/Search/SiteSearchQueryService.cs) | التحقق والتطبيع والتجميع |
| [SearchGroups.cs](../../../src/Onyx.Application/Search/SearchGroups.cs) | المجموعات، ترتيب العرض، أوزان الترتيب (قيم ضبط منتج لا حقائق عمل) |
| [PostgresSiteSearchService.cs](../../../src/Onyx.Infrastructure/Search/PostgresSiteSearchService.cs) | `ISiteSearchService` — SQL بمعاملات مربوطة |
| [SearchDocument.cs](../../../src/Onyx.Infrastructure/Search/SearchDocument.cs)، [SearchDocumentRepository.cs](../../../src/Onyx.Infrastructure/Search/SearchDocumentRepository.cs)، [SearchDocumentConfiguration](../../../src/Onyx.Infrastructure/Persistence/Configurations/Search/SearchDocumentConfiguration.cs) | الجدول؛ أعمدة `tsvector` مولّدة من قاعدة البيانات + GIN + trigram |
| [SearchIndexReconcileJob](../../../src/Onyx.Infrastructure/BackgroundJobs/SearchIndexReconcileJob.cs) | `every:30m` |
| API [SearchController](../../../src/Onyx.API/Controllers/SearchController.cs)؛ Web [SiteSearchController](../../../src/Onyx.Web/Controllers/SiteSearchController.cs)، [Search/](../../../src/Onyx.Web/Search/) | `/ar/بحث`، `/en/search` — GET، noindex، no-store |

## 3. التدفق

```
نشر/سحب/تغيير رابط → حدث (ContentPublicationChanged / PublicUrlsChanged / NewsItemPublicationChanged)
  → Worker → SearchIndexer.ReindexAsync(id) → upsert أو حذف لكل لغة
كل 30 دقيقة → SearchIndexReconcileJob → ReconcileAsync (المفقود، القديم، المنقول، غير الحي)
زائر → GET /ar/بحث?q=… → SiteSearchController (يقطع الاستعلام قبل الـ API؛ الصفحة الفارغة لا تستدعي الـ API)
  → GET /api/v1/search?language&q (سياسة معدل "search": 30/دقيقة لكل IP، no-store)
  → SiteSearchQueryService.SearchAsync → ParseQuery → PostgresSiteSearchService.SearchAsync → تجميع
```

## 4. الاختبارات

- [SearchTextNormalizerTests](../../../tests/Onyx.Application.Tests/Search/SearchTextNormalizerTests.cs) — نفس التطبيع للفهرس والاستعلام، البادئة، عدم تسرب الصيغة، الصيغ العارية، ربط الأنواع بالمجموعات.
- [SiteSearchApiTests](../../../tests/Onyx.API.IntegrationTests/Search/SiteSearchApiTests.cs) — الخادم يوفر الإعدادات العربية/الإنجليزية وtrigram، تجاهل التشكيل وأشكال الحروف والأرقام، التجذيع والأسماء والأخطاء، فصل اللغات، **المسودات والإثبات غير الموثق وnoindex وغير الحي لا تُعاد أبدًا**، الإصلاح الدوري، النص العدائي يبقى نصًا، الحدود وعدم التخزين.
- [SiteSearchTests (Web)](../../../tests/Onyx.Web.Tests/SiteSearchTests.cs).
- [RateLimitingTests](../../../tests/Onyx.API.IntegrationTests/Http/RateLimitingTests.cs) (`Site_search_has_its_own_per_client_limit`).

## 5. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| قواعد التطبيع | **يجب** إعادة فهرسة كل شيء (احذف الصفوف ودع `ReconcileAsync` يعيد البناء، أو أضف ترحيلًا) لأن الفهرس والاستعلام يجب أن يتطابقا؛ `SearchTextNormalizerTests` |
| نوع محتوى جديد قابل للبحث | `SearchGroups.For`/`Ordered`/`Boost`، ترجمات المجموعات في Web |
| شروط الفهرسة | يجب أن تطابق قاعدة خريطة الموقع (`PublicContentBuilder` — قابل للفهرسة وذاتي canonical) |
| استبدال المحرك (دلالي/خارجي) | نفّذ `ISiteSearchService` جديدًا؛ الواجهة تستقبل نصًا مطبّعًا محدود الطول |
| امتداد PostgreSQL جديد | يحتاج موافقة (CLAUDE.md §19) |

## 6. الحالة

- **منفّذ:** v1 على PostgreSQL.
- **مستقبلي:** بحث دلالي/متجهي أو محرك خارجي (خلف نفس المنفذ).
