# 19 — الـ Worker والمهام والـ Outbox (ملخص الميزة)

> التفاصيل الكاملة (الخيارات، كل مهمة، الدورات، خريطة الأحداث→المعالجات، استكشاف الأخطاء) في **[BACKGROUND-JOBS-AR.md](../BACKGROUND-JOBS-AR.md)**. هذا الملف يلخّص الميزة بصيغة ملفات الميزات.

## 1. ماذا تفعل

مضيف خلفي واحد (`Onyx.Worker`) يعالج: الـ outbox (أحداث المجال → المعالجات)، إرسال البريد، و**7** مهام مجدولة مسجلة في `AddWorkerProcessing`: التنظيف التشغيلي، جدولة المحتوى، الاحتفاظ، إصلاح فهرس البحث، استيعاب الأخبار، مزامنة GA4، مزامنة Search Console. (اختبار `JobSchedulingTests` يتوقع 8 لأنه يسجّل `TestJob` إضافية.)

لا يوجد سطح HTTP عام؛ فقط فحوص صحة على loopback. الـ Web والـ API لا ينفذان أي عمل طويل (أخبار، AI، تكاملات، أدوات) بشكل متزامن — يذهب للـ Worker (CLAUDE.md §5).

## 2. الآلية المعتمدة

`BackgroundService` مدمجة + جداول `job_definitions`/`job_runs`/`outbox_messages`/`email_messages` + المطالبة بـ `FOR UPDATE SKIP LOCKED` مع عقد إيجار (مالك + انتهاء؛ العقود المنتهية قابلة للاستعادة) ⇒ آمن متعدد النسخ وقابل للنقل. الجداول تُحلَّل داخليًا (`every:…`، `daily@HH:mm` UTC). التراجع الأسي وحالة Dead-letter. **لا Hangfire ولا Quartz.NET.**

## 3. الملفات الرئيسية

[WorkerServiceCollectionExtensions.cs](../../../src/Onyx.Infrastructure/WorkerServiceCollectionExtensions.cs)، [Onyx.Worker/Program.cs](../../../src/Onyx.Worker/Program.cs)، [BackgroundJobs/](../../../src/Onyx.Infrastructure/BackgroundJobs/)، [Jobs/ (Application)](../../../src/Onyx.Application/Jobs/)، [Persistence/Outbox/](../../../src/Onyx.Infrastructure/Persistence/Outbox/)، [Persistence/Interceptors/](../../../src/Onyx.Infrastructure/Persistence/Interceptors/).

## 4. الاختبارات

[tests/Onyx.API.IntegrationTests/Worker/](../../../tests/Onyx.API.IntegrationTests/Worker/) — `JobSchedulingTests`، `OutboxProcessingTests`، `EmailProcessingTests`، `WorkerCompositionTests`، `NotificationVisibilityTests`؛ و[JobScheduleTests](../../../tests/Onyx.Application.Tests/Jobs/JobScheduleTests.cs)، [RetryBackoffTests](../../../tests/Onyx.Application.Tests/Jobs/RetryBackoffTests.cs).

## 5. الحالة

- **منفّذ:** كل ما سبق.
- **لا يوجد:** واجهة إدارة لعرض `job_runs` أو تعديل الجداول/تعطيل المهام (تتم عبر قاعدة البيانات مباشرة حاليًا)، ولا تنفيذ أدوات Phase 2.
