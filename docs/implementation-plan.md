# Onyx — Implementation Plan (Scaffolding + Phase 1)

**Approved:** 2026-10-03. Rules: `CLAUDE.md`. Design: `docs/architecture.md` (Rev 4).

## Working rules for every stage
- **Done means:** solution builds with **0 warnings** (warnings are errors) → all required tests pass (architecture tests included) → `dotnet list package --vulnerable --include-transitive` is clean → **one descriptive Git commit**. Never commit secrets.
- Each stage ends with a report: what was built, test results, data model and migrations added, packages added (exact versions), deviations, and decisions needed.
- Migrations are created only in the stage that introduces the entities, on the approved conceptual model.
- New packages beyond the approved list are requested at the stage that needs them.
- **Checkpoints:** **STOP after every stage** for owner review unless the owner explicitly authorizes several stages together.
- **Backend first, visual design last (owner decision, 2026-10-03).** All backend, infrastructure, admin/back-office capabilities, integrations and core functionality are built **before** any public UI/visual design work.
  - Stage 5 (design system) is **deferred** until Stages 6–12 are complete; its design-approval checkpoint moves with it.
  - Until then, public pages built in Stages 6–12 are **functional, semantic, unstyled templates**: correct HTML, RTL/LTR, SEO markup, accessibility. No visual design, no design tokens, no brand styling.
  - Admin screens use minimal neutral styling so they are usable.
  - **No public visual design is implemented before the owner approves the design direction in Stage 5.**

### Execution order
`0 → 1 → 2 → 3 → 4 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 5 (design system + approval) → 13`

## Status
| Stage | Status |
|---|---|
| 0 Repository and solution skeleton | **done**, commit `85f6016` |
| 1 Backend foundation and test infrastructure | **done**, commits `bca0181`, `c9d0e2a` (Asp.Versioning.OpenApi follow-up) |
| 2 Worker, outbox, email, notifications | **done**, commit `5529048` |
| 3 Identity, permissions, audit | **done**, commit `6301cc3` |
| 4 Onyx.Web foundation | **done**: 334/334 tests (local PostgreSQL 18); build 0 warnings; no vulnerable packages. Awaiting owner review before Stage 6. |
| 6–12, 5, 13 | awaiting approval (in execution order) |

---

### Stage 0 — Repository and solution skeleton
**Build:**
- `git init`, `.gitignore` (incl. user-secrets, `.env`, local settings), `.gitattributes`, `.editorconfig`.
- `global.json` (SDK 10.0.x), `Directory.Build.props` (net10.0, nullable, warnings as errors, analyzers), `Directory.Packages.props` (central versions).
- `Onyx.slnx` with `src/` (Domain, Contracts, Application, Infrastructure, API, Web, Worker) and `tests/` (Domain, Application, API.Integration, Web, Architecture). Project references exactly per CLAUDE.md §5. `dotnet-ef` local tool manifest.
- Architecture tests: project-reference rules (from the `.csproj` files) + type-dependency rules (NetArchTest).

**Verify:** clean build; architecture tests pass; a deliberately forbidden reference makes them fail (then reverted); vulnerable-package scan clean. **Commit.**

### Stage 1 — Backend foundation and test infrastructure
**Build:**
- **Domain:** `Entity`, `AuditableEntity`, `ISoftDeletable`, `IAuditable`, `[SensitiveData]`/`[NotAudited]`, domain events, `Result`/`Error`, and the `AuditLog` entity.
- **Contracts:** `PagedResult<T>`, the error/problem contract, and the full **permission catalogue**.
- **Application:** DI entry point, `ICurrentUser`, `IRequestContext`.
- **Infrastructure:**
  - `ApplicationDbContext` (Npgsql), with the in-house snake_case convention and the soft-delete query filter;
  - interceptors: audit-field stamping, soft delete, domain events → outbox, entity audit log with redaction;
  - outbox and audit-log tables, plus the design-time factory and **first migration**;
  - validated options and a DB health check.
- **API:**
  - Serilog with correlation id; a global `IExceptionHandler` → ProblemDetails with no stack traces outside Development;
  - security headers and server-header removal; HTTPS/HSTS; request size limits; deny-by-default CORS; forwarded-headers settings;
  - API versioning; OpenAPI + Swagger UI in Development only;
  - built-in rate-limiting policies; health endpoints (live/ready); `ICurrentUser`/`IRequestContext` implementations.
