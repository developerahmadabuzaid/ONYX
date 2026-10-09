# 14 — الخصوصية: طلبات أصحاب البيانات (التصدير والمحو) والاحتفاظ

## 1. ماذا تفعل الميزة

**طلبات أصحاب البيانات** (يشغّلها موظف يحمل `Privacy.Manage` — صلاحية مميزة تفرض 2FA):
- `Received → Verified → Completed` أو `Rejected`. **لا تنفيذ قبل تسجيل كيفية التحقق من هوية الطالب.**
- صاحب البيانات يُحدَّد فقط بالبريد المخزن على الطلب عند استلامه؛ الخطوات التالية تأخذ معرّف الطلب فقط ⇒ لا يمكن توجيه عملية لسجلات شخص آخر.
- **التصدير:** ملف JSON يُولَّد عند الطلب ولا يُخزن، ويمكن تنزيله مجددًا بعد الإكمال ما لم يُمحَ الشخص.
- **المحو:** معاملة واحدة تمحو حساب الإدارة (إن وُجد)، كل سجلات Leads بهذا البريد، رسائل البريد إليه (حتى المعلقة)، وعنوانه من كل طلب يخصه (تبقى تجزئة SHA-256 فقط). يتطلب كتابة البريد مرة أخرى للتأكيد. التكرار لا يغيّر شيئًا (قفل صف الطلب يجعل التنفيذات المتزامنة متسلسلة).

**الاحتفاظ:** سياسة لكل فئة بيانات بفترة قابلة للضبط وإجراء (حذف/إخفاء هوية). **لا فترات مبرمجة**: فئة بلا سياسة محفوظة ومفعّلة بفترة لا تُعالج أبدًا ⇒ تثبيت جديد لا يحذف شيئًا.

## 2. فئات الاحتفاظ ([RetentionCategory](../../../src/Onyx.Domain/Privacy/RetentionCategory.cs))

| الفئة | ما تشمله | الإجراءات المسموحة |
|---|---|---|
| `LeadsAndInquiries` | Leads مع استفساراتها وموافقاتها وملاحظاتها وأنشطتها وحجوزاتها، مقيسة من آخر استفسار؛ **Lead له حجز مؤكد قادم لا يُمس** | Delete، Anonymize |
| `EmailMessages` | الرسائل المرسلة أو الميتة (المعلقة لا تُمس) | Delete |
| `CookieConsentReceipts` | الإيصالات | Delete |
| `Notifications` | الإشعارات وحالات قراءتها | Delete |
| `AuditLogs` | سجل التدقيق | Delete، Anonymize |
| `DataSubjectRequests` | الطلبات المغلقة | Delete |
| `AnalyticsAggregates` | مجاميع GA4/Search Console اليومية (لا بيانات شخصية) — Stage 12 | Delete |

`RetentionPolicy.Update`: الفترة 1–36500 يومًا؛ التفعيل يتطلب فترة (`Retention.PeriodRequired`)؛ الإجراء يجب أن يكون مسموحًا للفئة.

## 3. الملفات

| المسار | الدور |
|---|---|
| [src/Onyx.Domain/Privacy/](../../../src/Onyx.Domain/Privacy/) | `DataSubjectRequest` (`Receive`، `Verify`، `Reject`، `EnsureExecutable`، `Complete`، `ForgetSubject`، `HashEmail`)، `RetentionPolicy`، `ErasedValues`، `CookieConsentReceipt` |
| [DataSubjectRequestService.cs](../../../src/Onyx.Application/Privacy/DataSubjectRequestService.cs) | مسجّل مع الهوية (API فقط) لأنه يمحو حسابات الإدارة |
| [SubjectEraser.cs](../../../src/Onyx.Application/Privacy/SubjectEraser.cs) | **المحو المشترك** بين طلبات المحو والإخفاء بالاحتفاظ |
| [RetentionService.cs](../../../src/Onyx.Application/Privacy/RetentionService.cs) | السياسات والتنفيذ |
| [DataSubjectExportBuilder.cs](../../../src/Onyx.Application/Privacy/DataSubjectExportBuilder.cs) | بناء JSON (UTF-8، الحروف العربية كما هي، إزالة رموز الروابط لمرة واحدة من أجسام الرسائل) |
| [IPersonalDataStore](../../../src/Onyx.Application/Privacy/IPersonalDataStore.cs) → [PersonalDataStore](../../../src/Onyx.Infrastructure/Privacy/PersonalDataStore.cs) | التحميل والحذف على مستوى المجموعات وتقليص التدقيق |
| [IAccountPrivacyStore](../../../src/Onyx.Application/Privacy/IAccountPrivacyStore.cs) → [AccountPrivacyStore](../../../src/Onyx.Infrastructure/Identity/AccountPrivacyStore.cs) | تصدير/محو حساب الإدارة؛ يرفض محو النفس (`Privacy.SelfErasure`) وآخر SuperAdmin نشط (`Privacy.LastSuperAdmin`)؛ يُلغي الجلسات (`DataSubjectErasure`) |
| [RetentionEnforcementJob](../../../src/Onyx.Infrastructure/BackgroundJobs/RetentionEnforcementJob.cs) | `daily@02:30` |
| API: [AdminDataSubjectRequestsController](../../../src/Onyx.API/Controllers/Admin/AdminDataSubjectRequestsController.cs)، [AdminRetentionPoliciesController](../../../src/Onyx.API/Controllers/Admin/AdminRetentionPoliciesController.cs) | `no-store` |
| Web Admin: [PrivacyRequestsController](../../../src/Onyx.Web/Areas/Admin/Controllers/PrivacyRequestsController.cs)، [RetentionController](../../../src/Onyx.Web/Areas/Admin/Controllers/RetentionController.cs) | التصدير يُبث دون تخزين مؤقت |

