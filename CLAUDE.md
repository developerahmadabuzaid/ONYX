# CLAUDE.md — Onyx Vanguard Solutions Website

Permanent engineering, architecture, business and content rules for this repository. Follow them in every task.
If a request conflicts with these rules, stop and raise the conflict instead of quietly breaking a rule.

> **Project status:** Architecture revision 4 (owner decisions of 2026-10-03). Implementation follows `docs/implementation-plan.md` **stage by stage**.
> **Review checkpoints:** stop after **every** stage for owner review unless the owner explicitly authorizes several. Never start a stage the owner has not approved.
> **Backend first:** backend, infrastructure, admin capabilities, integrations and core functionality come before any public UI/visual design. Stage 5 (design system + design approval) is deferred until after Stage 12. Until then, public pages are functional, unstyled semantic templates only. **Never implement public visual design before the owner approves it** (`docs/implementation-plan.md`).
> **Current priority (2026-10-13):** the Admin area and the CMS-rendered public pages have been brought onto the shared design system (`tokens.css`, §16), and the public site has been re-skinned. Admin behaviour and CMS management are functionally complete; further work follows the open items in §29.
> Full design: `docs/architecture.md`. Items marked **[OPEN]** there (§29) are content, credential or legal inputs. Ask about them; never guess them.
>
> **Phase 1 (launch) scope** includes the bilingual CMS site, Work, industries, insights, booking, leads + email, consent, retention, search, notifications, audit, the Worker, **the News Hub, and the Admin Analytics & Marketing dashboard (GA4 + Search Console)**. Phase 2: tools + AI Content Engine. Phase 3: AI Advisor, ad platforms, market pages (`docs/architecture.md` §28).

---

## 1. Product and positioning

- **Onyx Vanguard Solutions (brand: ONYX)** is a professional technology company with a broad corporate identity. Its general marketing and SEO copy is **not positioned around a country or region**: do not repeatedly target or mention Saudi Arabia, the GCC, the Gulf or any other geography in marketing copy, headings, CTAs, SEO titles or descriptions, and do not replace them with another region. (Owner decision of 2026-10-10.)
- Public offices: **Riyadh, Granada District (KSA)** and **Amman (Jordan)** only. Never imply any other office. These verified locations stay in the footer, contact information and the About page's office details; legal text may name them where legally relevant.
- **Branding:** always "ONYX" / "Onyx Vanguard Solutions" ("أونيكس" in Arabic copy). **Never use "ONYX LIFE".** Use only the official logo supplied by the owner; never generate a substitute.
- Audience: SMEs, mid-market and enterprise. Never position Onyx exclusively for one segment.
- **Five integrated pillars:** Software & IT · AI Solutions · Automation · Cybersecurity · Digital Growth & Creative. One connected offering, never five unrelated businesses.
- **Core message:** Onyx understands the business, its processes and its problems first, then designs technology around it. Onyx works across industries and never implies a closed industry list.
- The site is a **corporate site + interactive technology experience + lead-generation platform**. AI is a major pillar. SEO is a flagship capability backed by Onyx's own SEO platform.
- **No public WhatsApp CTA or button at launch.** WhatsApp is a future integration point only. "AI + WhatsApp" may be described as a service capability.

## 2. Business and content rules (permanent, non-negotiable)

### 2.1 Proof policy
All public proof must be **real and verified**. Never create, invent or use as a placeholder in production:
clients, logos, testimonials, reviews, case studies, statistics, outcomes, metrics, partnerships, certifications, awards, offices, team size, years of experience, project names, client names, screenshots, features, technologies, integrations, dates.
- Missing information means the section is built but **hidden**, never padded.
- Proof-bearing content needs a recorded verification (who, when, source, client permission) before publishing. This is a domain rule.
- Development-only sample data must be obviously fictional, marked as sample, and never seeded or deployed to production.

### 2.2 Portfolio and product research
- Confirmed portfolio: **سجل الطبي / Medical Record System** (KSA and Jordan editions) · **Creative Talent Marketplace** (name pending) · **TireZone** (mytirezone.com, US) · **DealGo** · **Al Baraka** · **إيثاق العقارية / Ethaq Real Estate**. Facts and claim boundaries are in `docs/architecture.md` §9.
- **Competitor benchmarking is internal product research only:** benchmark → compare with the actual implementation → build approved features → only then claim them publicly. "Competitor has X" never means "Onyx has X".
- Research lives in `docs/product-research/` and is never rendered on the site. Competitors are never named in public content without explicit owner approval. No "most complete" or "all features" claims without evidence.
- TireZone's AI gives **possible explanations / preliminary guidance**, never "definitive mechanical diagnosis".
- DealGo: no invented performance, volume, user or outcome numbers.
- **Full project content lives only in Work.** Service and industry pages may show one small contextual link to a related case study. The homepage shows **max 3** featured projects.

