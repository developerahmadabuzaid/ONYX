# 01 — الهوية والمصادقة والتحقق الثنائي والجلسات (BFF)

## 1. ماذا تفعل الميزة

تسجيل دخول مسؤولي لوحة الإدارة فقط (لا تسجيل عام). يقوم Onyx.API بكل التحقق (Identity، كلمات المرور، القفل، TOTP، الجلسات، رموز JWT). يعمل Onyx.Web كـ **BFF**: يحتفظ برمزي الوصول والتحديث داخل كوكي مشفّر وموقّع، ولا يراهما JavaScript المتصفح أبدًا.

**دورة الحياة باختصار:**
1. دعوة (من مسؤول يحمل `Users.Manage`) → بريد برابط → قبول الدعوة وتعيين كلمة مرور → `EmailConfirmed = true`.
2. دخول بكلمة المرور (+ Turnstile إن كان مهيأً) → إما رموز مباشرة، أو تحدٍّ ثانٍ (TOTP/رمز استرداد)، أو **تسجيل إلزامي** لمصادِق.
3. جلسة: رمز وصول JWT ≈ 10 دقائق + رمز تحديث دوّار مُجزّأ، خمول 60 دقيقة، حد مطلق 12 ساعة.
4. انتهاء: تسجيل خروج، تغيير/إعادة تعيين كلمة المرور، تعطيل المستخدم، إعادة ضبط 2FA، كشف إعادة استخدام رمز التحديث، أو انتهاء الخمول/الحد المطلق.

## 2. الملفات والأصناف

| الطبقة | المسار | الأصناف الرئيسية |
|---|---|---|
| Contracts | [src/Onyx.Contracts/Identity/](../../../src/Onyx.Contracts/Identity/) | `LoginRequest`، `LoginResponse`/`LoginStatus`، `TwoFactorLoginRequest`، `TokenPairResponse`، `CurrentUserResponse`، طلبات كلمة المرور والدعوة |
| Application | [src/Onyx.Application/Identity/](../../../src/Onyx.Application/Identity/) | `IAuthService`، `ISessionValidator`، `IIdentityBootstrapService`، `TwoFactorPolicy`، المدققات في `Validation/IdentityRequestValidators.cs` |
| Infrastructure | [src/Onyx.Infrastructure/Identity/](../../../src/Onyx.Infrastructure/Identity/) | `AuthService`، `SessionManager`، `SessionValidator`، `JwtTokenIssuer`، `ProtectedTokenUserStore`، `Totp`، `TokenHashing`، `RefreshSuccessorSealer`، `PasswordTimingEqualizer`، `IdentityBootstrapService`، الكيانات `ApplicationUser`/`UserSession`/`RefreshToken`/`LoginChallenge` |
| التسجيل | [IdentityServiceCollectionExtensions.cs](../../../src/Onyx.Infrastructure/IdentityServiceCollectionExtensions.cs) | `AddOnyxIdentity` — للـ API فقط |
| API | [AuthController.cs](../../../src/Onyx.API/Controllers/AuthController.cs)، [AdminCommandRunner.cs](../../../src/Onyx.API/Configuration/AdminCommandRunner.cs) | |
| Web (BFF) | [src/Onyx.Web/Authentication/](../../../src/Onyx.Web/Authentication/)، [ApiClients/Handlers/](../../../src/Onyx.Web/ApiClients/Handlers/)، [Areas/Admin/Controllers/AccountController.cs](../../../src/Onyx.Web/Areas/Admin/Controllers/AccountController.cs)، [AuthenticatorQrCode.cs](../../../src/Onyx.Web/Areas/Admin/AuthenticatorQrCode.cs) | `AdminSignInService`، `BffCookieEvents`، `SessionRefreshCoordinator`، `AccessTokenHandler`، `AdminSessionEndedFilter`، `CurrentAdminContext` |

## 3. نقاط الدخول

