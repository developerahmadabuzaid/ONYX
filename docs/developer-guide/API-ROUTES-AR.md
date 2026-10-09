# فهرس مسارات واجهة البرمجة (API) — Onyx.API

> هذا الفهرس مستخرج آليًا من سمات `[Route]` و`[Http*]` و`[HasPermission]` و`[AllowAnonymous]` في ملفات المتحكمات، ثم رُوجع يدويًا مقابل جسم كل إجراء. المرجع الحاكم دائمًا هو الكود في [src/Onyx.API/Controllers/](../../src/Onyx.API/Controllers/).
> آخر تحقق: الالتزام `7271ad6` (Stage 12).

## 1. قواعد عامة تنطبق على كل المسارات

| القاعدة | التفاصيل | المصدر |
|---|---|---|
| البادئة والإصدار | كل المسارات تبدأ بـ `api/v{version:apiVersion}/…` ويُقرأ الإصدار من مقطع الرابط (`UrlSegmentApiVersionReader`). الإصدار الوحيد الحالي `1.0`، أي `/api/v1/...`. لا يوجد إصدار افتراضي ضمني (`AssumeDefaultVersionWhenUnspecified = false`). | [ApiServiceCollectionExtensions.cs](../../src/Onyx.API/Configuration/ApiServiceCollectionExtensions.cs) |
| الافتراض الآمن | سياسة احتياطية (`FallbackPolicy`) تفرض مستخدمًا مُصادَقًا على كل نقطة نهاية ما لم تحمل `[AllowAnonymous]`. الطلب الذي لا يطابق أي نقطة نهاية يمرّ ليأخذ 404 عاديًا. | [AuthenticatedEndpointRequirement.cs](../../src/Onyx.API/Authorization/AuthenticatedEndpointRequirement.cs) |
| الصلاحيات | `[HasPermission(X)]` تنشئ سياسة باسم `Permission:X` ديناميكيًا عبر `PermissionPolicyProvider`، ويتحقق `PermissionAuthorizationHandler` من الصلاحية بقراءتها من الخادم (`IPermissionService.GetAsync`) وليس من رمز JWT. سمة على مستوى الصنف + سمة على الإجراء = **كلتاهما** مطلوبتان. | [HasPermissionAttribute.cs](../../src/Onyx.API/Authorization/HasPermissionAttribute.cs)، [PermissionAuthorizationHandler.cs](../../src/Onyx.API/Authorization/PermissionAuthorizationHandler.cs) |
| المصادقة | Bearer JWT فقط (لا كوكيز في الـ API). المتصفح لا يستدعي الـ API مباشرة؛ Onyx.Web هو الوسيط (BFF). CORS غير مسجّل عمدًا. | [IdentityServiceCollectionExtensions.cs](../../src/Onyx.Infrastructure/IdentityServiceCollectionExtensions.cs) |
| تحديد المعدل | محدِّد عام لكل طلب (افتراضي 300/دقيقة لكل IP)، وسياسة `strict` (افتراضي 10/دقيقة) وسياسة `search` (افتراضي 30/دقيقة). المفتاح هو IP العميل بعد معالجة الترويسات المُمرَّرة من وكلاء موثوقين فقط. الرفض = 429 مع `Retry-After`. | [RateLimitingOptions.cs](../../src/Onyx.API/Configuration/RateLimitingOptions.cs) |
| حجم الجسم | الحد العام 1 ميغابايت (`ApiSecurity:MaxRequestBodySizeBytes`)؛ يمكن لنقطة نهاية رفعه بـ `[RequestSizeLimit]` حتى سقف مطلق 50 ميغابايت. التجاوز = 413. | [RequestBodySizeLimitMiddleware.cs](../../src/Onyx.API/Middleware/RequestBodySizeLimitMiddleware.cs) |
| شكل الأخطاء | كل خطأ متوقع (`Result` فاشل) يتحول إلى RFC 9457 ProblemDetails مع امتداد `code` ثابت (مثل `Content.InvalidTransition`). ربط الأنواع: Validation→400، Unauthorized→401، Forbidden→403، NotFound→404، Conflict→409، Unavailable→503، غير ذلك→500. | [ResultHttpExtensions.cs](../../src/Onyx.API/Http/ResultHttpExtensions.cs) |
| الاستثناءات غير المتوقعة | `GlobalExceptionHandler`: لا تصل رسالة الاستثناء ولا نوعه للعميل أبدًا. `ConcurrencyConflictException` → 409 مع `code = Concurrency.Conflict`. | [GlobalExceptionHandler.cs](../../src/Onyx.API/ErrorHandling/GlobalExceptionHandler.cs) |
| معرّف الترابط | `X-Correlation-ID` يُقبل إن طابق `^[A-Za-z0-9._-]{8,64}$` وإلا يُولَّد جديد، ويُعاد في الاستجابة ويُضاف لكل سجل ولسجل التدقيق. | [CorrelationIdMiddleware.cs](../../src/Onyx.API/Middleware/CorrelationIdMiddleware.cs) |
| ترويسات الأمان | CSP صارمة `default-src 'none'`، و`X-Frame-Options: DENY`، و`Referrer-Policy: no-referrer`… (Swagger مستثنى في Development فقط). | [SecurityHeadersMiddleware.cs](../../src/Onyx.API/Middleware/SecurityHeadersMiddleware.cs) |
| فحوص الصحة | `/health/live` و`/health/ready` (الأخير يفحص قاعدة البيانات) — مجهولة ومستثناة من تحديد المعدل. | [ApiApplicationBuilderExtensions.cs](../../src/Onyx.API/Configuration/ApiApplicationBuilderExtensions.cs) |
| OpenAPI/Swagger | في بيئة Development فقط (`/openapi/v1.json` و`/swagger`). | نفس الملف |