- **Tests:**
  - `ITestDatabase` + `LocalPostgresTestDatabase` with all safety guards, and the API `WebApplicationFactory`;
  - test-only controllers to exercise error handling and rate limiting;
  - a test-only DbContext for interceptor verification.

**Verify:**
- guards reject unsafe connection strings, superuser roles, foreign names, and databases this run didn't create;
- the test DB is created, migrated and dropped;
- ProblemDetails has no stack trace in Production; security headers are present; 429 on rate limit; 413 on oversized body; no CORS headers;
- live/ready health checks work; correlation id round-trips;
- interceptors stamp audit fields, soft-delete, write outbox and audit rows, and redact sensitive data;
- unit tests for `Result`, domain events and the permission catalogue.

**Commit. → STOP for owner review.**

### Stage 2 — Worker, outbox, email, notifications
- **Build:**
  - Worker host with `JobDefinition`/`JobRun` (and migration) and an in-house schedule parser;
  - lease-based claiming with `FOR UPDATE SKIP LOCKED`; outbox dispatcher with retries and a dead-letter state;
  - `IEmailSender` + SMTP adapter (**MailKit — approval requested then**) + a Development pickup-directory sender, `EmailMessage` outbox, and Arabic/English templates;
  - `AdminNotification` + read state; generic `ProviderConnection` status model.
- **Verify:** no double processing with two Worker instances; retry/dead-letter; queued email survives transport failure; notification visibility per permission.

**Stage 2 delivered notes:**
- Domain: `AdminNotification`, `NotificationReadState`, `ProviderConnection` (xmin concurrency, failure event on transition only).
- Worker: `BackgroundService` loops, `JobDefinition`/`JobRun`, in-house schedules (`every:Ns|m|h`, `daily@HH:mm` UTC), `SKIP LOCKED` lease claiming with owner fencing, retries + dead letters for outbox and email. Internal health endpoint on loopback.
- Email: MailKit SMTP, Development pickup directory, unconfigured mode (queued, provider NotConfigured), code-owned AR/EN templates with HTML encoding.
- **Deferred:**
  - Notification Center and provider-connection **admin API endpoints** move to Stage 3, since they need authentication and permissions.
  - **Admin-editable email copy** (`EmailTemplate` entity) moves to Stage 6 (CMS). Stage 2 templates are code-owned.
  - The only production template is `system.provider-test`. Feature templates arrive with their stages.

### Stage 3 — Identity, permissions, audit
- **Build:**
  - Identity (uuid users), lockout, email confirmation, password reset, TOTP 2FA (required for SuperAdmin);
  - JWT access + rotating hashed refresh tokens (grace window, family revocation), security-stamp checks;
  - `[HasPermission]` policy provider with server-side permission cache;
  - permission and role seeders; one-time SuperAdmin bootstrap command from environment variables (no default password);
  - users, roles and audit-log admin APIs.
- **Verify:** 401/403 matrix; refresh reuse revokes the family; lockout/2FA; security events audited with no secrets.

**Stage 3 delivered notes:**
- **Identity and authentication:** admin login, the second factor, mandatory authenticator enrolment, refresh/rotation/reuse detection, logout, forgot/reset password, invitation acceptance, change password, `/me`.
- **Admin APIs:** users, roles, permissions catalogue, audit logs, plus the Stage 2 deferrals: notifications and provider connections (list, enable, disable, email test).
- **Operator commands:** `seed-identity` and `bootstrap-superadmin`, with the password from an environment variable.
- **Migration** `IdentityAndSessions` is additive only (12 tables, 12 indexes).
- **Deferred:**
  - Turnstile on admin sign-in moves to Stage 8 (when Turnstile is introduced).
  - Self-service authenticator enrolment for non-privileged users, an admin "active sessions" list, and email-address change are not built.
  - Data-subject export/delete of identity data moves to Stage 9.