**API** (`/api/v1/auth/...`): انظر [API-ROUTES-AR.md §2.1](../API-ROUTES-AR.md). كل الاستجابات `no-store`؛ نقاط الدخول المجهولة الحساسة بسياسة `strict`.

**Web Admin** — [AccountController](../../../src/Onyx.Web/Areas/Admin/Controllers/AccountController.cs) تحت `admin/account`:

| المسار | الطريقة | الوصول | ماذا يفعل |
|---|---|---|---|
| `login` | GET/POST | مجهول | نموذج الدخول (antiforgery + Turnstile)، يستدعي `IAuthApiClient` |
| `two-factor` | GET/POST | مجهول | رمز التحدي محفوظ في TempData المشفّر وليس في الصفحة |
| `two-factor/setup` | GET/POST | مجهول | عرض المفتاح + رمز QR (SVG data URI) ثم رموز الاسترداد مرة واحدة |
| `logout` | POST | مُصادَق | يُلغي رمز التحديث في الـ API ويمسح الكوكي |
| `forgot-password` | GET/POST | مجهول | يعرض دائمًا نفس التأكيد |
| `reset-password` / `accept-invitation` | GET/POST | مجهول | الرمز يُنقل من الرابط إلى TempData ثم يُرسل في جسم الطلب للخادم |
| `change-password` | GET/POST | مُصادَق | |
| `access-denied` | GET | مجهول | |
| `/admin/language` | GET | مجهول | تبديل لغة واجهة الإدارة |

**أوامر المشغّل (CLI، لا سطح HTTP):** `seed-identity`، `bootstrap-superadmin`، `reset-two-factor` — عبر `AdminCommandRunner.TryRunAsync` قبل تشغيل المضيف. كلمة مرور الإقلاع من `ONYX_BOOTSTRAP_PASSWORD` فقط. التفاصيل في [development-setup.md §5، §7](../../development-setup.md).

## 4. التدفق من الطرف إلى الطرف

### 4.1 تسجيل الدخول

```
Browser → POST /admin/account/login (Onyx.Web, antiforgery)
  → IAuthApiClient.LoginAsync → POST /api/v1/auth/login (X-Forwarded-For = IP الزائر)
     → AuthController.Login → AuthService.LoginAsync
         1. LoginRequestValidator
         2. VerifyCaptchaAsync(token, "admin-login")  ← فقط إن كان Turnstile مهيأً
         3. FindByEmailAsync؛ غير موجود/معطل/غير مُفعّل/مقفل → PasswordTimingEqualizer.Burn + نفس الخطأ Auth.InvalidCredentials
         4. CheckPasswordAsync؛ فشل → AccessFailedAsync (5 محاولات = قفل 15 دقيقة) + تدقيق LoginFailed/LockedOut
         5. TwoFactorEnabled → LoginChallenge(TwoFactor, 5 دقائق) → LoginStatus.TwoFactorRequired
         6. TwoFactorPolicy.IsRequired → LoginChallenge(TwoFactorEnrollment, 15 دقيقة) → TwoFactorEnrollmentRequired
         7. وإلا CompleteSignInAsync → SessionManager.Start → TokenPairResponse + تدقيق LoginSucceeded
  ← AdminSignInService.SignInAsync: GET /auth/me بالرمز، ثم إصدار كوكي __Host-onyx.admin
```

### 4.2 كل طلب إداري لاحق

```
Onyx.Web Admin controller → typed client (AccessTokenHandler)
   يقرأ access_token من التذكرة المشفّرة → Authorization: Bearer
   401 → AdminSessionEndedException → AdminSessionEndedFilter: SignOut + Redirect إلى Login
Onyx.API: JwtBearer (HS256، issuer/audience، عمر) → OnTokenValidated:
   يتطلب claims: sub، sid، sst → ISessionValidator.IsValidAsync(userId, sessionId, stampHash)
   (الجلسة غير ملغاة، المستخدم نشط، قبل الحد المطلق، تجزئة ختم الأمان مطابقة بمقارنة ثابتة الزمن)
   مخزّن مؤقتًا لكل نسخة Sessions:ValidationCacheSeconds (افتراضي 15 ث)
```

