# 11 — حجز الاستشارات

## 1. ماذا تفعل الميزة

- حجز موعد استشارة من فترات يحددها المسؤول داخليًا (المزوّد الافتراضي `InternalAvailabilityProvider`؛ لا اقتران بـ Google/M365/Calendly).
- **مغلق افتراضيًا:** لا فترات حتى يُفعّل المسؤول الحجز ويضبط طول الفترة والأفق وساعات أسبوعية. حينها يرى الزائر نموذج «طلب استشارة» بدلًا من الحجز.
- الأوقات تُخزن UTC؛ منطقة العمل قابلة للضبط (افتراضي `Asia/Riyadh`).
- **منع الحجز المزدوج** مرتين: فحص الفترة في الخدمة + فهرس فريد على `start_utc` للحجوزات المؤكدة (يحسم الطلبات المتزامنة → «الموعد غير متاح»).
- رابط إدارة موقّع: رمز 256-bit عشوائي، **التجزئة فقط** مخزنة، الرمز يُرسل في جسم الطلب لا في الرابط النهائي، وينتقل للحجز البديل عند إعادة الجدولة.
- معاملة واحدة تخزن: الحجز + `Inquiry(Consultation)` + Lead + بريد تأكيد الزائر **مع ملف ‎.ics** + أحداث الـ outbox.

## 2. الملفات

| الطبقة | المسار |
|---|---|
| Domain | [src/Onyx.Domain/Booking/](../../../src/Onyx.Domain/Booking/) — `BookingSettings` (صف وحيد، `IsBookable`)، `AvailabilityRule` (نوافذ أسبوعية على حدود 5 دقائق، `Overlaps`)، `AvailabilityException` (تواريخ محجوبة)، `ConsultationBooking` (`Confirm`، `Cancel`، `MarkRescheduled`، `RecordOutcome`، `EraseVisitorAccess`، `CanBeManagedByVisitor`)، `BookingStatus`، الأحداث |
| Application | [src/Onyx.Application/Booking/](../../../src/Onyx.Application/Booking/) — `BookingService`، `SlotCalculator`، `InternalAvailabilityProvider`، `InternalBookingCalendarProvider` (لا يفعل شيئًا)، `BookingMessages`، `IcsCalendar`، `BookingAdministrationService`، `AvailabilityAdministrationService`، المعالجات |
| Infrastructure | `BookingRepository`، `AvailabilityRepository`؛ قوالب `BookingConfirmedEmailTemplate`، `BookingCancelledEmailTemplate`، `BookingInternalEmailTemplate`؛ `ConsultationBookingConfiguration.ConfirmedSlotIndex` |
| API | [ConsultationsController](../../../src/Onyx.API/Controllers/ConsultationsController.cs)، [AdminBookingsController](../../../src/Onyx.API/Controllers/Admin/AdminBookingsController.cs)، [AdminAvailabilityController](../../../src/Onyx.API/Controllers/Admin/AdminAvailabilityController.cs) |
| Web | [ConsultationController](../../../src/Onyx.Web/Controllers/ConsultationController.cs) (احجز-استشارة / إدارة-الحجز)، Admin [BookingsController](../../../src/Onyx.Web/Areas/Admin/Controllers/BookingsController.cs)، [AvailabilityController](../../../src/Onyx.Web/Areas/Admin/Controllers/AvailabilityController.cs) |

## 3. التدفق

```
GET availability → InternalAvailabilityProvider → SlotCalculator.Calculate(settings, rules, blockedDates, busy, now)
   شبكة من النوافذ الأسبوعية بتوقيت العمل؛ يُزال: المحجوب، داخل مهلة الإشعار، بعد الأفق، ما يلامس حجزًا مؤكدًا (+ الهامش على الجانبين)؛
   الأوقات غير الموجودة (فجوة التوقيت الصيفي) تُتخطى
POST /api/v1/consultations (BookConsultationRequest: Form + StartUtc)
 → BookingService.BookAsync:
     غير قابل للحجز → Booking.Closed (409)
     InquiryIntake.PrepareAsync(Consultation, form, "booking")  ← نفس خط النماذج (honeypot، الموافقة، Turnstile)
     الفترة يجب أن تطابق تمامًا فترة متاحة → وإلا Booking.SlotUnavailable
     InquiryRecorder.RecordAsync → ConsultationBooking.Confirm(… hash(token), expiry = end + ManageLinkLifetime) → حدث BookingConfirmed
     نشاط BookingConfirmed على الـ Lead؛ بريد التأكيد (BookingMessages.Confirmed + ‎.ics + رابط الإدارة) في نفس المعاملة
     SaveAsync: تعارض الفهرس الفريد (SlotTakenCode) → Booking.SlotUnavailable
 ← 201 BookingConfirmedResponse (مرجع، البداية/النهاية UTC والمحلية، المنطقة)
Worker: BookingConfirmedHandler (مفتاح "booking-confirmed:{id}") → calendar.BookingConfirmedAsync (لا شيء افتراضيًا)
        + بريد داخلي + إشعار NewBooking (Bookings.View)
        InquirySubmittedHandler يتخطى نوع Consultation (لا إعلان مزدوج)
```

