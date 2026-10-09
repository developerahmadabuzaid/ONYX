# مسرد المصطلحات (لمطوّر .NET جديد على المشروع)

| المصطلح | الشرح | أين يظهر في Onyx |
|---|---|---|
| **.NET 10 / C#** | منصة التطوير ولغة البرمجة (إصدار دعم طويل LTS). | كل `src/` |
| **ASP.NET Core** | إطار ويب في .NET: Web API (متحكمات تعيد JSON) وMVC (صفحات Razor). | Onyx.API، Onyx.Web |
| **Razor / cshtml** | قوالب HTML مع C#؛ ترمّز المخرجات تلقائيًا ضد XSS. | `src/Onyx.Web/Views`، `Areas/Admin/Views` |
| **Area** | قسم معزول داخل تطبيق MVC. | `Areas/Admin` = لوحة الإدارة |
| **Controller / Action** | صنف يستقبل طلبات HTTP / دالة تعالج مسارًا واحدًا. | `Controllers/` |
| **Middleware** | مكوّن في خط معالجة كل طلب (ترويسات، أخطاء، معدل…). | `Onyx.API/Middleware`، `Onyx.Web/Security` |
| **Filter** | منطق يعمل قبل/بعد إجراءات MVC. | `AdminShellFilter`، `AnalyticsTagFilter`، `ApiUnavailableFilter` |
| **DI (حقن الاعتماديات)** | الحاوية تنشئ الأصناف وتمرر اعتمادياتها في المُنشئ. Scoped = لكل طلب، Singleton = واحد للتطبيق. | `DependencyInjection.cs`، `Program.cs` |
| **Composition Root (جذر التركيب)** | المكان الوحيد الذي تُربط فيه الطبقات معًا. | `Program.cs` في API/Worker/Web |
| **Options pattern** | ربط أقسام الإعدادات بأصناف مكتوبة، مع `ValidateOnStart` لرفض الإعداد الخاطئ عند الإقلاع. | `*Options.cs` |
| **User Secrets** | مخزن أسرار محلي للتطوير خارج المستودع. | [development-setup.md](../development-setup.md) |
| **EF Core** | ORM: يحوّل أصناف C# إلى جداول وLINQ إلى SQL. | `Onyx.Infrastructure/Persistence` |
| **DbContext** | جلسة EF مع قاعدة البيانات؛ هنا هو أيضًا `IUnitOfWork`. | `ApplicationDbContext` |
| **Migration (ترحيل)** | ملف يصف تغيير المخطط ويُطبَّق بالترتيب؛ المصدر الوحيد للمخطط. | `Persistence/Migrations` — [DATABASE-AR.md](DATABASE-AR.md) |
| **Model Snapshot** | صورة EF الحالية للمخطط؛ تُقارن بالنموذج لاكتشاف الانحراف. | `ApplicationDbContextModelSnapshot.cs` |
| **Interceptor** | خطاف EF يعمل عند الحفظ. | التدقيق، الحقول، الـ outbox |
| **Npgsql** | مزوّد PostgreSQL لـ .NET وEF. | Infrastructure |
| **jsonb / timestamptz / xmin** | أنواع PostgreSQL: JSON ثنائي / وقت بمنطقة / عمود نظام يتغير مع كل تحديث صف (يُستخدم للتزامن المتفائل). | الإعدادات |
| **tsvector / tsquery / GIN / pg_trgm** | فهرسة البحث النصي الكامل في PostgreSQL / الاستعلام / نوع فهرس / امتداد التشابه الثلاثي للأخطاء الإملائية. | [15-search-AR.md](features/15-search-AR.md) |
| **SKIP LOCKED** | يسمح لعدة عمليات بالمطالبة بصفوف مختلفة دون انتظار بعضها. | `JobScheduler`، `OutboxProcessor`، `EmailProcessor` |
| **Lease (عقد إيجار)** | قفل مؤقت بمالك ووقت انتهاء؛ ينتهي تلقائيًا إن تعطلت النسخة. | `locked_by`، `locked_until_utc` |
| **Optimistic concurrency (التزامن المتفائل)** | لا قفل عند القراءة؛ عند الحفظ يُرفض التغيير إن تغيّر الصف منذ القراءة (409). | `Version` (xmin)، `ConcurrencyConflictException` |
| **Clean Architecture / Layers** | فصل Domain/Application/Infrastructure/الواجهات مع اتجاه اعتماد للداخل. | [ARCHITECTURE-AR.md](ARCHITECTURE-AR.md) |
| **Domain entity / aggregate** | كائن عمل بهوية وسلوك يحمي قواعده (لا خصائص قابلة للتعيين من الخارج). | `Onyx.Domain` |
| **Value object / record** | قيمة بلا هوية تُقارن بمحتواها. | `SeoProfile`، `Attribution`، `ContentBlock` |
| **Domain event** | «شيء حدث» يرفعه كيان ويُعالج لاحقًا. | `ContentPublicationChanged`، `InquirySubmitted`… |
| **Outbox (صندوق الصادر)** | تخزين الأحداث في نفس معاملة التغيير ثم تسليمها لاحقًا ⇒ لا ضياع ولا كتابة مزدوجة. | `outbox_messages` |
| **At-least-once / Idempotent** | قد يُسلَّم الحدث أكثر من مرة / المعالجة المتكررة لا تغيّر النتيجة. | `IProcessedEventLog` |
| **Dead-letter** | رسالة توقفت محاولاتها بعد الحد الأقصى وتنتظر تدخلًا. | `dead_lettered_at_utc`، `DeadLettered` |
| **Exponential backoff** | زيادة زمن الانتظار بين المحاولات مضاعفًا. | `RetryBackoff` |
| **BackgroundService / Hosted service** | خدمة طويلة التشغيل داخل مضيف .NET. | Onyx.Worker |
| **Port / Adapter (منفذ/محوّل)** | واجهة في Application يحققها صنف في Infrastructure لمزوّد خارجي. | `IWebAnalyticsProvider` ← `GoogleAnalyticsDataProvider` |
| **Result / Result&lt;T&gt;** | قيمة تمثل نجاحًا أو خطأ متوقعًا مكتوبًا (`Error` برمز ثابت) بدل الاستثناء. | `Onyx.Domain/Common` |
| **ProblemDetails (RFC 9457)** | صيغة JSON قياسية لأخطاء HTTP. | `ResultHttpExtensions`، `ApiProblem` |
| **FluentValidation** | مكتبة قواعد تحقق للطلبات. | `*Validator.cs` في Application |
| **JWT** | رمز موقّع يحمل ادعاءات (claims). هنا قصير العمر وبلا صلاحيات. | `JwtTokenIssuer` |
| **Refresh token rotation** | كل استخدام لرمز التحديث يعطي رمزًا جديدًا ويبطل القديم؛ إعادة استخدام القديم = سرقة. | `SessionManager` |
| **BFF (Backend for Frontend)** | الخادم الأمامي يحتفظ بالرموز ويستدعي الـ API نيابة عن المتصفح. | Onyx.Web |
| **Data Protection** | واجهة .NET للتشفير/التوقيع بمفاتيح مُدارة (الكوكيز، المفاتيح المحمية). | Web والـ API |
| **TOTP / 2FA** | رموز لمرة واحدة مبنية على الوقت (تطبيقات المصادقة) / التحقق بعاملين. | `Totp`، `AuthService` |
| **Antiforgery (CSRF)** | رمز في النماذج يمنع إرسالها من مواقع أخرى. | كل POST في Web |
| **CSP / nonce** | سياسة أمان المحتوى تحدد مصادر السكربتات / قيمة عشوائية لكل طلب تسمح لسكربت مضمّن محدد فقط. | `SecurityHeadersMiddleware` |
| **HSTS** | إجبار المتصفح على HTTPS. | API وWeb |
| **CORS** | سماح المتصفح باستدعاءات عبر الأصول — غير مفعّل في الـ API. | — |
| **Rate limiting** | تحديد عدد الطلبات لكل عميل في نافذة زمنية. | `RateLimitingOptions` |
| **Turnstile** | تحدي Cloudflare لمكافحة الروبوتات؛ يتحقق الخادم من رمزه. | `TurnstileCaptchaVerifier` |
| **Honeypot** | حقل مخفي يملؤه الروبوت فقط. | `InquiryIntake` |
| **SSRF** | خداع الخادم ليطلب عناوين داخلية. | `SafeOutboundHttpClient` |
| **DNS rebinding** | تغيير إجابة DNS بين الفحص والاتصال للوصول لعنوان داخلي. | `OutboundAddressPolicy` + الاتصال بالعنوان المُدقق |
| **XSS** | حقن سكربت في الصفحات. | الأجسام نصية + ترميز Razor |
| **PII** | بيانات تعريف شخصية (اسم، بريد، هاتف، IP…). | `[SensitiveData]`، الخصوصية |
| **Data subject request** | طلب صاحب البيانات للتصدير أو المحو. | [14-privacy-retention-AR.md](features/14-privacy-retention-AR.md) |
| **Retention policy** | مدة الاحتفاظ بفئة بيانات وما يحدث بعدها. | `RetentionPolicy` |
| **Slug** | الجزء المقروء من الرابط (عربي أو إنجليزي). | `Slug.Normalize` |
| **NFC / NFKC** | أشكال تطبيع يونيكود لضمان تطابق الحروف المتكافئة. | `Slug`، `SearchTextNormalizer` |
| **Tashkeel / Tatweel** | الحركات العربية / حرف المدّ «ـ»؛ يُحذفان من الـ slug والبحث. | `TextNormalization` |
| **RTL** | اتجاه من اليمين لليسار للعربية. | `LanguageCatalog.Direction` |
| **Canonical / hreflang / x-default** | الرابط المرجعي للصفحة / ربط النسخ اللغوية / النسخة الافتراضية (العربية). | `SeoContext` |
| **noindex / robots.txt / sitemap** | منع الفهرسة / ملف قواعد الزاحفين / قائمة الروابط لمحركات البحث. | `SitemapController` |
| **lastmod** | آخر تعديل مرئي للصفحة في خريطة الموقع. | `LiveContentModifiedAtUtc` |
| **IndexNow** | بروتوكول لإبلاغ محركات البحث (Bing وغيره) بتغيّر الروابط. | `IndexNowNotifier` |
| **JSON-LD / Structured data** | بيانات منظمة تصف الصفحة لمحركات البحث. | `StructuredDataBuilder` |
| **CTA** | دعوة لإجراء (زر/رابط تحويل). | `CallToAction`، `CtaResolver` |
| **Lead / Inquiry / Funnel** | عميل محتمل / استفسار مفرد / مراحل التحويل. | [10](features/10-leads-crm-AR.md)، [17](features/17-analytics-search-console-AR.md) |
| **Attribution / UTM / first-touch** | نسب الاستفسار لمصدره / وسوم الحملات في الرابط / أول نقطة تواصل. | `Attribution`، `AttributionCookies` |
| **GA4 / Search Console** | Google Analytics 4 / أداة Google لأداء البحث. | Stage 12 |
| **Proof (إثبات)** | محتوى يدّعي نتائج/عملاء/قدرات؛ يتطلب تحققًا مسجلًا. | [08](features/08-proof-work-AR.md) |
| **Pillar / Cluster** | ركيزة خدمات أو صفحة ركيزة لعنقود مواضيع / مجموعة محتوى حول موضوع. | `ServicePillar`، `ContentCluster` |
| **Cannibalization (تنافس داخلي)** | صفحتان تستهدفان نفس الكلمة. | خريطة الكلمات، بوابة الجودة |
| **xUnit v3 / Microsoft.Testing.Platform** | إطار الاختبارات / مشغّل الاختبارات الحديث. | [TESTING-AR.md](TESTING-AR.md) |
| **WebApplicationFactory** | تشغيل التطبيق في الذاكرة للاختبارات. | `OnyxApiFactory`، `OnyxWebFactory` |
| **NetArchTest** | مكتبة لاختبار قواعد الاعتماد بين الأنواع. | Onyx.ArchitectureTests |
| **Stage (مرحلة)** | وحدة تنفيذ في خطة المشروع؛ كل مرحلة التزام واحد ومراجعة مالك. | [implementation-plan.md](../implementation-plan.md) |
| **Phase 1/2/3** | نطاق الإطلاق / الأدوات ومحرك المحتوى / المستشار والإعلانات والأسواق. | CLAUDE.md |