### 2.3 Services and capabilities
- Publish a service only when the owner confirms Onyx offers it.
- AI pillar: core AI services may have their own pages. AI + WhatsApp / CRM / existing systems are **use-case sections** inside the relevant service page. They become standalone pages only with real demand data, differentiated content, and no duplication.
- Claim an integration only when it is delivered or supported and verified. Never describe tool or platform functionality that doesn't exist.

### 2.4 Pricing and conversion
- **No public pricing for services:** no fixed prices, packages, "starting from" or SEO prices for services.
- **SaaS product plans (owner decision 2026-10-27):** a plan's price may be shown publicly only when an admin enters a real price and marks it public. Prices are never seeded, guessed or shown as placeholders.
- Conversion actions: Start a Project · Book a Consultation · Request Consultation · Contact / Talk to Onyx · Analyze Your Website · Check Your Security Posture · Discover AI/Automation Opportunities. (No WhatsApp at launch.)

### 2.5 SEO integrity
- **No doorway or thin pages.** No country, city or industry page without genuinely differentiated, useful content. Market pages need real local content. Industry pages are indexable only when they meet the content minimum (§9 below). There are no service × industry × city combination pages.
- Structured-data addresses: Riyadh (Granada District) and Amman only. `areaServed` may list served countries.

### 2.6 Copy quality and editorial standards
- Website copy is professional, natural, persuasive and **customer-focused**: explain what the client gains, not only what is built. Avoid robotic phrasing, empty buzzwords ("cutting-edge", "advanced solutions", "digital transformation" as filler), repeated introductions and needless repetition of the brand name.
- **Arabic and English are equally polished.** Arabic is clear, natural Modern Standard Arabic for business owners and executives (no slang, no literal translation); English is written independently, not translated word for word.
- Technical expertise stays visible (services, offerings, technologies, methods) but is explained through business value, so non-technical visitors are not overwhelmed.
- **Preserve content depth, service coverage and SEO quality.** Editorial work refines language; it never deletes services, sub-services, anchors or useful detail to make pages shorter.
- Content should be manageable through the CMS wherever appropriate (see §9 "Site pages").

### 2.7 Honest claims
- Never say "unhackable", "fully secure" or "guaranteed", and never promise business results.
- Tools give observations and recommendations. No fake precision, no fabricated finding counts.
- **Never fabricate SEO metrics** (search volume, keyword difficulty, ranking probability, traffic forecasts). Quantitative metrics are shown only from an integrated real source, with that source and the date stated.
- The "How it all works together" scenario is labelled illustrative.

## 3. Technology baseline

- .NET 10 (LTS), C# latest, nullable enabled, implicit usings, warnings as errors.
- ASP.NET Core Web API (controllers) + ASP.NET Core MVC (server-rendered Razor). Admin is an `Admin` area in Onyx.Web, built with MVC + light JavaScript.
- EF Core (Code First) + **PostgreSQL** via `Npgsql.EntityFrameworkCore.PostgreSQL`. Onyx has its own dedicated database. **No SQL Server packages or SQL Server-specific features.** Conventions: `docs/architecture.md` §3.2 (snake_case naming via in-house convention, `timestamptz` UTC, `jsonb`, `xmin` concurrency, `SKIP LOCKED` queues).
- **Portable deployment:** no hosting-provider coupling (no Azure/AWS-specific dependencies). The owner handles deployment later.
- Central Package Management, `Directory.Build.props`, `global.json`.
- **Approved packages:**
  - Data and identity: Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore(.Design/.Relational), dotnet-ef (local tool), Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.Authentication.JwtBearer.
  - Email: MailKit (SMTP transport; includes MimeKit).
  - API and libraries: Asp.Versioning.Mvc(.ApiExplorer), Asp.Versioning.OpenApi, Microsoft.AspNetCore.OpenApi, Swashbuckle.AspNetCore.SwaggerUI (Development only), FluentValidation (+DependencyInjectionExtensions), Microsoft.Extensions.Http.Resilience, HtmlSanitizer, Serilog.AspNetCore.
  - Web: Net.Codecrete.QrCodeGenerator (Onyx.Web only; authenticator enrolment QR code).
  - Tests: xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing, NetArchTest.Rules.
  - Exact versions live in `Directory.Packages.props`.
  - Packages for later stages (SMTP, image processing, RSS, Google APIs, …) need approval when their stage is reached. Avoid packages with unnecessary commercial licensing (e.g. FluentAssertions v8+).
- **Do not add** AutoMapper, MediatR, Hangfire or Quartz.NET (background processing is decided: built-in `BackgroundService` + DB tables). Ask before adding any NuGet package.

