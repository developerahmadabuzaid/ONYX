# الأمان والخصوصية — الضوابط المنفّذة ومخاطر التعديل

> هذا ملخص جامع لما هو **مبني** فعلًا، مع روابط إلى ملفات الميزات للتفاصيل. القواعد غير القابلة للتفاوض في CLAUDE.md §12–§14 و§10.
> **قاعدة ذهبية:** لا تُضعف أي ضابط أمني أو خصوصية أو موافقة أو اعتماد AI لكي «يعمل» شيء أو يمر اختبار.

## 1. المصادقة والجلسات (API) — [01-identity-auth-AR.md](features/01-identity-auth-AR.md)

| الضابط | التنفيذ |
|---|---|
| لا تسجيل عام | المستخدمون بالدعوة فقط (`Users.Manage`)؛ أول SuperAdmin بأمر CLI |
| كلمات المرور | Identity: طول ≥ 12، 4 أحرف فريدة؛ قفل بعد 5 محاولات/15 دقيقة؛ معادلة التوقيت للحسابات غير الموجودة (`PasswordTimingEqualizer`)؛ خطأ موحد |
| 2FA (TOTP) | إلزامي لـ SuperAdmin وحاملي `Users.Manage`، `Roles.Manage`، `ProviderConnections.Manage`، `Privacy.Manage`، `Leads.Export` (أو للجميع بالإعداد)؛ منع إعادة الرمز؛ 10 رموز استرداد لمرة واحدة؛ المفتاح والرموز **مشفّرة في القاعدة** (Data Protection)؛ ربط مفتاح التسجيل بالتحدي |
| رموز JWT | HS256، 10 دقائق، claims دنيا (`sub`، `sid`، `sst`، `jti`) — **لا صلاحيات**؛ تحقق الجلسة وختم الأمان في كل طلب |
| رموز التحديث | معتمة عشوائية، **التجزئة فقط** مخزنة، دوّارة، مهلة سماح 30 ث بخلَف مختوم، **إعادة الاستخدام تُلغي الجلسة كاملة** + إشعار أمني |
| حدود الجلسة | خمول 60 دقيقة، مطلق 12 ساعة؛ الإلغاء الخادمي فوري محليًا وخلال ≤ 15 ث في النسخ الأخرى |
| Turnstile على الدخول وإعادة التعيين | عند تهيئته؛ بدونه (غير الإنتاج) تبقى الحمايات الأخرى |
| أوامر الاسترداد | CLI فقط (لا HTTP)، مُدققة، مقيدة بآخر SuperAdmin مسجّل |

## 2. BFF في Onyx.Web

- الرموز داخل كوكي `__Host-onyx.admin` (HttpOnly، Secure، SameSite=Strict، Path=/، جلسة متصفح) مشفّر وموقّع بـ Data Protection. **ممنوع**: عرض الرموز في HTML، localStorage/sessionStorage، أو تسجيلها.
- التحديث من الخادم قبل الانتهاء بـ 60 ث؛ الرفض ⇒ خروج؛ انقطاع الـ API ⇒ إبقاء الجلسة.
- مفاتيح Data Protection لـ Web في `DataProtection:KeysDirectory` (اسم `Onyx.Web`) — **يجب أن تكون دائمة ومشتركة بين النسخ ومنسوخة احتياطيًا**؛ فقدانها يُخرج كل المسؤولين. مفاتيح الـ API في جدول `data_protection_keys` (فقدانها يُبطل مفاتيح 2FA المشفّرة!).
- Antiforgery على كل POST (`__Host-onyx.af`)؛ TempData مشفّر (`__Host-onyx.tmp`) لحمل التحديات والرموز لمرة واحدة.

## 3. التفويض — [02-roles-permissions-AR.md](features/02-roles-permissions-AR.md)

- **افتراضي آمن:** سياسة احتياطية تفرض المصادقة لكل نقطة نهاية ما لم تحمل `[AllowAnonymous]`؛ اختبار يتحقق أن كل إجراء يعلن تفويضه صراحة.
- الصلاحيات من الخادم في كل طلب (ذاكرة 60 ث)؛ نطاق النوع للمحتوى؛ نطاق المالك للعملاء المحتملين (`Leads.ViewAll`).
- حارس التصعيد: لا منح/سحب/تصرف في صلاحيات لا تملكها، ولا مسّ SuperAdmin إلا من SuperAdmin؛ آخر SuperAdmin نشط محمي من التعطيل/الخفض/المحو؛ لا تصرف على النفس.
- **الـ API هي نقطة الإنفاذ الوحيدة**؛ Web يُخفي فقط.

