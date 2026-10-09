# 12 — البريد الإلكتروني ومركز الإشعارات

## 1. ماذا تفعل الميزة

**البريد:**
- القوالب **مملوكة للكود** (التخطيط، الترميز، الروابط)؛ الإدارة تعدّل **النص فقط** لكل لغة (`EmailTemplateCopy`) ضمن قواعد العناصر النائبة. روابط إعادة التعيين والدعوة لا تكون أبدًا جزءًا من النص القابل للتعديل.
- `IEmailOutbox.QueueAsync` **يتحقق ويعرض** الرسالة ويضيفها لوحدة العمل الحالية ⇒ تُلتزم ذريًا مع التغيير الذي سببها. الـ Worker يرسلها لاحقًا مع إعادة المحاولة. فشل الإرسال لا يُضيّع البيانات الأصلية.
- كل قيمة ديناميكية تُرمَّز HTML؛ الاتجاه حسب اللغة (RTL للعربية)؛ بديل نص عادي دائمًا؛ الروابط https مطلقة فقط (`Email.UnsafeLink`).
- رسائل الزائر بلغة الإرسال؛ الرسائل الداخلية بالعربية (`LeadNotifier.InternalLanguage`) ولا تحمل بيانات اتصال الزائر.

**الإشعارات:** كل إشعار يحمل صلاحية مطلوبة؛ لا يراه إلا من يحملها. لا يُخزن نص، بل نوع + معاملات تُترجم في الواجهة. انظر [02-roles-permissions-AR.md §6](02-roles-permissions-AR.md) لجدول الأنواع والصلاحيات.

## 2. القوالب المسجّلة

كل القوالب الثمانية تنفّذ `IEditableEmailTemplate` (نصها قابل للتعديل من الإدارة)، والعمود الأخير هو العناصر النائبة **الإلزامية** (`RequiredPlaceholders`) التي لا يمكن حذفها من النص:

| المفتاح | الصنف | من يرسله | إلزامي |
|---|---|---|---|
| `identity.invitation` | [InvitationEmailTemplate](../../../src/Onyx.Infrastructure/Email/Templates/InvitationEmailTemplate.cs) | `UserAdministrationService` | `{hours}` (والرابط من الكود) |
| `identity.password-reset` | [PasswordResetEmailTemplate](../../../src/Onyx.Infrastructure/Email/Templates/PasswordResetEmailTemplate.cs) | `AuthService.RequestPasswordResetAsync` | `{minutes}` (والرابط من الكود) |
| `system.provider-test` | [ProviderTestEmailTemplate](../../../src/Onyx.Infrastructure/Email/Templates/ProviderTestEmailTemplate.cs) | `ProviderConnectionAdministrationService.QueueEmailTestAsync` | — |
| `leads.inquiry-received` | [InquiryReceivedEmailTemplate](../../../src/Onyx.Infrastructure/Email/Templates/InquiryReceivedEmailTemplate.cs) | `InquirySubmittedHandler` (للزائر) | `{reference}` |
| `leads.inquiry-internal` | [InquiryInternalEmailTemplate](../../../src/Onyx.Infrastructure/Email/Templates/InquiryInternalEmailTemplate.cs) | `LeadNotifier` | `{reference}` |
| `bookings.confirmed` | [BookingConfirmedEmailTemplate](../../../src/Onyx.Infrastructure/Email/Templates/BookingConfirmedEmailTemplate.cs) | `BookingMessages.Confirmed` | `{date}`، `{time}`، `{timezone}` |
| `bookings.cancelled` | [BookingCancelledEmailTemplate](../../../src/Onyx.Infrastructure/Email/Templates/BookingCancelledEmailTemplate.cs) | `BookingMessages.Cancelled` | `{date}`، `{time}` |
| `bookings.internal` | [BookingInternalEmailTemplate](../../../src/Onyx.Infrastructure/Email/Templates/BookingInternalEmailTemplate.cs) | معالجات الحجز | `{status}`، `{date}`، `{time}` |

## 3. الملفات

| المسار | الدور |
|---|---|
| [src/Onyx.Application/Email/](../../../src/Onyx.Application/Email/) | `IEmailOutbox`، `EmailRequest` (+ `EmailRequestValidator`)، `IEmailSender`، `EmailTransportException`/`EmailFailureKind`، `EmailCopyRules` (`Validate`، `Fill`)، `EmailTemplateAdministrationService` |
| [src/Onyx.Infrastructure/Email/](../../../src/Onyx.Infrastructure/Email/) | `EmailOutbox`، `EmailTemplateRenderer`، `EmailTemplateCatalog`، `EmailMessage`/`EmailMessageStatus`، `SmtpEmailSender` (MailKit)، `PickupDirectoryEmailSender`، `UnconfiguredEmailSender`، `MimeMessageFactory`، `EmailOptions`/`EmailOptionsValidator` |
| [EmailProcessor](../../../src/Onyx.Infrastructure/BackgroundJobs/EmailProcessor.cs) | الإرسال في الـ Worker — انظر [BACKGROUND-JOBS-AR.md §5](../BACKGROUND-JOBS-AR.md) |
| [EmailTemplateCopy](../../../src/Onyx.Domain/Messaging/EmailTemplateCopy.cs) | النص المعدّل (موضوع، عنوان، 1–10 فقرات، تسمية زر، تذييل) |
| [src/Onyx.Application/Notifications/](../../../src/Onyx.Application/Notifications/)، [AdminNotification](../../../src/Onyx.Domain/Notifications/AdminNotification.cs) | الإشعارات |
| API: [AdminEmailTemplatesController](../../../src/Onyx.API/Controllers/Admin/AdminEmailTemplatesController.cs)، [AdminNotificationsController](../../../src/Onyx.API/Controllers/Admin/AdminNotificationsController.cs)، `admin/provider-connections/email/test` | |
| Web Admin: [EmailTemplatesController](../../../src/Onyx.Web/Areas/Admin/Controllers/EmailTemplatesController.cs)، [NotificationsController](../../../src/Onyx.Web/Areas/Admin/Controllers/NotificationsController.cs) | |

