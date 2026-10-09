# 02 — الأدوار والصلاحيات والإشعارات حسب الصلاحية

## 1. ماذا تفعل الميزة

التفويض قائم على **الصلاحيات** وليس أسماء الأدوار. الدور حزمة صلاحيات. الصلاحيات تُحسب على الخادم في كل طلب (مع ذاكرة مؤقتة قصيرة) ولا توجد في رمز JWT. الاستثناء الوحيد لفحص اسم دور: حماية `SuperAdmin`.

## 2. الملفات والأصناف

| المسار | الدور |
|---|---|
| [Permissions.cs](../../../src/Onyx.Contracts/Authorization/Permissions.cs) | **الكتالوج الوحيد** للصلاحيات. القيم مخزنة في القاعدة: أضف ولا تُعِد التسمية. `Permissions.All` يكتشف الثوابت بالانعكاس |
| [SystemRoles.cs](../../../src/Onyx.Application/Identity/SystemRoles.cs) | الأدوار المزروعة وحزمها الافتراضية |
| [PermissionGrantPolicy.cs](../../../src/Onyx.Application/Identity/PermissionGrantPolicy.cs) | حارس تصعيد الامتيازات |
| [TwoFactorPolicy.cs](../../../src/Onyx.Application/Identity/TwoFactorPolicy.cs) | الصلاحيات المميزة التي تفرض 2FA |
| [PermissionService.cs](../../../src/Onyx.Infrastructure/Identity/PermissionService.cs) | `IPermissionService` — حساب الصلاحيات الفعلية + ذاكرة مؤقتة |
| [RoleCatalog.cs](../../../src/Onyx.Infrastructure/Identity/RoleCatalog.cs) | استعلامات الأدوار وعدّ SuperAdmins النشطين |
| [UserAdministrationService.cs](../../../src/Onyx.Infrastructure/Identity/UserAdministrationService.cs)، [RoleAdministrationService.cs](../../../src/Onyx.Infrastructure/Identity/RoleAdministrationService.cs) | إدارة المستخدمين والأدوار |
| [src/Onyx.API/Authorization/](../../../src/Onyx.API/Authorization/) | `HasPermissionAttribute`، `PermissionPolicyProvider`، `PermissionAuthorizationHandler`، `AuthenticatedEndpointRequirement` |
| [CurrentAdmin.cs](../../../src/Onyx.Application/Identity/CurrentAdmin.cs) | `ICurrentAdmin` للخدمات التي تحتاج صلاحيات الفاعل (مثل الإشعارات، نطاق أنواع المحتوى) |
| [NotificationService.cs](../../../src/Onyx.Application/Notifications/NotificationService.cs) | إشعارات لا يراها إلا من يحمل `RequiredPermission` |
| Web: [CurrentAdminContext.cs](../../../src/Onyx.Web/Authentication/CurrentAdminContext.cs)، [AdminNavigation.cs](../../../src/Onyx.Web/Areas/Admin/Navigation/AdminNavigation.cs) | **إخفاء** عناصر الواجهة فقط |

## 3. الأدوار المزروعة (`SystemRoles.DefaultBundles`)

| الدور | المحتوى |
|---|---|
| `SuperAdmin` | ثابت، `IsSystem = true`، يملك كل الكتالوج ضمنيًا (`PermissionService` يعيد `Permissions.All`)، لا يُعدّل ولا يُحذف |
| `ContentManager` | كل صلاحيات المجموعات: Pillars، Services، Industries، Projects، CaseStudies، Testimonials، Integrations، Insights، ContentEngine، News، NewsSources، Glossary، Pages، Ctas، Media، Seo، Redirects، Offices، EmailTemplates، Content، Competitors — **ما عدا** `Content.DeletePermanent` — + `Markets.View`، `BrandKit.View`، `Knowledge.View`، `Knowledge.Edit` |
| `Marketing` | Analytics.View، SearchPerformance.View، Seo.Edit، Ctas.View/Create/Edit، ContentEngine.View/Review، Insights.View/Create/Edit، Pages/Services/Industries/Projects/News.View، Content.View/Create/Edit/SubmitForReview، BrandKit.View، Knowledge.View، Competitors.View |
| `Sales` | Inquiries.View، Leads.View/Manage، Bookings.View/Manage، Availability.Manage |

الزرع: `IdentityBootstrapService.SeedRolesAsync` (أمر `seed-identity`) — ينشئ الأدوار المفقودة فقط ولا يكتب فوق الموجودة. الحزم الافتراضية لا تتضمن صلاحيات مميزة (اختبار `Default_role_bundles_use_only_catalogue_permissions_and_avoid_privileged_ones`).