## 4. Solution structure

```
OnyxWebsite/
├── src/
│   ├── Onyx.Domain/            # Entities, value objects, enums, domain rules, domain events. No dependencies.
│   ├── Onyx.Contracts/         # API DTOs, error contract, paging, permission names. No dependencies.
│   ├── Onyx.Application/       # Use cases, services, validators, mapping, ports.
│   ├── Onyx.Infrastructure/    # EF Core, Identity, repositories, interceptors, provider adapters, SafeOutboundHttp, engines.
│   ├── Onyx.API/               # REST controllers, auth, rate limiting, Turnstile verification, OpenAPI, composition root.
│   ├── Onyx.Web/               # Public site (/ar, /en) + Admin CMS area. HTTPS REST to Onyx.API only.
│   ├── Onyx.Worker/            # Phase 1 background host: outbox, email, scheduling, news, AI drafting, sync, retention, tools.
│   └── Onyx.Host/              # The single deployable app: runs Onyx.Web publicly and Onyx.API on loopback in one process (+ optional Worker jobs).
├── tests/  Onyx.Domain.Tests · Onyx.Application.Tests · Onyx.API.IntegrationTests · Onyx.Web.Tests · Onyx.ArchitectureTests
└── docs/   architecture.md · product-research/ (internal)
```

## 5. Dependency rules (enforced by architecture tests)

| Project | May reference | Must NEVER reference |
|---|---|---|
| Onyx.Domain | nothing | any project; EF Core; ASP.NET Core; Identity; provider SDKs |
| Onyx.Contracts | nothing | any project; any package beyond BCL / DataAnnotations |
| Onyx.Application | Domain, Contracts | Infrastructure, API, Web, Worker; EF Core; `Microsoft.AspNetCore.*`; **any provider SDK** (Google, email, calendar, LLM, ad platforms) |
| Onyx.Infrastructure | Application, Domain | API, Web, Worker |
| Onyx.API | Application, Contracts, Infrastructure (**composition root only**) | Web, Worker |
| Onyx.Worker | Application, Infrastructure (**composition root only**) | API, Web |
| Onyx.Web | Contracts | Domain, Application, Infrastructure, API, Worker; EF Core; any database driver (Npgsql) |
| Onyx.Host | API, Web (**composition only**) | — and nothing may reference Onyx.Host |

- **Onyx.Web never touches the database.** The same applies to the Admin area.
- Flow: `Controller → Application service → Repository interface → Repository implementation → DbContext → PostgreSQL`. Controllers never use DbContext or repositories.
- Domain entities never cross the API boundary. All API I/O uses DTOs from Onyx.Contracts.
- One modular monolith. **Deployment (owner decision 2026-11-16): one application** — `Onyx.Host` starts the API as an internal server on a loopback port (never public, no IIS integration) and the website as the public app in the same process; the website still reaches the API only through its typed HTTP clients, so every boundary above still holds. The Worker's jobs run inside it when `Host:RunWorker` is true, or as the separate Onyx.Worker host. Onyx.API and Onyx.Web remain runnable on their own for development and tests. **No microservices.**
- Web and API requests never run long news, AI, integration or tool work synchronously. That work goes to Worker.

## 6. Layer responsibilities and conventions

### Onyx.Domain
- Encapsulated entities; invariant-enforcing factories; behaviour methods. No persistence attributes.
- Base types: `Entity`, `AuditableEntity`, `ISoftDeletable`, `IHasTranslations<T>`, `IPublishable`, `IOrderable`, `IVerifiableProof`, `IMarketScoped`.
- Domain guards that must exist: unverified proof can't be published; `AiAssisted` insights can't be published or scheduled without recorded human approval; industry pages can't be published below the content minimum; booking slots can't be double-booked.
- Domain events go through an **outbox** and are processed by Worker (emails, notifications, search index, re-indexing).
- UTC via `TimeProvider`.

### Onyx.Application
- Feature folders; plain services behind interfaces; `Result`/`Result<T>` for expected failures; FluentValidation on every write.
- Ports: per-aggregate repositories + `IUnitOfWork` (no generic `IRepository<T>`, no `IQueryable` leaks), `ICurrentUser`, `IFileStorage`, `IEmailSender`, `IEmailTemplateRenderer`, `IAvailabilityProvider`, `IBookingCalendarProvider`, `ICaptchaVerifier` (Turnstile), `IWebAnalyticsProvider`, `ISearchPerformanceProvider`, `IAdPlatformProvider` (future), `ITextGenerationService` (LLM), `IContentOpportunitySource`, `INewsFeedReader`, `ISiteSearchService`, `ISeoAnalysisEngine`, `ISecurityPostureEngine`, `IOpportunityEngine`, `ICacheInvalidator`, `IHtmlSanitizer`.

