# 03 — التدقيق (Audit / Activity)

## 1. ماذا تفعل الميزة

سجل **إلحاق فقط** لكل تغيير حساس أو تحريري أو تشغيلي: من فعل، ماذا، على أي كيان، القيم قبل/بعد (مع حجب الحساس)، الوقت UTC، IP، ومعرّف الترابط. لا توجد واجهة تعديل أو حذف. العرض يتطلب `AuditLogs.View`.

مساران للكتابة:
1. **تلقائي** — `AuditLogInterceptor` لكل كيان ينفذ `IAuditable` (Created/Updated/Deleted/SoftDeleted).
2. **صريح** — `IAuditTrail.Record(...)` للأحداث الأمنية والإدارية (دخول، نشر، تصدير، محو…).

كلاهما يدخل **نفس وحدة العمل**: لا يُلتزم التغيير دون سجله والعكس.

## 2. الملفات

| المسار | الدور |
|---|---|
| [AuditLog.cs](../../../src/Onyx.Domain/Auditing/AuditLog.cs) | الكيان؛ `Record(...)` يرفض الوقت غير UTC ويقتطع الحقول لحدود الأعمدة |
| [AuditAction.cs](../../../src/Onyx.Domain/Auditing/AuditAction.cs) | 96 إجراءً (0–95) مخزنة كنص؛ إلحاق فقط |
| [AuditCategory.cs](../../../src/Onyx.Domain/Auditing/AuditCategory.cs) | Content، Publishing، Leads، Security، Integrations، Privacy، System |
| [IAuditable.cs](../../../src/Onyx.Domain/Common/IAuditable.cs)، [SensitiveDataAttribute.cs](../../../src/Onyx.Domain/Common/SensitiveDataAttribute.cs)، [NotAuditedAttribute.cs](../../../src/Onyx.Domain/Common/NotAuditedAttribute.cs) | علامات التحكم |
| [IAuditTrail.cs](../../../src/Onyx.Application/Auditing/IAuditTrail.cs) → [AuditTrail.cs](../../../src/Onyx.Infrastructure/Auditing/AuditTrail.cs) | الكتابة الصريحة |
| [AuditLogInterceptor.cs](../../../src/Onyx.Infrastructure/Persistence/Interceptors/AuditLogInterceptor.cs) | الكتابة التلقائية |
| [IAuditLogQueries.cs](../../../src/Onyx.Application/Auditing/IAuditLogQueries.cs) → [AuditLogQueries.cs](../../../src/Onyx.Infrastructure/Auditing/AuditLogQueries.cs) | البحث |
| [AdminAuditLogsController.cs](../../../src/Onyx.API/Controllers/Admin/AdminAuditLogsController.cs) | `GET /api/v1/admin/audit-logs` |

## 3. التدفق

```
خدمة Application → تعديل كيانات + auditTrail.Record(...) → unitOfWork.SaveChangesAsync
  SaveChanges:
   1. AuditFieldsInterceptor: ختم الحقول، تحويل الحذف الناعم
   2. AuditLogInterceptor.Capture: لكل IAuditable مضاف/معدّل/محذوف
        ResolveAction → Created | Deleted | SoftDeleted (IsDeleted false→true) | Updated
        BuildChanges: يتجاوز حقول الختم والـ xmin و[NotAudited]؛ [SensitiveData] → "[REDACTED]"
        Updated بلا تغييرات حقيقية → لا سجل
   3. DomainEventOutboxInterceptor
  فشل الحفظ → فصل سجلات التدقيق المعلقة (لا سجلات يتيمة)
```

`AuditTrail.Record` يأخذ المستخدم من `ICurrentUser` (أو `actorUserId` الممرّر صراحة، كما في أحداث الدخول قبل وجود جلسة) والـ IP/الترابط من `IRequestContext`. في الـ Worker يكون المستخدم null («system»).

## 4. الدوال المفتاحية