## 4. حماية HTTP

| الضابط | API | Web |
|---|---|---|
| HTTPS + HSTS | نعم (HSTS خارج Development) | نعم |
| ترويسات أمان | CSP `default-src 'none'`، X-Frame-Options DENY، nosniff، no-referrer، COOP/CORP | CSP **بـ nonce لكل طلب**، كل شيء مستضاف ذاتيًا؛ استثناء Turnstile لصفحات النماذج فقط وGA4 للاستجابات الموافِقة فقط؛ الإدارة `no-store` |
| CORS | غير مسجّل (رفض افتراضي) | — |
| حجم الطلب | 1 MB عام، رفع الوسائط 11 MB، سقف مطلق 50 MB | — |
| تحديد المعدل | عام 300/د، `strict` 10/د (الدخول، الاستعادة، النماذج، الحجز، الموافقة، المعاينة)، `search` 30/د — لكل IP حقيقي | — |
| الترويسات المُمرَّرة | موثوقة من `ForwardedHeaders:KnownProxies/KnownNetworks` فقط (فارغ = loopback) | Web يضع `X-Forwarded-For` = IP الزائر |
| الأخطاء | ProblemDetails بلا رسائل/أنواع/تتبع استثناءات | صفحات خطأ؛ API غير متاحة ⇒ 503 |
| السجلات | Serilog منظم؛ لا أسرار ولا PII؛ معرّف ترابط | لا رموز/QR/OTP في السجلات (اختبار) |

## 5. Turnstile (مكافحة الإساءة) — [10-leads-crm-AR.md](features/10-leads-crm-AR.md)

تحقق خادمي إلزامي (السر، الإجراء المتوقع، اسم المضيف، عمر ≤ 300 ث، استخدام واحد). **يفشل مغلقًا**: بلا سر ⇒ `Captcha.Unavailable` (503) للنماذج العامة. الإنتاج يرفض الإقلاع دون سر واسم مضيف (API) أو مفتاح موقع (Web). الإجراءات: `inquiry`، `booking`، `admin-login`، `password-reset`. Turnstile يُتحقق منه **آخرًا** في خط الاستقبال. الرمز والسر والـ IP لا تُسجَّل.

## 6. SSRF — [16-news-ssrf-AR.md §5](features/16-news-ssrf-AR.md)

كل رابط خارجي يقدّمه زائر أو مسؤول يمر بـ `SafeOutboundHttpClient`: DNS مرة واحدة + كل العناوين عامة + الاتصال بالعنوان المُدقق (ضد rebinding)، المنافذ الافتراضية، لا اعتماد ولا كوكيز، تحويلات مُعاد تحققها (≤ 3)، مهلات وحدود حجم بعد فك الضغط، robots.txt، User-Agent معرّف، تسجيل المضيف فقط. أي ميزة مستقبلية تجلب روابط (أدوات Phase 2) **يجب** أن تستخدمه.

## 7. XSS والمحتوى

- أجسام CMS **ليست HTML** (كتل نصية) ⇒ لا مسار XSS مخزن؛ Razor يرمّز كل شيء؛ لا `Html.Raw` لمحتوى المستخدمين.
- الروابط في المحتوى: مسار موقع آمن أو https مطلق فقط؛ CTA بلا WhatsApp؛ التحويلات داخلية فقط (`LocalRedirectPermanent` في Web).
- JSON-LD ووسم GA4 مُسلسلة مع هروب أحرف HTML.
- البريد: كل قيمة مرمّزة HTML، الروابط https، النص القابل للتعديل لا يتحكم في الروابط.
- الرفع: النوع من البايتات، إزالة البيانات الوصفية، SVG مرفوض، مفاتيح عشوائية خارج web root، لا path traversal.
- تصدير CSV: تحييد الصيغ (حقن CSV).

## 8. الأسرار والإعدادات

- **لا أسرار في Git.** محليًا User Secrets؛ في الإنتاج مخزن أسرار/متغيرات بيئة. أمثلة الأسرار: `ConnectionStrings:Onyx`، `Jwt:SigningKey`، `Turnstile:SecretKey`، بيانات SMTP، `Privacy:IpHashKey`، مفتاح حساب خدمة Google.
- كل الخيارات المكتوبة متحقق منها عند الإقلاع (`ValidateOnStart`).
- `provider_connections` لا تخزن أسرارًا؛ تغييرات الإعداد مُدققة **دون قيم**.
- أدوار PostgreSQL بأقل امتياز: `onyx_app` (DML)، `onyx_owner` (الترحيلات)، `onyx_test` (`NOSUPERUSER CREATEDB`) — [development-setup.md §2](../development-setup.md).
- فحص الحزم الضعيفة قبل كل إصدار ([TESTING-AR.md §3](TESTING-AR.md)).