## 4. التدفق

```
[HasPermission(X)] على الإجراء/الصنف
 → PermissionPolicyProvider.GetPolicyAsync("Permission:X") → RequireAuthenticatedUser + PermissionRequirement(X)
 → PermissionAuthorizationHandler: ICurrentUser.UserId → IPermissionService.GetAsync(userId)
     ذاكرة مؤقتة "onyx:permissions:{userId}" لمدة Sessions:PermissionCacheSeconds (60)
     SuperAdmin ⇒ الكتالوج كاملًا؛ غيره ⇒ اتحاد role_permissions مع تجاهل القيم غير الموجودة في الكتالوج
 → 403 إن لم تُحمل
```

**نطاق نوع المحتوى (طبقة ثانية):** صلاحيات `Content.*` العامة تتطلب أيضًا صلاحية النوع (`{Type}.View` للقراءة و`{Type}.Edit` لأي تغيير) — تُفحص داخل `ContentAuthorizer` في Application (انظر [04-cms-content-AR.md](04-cms-content-AR.md)). لهذا يستطيع Marketing صياغة الرؤى فقط رغم امتلاكه `Content.Create/Edit`.

## 5. الدوال المفتاحية وحراس الامتيازات

| الدالة | السلوك |
|---|---|
| `PermissionGrantPolicy.CanActOn(actor, targetPermissions, targetInvolvesSuperAdmin)` | SuperAdmin → نعم دائمًا. غيره → فقط إن كان يحمل **كل** صلاحيات الهدف ولا يمس SuperAdmin |
| `UserAdministrationService.InviteAsync` | يرفض منح أدوار تحوي صلاحيات لا يملكها المدعو (`User.InsufficientPrivileges` 403)، بريد مكرر → `User.EmailTaken`؛ تدقيق `UserInvited`؛ بريد دعوة |
| `UserAdministrationService.SetRolesAsync` | يقيّم الاتحاد قبل/بعد؛ يمنع خفض آخر SuperAdmin نشط (`User.LastSuperAdmin` 409)؛ `permissions.Invalidate(userId)`؛ يفرض 2FA إن أصبح مطلوبًا (إلغاء الجلسات)؛ تدقيق `UserRolesChanged` |
| `DisableAsync` / `EnableAsync` | لا تعطيل لآخر SuperAdmin؛ التعطيل يُلغي كل الجلسات؛ لا يمكن تنفيذها على النفس (`User.CannotManageSelf`) |
| `ResetTwoFactorAsync` / `RevokeSessionsAsync` | إلغاء الجلسات + تدقيق `TwoFactorReset` / `SessionRevoked` |
| `RoleAdministrationService.CreateAsync/UpdateAsync/DeleteAsync` | اسم `SuperAdmin` محجوز، الدور النظامي للقراءة فقط (`Role.SystemRoleReadOnly`)، لا حذف لدور مستخدم (`Role.InUse`)، يجب أن يحمل الفاعل الصلاحيات المضافة والمحذوفة؛ `InvalidateRoleAsync` لكل الأعضاء؛ فرض 2FA للأعضاء؛ تدقيق `RoleCreated/Updated/Deleted` |
| `NotificationService.PublishAsync` | يرفض صلاحية غير موجودة في الكتالوج (`Notification.UnknownPermission`) لأن الخطأ الإملائي يجعل الإشعار غير مرئي |
| `NotificationService.MarkReadAsync` | يعيد NotFound لإشعار غير مرئي — لا يكشف وجوده |

## 6. الإشعارات حسب الصلاحية

| النوع (`NotificationType`) | الصلاحية المطلوبة | المصدر |
|---|---|---|
| `NewLead` | `Leads.View` | [InquirySubmittedHandler](../../../src/Onyx.Application/Leads/InquirySubmittedHandler.cs) |
| `NewBooking` / `BookingCancelled` | `Bookings.View` | [BookingConfirmedHandler](../../../src/Onyx.Application/Booking/BookingConfirmedHandler.cs) / [BookingCancelledHandler](../../../src/Onyx.Application/Booking/BookingCancelledHandler.cs) |
| `JobFailed`، `EmailDeliveryFailed`، `OutboxMessageFailed` | `System.Jobs.View` | `JobScheduler`، `EmailProcessor`، `OutboxProcessor` |
| `IntegrationFailed` | `ProviderConnections.Manage` | `ProviderConnectionFailedHandler` |
| `SecurityAlert` (إعادة استخدام رمز تحديث) | `Users.Manage` | `AuthService.RefreshAsync` |
| `NewsIngestionFailed` | `NewsSources.Manage` | [NewsSourceFailingHandler](../../../src/Onyx.Application/News/NewsSourceFailingHandler.cs) |
| `ContentPublishingFailed` | `Content.Publish` | [ContentSchedulingService](../../../src/Onyx.Application/Content/ContentSchedulingService.cs) |