**قراءة الجداول:** عمود «الصلاحية» يجمع سمة الصنف + سمة الإجراء. `مجهول` = `[AllowAnonymous]`. `مُصادَق` = `[Authorize]` بلا صلاحية محددة. «الخدمة» هي الدالة التي يستدعيها الإجراء مباشرة.

---

## 2. المسارات العامة (مجهولة الهوية غالبًا)

### 2.1 المصادقة — [AuthController.cs](../../src/Onyx.API/Controllers/AuthController.cs) (`/api/v1/auth`، كل الاستجابات `no-store`)

| الطريقة | المسار | الصلاحية | معدل | الطلب | الخدمة (`IAuthService`) | ملاحظات |
|---|---|---|---|---|---|---|
| POST | `login` | مجهول | strict | `LoginRequest` | `LoginAsync` | يعيد رموزًا أو خطوة تحدٍّ (2FA/تسجيل مصادِق) |
| POST | `login/two-factor` | مجهول | strict | `TwoFactorLoginRequest` | `CompleteTwoFactorLoginAsync` | |
| POST | `two-factor/enrollment/start` | مجهول | strict | `TwoFactorEnrollmentStartRequest` | `StartTwoFactorEnrollmentAsync` | يتطلب رمز تحدٍّ من خطوة الدخول |
| POST | `two-factor/enrollment/complete` | مجهول | strict | `TwoFactorEnrollmentCompleteRequest` | `CompleteTwoFactorEnrollmentAsync` | |
| POST | `refresh` | مجهول | عام | `RefreshTokenRequest` | `RefreshAsync` | تدوير رمز التحديث مع كشف إعادة الاستخدام |
| POST | `logout` | مجهول | عام | `LogoutRequest` | `LogoutAsync` | |
| POST | `password/forgot` | مجهول | strict | `ForgotPasswordRequest` | `RequestPasswordResetAsync` | 202 دائمًا عند النجاح (لا يكشف وجود الحساب) |
| POST | `password/reset` | مجهول | strict | `ResetPasswordRequest` | `ResetPasswordAsync` | 204 |
| POST | `invitations/accept` | مجهول | strict | `AcceptInvitationRequest` | `AcceptInvitationAsync` | 204 |
| POST | `password/change` | مُصادَق | strict | `ChangePasswordRequest` | `ChangePasswordAsync` | 204 |
| GET | `me` | مُصادَق | عام | — | `GetCurrentUserAsync` | الملف الشخصي + الصلاحيات المحسوبة على الخادم |

### 2.2 المحتوى العام — [ContentController.cs](../../src/Onyx.API/Controllers/ContentController.cs) (`/api/v1/content`)

| الطريقة | المسار | الصلاحية | الطلب | الخدمة | ملاحظات |
|---|---|---|---|---|---|
| GET | `resolve?language&path` | مجهول | — | `IPublicContentService.ResolveAsync` | يعيد صفحة/تحويلًا/410/404 |
| GET | `sitemap?language` | مجهول | — | `IPublicContentService.GetSitemapAsync` | |
| GET | `listing?language&section&topic&page` | مجهول | — | `IPublicContentService.ListAsync` | |
| GET | `featured-projects?language` | مجهول | — | `IPublicContentService.GetFeaturedProjectsAsync` | حد أقصى 3 |
| GET | `preview?token` | مجهول | strict | `IContentPreviewService` | `Cache-Control: no-store` |

### 2.3 بقية المسارات العامة