### Onyx.Infrastructure
- DbContext, configurations, repositories, interceptors (audit, audit fields, soft delete, outbox), Identity stores, provider adapters, SafeOutboundHttp, engines, and DB-backed job/outbox processing primitives.
- Single `AddInfrastructure(...)` entry point.

### Onyx.API
- Controllers represent **business resources, never webpages**. Public: `/api/v1/{resource}`. Admin: `/api/v1/admin/{resource}`. The resource list is in `docs/architecture.md` §24.
- Thin controllers; `async` + `CancellationToken`; `[ProducesResponseType]`; explicit `[AllowAnonymous]` or `[HasPermission(...)]`; ProblemDetails via a global `IExceptionHandler`.

### Onyx.Web
- MVC public site + Admin CMS area. Typed API clients only (`IHttpClientFactory` + resilience). Views and controllers never build HTTP requests.
- Typed page sections. **No unrestricted page builder.** Sensitive structural logic (tool workflows, security logic, architecture) is code-driven, not CMS-editable.
- CTA resolution: item override → pillar default → page-type default → global default.
- Consent banner; non-essential scripts load only after consent.

### Onyx.Worker
- Responsibilities: outbox, email sending, scheduled publishing, news ingestion, AI content research/drafting, GA4/Search Console sync, retention enforcement, sitemap regeneration, and tool execution (Phase 2).
- **Approved mechanism:** built-in `BackgroundService` + DB-backed `JobDefinition`/`JobRun`/`OutboxMessage` tables with row leasing (claim with lease expiry + owner token; expired leases are reclaimable). Multi-instance safe and portable. Schedules are parsed in-house. Retries use exponential backoff and a dead-letter state. **No Hangfire or Quartz.NET.**
- Jobs are idempotent, call Application services, and are recorded as `JobRun`. Failures raise Admin notifications. No public HTTP surface except an internal health check.

## 7. Multilingual and market rules

- **Arabic is the default.** `/` → 302 `/ar/` unless the visitor explicitly chose English (switcher cookie). No `Accept-Language` auto-redirect; show a dismissible suggestion instead.
- **Localized URLs (final):** Arabic segments + Arabic slugs (`/ar/الخدمات/حلول-الذكاء-الاصطناعي`). English segments + English slugs (`/en/services/ai-solutions`). Per-language route table.
- **Slug normalization:** NFC; strip tashkeel/tatweel; hyphens; Arabic letters, a–z, 0–9 (Western digits); ≤ 60 characters recommended. Emitted UTF-8 percent-encoded. Slug changes create 301 `Redirect`s automatically. Never break published URLs.
- Arabic is native-quality, never literal machine translation. English is equally complete. Keyword research is per language.
- Translatable content uses `Entity` + `EntityTranslation`. Configured languages only; never `if (lang == "ar")` in business logic. Publish only when both languages are complete.
- Every public page: `<html lang dir>`, canonical, `hreflang` ar/en/x-default (→ Arabic), localized metadata and JSON-LD, sitemaps per language + index. Search and tool result pages are `noindex`.
- Emails, booking confirmations, tool reports and notifications are localized. Visitor emails use the **submission language**.
- Language and market are separate dimensions. No country copies of the site.
- RTL: CSS logical properties, correct mirroring (never mirror logos, media controls or numbers).

## 8. Interactive tools

- Shared pipeline: `POST /api/v1/tools/{tool}/runs` (Turnstile + per-IP and per-target limits) → `ToolRun` → Worker → poll → result.
- **Report gating:** every visitor gets a **genuinely useful free summary** of real findings. The **detailed report** (more real value) requires contact details, consent and Turnstile, creates an `Inquiry(ToolFollowUp)` linked to the `ToolRun`, and is delivered to the visitor. No fabricated counts and no hiding of all value.
- **SafeOutboundHttp** is mandatory for any visitor- or admin-supplied external URL (tools, news feeds): public domains only, ports 80/443, DNS resolved once with private/loopback/link-local/metadata ranges blocked (IPv4 + IPv6), connection pinned to the vetted IP, redirects re-validated, timeouts and size caps, no credentials or cookies, identifiable User-Agent, `robots.txt` respected for crawling, every request logged.
- **Security assessment: passive only.** Allowed: HTTPS/redirect, certificate and negotiated TLS from a normal handshake, security headers, cookie flags, version disclosure, SPF/DMARC/CAA/DNSSEC/MX, `security.txt`. **Forbidden:** port scanning, cipher sweeps, path brute force, vulnerability probing, payloads, exploitation, automated pentests, login/form interaction. Deeper testing needs ownership verification **and** signed authorization handled by staff. Reports never conclude that a site is secure or compromised.
- **SEO Analyzer:** behind `ISeoAnalysisEngine`, an adapter to the Onyx SEO platform. **Never invent its endpoints, metrics or capabilities.** The UI renders only what the engine declares.
- **Opportunity Finder:** rule-based, versioned questionnaire. No numbers, ROI or guarantees.
- **AI Advisor (later):** bilingual; grounded only in published, approved Onyx content; never invents prices, capabilities, clients, case studies, results, timelines, certifications or partnerships. Creates an inquiry only after visitor review and consent. Hands off to a human through inquiry/booking. Clearly labelled AI; never auto-opens; retrieved content is treated as data.