### 4.3 التحديث التلقائي

`BffCookieEvents.ValidatePrincipal` (في كل طلب Web مُصادَق): إذا تبقى أقل من 60 ثانية (`BffDefaults.RefreshMargin`) على انتهاء رمز الوصول → `SessionRefreshCoordinator` يدمج الطلبات المتزامنة → `POST /auth/refresh` → تخزين الرموز الجديدة وتجديد الكوكي. الـ API غير متاحة → تُبقي الجلسة (`IsUnavailable`). الرفض → إلغاء التذكرة وتسجيل الخروج.

داخل الـ API، `SessionManager.RefreshAsync(rawToken)`:
- يبحث بالتجزئة SHA-256؛ جلسة ملغاة/منتهية مطلقًا → Invalid.
- مستخدم غير نشط → إلغاء الجلسة (`UserDisabled`).
- رمز مستهلك سابقًا → `HandleConsumedTokenAsync`: داخل مهلة السماح (`RefreshGraceSeconds` = 30، حتى `MaxGraceRedemptions` = 3) يفتح `SealedSuccessor` (الخلف مشفّر بمفتاح مشتق من الرمز الخام الذي لا يعرفه إلا العميل) ويعيد **نفس** الخلف؛ خارجها → **إلغاء الجلسة كاملة** + `ReuseDetected`.
- رمز منتهي الخمول → إلغاء (`IdleExpired`).
- مطالبة ذرية بـ `ExecuteUpdateAsync … WHERE consumed_at_utc IS NULL`؛ إن سبقه طلب متزامن يُعاد التقييم مرة واحدة عبر مسار السماح.
- `AuthService.RefreshAsync` عند `ReuseDetected`: تدقيق `RefreshTokenReuseDetected` + إشعار `SecurityAlert` (Critical، `Users.Manage`).

## 5. الدوال المفتاحية