| المتحكم | الطريقة | المسار | الصلاحية/المعدل | الطلب | الخدمة | ملاحظات |
|---|---|---|---|---|---|---|
| [InquiriesController](../../src/Onyx.API/Controllers/InquiriesController.cs) | POST | `/api/v1/inquiries` | مجهول، strict | `SubmitInquiryRequest` | `IInquiryService.SubmitAsync` | 201 + `InquiryAcceptedResponse`؛ Turnstile إلزامي |
| [ConsultationsController](../../src/Onyx.API/Controllers/ConsultationsController.cs) | GET | `/api/v1/consultations/availability` | مجهول، strict (على الصنف) | — | `IBookingService.GetAvailabilityAsync` | |
| | POST | `/api/v1/consultations` | مجهول، strict | `BookConsultationRequest` | `IBookingService.BookAsync` | 201 + `BookingConfirmedResponse` |
| | POST | `/api/v1/consultations/manage` | مجهول، strict | `ManageBookingRequest` | `GetManagedAsync` | الرمز الموقّع في الجسم وليس في الرابط |
| | POST | `/api/v1/consultations/manage/cancel` | مجهول، strict | `ManageBookingRequest` | `CancelAsync` | |
| | POST | `/api/v1/consultations/manage/reschedule` | مجهول، strict | `RescheduleBookingRequest` | `RescheduleAsync` | |
| | POST | `/api/v1/consultations/manage/calendar` | مجهول، strict | `ManageBookingRequest` | `GetCalendarFileAsync` | ملف ‎.ics |
| [ConsentsController](../../src/Onyx.API/Controllers/ConsentsController.cs) | POST | `/api/v1/consents` | مجهول، strict | `RecordCookieConsentRequest` | `ICookieConsentService.RecordAsync` | 202 + `AcceptedResponse` |
| [MediaController](../../src/Onyx.API/Controllers/MediaController.cs) | GET | `/api/v1/media/{id:guid}?previewToken` | مجهول | — | `IMediaService.OpenPublicAsync` | ملف مع ETag = SHA-256 |
| [NewsController](../../src/Onyx.API/Controllers/NewsController.cs) | GET | `/api/v1/news?language&page` | مجهول | — | `PublicNewsService.GetHubAsync` | |
| | GET | `/api/v1/news/resolve?language&slug&page` | مجهول | — | `PublicNewsService.ResolveAsync` | |
| | GET | `/api/v1/news/latest?language&count` | مجهول | — | `PublicNewsService.GetLatestAsync` | |
| | GET | `/api/v1/news/sitemap?language` | مجهول | — | `PublicNewsService.GetSitemapAsync` | |
| [OrganizationController](../../src/Onyx.API/Controllers/OrganizationController.cs) | GET | `/api/v1/organization` | مجهول | — | `IOrganizationProfileService.GetVerifiedAsync` | 404 إن لم تكن البيانات موثقة/مفعّلة |
| [SearchController](../../src/Onyx.API/Controllers/SearchController.cs) | GET | `/api/v1/search?language&q` | مجهول، search، `no-store` | — | `SiteSearchQueryService.SearchAsync` | |
| [SearchEngineVerificationController](../../src/Onyx.API/Controllers/SearchEngineVerificationController.cs) | GET | `/api/v1/search-engine-verification/indexnow-key` | مجهول | — | `ISearchEngineNotifier.VerificationKey` | نص عادي أو 404 إن لم يُضبط |

---

## 3. مسارات الإدارة (`/api/v1/admin/...`)

> كل هذه المسارات تتطلب مستخدمًا مُصادَقًا + الصلاحية المذكورة. إخفاء الأزرار في Onyx.Web ليس حماية؛ الـ API هي نقطة الإنفاذ الوحيدة.

### 3.1 الهوية والمستخدمون والأدوار

