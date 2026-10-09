# 10 — الاستفسارات والعملاء المحتملون (CRM) والإسناد

## 1. ماذا تفعل الميزة

- **قاعدة بيانات الإدارة هي نظام السجل** لكل استفسار. البريد قناة إشعار فقط: النجاح = تخزين الاستفسار، أُرسل البريد أم لا.
- كل نموذج عام ينشئ `Inquiry` **غير قابل للتعديل** + `ConsentRecord` + يُلحق بـ `Lead` مفتوح لنفس البريد (أو يفتح Lead جديدًا) + حدث `InquirySubmitted` — **في معاملة واحدة**.
- الـ Worker يرسل بريد تأكيد للزائر **بلغة الإرسال**، وبريدًا داخليًا لا يحمل بيانات الاتصال (فقط النوع والمرجع ورابط Lead في الإدارة)، وإشعار `NewLead`.
- **الإسناد** (Attribution) مدرك للموافقة: بدون موافقة التحليلات يُحفظ سياق الجلسة المرسِلة فقط؛ First-touch ومعرّف عميل GA فقط مع الموافقة. المُحيل يُختصر إلى أصله (origin) فقط.
- **خط المبيعات:** `New → Contacted → Qualified → Proposal → Won/Lost` (`Won`/`Lost` يغلقان؛ يمكن إعادة الفتح). نقاط **مبنية على قواعد مسمّاة** (حد أقصى 100) — ترتّب العمل فقط وليست تنبؤًا ولا تظهر للزوار.

**أنواع الاستفسار** ([InquiryType](../../../src/Onyx.Domain/Leads/InquiryType.cs)): المقبولة من نقطة النماذج العامة (`InquiryTypes.PublicForms`): `StartProject`، `Contact`، `RequestConsultation`، `ServiceInquiry`، `SimilarProject`، `ContextualCta`. `Consultation` يُنشأ فقط عبر الحجز. `ToolFollowUp`، `OpportunityFinder`، `Advisor` محجوزة لمراحل لاحقة (غير مقبولة).

## 2. الملفات

| الطبقة | المسار | المحتوى |
|---|---|---|
| Domain | [src/Onyx.Domain/Leads/](../../../src/Onyx.Domain/Leads/) | `Inquiry` (`Submit`، `CheckRequirements`، `Erase`)، `ContactDetails`، `InquiryAnswers` (مُصدَّرة بإصدار)، `ConsentRecord`، `Attribution`، `TouchPoint`، `Lead` (`Open`، `AttachInquiry`، `ChangeStage`، `AssignTo`، `ChangePriority`، `Erase`)، `LeadScore`، `LeadNote`، `LeadActivity` |
| Application | [src/Onyx.Application/Leads/](../../../src/Onyx.Application/Leads/) | `InquiryIntake`، `InquiryRecorder`، `InquiryService`، `InquirySubmittedHandler`، `LeadNotifier`، `LeadAdministrationService`، `LeadCsv`، `LeadSettings`، `InquiryFormValidator` |
| Infrastructure | [InquiryRepository، LeadRepository](../../../src/Onyx.Infrastructure/Persistence/Repositories/)، [Leads/LeadsOptions](../../../src/Onyx.Infrastructure/Leads/)، قوالب البريد `InquiryReceivedEmailTemplate` و`InquiryInternalEmailTemplate` في [Email/Templates/](../../../src/Onyx.Infrastructure/Email/Templates/) | |
| API | [InquiriesController](../../../src/Onyx.API/Controllers/InquiriesController.cs)، [AdminLeadsController](../../../src/Onyx.API/Controllers/Admin/AdminLeadsController.cs)، [AdminInquiriesController](../../../src/Onyx.API/Controllers/Admin/AdminInquiriesController.cs) | |
| Web عام | [LeadFormsController](../../../src/Onyx.Web/Controllers/LeadFormsController.cs)، [Leads/](../../../src/Onyx.Web/Leads/) (`LeadPages`، `LeadPageController`، `LeadFormMapper`، `CtaLinks`)، [Attribution/](../../../src/Onyx.Web/Attribution/) | |
| Web Admin | [LeadsController](../../../src/Onyx.Web/Areas/Admin/Controllers/LeadsController.cs)، [InquiriesController](../../../src/Onyx.Web/Areas/Admin/Controllers/InquiriesController.cs) | |

## 3. التدفق من الطرف إلى الطرف