| الدالة | التوقيع | المدخلات/المخرجات | الآثار الجانبية والأخطاء |
|---|---|---|---|
| `AuthService.LoginAsync` | `Task<Result<LoginResponse>> LoginAsync(LoginRequest, CancellationToken)` | بريد، كلمة مرور، `CaptchaToken` | تدقيق Security لكل نتيجة؛ خطأ موحد `Auth.InvalidCredentials` (401) لكل فشل اعتماد؛ فشل Turnstile → `Captcha.Failed` (400) أو `Captcha.Unavailable` (503) |
| `AuthService.CompleteTwoFactorLoginAsync` | `Task<Result<TokenPairResponse>> (TwoFactorLoginRequest, …)` | `ChallengeToken` + (`Code` 6 أرقام **أو** `RecoveryCode`) | TOTP بمنع الإعادة (`LastTotpTimeStep`)؛ رمز الاسترداد مرة واحدة + تدقيق `TwoFactorRecoveryCodeUsed`؛ 5 أخطاء تُغلق التحدي (`MaxChallengeAttempts`) وتزيد عداد القفل |
| `AuthService.StartTwoFactorEnrollmentAsync` | `(TwoFactorEnrollmentStartRequest, …)` → `TwoFactorEnrollmentStartResponse(key, otpauthUri)` | رمز تحدي التسجيل | يولّد مفتاحًا جديدًا في كل بدء ويربطه بمعرّف التحدي (`AuthenticatorEnrollmentBinding`) |
| `AuthService.CompleteTwoFactorEnrollmentAsync` | → `TwoFactorEnrollmentCompleteResponse(recoveryCodes, tokens)` | | يرفض أي مفتاح من محاولة أخرى؛ يُفعّل 2FA، يولّد 10 رموز استرداد، تدقيق `TwoFactorEnabled`، ثم يبدأ الجلسة |
| `AuthService.RequestPasswordResetAsync` | `Task<Result> (ForgotPasswordRequest, …)` | | ينجح دائمًا (202) حتى لحساب غير موجود؛ يضع بريد `PasswordResetEmailTemplate` في الطابور فقط لحساب نشط مؤكد؛ Turnstile بإجراء `password-reset` |
| `AuthService.ResetPasswordAsync` | | `UserId`، `Token`، `NewPassword` | يفك القفل، يُلغي **كل** الجلسات (`PasswordReset`)، تدقيق `PasswordReset`؛ رمز سيئ → `Auth.InvalidToken` |
| `AuthService.AcceptInvitationAsync` | | | يتحقق من رمز `InvitationTokenProvider`، يضيف كلمة المرور، `EmailConfirmed = true`، يغيّر ختم الأمان (الرابط لمرة واحدة) |
| `AuthService.ChangePasswordAsync` | | | يُلغي كل الجلسات بما فيها الحالية (`PasswordChanged`) |
| `AuthService.GetCurrentUserAsync` | → `CurrentUserResponse` | | الصلاحيات محسوبة على الخادم من `IPermissionService` |
| `SessionManager.Start` | `TokenPairResponse Start(ApplicationUser)` | | يضيف `UserSession` + `RefreshToken` إلى وحدة العمل (المستدعي يحفظ) |
| `SessionManager.RevokeAllAsync` | `Task<int> (Guid userId, string reason, …)` | | `ExecuteUpdateAsync` فوري + مسح ذاكرة التحقق المؤقتة محليًا؛ النسخ الأخرى تلحق خلال `ValidationCacheSeconds` |
| `SessionValidator.IsValidAsync` | `Task<bool> (Guid, Guid, string, …)` | | **القفل بسبب كلمات مرور خاطئة لا يُنهي الجلسات القائمة** عمدًا (تعليق الصنف) |
| `JwtTokenIssuer.Issue` | `(string Token, DateTimeOffset ExpiresAtUtc) Issue(Guid userId, Guid sessionId, string stampHash, DateTimeOffset now)` | | claims فقط: `sub`، `sid`، `sst`، `jti` — لا صلاحيات ولا بيانات شخصية |
| `ProtectedTokenUserStore.SetTokenAsync/GetTokenAsync` | | | يشفّر `AuthenticatorKey` و`RecoveryCodes` بـ Data Protection (`Onyx.Identity.UserTokens.v1`) |
| `IdentityBootstrapService.RecoverSuperAdminTwoFactorAsync` | `(string email, string reason, string operatorIdentity, …)` | | مسموح فقط إن لم يوجد SuperAdmin آخر نشط ومسجّل 2FA؛ يزيل كل آثار العامل الثاني، يُلغي الجلسات والتحديات، تدقيق `TwoFactorRecoveredByOperator` |

## 6. المستدعون والاعتماديات

- `AuthService` ← [AuthController](../../../src/Onyx.API/Controllers/AuthController.cs) فقط.
- `SessionManager.RevokeAllAsync` ← `AuthService` (كلمة المرور)، [UserAdministrationService](../../../src/Onyx.Infrastructure/Identity/UserAdministrationService.cs) (تعطيل، إعادة ضبط 2FA، إلغاء جلسات، فرض 2FA)، [RoleAdministrationService](../../../src/Onyx.Infrastructure/Identity/RoleAdministrationService.cs) (فرض 2FA على أعضاء دور)، `IdentityBootstrapService`، وحذف الحساب في [AccountPrivacyStore](../../../src/Onyx.Infrastructure/Identity/AccountPrivacyStore.cs) (`ReasonDataSubjectErasure`).
- `ISessionValidator` ← حدث `OnTokenValidated` في `ConfigureJwtBearer`.
- يعتمد على: `IAuditTrail`، `IEmailOutbox` (قوالب `InvitationEmailTemplate`، `PasswordResetEmailTemplate`)، `INotificationService`، `ICaptchaVerifier`، `AdminLinkBuilder` (يبني روابط على `IdentitySecurity:AdminPortalBaseUrl`).

## 7. قاعدة البيانات