| المتحكم | الطريقة | المسار | الصلاحية | الطلب | الخدمة |
|---|---|---|---|---|---|
| [AdminUsersController](../../src/Onyx.API/Controllers/Admin/AdminUsersController.cs) `admin/users` | GET | `?search&page&pageSize` | `Users.Manage` | — | `IUserAdministrationService.ListAsync` |
| | GET | `{userId}` | `Users.Manage` | — | `GetAsync` |
| | POST | `` | `Users.Manage` | `InviteUserRequest` | `InviteAsync` (201) |
| | POST | `{userId}/invitation` | `Users.Manage` | — | `ResendInvitationAsync` |
| | PUT | `{userId}` | `Users.Manage` | `UpdateUserRequest` | `UpdateAsync` |
| | PUT | `{userId}/roles` | `Users.Manage` | `SetUserRolesRequest` | `SetRolesAsync` |
| | POST | `{userId}/disable` / `{userId}/enable` | `Users.Manage` | — | `DisableAsync` / `EnableAsync` |
| | POST | `{userId}/two-factor/reset` | `Users.Manage` | — | `ResetTwoFactorAsync` |
| | POST | `{userId}/sessions/revoke` | `Users.Manage` | — | `RevokeSessionsAsync` |
| [AdminRolesController](../../src/Onyx.API/Controllers/Admin/AdminRolesController.cs) `admin/roles` | GET/POST | `` | `Roles.Manage` | `SaveRoleRequest` | `IRoleAdministrationService.ListAsync/CreateAsync` |
| | PUT/DELETE | `{roleId}` | `Roles.Manage` | `SaveRoleRequest` | `UpdateAsync/DeleteAsync` |
| [AdminPermissionsController](../../src/Onyx.API/Controllers/Admin/AdminPermissionsController.cs) `admin/permissions` | GET | `` | `Roles.Manage` | — | يقرأ `Permissions.All` مباشرة ويجمعها حسب البادئة |
| [AdminAuditLogsController](../../src/Onyx.API/Controllers/Admin/AdminAuditLogsController.cs) `admin/audit-logs` | GET | `?…` (`AuditLogQuery`) | `AuditLogs.View` | — | `IAuditLogQueries.SearchAsync` |
| [AdminNotificationsController](../../src/Onyx.API/Controllers/Admin/AdminNotificationsController.cs) `admin/notifications` | GET | `?unreadOnly&page&pageSize` | مُصادَق | — | `INotificationService.ListAsync` (حسب صلاحيات المشاهد) |
| | GET | `unread-count` | مُصادَق | — | `CountUnreadAsync` |
| | POST | `{notificationId}/read` | مُصادَق | — | `MarkReadAsync` (404 للإشعار غير المرئي) |
| | POST | `read-all` | مُصادَق | — | `MarkAllReadAsync` |
| [AdminProviderConnectionsController](../../src/Onyx.API/Controllers/Admin/AdminProviderConnectionsController.cs) `admin/provider-connections` | GET | `` | `ProviderConnections.Manage` | — | `IProviderConnectionAdministrationService.ListAsync` |
| | POST | `{providerKey}/disable` / `{providerKey}/enable` | نفسها | — | `DisableAsync` / `EnableAsync` |
| | POST | `email/test` | نفسها | — | `QueueEmailTestAsync` (202 + معرّف الرسالة) |

### 3.2 المحتوى (CMS) — [AdminContentController.cs](../../src/Onyx.API/Controllers/Admin/AdminContentController.cs) (`admin/content`)

`{lang}` = `{languageCode:length(2)}`. التحقق من نطاق النوع (`{Type}.View/Edit`) يتم داخل الخدمة عبر `ContentAuthorizer` بالإضافة إلى الصلاحية المذكورة هنا.

| الطريقة | المسار | الصلاحية | الطلب | الخدمة |
|---|---|---|---|---|
| GET | `?type&status&isLive&needsUrlDecision&parentId&search&page&pageSize` | `Content.View` | — | `IContentAdministrationService.ListAsync` |
| POST | `` | `Content.Create` | `CreateContentRequest` | `CreateAsync` |
| GET | `{id}` | `Content.View` | — | `GetAsync` |
| PUT | `{id}/translations/{lang}` | `Content.Edit` | `UpdateContentTranslationRequest` | `UpdateTranslationAsync` |
| PUT | `{id}/translations/{lang}/seo` | `Seo.Edit` | `UpdateContentSeoRequest` | `UpdateSeoAsync` |
| PUT | `{id}/settings` | `Content.Edit` | `UpdateContentSettingsRequest` | `UpdateSettingsAsync` |
| POST | `{id}/delete` | `Content.DeletePermanent` | `DeleteContentRequest` | `DeletePermanentAsync` (204) |
| POST | `{id}/submit` | `Content.SubmitForReview` | `ContentVersionRequest` | `IContentWorkflowService.SubmitForReviewAsync` |
| POST | `{id}/approve` | `Content.Approve` | `ContentVersionRequest` | `ApproveAsync` |
| POST | `{id}/reject` | `Content.Approve` | `RejectContentRequest` | `RejectAsync` |
| POST | `{id}/publish` | `Content.Publish` | `ContentVersionRequest` | `PublishAsync` |
| POST | `{id}/schedule` | `Content.Publish` | `ScheduleContentRequest` | `ScheduleAsync` |
| POST | `{id}/schedule/cancel` | `Content.Publish` | `ContentVersionRequest` | `CancelScheduleAsync` |
| POST | `{id}/unpublish-schedule` | `Content.Publish` | `ScheduleUnpublishRequest` | `ScheduleUnpublishAsync` |
| POST | `{id}/unpublish-schedule/cancel` | `Content.Publish` | `ContentVersionRequest` | `CancelUnpublishScheduleAsync` |
| POST | `{id}/unpublish` | `Content.Publish` | `TakeOfflineRequest` | `UnpublishAsync` |
| POST | `{id}/archive` | `Content.Archive` | `TakeOfflineRequest` | `ArchiveAsync` |
| POST | `{id}/restore` | `Content.Archive` | `ContentVersionRequest` | `RestoreFromArchiveAsync` |
| POST | `{id}/url-resolution` | `Content.Publish` | `TakeOfflineRequest` | `ResolveOfflineAsync` |
| GET | `{id}/quality` | `Content.View` | — | `GetQualityReportAsync` |
| GET | `{id}/publishing-time` | `Content.View` | — | `RecommendPublishingTimeAsync` |
| GET | `{id}/revisions` | `Content.View` | — | `IContentRevisionService.ListAsync` |
| GET | `{id}/revisions/{number}` | `Content.View` | — | `GetAsync` |
| GET | `{id}/revisions/compare?from&to` | `Content.View` | — | `CompareAsync` |
| POST | `{id}/revisions/{number}/restore` | `Content.Edit` | `RestoreRevisionRequest` | `RestoreAsync` |
| POST | `{id}/preview-links` | `Content.View` | `CreatePreviewLinkRequest` | `IContentPreviewService` (`no-store`) |
| GET/POST | `{id}/relations` | `Content.View` / `Content.Edit` | `AddContentRelationRequest` | `ListRelationsAsync` / `AddRelationAsync` |
| DELETE | `{id}/relations/{relationId}` | `Content.Edit` | — | `RemoveRelationAsync` |
| GET | `{id}/related-suggestions` | `Content.View` | — | `SuggestRelatedAsync` |
| GET | `{id}/intelligence/{lang}` | `Content.View` | — | `GetContentProfileAsync` (خدمة الذكاء التحريري) |