```
GET /ar/تواصل-معنا?service=…&cta=…  (LeadFormsController.Contact)
  → نموذج مع: Turnstile widget (إجراء "inquiry")، حقل honeypot "Website"، سياق الصفحة مشفّرًا (form.Context)
POST (antiforgery) → LeadFormMapper.ToDto: يضيف رمز Turnstile، first/last touch من كوكيز onyx.ft/onyx.lt (مع الموافقة فقط)،
     DeviceClass من User-Agent، GA client id من _ga (مع الموافقة فقط)
  → ILeadsApiClient → POST /api/v1/inquiries
     → InquiryService.SubmitAsync:
         النوع ضمن PublicForms
         InquiryIntake.PrepareAsync (ترتيب ثابت):
            1. InquiryFormValidator (حدود بنيوية)
            2. Honeypot ممتلئ → PreparedInquiry.Spam: يُرد بنجاح ومرجع وهمي، لا يُخزن شيء، لا يُستهلك Turnstile
            3. ContactDetails.Create، InquiryAnswers.Create
            4. ContactConsent إلزامي
            5. Inquiry.CheckRequirements (رسالة مطلوبة لـ Contact/ServiceInquiry/SimilarProject/RequestConsultation؛
               ServiceInquiry يحتاج serviceId، SimilarProject يحتاج projectId)
            6. ResolveContextAsync: الخدمة/المشروع حي ومن النوع الصحيح، CTA نشط؛ يستنتج الركيزة
            7. ICaptchaVerifier.VerifyAsync(token, "inquiry", ip)  ← أخيرًا، لأن الرمز لاستخدام واحد
            8. Attribution.Create
         InquiryRecorder.RecordAsync: FindOpenByEmailAsync → Lead.Open أو الإلحاق؛ Inquiry.Submit (يرفع InquirySubmitted)؛
            ConsentRecord.Give(ConsentVersion من الإعدادات)؛ Lead.AttachInquiry (نقاط + نشاط)
         SaveChanges (معاملة واحدة) → 201 + المرجع "ONX-XXXXXXXX"
  → Web: يحفظ المرجع في TempData + حدث تحويل GA4 (إن وُجدت الموافقة) → صفحة التأكيد
Worker: OutboxProcessor → InquirySubmittedHandler:
     يتخطى إن مُحي أو كان Consultation أو عولج (processed key "inquiry-submitted:{id}")
     بريد leads.inquiry-received للزائر (لغته) + leads.inquiry-internal للمستلمين (عربي، بلا بيانات اتصال، رابط /admin/leads/{id})
     + إشعار NewLead (Leads.View) — الكل يُلتزم معًا
```

## 4. الدوال المفتاحية

| الدالة | السلوك |
|---|---|
| `InquiryService.SubmitAsync(SubmitInquiryRequest, ct)` → `InquiryAcceptedResponse` | انظر أعلاه. `CaptchaAction = "inquiry"` |
| `InquiryIntake.PrepareAsync(type, form, captchaAction, ct)` (internal) | مشترك مع الحجز |
| `InquiryRecorder.RecordAsync(prepared, ct)` (internal) | يرمي `InvalidOperationException` إن مُرّر Spam. ملاحظة موثقة: استفساران أولان متزامنان من نفس البريد قد يفتحان Lead-ين |
| `InquiryRecorder.Reference(inquiryId)` | `"ONX-" + آخر 8 أحرف` — لا يمنح أي وصول |
| `LeadAdministrationService.ListAsync/GetAsync` | **نطاق المالك:** بدون `Leads.ViewAll` (أو SuperAdmin) يرى المستخدم Leads الخاصة به وغير المسندة فقط (`LeadOwnerScope`). التفاصيل تعرض مسار الأصل (الإسناد) |
| `ChangeStageAsync` / `ChangePriorityAsync` / `AddNoteAsync` | تحقق النسخة (xmin)؛ تدقيق `LeadStageChanged` (من→إلى) / `LeadPriorityChanged` / `LeadNoteAdded` (معرّف الملاحظة فقط) |
| `AssignAsync` | المالك الجديد مستخدم نشط يحمل `Leads.View` (`Lead.InvalidOwner`)؛ تدقيق `LeadAssigned` |
| `WithdrawMarketingConsentAsync(leadId, consentId, ct)` | يسجل سحب موافقة التسويق (موافقة التواصل لا تتغير)؛ تدقيق `MarketingConsentWithdrawn` (فئة Privacy) |
| `ExportAsync(LeadListQuery, ct)` → `LeadExportFile` | الـ Leads المرئية للمستدعي فقط، بحد أقصى صفوف؛ CSV بـ BOM، كل الحقول مقتبسة، **تحييد الصيغ** (`'=…`)؛ تدقيق `LeadsExported` **بلا بيانات اتصال**. يتطلب `Leads.Export` (صلاحية مميزة ⇒ 2FA) |
| `LeadScore.Evaluate(inquiries)` | booked-consultation 30، project-or-consultation-request 20، service-or-project-interest 10، company-provided 10، phone-provided 10، detailed-brief 10 (≥3 إجابات)، repeat-inquiry 10 |

