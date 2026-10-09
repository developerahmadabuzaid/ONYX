# خريطة أثر التغيير بين الميزات ووصفات الصيانة

> كل ملف ميزة في [features/](features/) يحوي خريطة أثر خاصة به. هذا الملف يجمع **الاعتماديات العابرة للميزات** و**وصفات عملية** للتغييرات الشائعة. ما لم يُذكر خلاف ذلك، كل علاقة هنا متحقق منها بالبحث في الكود عند `7271ad6`.

## 1. خريطة الاعتماديات بين الميزات

```
                       ┌──────────────── الهوية/الصلاحيات ────────────────┐
                       │ (كل خدمة إدارية: ICurrentAdmin / ContentAuthorizer)│
                       └───────────────────────┬──────────────────────────┘
                                               │
   الوسائط ◄── CMS (ContentItem) ──► SEO/المسارات/التحويلات ──► IndexNow
     ▲   (MediaUsageIndexer) │  │                │ PublicUrlsChanged
     │                       │  │                ▼
  هوية العلامة ──► بيانات Organization   البحث (SearchIndexer) ◄── الأخبار (NewsSearchIndexer)
                             │  │ ContentPublicationChanged
                             │  └──► الإثبات/المشاريع المميزة، الرؤى/المؤلفون
                             │
                             ├──► الذكاء التحريري (الكلمات، العناقيد، التوصيات ← بوابة الجودة)
                             │            ▲
                             │            └── لقطات الأداء ◄── التحليلات (GA4/GSC sync)
                             │                                        ▲
   النماذج العامة ── الاستفسارات/Leads ◄── الحجز                       │
        (Turnstile،   │  الإسناد ─────────────────────────► قمع الطرف الأول (AnalyticsFunnelRepository)
        الموافقة)     │
                      ├──► البريد (outbox) ──► الـ Worker ──► اتصالات المزوّدين ──► الإشعارات
                      └──► الخصوصية (المحو/التصدير/الاحتفاظ ← SubjectEraser، PersonalDataStore)
   التدقيق: يلتقط تغييرات كل الكيانات IAuditable + الإجراءات الصريحة في كل الخدمات
```

## 2. نقاط الالتقاء الحرجة (تغييرها يمس عدة ميزات)

| الرمز | المكان | يستخدمه |
|---|---|---|
| `ContentRouteSegments` | [Contracts/Content](../../src/Onyx.Contracts/Content/ContentRouteSegments.cs) | `ContentPathService` (API)، `SectionRoutes`/`PageController`/`LeadPages`/`SearchPages`/`NewsPages` (Web)، `PublicContentService` |
| `Permissions` | [Contracts/Authorization](../../src/Onyx.Contracts/Authorization/Permissions.cs) | كل المتحكمات، `ContentAuthorizer`، `SystemRoles`، `TwoFactorPolicy`، `AdminNavigation`، الإشعارات |
| `SupportedLanguages.All` | [Application/Localization](../../src/Onyx.Application/Localization/SupportedLanguages.cs) | اكتمال النشر، المدققات، البريد، الأخبار، الاحتفاظ… |
| `ContentSnapshot` | [Application/Content](../../src/Onyx.Application/Content/ContentSnapshot.cs) | المراجعات، النشر، البصمة، المعاينة، البحث، الوسائط، الروابط الداخلية |
| `PublicPageFingerprint` | [Application/Content](../../src/Onyx.Application/Content/PublicPageFingerprint.cs) | `lastmod`، إعلانات IndexNow |
| `SearchTextNormalizer` | [Application/Search](../../src/Onyx.Application/Search/SearchTextNormalizer.cs) | البحث، تصنيف الأخبار (`NewsClassifier`) |
| `TextNormalization.NormalizeKeyword` | [Domain/Content](../../src/Onyx.Domain/Content/TextNormalization.cs) | كشف التنافس، خريطة الكلمات |
| `InquiryIntake` / `InquiryRecorder` | [Application/Leads](../../src/Onyx.Application/Leads/) | النماذج **والحجز** |
| `SubjectEraser` | [Application/Privacy](../../src/Onyx.Application/Privacy/SubjectEraser.cs) | طلبات المحو **والاحتفاظ** |
| `IProviderConnectionService` | [Application/Providers](../../src/Onyx.Application/Providers/) | البريد، التحليلات، الأخبار، IndexNow |
| `INotificationService` | [Application/Notifications](../../src/Onyx.Application/Notifications/) | الهوية، المحتوى، العملاء، الحجز، الأخبار، المزوّدون، الـ Worker |
| `ApplicationDbContext.SaveChangesAsync` (ترجمة القيود) | [Infrastructure/Persistence](../../src/Onyx.Infrastructure/Persistence/ApplicationDbContext.cs) | المشاريع المميزة، الحجز، الأخبار |
| ترتيب المعترضات | [Infrastructure/DependencyInjection.cs](../../src/Onyx.Infrastructure/DependencyInjection.cs) | التدقيق، الحذف الناعم، الـ outbox |

## 3. وصفات عملية