**إثبات المحتوى** — [AdminContentProofController.cs](../../src/Onyx.API/Controllers/Admin/AdminContentProofController.cs) (`admin/content/{id}`):

| الطريقة | المسار | الصلاحية | الطلب | الخدمة (`IContentProofService`) |
|---|---|---|---|---|
| POST | `proof-verification` | `Proof.Verify` | `VerifyProofRequest` | `VerifyAsync` |
| POST | `proof-verification/revoke` | `Proof.Verify` | `ContentVersionRequest` | `RevokeAsync` |
| POST | `featured` | `Projects.Publish` | `SetFeaturedRequest` | `SetFeaturedAsync` |

### 3.3 موارد تحريرية مساندة

| المتحكم | الطريقة | المسار | الصلاحية | الخدمة |
|---|---|---|---|---|
| [AdminRedirectsController](../../src/Onyx.API/Controllers/Admin/AdminRedirectsController.cs) `admin/redirects` | GET | `?language&search&page&pageSize` | `Redirects.Manage` | `IRedirectAdministrationService.ListAsync` |
| | POST / PUT `{id}` / DELETE `{id}` | | `Redirects.Manage` | `CreateAsync` / `UpdateAsync` / `DeleteAsync` (`SaveRedirectRequest`) |
| [AdminCtasController](../../src/Onyx.API/Controllers/Admin/AdminCtasController.cs) `admin/ctas` | GET | `` | `Ctas.View` | `ICallToActionService.ListAsync` |
| | POST | `` | `Ctas.Create` | `CreateAsync` (`SaveCtaRequest`) |
| | PUT | `{id}` | `Ctas.Edit` | `UpdateAsync` |
| | GET | `defaults` | `Ctas.View` | `GetDefaultsAsync` |
| | PUT | `defaults` | `Ctas.Edit` | `SetDefaultAsync` (`SetCtaDefaultRequest`) |
| [AdminMediaController](../../src/Onyx.API/Controllers/Admin/AdminMediaController.cs) `admin/media` | GET | `?search&page&pageSize` | `Media.View` | `IMediaService.ListAsync` |
| | POST | `` (ملف `multipart`) | `Media.Upload`، `[RequestSizeLimit(11 MB)]` | `UploadAsync` |
| | GET | `{id}` / `{id}/usages` | `Media.View` | `GetAsync` / `ListUsagesAsync` |
| | PUT | `{id}/texts` | `Media.Edit` | `UpdateTextsAsync` |
| | DELETE | `{id}` | `Media.Delete` | `DeleteAsync` |
| | GET | `{id}/content` | `Media.View` | `OpenAdminAsync` (ملف) |
| [AdminAuthorsController](../../src/Onyx.API/Controllers/Admin/AdminAuthorsController.cs) `admin/authors` | GET | `` | `Insights.View` | `IAuthorService.ListAsync` |
| | POST / PUT `{id}` / DELETE `{id}` | | `Insights.Edit` | `CreateAsync` / `UpdateAsync` / `DeleteAsync` |
| [AdminProofItemsController](../../src/Onyx.API/Controllers/Admin/AdminProofItemsController.cs) `admin/proof-items` | GET | `?contentItemId` | `Content.View` | `IProofItemService.ListAsync` |
| | POST / PUT `{id}` | | `Content.Edit` | `CreateAsync` / `UpdateAsync` (`SaveProofItemRequest`) |
| | POST | `{id}/verify` | `Proof.Verify` | `VerifyAsync` |
| | POST | `{id}/publish` / `{id}/unpublish` | `Content.View`* | `PublishAsync` / `UnpublishAsync` |
| | DELETE | `{id}` | `Content.Edit` | `DeleteAsync` |
| [AdminBrandKitController](../../src/Onyx.API/Controllers/Admin/AdminBrandKitController.cs) `admin/brand-kit` | GET / PUT | `` | `BrandKit.View` / `BrandKit.Manage` | `IBrandKitService.GetAsync` / `UpdateAsync` |
| [AdminEmailTemplatesController](../../src/Onyx.API/Controllers/Admin/AdminEmailTemplatesController.cs) `admin/email-templates` | GET | `` | `EmailTemplates.Manage` | `IEmailTemplateAdministrationService.ListAsync` |
| | PUT / DELETE | `{templateKey}/{languageCode:length(2)}` | `EmailTemplates.Manage` | `SaveCopyAsync` / `ResetCopyAsync` |