**إدارة الزائر** (`manage`، `manage/cancel`، `manage/reschedule`، `manage/calendar`): الرمز يُتحقق من شكله ثم يُبحث بالتجزئة؛ الحجز نشط وقبل انتهاء الرابط. الإلغاء/إعادة الجدولة فقط قبل بدء الموعد (`CanBeManagedByVisitor`). إعادة الجدولة تنشئ حجزًا جديدًا (`RescheduledFromId`) وتعلّم القديم `Rescheduled`، والرابط نفسه يبقى صالحًا. صفحة Web تنقل الرمز من الرابط إلى الخادم ولا تُفهرس.

**الموظفون:** `BookingAdministrationService.CancelAsync` (اختياريًا إبلاغ الزائر) و`RecordOutcomeAsync` (`Completed`/`NoShow` فقط بعد بدء الموعد — `Booking.OutcomeTooEarly`)؛ تدقيق `BookingStatusChanged`. `AvailabilityAdministrationService`: الإعدادات (`BookingSettingsChanged`)، نوافذ بلا تداخل (`Availability.Overlap`)، تواريخ محجوبة اليوم أو لاحقًا (`AvailabilityChanged`).

## 4. الإعدادات والحدود (Domain)

طول الفترة 15–240 دقيقة بخطوات 5؛ الهامش 0–120 دقيقة؛ الإشعار 0–336 ساعة؛ الأفق 1–180 يومًا؛ منطقة IANA صالحة. لا قيم مسبقة: «مدخلات عمل» يضعها المسؤول (CLAUDE.md؛ `docs/architecture.md` §13.4).

## 5. الاختبارات

- [BookingTests](../../../tests/Onyx.Domain.Tests/Booking/BookingTests.cs)، [SlotCalculatorTests](../../../tests/Onyx.Application.Tests/Booking/SlotCalculatorTests.cs) (النوافذ بتوقيت العمل، الإشعار والهامش والأفق، فجوات التوقيت الصيفي)، [IcsAndCsvTests](../../../tests/Onyx.Application.Tests/Booking/IcsAndCsvTests.cs) (UTC، CRLF، الطي على حدود الأحرف، ترميز رابط الإدارة).
- [BookingApiTests](../../../tests/Onyx.API.IntegrationTests/Leads/BookingApiTests.cs) — مغلق حتى الإعداد، كل شيء في معاملة، **حجزان متزامنان لنفس الفترة يؤكدان واحدًا فقط**، رمز Turnstile لإجراء `booking`، الرابط الموقّع، النتائج بعد البدء فقط، عدم تداخل النوافذ.
- [LeadNotificationWorkerTests](../../../tests/Onyx.API.IntegrationTests/Leads/LeadNotificationWorkerTests.cs) (`Bookings_notify_staff_through_the_worker_and_a_consultation_is_not_announced_twice`).
- Web: [LeadFormsSiteTests](../../../tests/Onyx.Web.Tests/LeadFormsSiteTests.cs) (`Without_online_slots_the_booking_page_sends_a_consultation_request`، `The_manage_page_is_private_and_sends_the_token_only_in_request_bodies`)، [AdminLeadsTests](../../../tests/Onyx.Web.Tests/AdminLeadsTests.cs).

## 6. خريطة أثر التغيير

| التغيير | راجع |
|---|---|
| خوارزمية الفترات | `SlotCalculator` + `InternalAvailabilityProvider.FindSlotAsync` + `SlotCalculatorTests` |
| مزوّد تقويم خارجي | نفّذ `IAvailabilityProvider`/`IBookingCalendarProvider` في Infrastructure (يحتاج موافقة حزمة ومالك) واستبدل التسجيل في `AddLeads`؛ لا أنواع مزوّد في Application |
| حالة حجز جديدة | `BookingStatus` (إلحاق)، فلتر الفهرس الفريد (`status = 'Confirmed'`)، `IsActive`، `BookingAdministrationService.ListAsync`، الاحتفاظ (الحجوزات القادمة تحمي الـ Lead من الحذف) |
| محتوى البريد/‎.ics | `BookingMessages`، `IcsCalendar`، القوالب، حد المرفقات في `EmailRequest` |

## 7. الحالة

- **منفّذ:** الحجز الداخلي كاملًا.
- **مُعطّل حتى الإعداد:** يحتاج مسؤولًا يضبط الإعدادات والساعات (`Availability.Manage`).
- **مؤجّل:** مزامنة التقويم الخارجية (مستقبلية، تحتاج موافقة). `BookingStatus.Requested` محجوز لمزوّد «اعتماد أولًا» مستقبلي.