### Stage 4 — Onyx.Web foundation
- **Build:**
  - per-language route tables (Arabic segments/slugs, English segments/slugs), root → `/ar/`, language switcher, canonical/hreflang helpers, RTL/LTR layouts;
  - typed API clients + handlers + resilience;
  - BFF cookie per §25.1 (encrypted ticket, refresh, revocation, Data Protection key persistence);
  - antiforgery, CSP nonces, cookie consent (receipts via API);
  - Admin shell (login, 2FA, permission-aware navigation, notifications badge); minimal neutral styles.
- **Verify:** Arabic URL resolution and encoding; redirects; hreflang/canonical; CSRF; cookie flags and size budget; end-to-end login/refresh/logout with no token reaching the browser; analytics scripts absent without consent.

**Stage 4 delivered notes:**
- **Public foundation:**
  - `/` → `/ar/` (or the explicit `onyx.lang` choice); `/{ar|en}/` home shell (site name only; no invented content).
  - Language switcher and an Accept-Language *suggestion* (never a redirect).
  - Canonical, hreflang (ar/en/x-default) and `<html lang dir>`; localized 404/500 pages.
  - `LocalizedRouteTable` + dynamic route transformer ready for Arabic/English segments and slugs (pages register in Stage 6).
  - `Site:AllowIndexing=false` adds `noindex` until launch.
- **Cookie consent:** banner (Necessary / Analytics / Marketing), `onyx.consent` cookie versioned by `Consent:PolicyVersion`, and a server-side receipt via `POST /api/v1/consents`. Receipts are append-only, with the IP stored only as an optional keyed hash. No analytics scripts exist yet; they must check `ConsentState`.
- **Admin:**
  - BFF sign-in with 2FA and enrolment.
  - Refresh, revocation handling, logout, change password, forgot/reset password, invitation acceptance.
  - Admin shell with a permission-filtered menu, dashboard placeholder, Notification Center (list, mark read, unread badge).
  - Admin UI language follows the admin's preference.
- **Security:** CSP with per-request nonces, security headers, global antiforgery, `__Host-` cookies, `no-store` on admin pages, `no-referrer` on account pages, token-stripping redirects for emailed links, `/admin`-only return URLs.
- **Web↔API:** typed clients with resilience (POSTs never retried). Visitor IP sent as `X-Forwarded-For` (Web resolves it via its own trusted-proxy list), plus correlation id and language. API outages become a service message or a 503 page and never sign the admin out.
- **Migration** `CookieConsentReceipts` is additive only (1 table, 2 indexes).
- **Deferred:**
  - QR code for authenticator enrolment: delivered as a Stage 4 follow-up (`Net.Codecrete.QrCodeGenerator` 3.2.1, approved), with the text key kept as a fallback.
  - Visual design (Stage 5).
  - Turnstile on sign-in and forms (Stage 8).
  - Analytics script loading (Stage 12).
  - An admin "active sessions" view.

**Stage 4 approved decisions (owner, 2026-10-04):**
- **Indexing:** the site stays `noindex` throughout development (`Site:AllowIndexing=false`). Indexing is enabled only at launch.
- **Cookie consent lifetime:** 180 days, configurable (`Consent:CookieLifetimeDays`).
- **Admin sign-in language:** anonymous Admin authentication pages (sign-in, 2FA, forgot/reset password, invitation) default to Arabic, with English switching available.
- **Client IP and privacy:**
  - The real IP is **not** stored permanently for ordinary page visits.
  - Consent receipts store only a keyed hash (HMAC-SHA256) of the IP, and only when `Privacy:IpHashKey` is configured; otherwise no IP is stored.
  - The architecture may capture and use the real client IP where legitimately needed: for security (rate limiting, abuse prevention, audit of security events) and for important conversion events (leads, bookings, tool conversions). This is subject to consent, the privacy policy, retention rules and provider requirements, decided in the stages that build those features.
- **Authenticator enrolment:** QR-code enrolment is approved. It encodes the existing `otpauth://` URI of the current enrolment attempt only (no second secret, no separate flow), with the textual setup key kept as a fallback.

### Stage 5 — Design system → **STOP for design approval** *(deferred: runs after Stage 12, before Stage 13)*
- **Build:** white-first tokens; Arabic + English type pairing; grid; core components (nav, mega menu, footer, buttons, forms, sections); Onyx stone signature (SVG first) based on the **real logo** (no replacement logo); reduced motion; performance budget; **preview page**.
- **Verify:** RTL/LTR side by side, keyboard and contrast checks, page-weight budget.