`users` (حقول إضافية في `ApplicationUser`: `DisplayName`، `PreferredLanguage`، `IsActive`، `LastLoginAtUtc`، `LastTotpTimeStep`)، `user_tokens` (المفاتيح المشفّرة وربط التسجيل)، `user_sessions`، `refresh_tokens` (التجزئة + `sealed_successor`)، `login_challenges` (التجزئة)، `data_protection_keys` (حلقة مفاتيح الـ API عبر `DbXmlRepository`). انظر [DATABASE-AR.md §3.2](../DATABASE-AR.md).

## 8. الإعدادات والأمان والخصوصية

| القسم | المفاتيح المهمة |
|---|---|
| `Jwt` | `Issuer`، `Audience`، `SigningKey` (سر، Base64 ≥ 32 بايت)، `AccessTokenMinutes` (10)، `ClockSkewSeconds` (30) |
| `Sessions` | `IdleTimeoutMinutes` 60، `AbsoluteLifetimeHours` 12، `RefreshGraceSeconds` 30، `MaxGraceRedemptions` 3، `TwoFactorChallengeMinutes` 5، `EnrollmentChallengeMinutes` 15، `MaxChallengeAttempts` 5، `ValidationCacheSeconds` 15، `PermissionCacheSeconds` 60 |
| `IdentitySecurity` | `RequireTwoFactorForAllUsers`، `AdminPortalBaseUrl`، `InvitationLifetimeHours` 72، `PasswordResetLifetimeMinutes` 60 |
| سياسة كلمة المرور (`AddIdentityCore`) | طول ≥ 12، 4 أحرف فريدة على الأقل، بلا اشتراط أنواع أحرف؛ قفل بعد 5 محاولات لمدة 15 دقيقة |
| الكوكي في Web | `__Host-onyx.admin`، HttpOnly، Secure، SameSite=Strict، Path=/، جلسة متصفح (`IsPersistent = false`)، بلا تمديد منزلق، الانتهاء = الحد المطلق للجلسة |
| مفاتيح Web Data Protection | `DataProtection:KeysDirectory` (اسم التطبيق `Onyx.Web`) — يجب أن تكون دائمة ومنسوخة احتياطيًا |

- `UserSession.IpAddress` بيانات شخصية تخضع للاحتفاظ.
- لا تُسجَّل الرموز أبدًا؛ اختبار `No_secret_uri_QR_OTP_or_recovery_code_reaches_the_logs` يتحقق من ذلك في Web.

## 9. الاختبارات

| الملف | ما يتحقق منه |
|---|---|
| [AuthenticationTests](../../../tests/Onyx.API.IntegrationTests/Identity/AuthenticationTests.cs) | خطأ 401 موحد، القفل بعد 5، إلزام التسجيل للـ SuperAdmin والصلاحيات المميزة، منع إعادة رمز TOTP، رموز الاسترداد مرة واحدة، تشفير المفاتيح |
| [SessionTokenTests](../../../tests/Onyx.API.IntegrationTests/Identity/SessionTokenTests.cs) | التدوير ومهلة السماح، كشف الإعادة، الخمول والحد المطلق، 10 دقائق للوصول، الخروج، التعطيل، التجزئة فقط، رفض الرموز المزورة |
| [EnrollmentBindingTests](../../../tests/Onyx.API.IntegrationTests/Identity/EnrollmentBindingTests.cs) | ربط المفتاح بالتحدي ورفض المفاتيح القديمة |
| [PasswordAndInvitationTests](../../../tests/Onyx.API.IntegrationTests/Identity/PasswordAndInvitationTests.cs) | عدم كشف وجود الحساب، رابط لمرة واحدة، إنهاء الجلسات |
| [OperatorTwoFactorRecoveryTests](../../../tests/Onyx.API.IntegrationTests/Identity/OperatorTwoFactorRecoveryTests.cs) / [BootstrapTests](../../../tests/Onyx.API.IntegrationTests/Identity/BootstrapTests.cs) | أوامر المشغّل |
| [IdentityPrimitivesTests](../../../tests/Onyx.API.IntegrationTests/Identity/IdentityPrimitivesTests.cs) | متجهات RFC 6238 وRFC 4648، الختم، الرموز العشوائية |
| [AdminSignInTurnstileApiTests](../../../tests/Onyx.API.IntegrationTests/Leads/AdminSignInTurnstileApiTests.cs) | Turnstile للدخول وإعادة التعيين |
| [AdminAuthenticationTests](../../../tests/Onyx.Web.Tests/AdminAuthenticationTests.cs)، [AuthenticatorQrCodeTests](../../../tests/Onyx.Web.Tests/AuthenticatorQrCodeTests.cs)، [TokenLinkTests](../../../tests/Onyx.Web.Tests/TokenLinkTests.cs) | الكوكي المحصّن، التحديث من الخادم، الإلغاء، QR، روابط الرموز |