## 4. الدوال المفتاحية

| الدالة | السلوك |
|---|---|
| `EmailOutbox.QueueAsync(EmailRequest, ct)` → `Result<Guid>` | يتحقق (`Email.Invalid`: عنوان صالح، لا أسطر جديدة في الاسم، لغة حرفان صغيران، نموذج غير مفرط الحجم، مرفقات صغيرة لملفات التقويم فقط) → `RenderAsync` → يضيف `EmailMessage(Pending)` دون حفظ |
| `EmailTemplateRenderer.RenderAsync(…)` | `Email.UnknownTemplate`، `Email.UnsupportedLanguage`، `Email.MissingValues`، `Email.UnsafeLink`؛ يطبّق النص المعدّل إن وُجد |
| `EmailTemplateAdministrationService.SaveCopyAsync(templateKey, lang, request, ct)` | `EmailTemplateCopy.Update` (أطوال، نص عادي) + `EmailCopyRules.Validate` (العناصر النائبة المعلنة فقط، الإلزامية موجودة، لا أقواس شاردة، لا إضافة/حذف للزر)؛ تدقيق `EmailTemplateCopyChanged` |
| `ResetCopyAsync` | يعيد للنص الافتراضي في الكود؛ تدقيق `EmailTemplateCopyReset` |
| `NotificationService.PublishAsync/ListAsync/CountUnreadAsync/MarkReadAsync/MarkAllReadAsync` | انظر [02](02-roles-permissions-AR.md)؛ صفحات ≤ 100، الأحدث أولًا، حالة القراءة لكل مستخدم |

## 5. الإعدادات (`Email`، يُتحقق منها عند إقلاع الـ Worker)

`Transport` = `None` | `Smtp` | `PickupDirectory`؛ `FromAddress`، `FromName` (افتراضي «Onyx Vanguard Solutions»)، `Smtp` (المضيف، المنفذ، الأمان، بيانات الاعتماد معًا أو لا شيء — من مخزن الأسرار)، `PickupDirectory` (للتطوير). إعداد غير آمن أو ناقص يفشل التحقق (اختبارات `WorkerCompositionTests`).

## 6. الاختبارات

- [EmailProcessingTests](../../../tests/Onyx.API.IntegrationTests/Worker/EmailProcessingTests.cs) — العرض بلغة الإرسال والحفظ مع وحدة العمل، ترميز القيم، رفض الروابط غير الآمنة، تأجيل الدفعة عند انقطاع الناقل، Dead-letter مع إشعار، الناقل غير المهيأ، كل رسالة تُرسل مرة واحدة بين Worker-ين.
- [PickupDirectoryEmailSenderTests](../../../tests/Onyx.API.IntegrationTests/Worker/PickupDirectoryEmailSenderTests.cs)، [WorkerCompositionTests](../../../tests/Onyx.API.IntegrationTests/Worker/WorkerCompositionTests.cs).
- [EmailRequestValidatorTests](../../../tests/Onyx.Application.Tests/Email/EmailRequestValidatorTests.cs)، [ContentGovernanceTests](../../../tests/Onyx.Application.Tests/Content/ContentGovernanceTests.cs) (قواعد العناصر النائبة).
- [CmsAdministrationApiTests](../../../tests/Onyx.API.IntegrationTests/Content/CmsAdministrationApiTests.cs) (`Email_copy_cannot_drop_required_placeholders_or_inject_unknown_ones`).
- [NotificationVisibilityTests](../../../tests/Onyx.API.IntegrationTests/Worker/NotificationVisibilityTests.cs)، [AdminNotificationTests](../../../tests/Onyx.Domain.Tests/Notifications/AdminNotificationTests.cs).

## 7. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| قالب جديد | صنف `IEmailTemplate` (أو `IEditableEmailTemplate`) + تسجيله `AddSingleton<IEmailTemplate, …>` في `DependencyInjection` المناسب (`AddLeads`/`AddOnyxIdentity`/…) + اختبار عرض |
| عنصر نائب إلزامي جديد في قالب قابل للتعديل | النصوص المحفوظة مسبقًا في `email_template_copies` ستفشل التحقق عند الحفظ التالي — راجعها |
| ناقل جديد (مزوّد معاملات) | نفّذ `IEmailSender` (+ `ProviderKey`، `IsConfigured`)، أضف قيمة `EmailTransport`، حدّث الاختيار في `AddWorkerProcessing`؛ يحتاج موافقة حزمة |
| بيانات شخصية في البريد | `email_messages` تخضع للاحتفاظ والمحو؛ التصدير يزيل رموز الروابط لمرة واحدة من الأجسام (`DataSubjectExportBuilderTests`) |

## 8. الحالة

- **منفّذ:** كل ما سبق.
- **غير مهيأ افتراضيًا:** الناقل (`Transport = None`) ⇒ الرسائل تبقى في الطابور ويظهر المزوّد NotConfigured.
- **مؤجّل:** تصميم بصري للبريد (مع مرحلة التصميم).