## 9. Content systems

- **Site pages (homepage, services, company and legal pages)** are code-owned in Onyx.Web (`SiteContent/*.json` + `SharedResource` resx) and own exact localized paths; every other path resolves through the CMS. Editors change them through the Admin **site content** module, which stores **published overrides** (`SiteContentDocument`, draft → publish, revisions) in the API database. The public site overlays published overrides on the code-owned content at render time: only text, visibility, order, selected images and SEO fields are overridable — **never paths, structure, anchors or link targets** — so routes, hreflang, sitemaps and structured data stay intact, and the code-owned content remains the fallback when the API is unavailable. Do not create a second source of truth.

- **Industries:** broad, extensible taxonomy (`TaxonomyOnly` vs `Published`). An industry page is published and indexable only once it meets the content minimum (context, problems, Onyx approach, relevant services/use cases).
- **AI Content Engine (Admin):** scheduled research → opportunity → proposal with reasoning → native Arabic draft + native English draft (not translations) → Pending Review → human edit/approve/reject/schedule → publish. **Never auto-publish AI-generated Onyx articles.** Proposals show reasoning, market, keywords, intent, pillar/service, title, slug, meta, internal links, brief and sources. Metrics only from integrated sources, with provenance.
- **News Hub (public):** allowlisted sources → normalize → dedupe → classify → original concise summaries (ar/en) → attribution → auto-publish allowed for allowlisted sources.
  - Store metadata + summary only. Never full third-party text or source images.
  - Always show the publisher, original time, a link to the original, and an "Onyx summary" label.
  - Admin can hide, edit, unpublish or delete items and allowlist/blocklist sources, all audited.
  - Individual auto-published items default to `noindex, follow` (pending confirmation).
  - **Launch scope.** Until an LLM provider is active, items are fetched, normalized, deduplicated and classified, then stored as **Held**. Nothing is auto-published without an Onyx summary. Editors can summarize and publish manually.
- **Site search:** `SearchDocument` index of **published public content only**, per language, kept in sync by domain events. v1 uses **PostgreSQL** native search behind `ISiteSearchService`:
  - one shared Arabic normalizer applied at index and query time;
  - weighted `tsvector` (`arabic`/`english` configurations + `simple` for exact names) with GIN indexes;
  - `pg_trgm` for typo/partial matching;
  - `websearch_to_tsquery` with bound parameters only.
  
  A semantic/vector or external engine can be swapped in later. Results are grouped by type.
- **LLM usage** (content engine, news, advisor) goes through `ITextGenerationService`. No secrets or lead PII in prompts. External content is treated as untrusted data. Outputs are sanitized.

## 10. Leads, booking, email, consent and retention

- **Admin DB is the system of record** for every inquiry. **Email is only a notification channel.** A submission succeeds once it is stored, whether or not the email goes out.
- Inquiry types: StartProject, Contact, Consultation, RequestConsultation, ServiceInquiry, SimilarProject, ToolFollowUp, OpportunityFinder, ContextualCta, Advisor (later). All use `Inquiry` (immutable) → `Lead` (pipeline).
- **Attribution** on every inquiry: first/last touch, UTM source/medium/campaign/content/term, referrer, landing page, submission page, language, market, device class, tool source/`ToolRunId`, and GA client id (only with analytics consent). Lead detail in Admin shows the origin trail.
- **Booking:** qualification form → slot → confirmation → stored → internal email + visitor confirmation (with .ics). Behind `IAvailabilityProvider`/`IBookingCalendarProvider`; the default is internal admin-managed availability. Slots are stored in UTC with a configurable business time zone (Asia/Riyadh default). Double booking is prevented. Cancel/reschedule uses signed links. No Google/M365/Calendly coupling.
- **Email:** `IEmailSender` + templates (code-owned layout, admin-editable translatable copy, RTL/LTR, plain-text alternative). Sent through an outbox by Worker with retries. Recipient addresses come from settings. Never echo unsanitized input as HTML.
- **Cookie consent:** Necessary / Analytics / Marketing. Versioned `CookieConsentReceipt` stored server-side. A policy version change re-prompts. Analytics and marketing scripts load only after consent.
- **Form consent:** a `ConsentRecord` on every inquiry; contact consent and marketing consent are separate and withdrawable.
- **Retention:** `RetentionPolicy` per data category with a configurable period and action (delete/anonymize). **No hard-coded periods.** Worker enforces it. Data-subject export and deletion are supported and audited. PII never goes to logs. A banner is not a compliance claim.