## 4. التدفقات

**المحو** — `DataSubjectRequestService.EraseAsync(id, ConfirmErasureRequest, ct)` داخل `ExecuteInTransactionAsync`:
1. `requests.LockAsync(id)` (قفل الصف).
2. مكتمل مسبقًا → يعاد كما هو.
3. `EnsureExecutable(Erasure)` (موثق، والشخص غير ممحو).
4. مطابقة البريد المكتوب للتأكيد (`Privacy.ConfirmationMismatch`).
5. `accounts.EraseAsync` → `personalData.LoadSubjectAsync` → `SubjectEraser.EraseAsync`:
   - `Inquiry.Erase` / `Lead.Erase` (يبقى فقط غير الشخصي؛ بريد الـ Lead الممحو لا يطابق أبدًا).
   - سحب موافقات التسويق.
   - `ConsultationBooking.EraseVisitorAccess` (إلغاء القادم دون إرسال بريد للزائر، تعطيل رابط الإدارة).
   - حذف ملاحظات الموظفين.
   - `MinimizeAuditAsync`: إزالة IP الزائر والقيم القديمة غير المحجوبة من سجلات التدقيق المتعلقة (السجلات نفسها تبقى).
6. `DeleteEmailsToAsync` → `ForgetSubject` على كل الطلبات ذات التجزئة نفسها.
7. `Complete` + تدقيق `DataSubjectErased` **بالأعداد فقط**.

**التنفيذ الدوري** — `RetentionService.EnforceAsync`: لكل سياسة نشطة، حتى `Privacy:RetentionMaxBatchesPerRun` دفعة (50) بحجم `RetentionBatchSize` (200)، كل دفعة في معاملتها؛ الـ Leads: حذف الرسم الكامل أو `SubjectEraser` للإخفاء؛ الفئات الأخرى: `ApplyRetentionBatchAsync` على مستوى المجموعات. تدقيق `RetentionRunCompleted` بالأعداد ومؤشر «المزيد في التشغيل التالي». `PreviewAsync` يعدّ ما سيتأثر دون تنفيذ.

## 5. الاختبارات

- [PrivacyRulesTests](../../../tests/Onyx.Domain.Tests/Privacy/PrivacyRulesTests.cs) — التنفيذ بعد التحقق فقط، إعادة تنزيل التصدير، لا فترات افتراضية، المحو يُبقي غير الشخصي ومتكرر بأمان، إلغاء الحجوزات القادمة.
- [DataSubjectErasureApiTests](../../../tests/Onyx.API.IntegrationTests/Privacy/DataSubjectErasureApiTests.cs) — المحو في كل مكان دون مسّ الآخرين، التكرار والتزامن، **الأعمال المنتظرة لا تعيد إنشاء البيانات الممحوة**، حماية النفس وآخر SuperAdmin.
- [DataSubjectExportApiTests](../../../tests/Onyx.API.IntegrationTests/Privacy/DataSubjectExportApiTests.cs)، [DataSubjectExportBuilderTests](../../../tests/Onyx.Application.Tests/Privacy/DataSubjectExportBuilderTests.cs).
- [RetentionApiTests](../../../tests/Onyx.API.IntegrationTests/Privacy/RetentionApiTests.cs) — لا معالجة بلا سياسة، اختيار المستحق بدقة، حماية الحجوزات القادمة، الدفعات المحدودة.
- [AnalyticsReportsApiTests](../../../tests/Onyx.API.IntegrationTests/Analytics/AnalyticsReportsApiTests.cs) (`Analytics_aggregates_follow_an_owner_set_retention_policy_and_hold_no_personal_data`).
- Web: [AdminPrivacyTests](../../../tests/Onyx.Web.Tests/AdminPrivacyTests.cs).

## 6. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| **أي جدول جديد يحوي بيانات شخصية** | `RetentionCategory` (إلحاق) + `RetentionPolicy.AllowedActions` + `PersonalDataStore` (`LoadSubjectAsync`، `ApplyRetentionBatchAsync`، `CountRetentionDueAsync`، التغطية) + `DataSubjectExportBuilder` + `SubjectEraser` إن كان مرتبطًا بالـ Leads + الاختبارات أعلاه |
| حقل شخصي جديد في `Inquiry`/`Lead` | `Erase` في الكيان، `[SensitiveData]`، التصدير، `MinimizeAuditAsync` |
| `SubjectEraser` | يؤثر على المحو **والاحتفاظ** معًا |
| حساب إدارة | `AccountPrivacyStore` (الحراس)، `SessionManager.ReasonDataSubjectErasure` |

## 7. مثال

**تفعيل حذف سجلات البريد بعد 180 يومًا:** `PUT /api/v1/admin/retention-policies/EmailMessages` بـ `{ "action": "Delete", "periodDays": 180, "isEnabled": true }` (أو من شاشة الاحتفاظ). جرّب أولًا `GET …/EmailMessages/preview?action=Delete&periodDays=180`. الفترة الفعلية قرار مالك/قانوني — لا تضعها في الكود.

## 8. الحالة

- **منفّذ:** كل ما سبق.
- **بانتظار قرار المالك:** فترات الاحتفاظ لكل الفئات (بما فيها `AnalyticsAggregates`).
- **مستقبلي:** فئات لتشغيلات الأدوات ومحادثات Advisor مع مراحلها.