\* `publish/unpublish` لعناصر الإثبات محمية على مستوى المتحكم بـ `Content.View` فقط؛ الصلاحية الفعلية للنشر تتحقق داخل `ProofItemService` (انظر [features/08-proof-work-AR.md](features/08-proof-work-AR.md)).

### 3.4 المعرفة والذكاء التحريري (Knowledge / Intelligence)

| المتحكم | الطريقة | المسار | الصلاحية | الخدمة |
|---|---|---|---|---|
| [AdminKnowledgeController](../../src/Onyx.API/Controllers/Admin/AdminKnowledgeController.cs) `admin/knowledge` | GET | `?category&status&search` | `Knowledge.View` | `IKnowledgeService.ListAsync` |
| | GET | `{id}` | `Knowledge.View` | `GetAsync` |
| | POST / PUT `{id}` | | `Knowledge.Edit` | `CreateAsync` / `UpdateAsync` (`SaveKnowledgeEntryRequest`) |
| | POST | `{id}/approve` / `{id}/retire` | `Knowledge.Approve` | `ApproveAsync` / `RetireAsync` |
| | GET | `governance-rules` | `Knowledge.View` | `GetGovernanceRules` (متزامن) |
| | GET / POST | `information-requests` | `Knowledge.View` | `ListRequestsAsync` / `RaiseRequestAsync` |
| | POST | `information-requests/{id}/resolve` | `Knowledge.Approve` | `ResolveRequestAsync` |
| [AdminKeywordsController](../../src/Onyx.API/Controllers/Admin/AdminKeywordsController.cs) `admin/keywords` | GET | `?language&status&search&page&pageSize` | `Content.View` | `IKeywordMapService.ListAsync` |
| | GET | `conflicts?language&keyword&excludeContentItemId` | `Content.View` | `CheckConflictsAsync` |
| | POST / PUT `{id}` / DELETE `{id}` | | `Seo.Edit` | `CreateAsync` / `UpdateAsync` / `DeleteAsync` |
| [AdminClustersController](../../src/Onyx.API/Controllers/Admin/AdminClustersController.cs) `admin/clusters` | GET | `` | `Content.View` | `IContentClusterService.ListAsync` |
| | POST / PUT `{id}` / DELETE `{id}` | | `Seo.Edit` | `CreateAsync` / `UpdateAsync` / `DeleteAsync` |
| | PUT | `{id}/members` | `Seo.Edit` | `SetMemberAsync` |
| | DELETE | `{id}/members/{contentItemId}` | `Seo.Edit` | `RemoveMemberAsync` |
| [AdminCompetitorsController](../../src/Onyx.API/Controllers/Admin/AdminCompetitorsController.cs) `admin/competitors` | GET | `` | `Competitors.View` | `ICompetitorService.ListAsync` |
| | POST / PUT `{id}` / DELETE `{id}` | | `Competitors.Manage` | `CreateAsync` / `UpdateAsync` / `DeleteAsync` |
| [AdminSeoRecommendationsController](../../src/Onyx.API/Controllers/Admin/AdminSeoRecommendationsController.cs) `admin/seo-recommendations` | GET | `?status&contentItemId&kind&page&pageSize` | `Content.View` | `ISeoRecommendationService.ListAsync` |
| | GET | `{id}` | `Content.View` | `GetAsync` |
| | POST | `` | `Seo.Edit` | `ProposeAsync` |
| | POST | `{id}/approve` / `{id}/reject` | `Seo.Approve` | `ApproveAsync` / `RejectAsync` |
| | POST | `{id}/apply` / `{id}/mark-applied` | `Seo.Edit` | `ApplyAsync` / `MarkAppliedAsync` |
| [AdminIntelligenceController](../../src/Onyx.API/Controllers/Admin/AdminIntelligenceController.cs) `admin/intelligence` | GET | `capabilities` | `Content.View` | `IIntelligenceStatusService.GetCapabilitiesAsync` |
| | GET | `link-report` | `Content.View` | `IInternalLinkAnalyzer.AnalyzeAsync` |
| | POST | `link-report/recommendations` | `Seo.Edit` | `GenerateRecommendationsAsync` |
| [AdminContentCalendarController](../../src/Onyx.API/Controllers/Admin/AdminContentCalendarController.cs) `admin/content-calendar` | GET | `?status&page&pageSize` | `ContentEngine.View` | `IContentCalendarService.ListAsync` |
| | POST | `` | `ContentEngine.Review` | `ProposeAsync` |
| | POST | `{id}/accept` / `{id}/reject` | `ContentEngine.Approve` | `AcceptAsync` / `RejectAsync` |
| | POST | `{id}/link` / `{id}/complete` | `ContentEngine.Review` | `LinkContentAsync` / `CompleteAsync` |