## 10. خريطة أثر التغيير

| إذا غيّرت… | راجع أيضًا |
|---|---|
| claims رمز الوصول في `JwtTokenIssuer`/`OnyxClaimTypes` | `ConfigureJwtBearer.OnTokenValidated`، `HttpCurrentUser.UserId` (يقرأ `sub`)، `SessionValidator`، `SessionTokenTests` |
| `SessionOptions` (الخمول/الحد/السماح) | `BffCookieEvents` و`BffDefaults.RefreshMargin` في Web، `SessionTokenTests`، [SECURITY-PRIVACY-AR.md](../SECURITY-PRIVACY-AR.md) |
| `TwoFactorPolicy.PrivilegedPermissions` | `AuthService.LoginAsync` (خطوة 6)، `UserAdministrationService.EnforceTwoFactorRequirementAsync`، `RoleAdministrationService.EnforceTwoFactorRequirementForMembersAsync`، `IdentityPolicyTests`، `AdministrationGuardTests.Granting_a_privileged_permission_forces_authenticator_enrolment` |
| رسائل الخطأ في `AuthService` | يجب أن تبقى موحدة (منع تعداد الحسابات)؛ `AuthenticationTests.Wrong_password_and_unknown_account_return_the_same_generic_401` |
| اسم/خصائص كوكي BFF | `BffDefaults`، `WebServiceCollectionExtensions` (AddCookie)، حجم الكوكي < 4 KB، `AdminAuthenticationTests` |
| `ProtectedTokenUserStore` أو اسم الحامي | فك تشفير المفاتيح المخزنة سيفشل لكل المستخدمين — يتطلب ترحيل بيانات |

## 11. أمثلة عملية

- **لتغيير مدة رمز الوصول:** ابدأ من `Jwt:AccessTokenMinutes` ([JwtOptions](../../../src/Onyx.Infrastructure/Identity/JwtOptions.cs)) — المدى 1–30. ثم راجع `BffDefaults.RefreshMargin` (يجب أن يبقى أقل من العمر)، و`SessionTokenTests.Access_tokens_expire_after_ten_minutes`.
- **لإضافة خطوة تحقق عند الدخول:** ابدأ من `AuthService.LoginAsync` قبل `CompleteSignInAsync`. حافظ على `timingEqualizer.Burn` في مسارات الفشل، وأضف قيمة جديدة لـ `LoginStatus` في Contracts ومعالجتها في `AccountController.Login`.
- **لفرض 2FA على الجميع:** اضبط `IdentitySecurity:RequireTwoFactorForAllUsers = true` — لا حاجة لتغيير كود.

## 12. الحالة

- **منفّذ:** كل ما سبق.
- **غير منفّذ في واجهة الإدارة (Web):** إدارة المستخدمين والأدوار وعرض سجل التدقيق واتصالات المزوّدين العامة متاحة **عبر الـ API فقط**؛ لا توجد متحكمات Admin لها في Onyx.Web (قائمة [AdminNavigation](../../../src/Onyx.Web/Areas/Admin/Navigation/AdminNavigation.cs) لا تحتويها).
- **خارج النطاق:** تسجيل عام، دخول اجتماعي، WebAuthn.