| الدالة | التوقيع | ملاحظات |
|---|---|---|
| `IAuditTrail.Record` | `void Record(AuditAction action, AuditCategory category, string entityType, string? entityId, object? details = null, Guid? actorUserId = null)` | `details` يُسلسل JSON؛ **ممنوع** أن يحوي أسرارًا أو رموزًا أو كلمات مرور. للسجلات كثيفة PII سجّل أسماء الحقول المتغيرة لا قيمها (مثال: `UserAdministrationService` يسجل `fields = ["displayName", …]`، وتصدير العملاء لا يسجل بيانات الاتصال) |
| `AuditLogQueries.SearchAsync` | `Task<PagedResult<AuditLogResponse>> (AuditLogQuery, …)` | فلاتر: UserId، Category، Action، EntityType، EntityId، From/To؛ ترتيب تنازلي بالزمن؛ حجم صفحة ≤ 100 |

**أمثلة على خصائص مستبعدة/محجوبة فعليًا:** `ContentItem.ProofVerification` (`[NotAudited]` — قد تسمي جهات اتصال العميل؛ التحقق والإلغاء مُدققان صراحة)، `ContentTranslation.Body` و`Faqs` (`[NotAudited]` — محفوظة في المراجعات).

## 5. أين يُستدعى التدقيق الصريح (عينة متحقق منها)

| المجال | الإجراءات | المكان |
|---|---|---|
| الأمان | LoginSucceeded/Failed، LockedOut، TwoFactor*، Password*، RefreshTokenReuseDetected، Logout، SessionRevoked | [AuthService](../../../src/Onyx.Infrastructure/Identity/AuthService.cs) |
| المستخدمون/الأدوار | UserInvited… RoleDeleted، InitialAdministratorCreated، TwoFactorRecoveredByOperator | `UserAdministrationService`، `RoleAdministrationService`، `IdentityBootstrapService` |
| التكاملات | ProviderConnectionDisabled/Enabled، ProviderTestRequested، AnalyticsSyncRequested | `ProviderConnectionAdministrationService`، `AnalyticsIntegrationService` |
| المحتوى والنشر | Content*، Redirect*، Media*، Proof*، Project(Un)Featured، SeoRecommendation*، Knowledge*، CalendarEntryDecided، BrandKitUpdated، EmailTemplateCopy* | خدمات Content/Proof/Media/Intelligence (انظر ملفات الميزات) |
| العملاء المحتملون | LeadStageChanged، LeadAssigned، LeadPriorityChanged، LeadNoteAdded، LeadsExported، MarketingConsentWithdrawn، Booking* ، AvailabilityChanged | خدمات Leads/Booking |
| الخصوصية | DataSubject*، RetentionPolicyChanged، RetentionRunCompleted | خدمات Privacy |
| الأخبار | NewsSource*، NewsItem*، NewsCategory* | خدمات News |

## 6. الاختبارات

- [AuditLogTests](../../../tests/Onyx.Domain.Tests/Auditing/AuditLogTests.cs) — الحقول، رفض غير UTC، الاقتطاع.
- [PersistenceInterceptorTests](../../../tests/Onyx.API.IntegrationTests/Persistence/PersistenceInterceptorTests.cs) — الختم + التدقيق مع الحجب + الـ outbox ذريًا؛ لا سجل بلا تغيير؛ الحذف الناعم.
- [AdminOperationsApiTests](../../../tests/Onyx.API.IntegrationTests/Identity/AdminOperationsApiTests.cs) — `Audit_log_api_filters_security_events_and_never_contains_secrets`.

## 7. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| إضافة `AuditAction` | أضف في النهاية برقم جديد (لا إعادة ترقيم)؛ ترجمة العرض إن وُجدت واجهة |
| جعل كيان جديد `IAuditable` | علّم الخصائص الحساسة بـ `[SensitiveData]` والكبيرة بـ `[NotAudited]`؛ اختر `AuditCategory` المناسب بتجاوز الخاصية الافتراضية |
| ترتيب المعترضات في `AddInfrastructure` | **لا تغيّره**: التدقيق يعتمد على تحويل الحذف الناعم الذي يسبقه |
| محو البيانات الشخصية | سجلات التدقيق لا تُمحى؛ لذلك لا تضع PII في `details` — راجع [14-privacy-retention-AR.md](14-privacy-retention-AR.md) |

## 8. الحالة

- **منفّذ:** الكتابة التلقائية والصريحة، والبحث عبر الـ API.
- **غير منفّذ:** شاشة عرض سجل التدقيق في Onyx.Web (لا متحكم Admin).