### 3.5 العملاء المحتملون والحجز

| المتحكم | الطريقة | المسار | الصلاحية | الخدمة |
|---|---|---|---|---|
| [AdminInquiriesController](../../src/Onyx.API/Controllers/Admin/AdminInquiriesController.cs) `admin/inquiries` | GET | `?…` (`InquiryListQuery`) | `Inquiries.View` | `ILeadAdministrationService.ListInquiriesAsync` |
| [AdminLeadsController](../../src/Onyx.API/Controllers/Admin/AdminLeadsController.cs) `admin/leads` | GET | `?…` (`LeadListQuery`) | `Leads.View` | `ListAsync` |
| | GET | `{id}` | `Leads.View` | `GetAsync` |
| | PUT | `{id}/stage` | `Leads.Manage` | `ChangeStageAsync` |
| | PUT | `{id}/priority` | `Leads.Manage` | `ChangePriorityAsync` |
| | PUT | `{id}/owner` | `Leads.Assign` | `AssignAsync` |
| | POST | `{id}/notes` | `Leads.Manage` | `AddNoteAsync` |
| | POST | `{id}/consents/{consentId}/withdraw-marketing` | `Leads.Manage` | `WithdrawMarketingConsentAsync` (204) |
| | GET | `assignees` | `Leads.Assign` | `ListAssigneesAsync` |
| | GET | `export?…` | `Leads.Export` | `ExportAsync` (ملف CSV) |
| [AdminBookingsController](../../src/Onyx.API/Controllers/Admin/AdminBookingsController.cs) `admin/bookings` | GET | `?…` (`BookingListQuery`) / `{id}` | `Bookings.View` | `IBookingAdministrationService.ListAsync` / `GetAsync` |
| | POST | `{id}/cancel` / `{id}/outcome` | `Bookings.Manage` | `CancelAsync` / `RecordOutcomeAsync` |
| [AdminAvailabilityController](../../src/Onyx.API/Controllers/Admin/AdminAvailabilityController.cs) `admin/availability` | GET | `` | `Availability.Manage` | `IAvailabilityAdministrationService.GetAsync` |
| | PUT | `settings` | نفسها | `SaveSettingsAsync` |
| | POST / DELETE | `rules` / `rules/{id}` | نفسها | `AddRuleAsync` / `DeleteRuleAsync` |
| | POST / DELETE | `exceptions` / `exceptions/{id}` | نفسها | `AddExceptionAsync` / `DeleteExceptionAsync` |

### 3.6 الخصوصية

| المتحكم | الطريقة | المسار | الصلاحية | الخدمة |
|---|---|---|---|---|
| [AdminDataSubjectRequestsController](../../src/Onyx.API/Controllers/Admin/AdminDataSubjectRequestsController.cs) `admin/data-subject-requests` (`no-store`) | GET | `?…` / `{id}` | `Privacy.Manage` | `IDataSubjectRequestService.ListAsync` / `GetAsync` |
| | POST | `` | نفسها | `CreateAsync` (201) |
| | POST | `{id}/verify` / `{id}/reject` | نفسها | `VerifyAsync` / `RejectAsync` |
| | POST | `{id}/export` | نفسها | `ExportAsync` |
| | POST | `{id}/erase` | نفسها | `EraseAsync` (`ConfirmErasureRequest`) |
| [AdminRetentionPoliciesController](../../src/Onyx.API/Controllers/Admin/AdminRetentionPoliciesController.cs) `admin/retention-policies` (`no-store`) | GET | `` | `Privacy.Manage` | `IRetentionService.ListAsync` |
| | PUT | `{category}` | نفسها | `SaveAsync` |
| | GET | `{category}/preview?action&periodDays` | نفسها | `PreviewAsync` |

### 3.7 الأخبار