### 3.1 «أريد إضافة حقل جديد لصفحات المحتوى»
1. `ContentTranslation` (إن كان لكل لغة) أو `ContentItem` + دالة التحديث + التحقق في Domain.
2. `ContentSnapshot` (مع **توافق رجوعي** للقطات القديمة) ← وإلا تفشل الاستعادة والمقارنة.
3. `PublicPageFingerprint`: هل يراه الزائر/محرك البحث؟ إن نعم أضفه.
4. `ContentMapping` + DTO في Contracts + `ContentAdministrationService`.
5. `PublicContentBuilder` إن كان يُعرض؛ `ContentQualityGate` إن احتاج قاعدة.
6. إعداد EF + ترحيل (بموافقة) + `has-pending-model-changes`.
7. Web: نموذج الإدارة والعرض العام + ترجمات.
8. اختبارات Domain + API + Web.

### 3.2 «أريد تغيير مدة رمز الوصول أو الجلسة»
`Jwt:AccessTokenMinutes` / `Sessions:*` في الإعدادات. راجع `BffDefaults.RefreshMargin` وCLAUDE.md §12 (القيم الموثقة) و`SessionTokenTests`.

### 3.3 «أريد إضافة صلاحية جديدة»
[02-roles-permissions-AR.md §9](features/02-roles-permissions-AR.md).

### 3.4 «أريد نوع استفسار عام جديد»
[10-leads-crm-AR.md §8](features/10-leads-crm-AR.md): `InquiryType` + `InquiryTypes.PublicForms` + `CheckRequirements` + `LeadScore` + نماذج Web + `ConversionEvent` + المستلمون.

### 3.5 «أريد تغيير قواعد التحقق من الإثبات»
`ProofVerification.Create` → `ContentItem.VerifyProof`/`ProofItem.Verify` → `ContentQualityGate.CheckProofAndProvenance` → `ContentProofService`/`ProofItemService` (التدقيق بلا ملاحظات) → `WorkAndProofApiTests`، `WorkProofInsightRulesTests`. **لا تُضعف** سياسة الإثبات (CLAUDE.md §2.1).

### 3.6 «أريد تخزين بيانات شخصية جديدة»
[14-privacy-retention-AR.md §6](features/14-privacy-retention-AR.md): فئة احتفاظ، `PersonalDataStore` (تحميل/تصدير/محو/احتفاظ)، `[SensitiveData]`، `Erase`، الاختبارات.

### 3.7 «أريد مهمة خلفية جديدة»
[BACKGROUND-JOBS-AR.md §3.3](BACKGROUND-JOBS-AR.md) + تحديث العدد في `JobSchedulingTests`.

### 3.8 «أريد ربط مزوّد خارجي جديد (LLM، تقويم، إعلانات…)»
1. **موافقة المالك على الحزمة** أولًا (CLAUDE.md §3).
2. المنفذ في Application (بلا أنواع المزوّد)؛ المحوّل في Infrastructure فقط؛ قاعدة معمارية تعزل الحزمة (كما لـ Google وRSS).
3. `ProviderKeys` + تقارير `IProviderConnectionService`؛ `NotConfigured` حتى تتوفر بيانات الاعتماد واختبار اتصال ناجح؛ **لا بيانات وهمية**.
4. الأسرار في مخزن الأسرار فقط؛ الخيارات متحقق منها عند الإقلاع.
5. كل روابط المستخدم عبر `SafeOutboundHttpClient`.
6. العمل الطويل في الـ Worker.

### 3.9 «أريد تعديل نص بريد»
من الإدارة (قوالب البريد) دون كود — مع الحفاظ على العناصر النائبة الإلزامية. لتغيير التخطيط/الروابط: صنف القالب في [Email/Templates/](../../src/Onyx.Infrastructure/Email/Templates/).

### 3.10 «أريد تغيير مقطع رابط قسم (مثل `/en/work`)»
**يكسر روابط منشورة.** غيّر `ContentRouteSegments` (+ `Reserved`)، ثم أنشئ قواعد `Redirect` للمسارات القديمة أو أضف مسارات تاريخية، وحدّث اختبارات Web. الأفضل تجنّبه بعد الإطلاق.

### 3.11 «أريد تفعيل GA4/Search Console»
لا كود: `Analytics:Google:Ga4PropertyId`، `SearchConsoleSiteUrl`، مفتاح حساب الخدمة (سر)، و`Analytics:Ga4MeasurementId` في Web؛ ثم «مزامنة الآن» من شاشة التكاملات. التفاصيل في [development-setup.md §12](../development-setup.md).

## 4. ما يجب تحديثه دائمًا مع أي تغيير

- الاختبارات (Domain/Application/API/Web حسب الطبقة) + اختبارات 401/403 للنقاط الجديدة.
- ملفات `.resx` (ar وen معًا — المفاتيح متطابقة).
- هذا الدليل: ملف الميزة + [API-ROUTES-AR.md](API-ROUTES-AR.md) / [DATABASE-AR.md](DATABASE-AR.md) / [BACKGROUND-JOBS-AR.md](BACKGROUND-JOBS-AR.md) حسب الحاجة.
- [docs/architecture.md](../architecture.md) و[docs/implementation-plan.md](../implementation-plan.md) إن تغير قرار معماري أو مرحلة.