### Stage 6 — CMS core and public structure
- **Build:** translations, media (**image library approval requested then**), pillars, services (+ use cases, FAQs), industries (taxonomy/published with the content minimum), typed page sections, CTAs, settings, offices, markets, redirects, SEO metadata; Admin screens; public home, pillars, services, industries, about, offices, legal page shells (unpublished); sitemaps, robots, Organization JSON-LD; seeders (languages, markets, pillars with proposed Arabic names for confirmation, Riyadh – Granada District, Amman).
- **Verify:** publish requires both languages; slug change → 301; unpublished hidden; sitemap/hreflang validity; permission matrix; cache invalidation.

### Stage 7 — Work, proof, insights, glossary, integrations
- **Build:** projects, case studies, capabilities, testimonials, metrics, with the proof guard and the 3-featured limit; insights with scheduled publishing and the AI-approval guard; topics, glossary, integrations showcase.
- **Verify:** guards can't be bypassed through the API; scheduled publishing runs via the Worker.

### Stage 8 — Leads, booking, Turnstile, attribution
- **Build:** Inquiry → Lead with attribution (consent-aware); wizard + contact/consultation/service/similar-project forms; server-side Turnstile, honeypot, rate limits; booking (availability rules, slots, `.ics`, signed links, uniqueness); internal + visitor emails in the submission language; Admin leads (pipeline, origin panel, notes, assignment, audited export).
- **Verify:** inquiry persists when email fails; Turnstile rejection; concurrent double booking blocked; attribution with/without consent.

### Stage 9 — Privacy operations
- **Build:** retention policies (configurable, no hard-coded periods), enforcement job, data-subject export/delete, audit.
- **Verify:** exact delete/anonymize per policy; complete export; deletion completeness.

### Stage 10 — Site search (PostgreSQL)
- **Build:**
  - `SearchDocument` with event-driven indexing;
  - shared `ArabicTextNormalizer`; normalized columns;
  - weighted generated `tsvector` (`arabic` / `english` + `simple` for names) with GIN indexes;
  - `pg_trgm` extension (migration) + trigram GIN index on titles;
  - `websearch_to_tsquery` querying with bound parameters; ranking (`ts_rank_cd` × type boost × freshness + trigram);
  - grouped results page (`noindex`), rate limits, length caps.
- **Verify:** the `arabic` configuration and ICU locale on the server (fallback `simple` documented if absent); Arabic queries robust to diacritics/alef/ta marbuta/alef maqsura/hamza variants and Arabic-Indic digits; English stemming; exact brand names; typo tolerance; unpublished never returned; SQL-injection attempts treated as text.

### Stage 11 — News Hub
- **Build:** SafeOutboundHttp; sources/items; feed reading (**RSS library approval requested then**); normalize, dedupe, classify; `ITextGenerationService` (inactive → items Held); Admin news/sources; public news (items `noindex` by default); homepage section.
- **Verify:** SSRF suite; dedupe; no auto-publish without a summary; no full text stored; attribution on every item.

### Stage 12 — Admin Analytics & Marketing
- **Build:** GA4 + Search Console adapters (**Google API package approval requested then**; inactive until credentials), sync jobs, local aggregates, dashboards, funnel view; consent-gated GA4 tag + conversion events.
- **Verify:** fake adapters end to end in tests; "Not connected" with no credentials and never sample data.

### Stage 13 — Hardening and launch readiness
- Security review (permission matrix, CSP, rate limits, uploads), Core Web Vitals and caching, WCAG 2.2 AA audit, SEO audit, production configuration checklist, backup/recovery runbook (PostgreSQL PITR), README, host-neutral deployment notes.

## Owner inputs by stage
| Needed for | Input |
|---|---|
| Running integration tests (from Stage 1) | PostgreSQL test role + `ONYX_TEST_POSTGRES` (`docs/development-setup.md`) |
| Running the API locally | `onyx_dev` database + roles + user-secrets connection string |
| Stage 5 | Final logo vector (SVG); Arabic lockup if any |
| Stage 8 | Booking hours/format; info email address(es); Turnstile keys |
| Stage 11 | LLM provider; news source allowlist |
| Stage 12 | GA4 / Search Console access |
| Before launch | Content, legal texts, retention periods, office details |