| المتحكم | الطريقة | المسار | الصلاحية | الخدمة |
|---|---|---|---|---|
| [AdminNewsController](../../src/Onyx.API/Controllers/Admin/AdminNewsController.cs) `admin/news` (`News.View` على الصنف، `no-store`) | GET | `?…` / `{id}` | `News.View` | `NewsAdministrationService.ListAsync` / `GetAsync` |
| | PUT | `{id}/translations/{language}` | + `News.Edit` | `EditTranslationAsync` |
| | PUT | `{id}/category` | + `News.Edit` | `ChangeCategoryAsync` |
| | POST | `{id}/publish` / `{id}/hide` / `{id}/unpublish` | + `News.Publish` | `PublishAsync` / `HideAsync` / `UnpublishAsync` |
| | PUT | `{id}/indexing` | + `News.Publish` | `SetIndexingAsync` |
| | POST | `{id}/delete` | + `News.Delete` | `DeleteAsync` |
| [AdminNewsSourcesController](../../src/Onyx.API/Controllers/Admin/AdminNewsSourcesController.cs) `admin/news-sources` (`NewsSources.Manage`، `no-store`) | GET | `` / `{id}` | | `NewsSourceAdministrationService.ListAsync` / `GetAsync` |
| | POST | `` | | `CreateAsync` (201) |
| | PUT | `{id}` | | `UpdateAsync` |
| | POST | `{id}/allow` / `{id}/block` | | `AllowAsync` / `BlockAsync` |
| [AdminNewsCategoriesController](../../src/Onyx.API/Controllers/Admin/AdminNewsCategoriesController.cs) `admin/news-categories` (`News.View`، `no-store`) | GET | `` | | `NewsCategoryAdministrationService.ListAsync` |
| | POST / PUT `{id}` | | + `News.Edit` | `CreateAsync` / `UpdateAsync` |

### 3.8 التحليلات وأداء البحث (Stage 12) — جميعها `no-store`

| المتحكم | الطريقة | المسار | الصلاحية | الخدمة (`AnalyticsReportService` ما لم يُذكر) |
|---|---|---|---|---|
| [AdminAnalyticsController](../../src/Onyx.API/Controllers/Admin/AdminAnalyticsController.cs) `admin/analytics` | GET | `overview`، `traffic`، `landing-pages`، `campaigns`، `conversions`، `content`، `funnel` (+ `AnalyticsReportQuery`) | `Analytics.View` | `OverviewAsync`، `TrafficAsync`، `LandingPagesAsync`، `CampaignsAsync`، `ConversionsAsync`، `ContentAsync`، `FunnelAsync` |
| [AdminSearchPerformanceController](../../src/Onyx.API/Controllers/Admin/AdminSearchPerformanceController.cs) `admin/search-performance` | GET | `overview`، `queries`، `pages`، `signals` | `SearchPerformance.View` | `SearchOverviewAsync`، `SearchQueriesAsync`، `SearchPagesAsync`، `SearchSignalsAsync` |
| [AdminAnalyticsIntegrationsController](../../src/Onyx.API/Controllers/Admin/AdminAnalyticsIntegrationsController.cs) `admin/analytics-integrations` | GET | `` | `ProviderConnections.Manage` | `AnalyticsIntegrationService.ListAsync` |
| | POST | `{providerKey}/sync` | نفسها | `AnalyticsIntegrationService.RequestSyncAsync` → 202 (لا ينفذ المزامنة؛ يجعل مهمة الـ Worker مستحقة الآن) |

---

## 4. ما ليس موجودًا في الـ API حاليًا (لا تفترضه)

- لا توجد مسارات لأدوات Phase 2 (`/api/v1/tools/...`)، رغم وجود صلاحيات `Tools.Configure` و`ToolRuns.View` في الكتالوج — هي صلاحيات محجوزة دون نقاط نهاية.
- لا توجد مسارات للأسواق (`Markets.*`) ولا للمكاتب (`Offices.Manage`) ولا للإعدادات العامة (`Settings.Manage`) ولا للشهادات (`Testimonials.*`)؛ الصلاحيات معرّفة في [Permissions.cs](../../src/Onyx.Contracts/Authorization/Permissions.cs) فقط.
- لا يوجد مسار لعرض «مهام النظام» (JobRuns) في الـ API رغم صلاحية `System.Jobs.View` المستخدمة لإشعارات الفشل — غير مؤكد — يحتاج مراجعة إن ظهر لاحقًا.
- لا يوجد مسار لـ AI Advisor أو منصات الإعلانات أو WhatsApp (خارج النطاق — Phase 2/3).

## 5. عند إضافة أو تعديل مسار

1. عرّف DTO الطلب/الاستجابة في `Onyx.Contracts` (لا تمرّر كيانات Domain).
2. أضف السمة الصريحة `[HasPermission(...)]` أو `[AllowAnonymous]` — النسيان يعني 401 لكل الطلبات بسبب السياسة الاحتياطية.
3. أضف `[EnableRateLimiting(RateLimitPolicies.Strict)]` لأي نموذج عام مجهول.
4. أضف اختبارات 401/403 في [tests/Onyx.API.IntegrationTests/](../../tests/Onyx.API.IntegrationTests/) (قاعدة CLAUDE.md §18).
5. أضف الدالة المقابلة في عميل Onyx.Web المكتوب (`src/Onyx.Web/ApiClients/`) — Web لا يبني طلبات HTTP يدويًا.
6. حدّث هذا الملف.