## 11. Admin analytics and marketing

- GA4 and Search Console go through `IWebAnalyticsProvider` / `ISearchPerformanceProvider`. Future Google/Meta/TikTok/Snapchat Ads go through `IAdPlatformProvider`. **No provider types in Domain or Application.**
- Worker syncs aggregates into local tables. Admin dashboards read local data. Every metric states its source and date range.
- Funnel: Campaign → Visit → Landing page → Service/Tool interaction → Conversion → Lead → Qualified → Won/Lost.
- Provider secrets live only in the secret store. `ProviderConnection` stores non-secret config and status. Configuration changes are audited without values.
- **Phase 1 scope, inactive until configured.** The analytics dashboard, GA4/Search Console adapters and sync jobs are built at launch.
  - Providers that need credentials (GA4, Search Console, LLM, and later others) stay `NotConfigured` until credentials exist and a connection test passes.
  - While inactive, sync jobs are skipped and Admin shows "Not connected". **Never display sample, mock or placeholder metrics.**
  - Fake adapters are registered only in Development/Test.

## 12. Authentication, authorization, notifications

- Identity in the API (hashing, lockout, email confirmation, TOTP 2FA mandatory for privileged roles). **No public staff registration:** staff accounts are invited or bootstrapped only.
- **Customer accounts (owner decision 2026-10-27)** are a separate, isolated identity (`docs/architecture.md` §24.8): their own table, opaque server-side sessions, their own authentication scheme in both apps and their own cookie (`__Host-onyx.customer`). Public sign-up requires Turnstile, rate limits, versioned terms consent and email verification before sign-in. A customer can never hold a role or permission, reach `/admin` or any `api/v1/admin/*` endpoint, and staff tokens are never accepted on customer endpoints. Customer 2FA is optional.
  - **Privileged (2FA mandatory):** SuperAdmin, or anyone holding `Users.Manage`, `Roles.Manage`, `ProviderConnections.Manage`, `Privacy.Manage` or `Leads.Export`; or everyone if `IdentitySecurity:RequireTwoFactorForAllUsers` is set.
  - **Secure default:** every API endpoint requires authentication unless it explicitly declares `[AllowAnonymous]`. Admins can never grant or act on permissions they don't hold.
- Short-lived JWT access tokens + rotating, hashed, revocable refresh tokens with reuse detection.
- **BFF (option a, approved).** Access and refresh tokens live **inside Onyx.Web's encrypted, signed authentication cookie** (Data Protection), named `__Host-onyx.admin`, with `HttpOnly`, `Secure`, `SameSite=Strict`, browser-session lifetime.
  - **Never** expose tokens to browser JavaScript, render them into HTML, store them in localStorage/sessionStorage, or log them.
  - **Lifetimes:** access JWT ≈ 10 min with minimal claims (no permission list); the opaque refresh token is hashed in the API DB, rotated with a short grace window, and reuse revokes the family. Idle limit ≈ 60 min, absolute limit ≈ 12 h.
  - **Revocation:** server-side revocation and security-stamp checks.
  - **Size and keys:** cookie size kept under 4 KB. Web Data Protection keys are persisted and backed up.
  - Full specification: `docs/architecture.md` §25.1.
- **Permission-based authorization.** Roles (SuperAdmin, ContentManager, Marketing, Sales) are permission bundles. Constants live in Contracts. `[HasPermission]` uses a dynamic policy provider. Permissions are resolved server-side. Never check role names, except to protect `SuperAdmin`. The full permission catalogue is in `docs/architecture.md` §23.
- **Notification Center:** each notification carries a required permission. Users see only notifications they are permitted to see. Messages contain no detail beyond that permission. Links are re-authorized on open.
- The API is the only enforcement point. Web only hides UI.

## 13. Audit / activity

- An interceptor audits `IAuditable` changes. Explicit audit covers publish/unpublish, AI article approve/reject/schedule/publish, news item and source changes, lead stage/owner changes and exports, data-subject requests, permission/role/user/security events, integration configuration changes, and retention runs.
- Records include user, action, category, entity, before/after diff, UTC time, IP and correlation id.
- **Never store secrets, tokens or credentials.** For PII-heavy records, prefer changed field names over values.
- Append-only. No edit or delete API. Viewing requires `AuditLogs.View`.

## 14. Security rules (non-negotiable)