## 5. الإعدادات (`Leads`)

`ConsentVersion` (غيّره عند تغيير نص الموافقة)، `DefaultRecipients`، `RecipientsByType` (المفاتيح أسماء `InquiryType`)، `ManageLinkHoursAfterSlot` — مُتحقق منها عند الإقلاع ([LeadsOptions](../../../src/Onyx.Infrastructure/Leads/)). `AdminBaseUrl` يأتي من `IdentitySecurity:AdminPortalBaseUrl` و`PublicBaseUrl` من `Site:PublicBaseUrl`.

## 6. قاعدة البيانات والخصوصية

`inquiries`، `consent_records` (واحد لكل استفسار)، `leads`، `lead_notes`، `lead_activities`. الحقول الشخصية معلّمة `[SensitiveData]` فتُحجب في التدقيق: الاسم، البريد، الهاتف، الرسالة، الاسم المعروض، نص الملاحظة. `Inquiry.Erase`/`Lead.Erase` يزيلان الحقول الشخصية مع إبقاء النوع واللغة والتواريخ ووسوم الحملة؛ بريد Lead الممحو لا يطابق أبدًا فيفتح الاستفسار التالي Lead جديدًا. الاحتفاظ: [14-privacy-retention-AR.md](14-privacy-retention-AR.md).

## 7. الاختبارات

- [InquiryAndConsentTests](../../../tests/Onyx.Domain.Tests/Leads/InquiryAndConsentTests.cs)، [LeadTests](../../../tests/Onyx.Domain.Tests/Leads/LeadTests.cs)، [AttributionTests](../../../tests/Onyx.Domain.Tests/Leads/AttributionTests.cs).
- [InquiryApiTests](../../../tests/Onyx.API.IntegrationTests/Leads/InquiryApiTests.cs) — معاملة واحدة، رفض Turnstile لا يخزن شيئًا، الرمز يُقبل مرة، honeypot، الرفض قبل Turnstile، الإسناد حسب الموافقة، الإلحاق بالـ Lead المفتوح.
- [LeadAdministrationApiTests](../../../tests/Onyx.API.IntegrationTests/Leads/LeadAdministrationApiTests.cs) — نطاق الرؤية، الإسناد، التدقيق والنسخ، التصدير.
- [LeadNotificationWorkerTests](../../../tests/Onyx.API.IntegrationTests/Leads/LeadNotificationWorkerTests.cs) — البريدان والإشعار مرة واحدة؛ **الاستفسار يبقى إن فشل ناقل البريد**.
- [TurnstileNotConfiguredApiTests](../../../tests/Onyx.API.IntegrationTests/Leads/TurnstileNotConfiguredApiTests.cs) — الفشل المغلق.
- [IcsAndCsvTests](../../../tests/Onyx.Application.Tests/Booking/IcsAndCsvTests.cs) — CSV.
- Web: [LeadFormsSiteTests](../../../tests/Onyx.Web.Tests/LeadFormsSiteTests.cs)، [AdminLeadsTests](../../../tests/Onyx.Web.Tests/AdminLeadsTests.cs).

## 8. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| نوع استفسار عام جديد | `InquiryType` (إلحاق)، `InquiryTypes.PublicForms` (Contracts)، `Inquiry.CheckRequirements`، `LeadScore`، `LeadsOptions.RecipientsByType`، نماذج Web، `ConversionEvent` في Web (أنواع الاستفسار المسموح بها في حدث GA4) |
| حقل جديد في النموذج | `InquiryFormDto` + `InquiryFormValidator` + `ContactDetails`/`InquiryAnswers` + `Inquiry.Erase` + `PersonalDataStore` (التصدير/المحو) + `[SensitiveData]` |
| ترتيب `PrepareAsync` | Turnstile يجب أن يبقى **آخرًا** (رمز لاستخدام واحد)؛ الـ honeypot قبل Turnstile |
| `Attribution` | `AttributionCookies` في Web، `Inquiry.Erase` (ما يبقى بعد المحو)، `AnalyticsFunnelRepository` (القمع يُجمّع حسب الإسناد) |
| نص الموافقة | ارفع `Leads:ConsentVersion` |

## 9. مثال

**لإرسال استفسارات `ServiceInquiry` إلى صندوق بريد مختلف:** أضف في الإعدادات `"Leads": { "RecipientsByType": { "ServiceInquiry": ["sales@…"] } }` — `LeadSettings.RecipientsFor` يختار القائمة الخاصة بالنوع وإلا الافتراضية. لا تغيير في الكود.

## 10. الحالة

- **منفّذ:** كل ما سبق.
- **لا يوجد:** زر/قناة WhatsApp عامة (مرفوضة عند الإطلاق)، CRM خارجي (مستقبلي)، أنواع Phase 2/3.