## 9. الخصوصية — [13](features/13-consent-AR.md)، [14](features/14-privacy-retention-AR.md)

| الموضوع | التنفيذ |
|---|---|
| نظام السجل | قاعدة الإدارة؛ البريد إشعار فقط؛ الاستفسار يبقى إن فشل البريد |
| الموافقة على النماذج | `ConsentRecord`: تواصل (إلزامي) + تسويق (منفصل، قابل للسحب)، بنسخة نص |
| موافقة الكوكيز | إيصال خادمي مجهول بنسخة؛ تغيير النسخة يعيد السؤال؛ السكربتات بعد الموافقة فقط؛ السحب يحذف كوكيز GA والإسناد |
| الإسناد | first-touch ومعرّف GA فقط مع موافقة التحليلات؛ المُحيل أصل فقط |
| IP | لا يُخزن دون `Privacy:IpHashKey`؛ معه HMAC فقط (إيصالات الموافقة). الجلسات تخزن IP (بيانات شخصية تخضع للاحتفاظ) |
| PII في السجلات | ممنوع؛ `[SensitiveData]` يحجب في التدقيق؛ التصدير والمحو مُدققان بالأعداد فقط |
| حقوق أصحاب البيانات | تصدير ومحو بعد تحقق مسجّل، معاملة واحدة، متكرر الأمان |
| الاحتفاظ | سياسة لكل فئة، **لا فترات مبرمجة**؛ الافتراضي لا يحذف شيئًا |
| بيانات GA4 | مسار + `utm_*` فقط؛ لا PII؛ أحداث تحويل بلا بيانات شخصية؛ لا Consent Mode v2 |
| الشريط | ليس ادعاء امتثال |

## 10. مخاطر التعديل (اقرأ قبل لمس هذه المناطق)

| المنطقة | الخطر | التحقق المطلوب |
|---|---|---|
| ترتيب المعترضات في `AddInfrastructure` | تدقيق خاطئ للحذف الناعم، أحداث مفقودة | `PersistenceInterceptorTests` |
| `[AllowAnonymous]` على نقطة جديدة | كشف بيانات | `AuthorizationTests`، مراجعة يدوية |
| رسائل الأخطاء في `AuthService` | تعداد الحسابات | `AuthenticationTests` |
| `ProtectedTokenUserStore`/مفاتيح Data Protection | فقدان كل مفاتيح 2FA | خطة ترحيل + نسخ احتياطي |
| `OutboundAddressPolicy`/`SafeOutboundHttpClient` | SSRF إلى البنية الداخلية/البيانات الوصفية السحابية | `SafeOutboundHttpTests` |
| `SecurityHeadersMiddleware` (CSP) | XSS/تسريب الرموز | `PublicSiteTests`، `AnalyticsSiteTests`، `LeadFormsSiteTests` |
| `InquiryIntake.PrepareAsync` | تجاوز Turnstile أو استهلاكه | `InquiryApiTests` |
| `SubjectEraser`/`PersonalDataStore` | محو ناقص أو محو بيانات آخرين | `DataSubjectErasureApiTests`، `RetentionApiTests` |
| حراس النشر (الإثبات، AI) | نشر إثبات غير موثق/محتوى AI غير معتمد | `WorkAndProofApiTests`، `InsightsApiTests` |
| `ConsentState`/`AnalyticsTagFilter` | تحميل التتبع دون موافقة | `AnalyticsSiteTests`، `ConsentTests` |
| `AuditLogInterceptor`/`AuditTrail` | تسريب أسرار/PII في سجل غير قابل للحذف | `AdminOperationsApiTests` |

## 11. خارج النطاق حاليًا

اختبارات الأمان النشطة (فحص المنافذ، الحمولات) **ممنوعة** — أداة وضع الأمان المستقبلية سلبية فقط (CLAUDE.md §8). النسخ الاحتياطي والاستعادة (CLAUDE.md §15) مسؤولية الاستضافة وتُوثَّق عند اختيارها؛ لا كود نسخ احتياطي خاص بمزوّد في التطبيق.