`ArticleReadyForReview` و`ToolAttention` معرّفان في الـ enum لكن البحث في الكود لم يجد أي مُصدر لهما — قيم محجوزة لمراحل لاحقة (محرك المحتوى، الأدوات).

الرسالة لا تُخزّن نصًا: الواجهة تترجم `Type` + `Parameters` (≤ 10 معاملات، قيم ≤ 200 حرف). الروابط تُعاد تفويضها عند الفتح.

## 7. الاختبارات

| الملف | ما يتحقق منه |
|---|---|
| [AuthorizationTests](../../../tests/Onyx.API.IntegrationTests/Identity/AuthorizationTests.cs) | 401/403 لكل نقطة إدارية، **كل نقطة نهاية تعلن تفويضها صراحة**، كل صلاحية مستخدمة موجودة في الكتالوج، تغيّر الصلاحيات يسري دون إعادة دخول |
| [AdministrationGuardTests](../../../tests/Onyx.API.IntegrationTests/Identity/AdministrationGuardTests.cs) | حراس SuperAdmin والتصعيد وإلزام 2FA |
| [PermissionCatalogueTests](../../../tests/Onyx.Application.Tests/Authorization/PermissionCatalogueTests.cs)، [IdentityPolicyTests](../../../tests/Onyx.Application.Tests/Identity/IdentityPolicyTests.cs) | تفرد وتنسيق القيم، سياسة المنح و2FA |
| [NotificationVisibilityTests](../../../tests/Onyx.API.IntegrationTests/Worker/NotificationVisibilityTests.cs)، [NotificationServiceTests](../../../tests/Onyx.Application.Tests/Notifications/NotificationServiceTests.cs) | رؤية الإشعارات لكل صلاحية |
| [LocalizationAndHandlerUnitTests](../../../tests/Onyx.Web.Tests/LocalizationAndHandlerUnitTests.cs) (`Navigation_hides_items_the_admin_has_no_permission_for`) | إخفاء القائمة |

## 8. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| إضافة صلاحية | `Permissions.cs` (مجموعة + ثابت)، `SystemRoles.DefaultBundles` إن لزم، `TwoFactorPolicy` إن كانت مميزة، `AdminNavigation`، مفاتيح الترجمة في `SharedResource.*.resx` إن عُرضت، [API-ROUTES-AR.md](../API-ROUTES-AR.md) |
| إعادة تسمية صلاحية | **ممنوع** — القيم مخزنة في `role_permissions`؛ `PermissionService` سيتجاهل القيمة القديمة بصمت |
| تغيير `PermissionGrantPolicy` | `UserAdministrationService`، `RoleAdministrationService`، `AdministrationGuardTests`، `IdentityPolicyTests` |
| تغيير مدة ذاكرة الصلاحيات | `Sessions:PermissionCacheSeconds`؛ `AuthorizationTests.Permission_changes_apply_without_signing_in_again` |

## 9. مثال

**لإضافة صلاحية `Reports.View` لمسار جديد:** أضف `public static class Reports { public const string View = "Reports.View"; }` في `Permissions`، ضع `[HasPermission(Permissions.Reports.View)]` على المتحكم، أضفها لحزمة الدور المناسب في `SystemRoles` (لن تُطبّق على أدوار موجودة؛ الزرع لا يكتب فوقها)، أضف عنصر القائمة في `AdminNavigation`، واختبر 401/403.

## 10. الحالة

- **منفّذ:** كل ما سبق عبر الـ API.
- **بلا واجهة Admin في Web:** إدارة المستخدمين/الأدوار/الصلاحيات (الـ API فقط).
- **صلاحيات محجوزة بلا نقاط نهاية:** `Tools.*`، `ToolRuns.View`، `Markets.*`، `Offices.Manage`، `Settings.Manage`؛ `Testimonials.*` مستخدمة داخل خدمة عناصر الإثبات (انظر [08-proof-work-AR.md](08-proof-work-AR.md)).