- HTTPS + HSTS. **No secrets in source control.** Secrets live in User Secrets locally and a secret store / environment variables in production. Typed options validated on start.
- EF LINQ / parameterized queries only. Server-side validation everywhere. Request size limits.
- **Cloudflare Turnstile** on contact, start-project, consultation request/booking, tool runs, report requests and other abuse-sensitive anonymous actions. **Server-side verification is mandatory** (secret, hostname/action, single use). Client-side checks are never trusted.
- Rate limiting: auth, password reset, forms, booking, tools and search. Partition by real client IP. Trust forwarded headers only from known proxies or Onyx.Web.
- CORS deny-by-default. Antiforgery in Onyx.Web. The API is bearer-only.
- XSS: Razor encoding; `Html.Raw` only for sanitized rich text; CSP with nonces; analytics domains are allowed, but scripts are injected only after consent.
- Security headers on both apps. Upload validation (allow-list + magic bytes, size, random names, outside the web root, re-encoding, SVG rejected unless sanitized).
- **SSRF:** see §8. All visitor- or admin-supplied URLs go through SafeOutboundHttp.
- Data Protection keys are persisted durably, shared across instances, and backed up.
- Production: no developer exception page; Swagger disabled or protected. Structured logging without secrets or PII.
- Least-privilege PostgreSQL roles: the runtime app role has DML only; a separate migration/owner role is used by the pipeline; the test role is `NOSUPERUSER CREATEDB` and used only by tests. Vulnerable-package scan before releases.
- Never claim "unhackable" or "100% secure". Defense in depth.

## 15. Backup and recovery

- Production requires automated PostgreSQL backups (base backups + continuous WAL archiving for point-in-time recovery, plus periodic `pg_dump`), separate media/file backups, a secret-store recovery procedure, a Data Protection key ring backup, defined retention, **periodic restore tests** and a written recovery runbook (RPO/RTO). The implementation is finalized for the chosen host. No provider-specific backup code in the application.

## 16. Design direction

**Bright, light-themed, colourful — "Luminous Onyx".** This section describes the shipped system (`src/Onyx.Web/wwwroot/css/tokens.css`), approved by the owner on 2026-10-13.

- **Canvas:** white, with cool tints (`--paper-2`, `--paper-3`) and one warm tint (`--paper-warm`) to separate sections and create depth. Never one flat white page.
- **Type:** near-black cool ink (`--ink` #0b1320), `--muted` for secondary text. Self-hosted Geist (Latin) + Readex Pro (Arabic) as one family via `unicode-range`. No external font or CDN request (CSP `font-src 'self'`).
- **Accents:** teal (`#0fb5a8`) is the brand primary; bright sky blue, soft violet/lilac, coral pink, sunny yellow and fresh green support it (owner direction 2026-10-14: energetic and optimistic, never flat). Colour is used deliberately, not decoratively: repeating sets (process steps, icons, chips, card edges) cycle the palette in DOM order, the five pillars each own a colour (`.pillar--*`), and state colour is reserved for publication state and validation. Section surfaces alternate white with cool, sky, warm and rainbow washes so the page breathes.
- **Contrast discipline (WCAG 2.2 AA is a hard requirement).** The vivid steps (`--teal`, `--sky`, `--green`, `--amber`, `--violet`, `--rose`) are **graphics only** — fills, borders, icon discs, tints, decorative shapes. They sit below 4.5:1 on white and must never carry text. Anything with text uses the `-2`/`-3` step (`--teal-2` #0a7d76, `--sky-3` #155ea6, `--amber-2` #92400e, …). `--grad-cta` is contrast-checked across its whole ramp (white label, worst point 6.6:1). Focus rings use `--teal-2`, never `--teal`. **When a new colour is added, its contrast must be measured, not assumed.**
- **Components:** modern cards (layered soft shadows, rounded corners, hairline borders), pill buttons with a directional hover sweep, badges and status pills, skeletons, empty states. Gradients are used selectively (hero, CTA, primary button) — never on every surface.
- **The Onyx stone is a restrained signature** (hero only, `onyx-stone.js`, with a static `.webp` fallback for no-JS, reduced motion and slow devices), **not repeated in every section**.
- **Motion:** subtle. Reveal-on-scroll via `IntersectionObserver`, short transitions, `prefers-reduced-motion` honoured globally. No `filter: blur()` on large elements (it works against the INP/LCP budget).
- **RTL is non-negotiable:** CSS logical properties only, never `left`/`right` for anything that mirrors; `--start`/`--flip` drive direction-aware sweeps and arrows. Logos, media controls and numbers are never mirrored.
- **Accessibility:** WCAG 2.2 AA, visible focus on every interactive element, accessible controls, `forced-colors` support.
- **Performance:** LCP < 2.5s, INP < 200ms, CLS < 0.1. Self-hosted assets only. No frontend build step, no npm, no CSS framework — `tokens.css` + `site.css` + `admin.css` are hand-written and served directly, which is also what keeps CSP at `'self'`.
- **Single source of truth:** all colour, spacing, radius, shadow and motion tokens live in `tokens.css`, linked by both the public site and the Admin, so the palette is changed in one place. Component styles never redefine a token.
- **Admin dark theme (owner decision 2026-11-25).** The Admin only (never the public site) may switch to a dark theme: the dark token values live in `tokens.css` under `html[data-surface="admin"]` (explicit `data-theme="dark"`, or the device setting when the choice is `system`). Every dark text/surface pair was measured (lowest 6.5:1). White text sits only on the `--solid-*` fills or `--grad-cta`, which stay deep in both themes.
- **Charts (owner decision 2026-11-25):** server-rendered SVG (`Areas/Admin/Charts`), no chart library; labels are HTML, every chart has a data-table fallback, and only real counts are drawn.

## 17. Coding standards

- SOLID, small cohesive classes, constructor injection, no service locator, no static mutable state.
- `async`/`await` end to end; no `.Result`/`.Wait()`; `CancellationToken` everywhere.
- PascalCase types, `_camelCase` fields, `Async` suffix, intent-named DTOs. File-scoped namespaces; one public type per file.
- EF: `AsNoTracking()` reads, DTO projection, no N+1, paginated lists with capped page size, optimistic concurrency via PostgreSQL `xmin` (`uint Version`) on admin-edited aggregates.
- No abstraction without a current need. Comments explain *why*.

## 18. Testing

- Unit tests for the domain guards (proof, AI approval, industry minimum, double booking), slug normalization and validators.
- API integration tests use `WebApplicationFactory` + **a local PostgreSQL server with an isolated, disposable database per test run** (`onyx_test_{yyyyMMddHHmmss}_{random8}`).
  - **Lifecycle:** created by the fixture, migrated with the real EF migrations, dropped afterwards (`DROP DATABASE … WITH (FORCE)`).
  - **Connection:** from `ONYX_TEST_POSTGRES` only, which must point at the `postgres` maintenance database. It uses a dedicated **`NOSUPERUSER CREATEDB`** test role, which PostgreSQL itself prevents from dropping databases it doesn't own. Database names are never read from config.
  - **Guards:** refuse a superuser role, any other database, any name not matching the pattern, and any database this run didn't create.
  - **Missing configuration:** integration tests are skipped (not failed) when `ONYX_TEST_POSTGRES` is unset.
  - **Portability:** access goes through `ITestDatabase`, so containerized PostgreSQL can be added for CI later.
  - **Tests must never use, modify, migrate, truncate or drop development or production databases.** No EF in-memory provider. See `docs/architecture.md` §3.1.
- 401/403 tests for every protected endpoint. Notification visibility tests per permission.
- Turnstile verification is enforced server-side (tests with a fake verifier).
- SafeOutboundHttp tests: private IP, metadata, IPv6, redirect-to-internal, DNS rebinding.
- Outbox and email: an inquiry persists even when the email transport fails.
- `Onyx.ArchitectureTests` enforces §5. Failures block.

## 19. Database and migrations

- Code First. Migrations in Onyx.Infrastructure, created only after approval.
- The application schema is created **only by EF migrations**. Never `EnsureCreated()` for the application context. (The only exception: a test-only DbContext in its own disposable test database, used to verify infrastructure.) Never auto-migrate in production. Apply migrations from the pipeline (idempotent script or bundle). Extensions (e.g. `pg_trgm`) are enabled by migrations; new extensions need approval.
- Seeders cover languages, markets, the 5 pillars, permissions, roles, offices (Riyadh – Granada District, Amman), news categories, and retention policy categories (periods unset). No fake proof.

## 20. Future growth (design for it, don't build it yet)

AI Advisor, ad-platform providers, CRM, WhatsApp integration (only if approved), calendar provider sync, market pages, semantic search. Extension points: outbox events, Application ports, provider adapters, engine interfaces. No speculative code.

## 21. Working agreement for Claude Code

- State the plan and affected files before large changes. Wait for approval on scope or architecture changes.
- Never weaken a security, dependency, proof, pricing, consent or AI-approval rule to make something compile, pass or look better.
- Never invent business facts. Ask for real content or leave the section hidden.
- After changes: build, run the relevant tests, and report results honestly.
- **Git:** after each implementation stage passes its build, required tests and package/security checks, create **one descriptive commit** for that stage. Never commit secrets, credentials, password-bearing connection strings, user-secrets files, `.env` files or sensitive generated files. Never push unless asked. Outside stage completion, commit only when asked.
