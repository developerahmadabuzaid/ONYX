# Onyx Vanguard Solutions — Solution & Product Architecture

**Revision 4, 2026-10-03.** Implementation approved for **Stage 0 and Stage 1 only** (see `docs/implementation-plan.md`).
**Companion:** `CLAUDE.md` holds the permanent rules. This document holds the design behind them.
Items marked **[OPEN]** are listed in §29 and must not be guessed during implementation.

| Rev | Change |
|---|---|
| 1 | Initial architecture (projects, layering, auth, security, multilingual). |
| 2 | Owner decisions: 5 pillars, GCC/Saudi positioning, proof policy, no pricing, interactive tools, lead model, Arabic default. |
| 3 | Owner decisions: confirmed portfolio, industries at launch, localized URLs final, offices, email-notified leads, consultation booking, cookie consent, retention, Turnstile, Admin analytics and marketing, AI Content Engine, News Hub, site search, notification centre, expanded audit, backup/recovery, white-first brand direction, report gating, AI page structure. **WhatsApp CTA removed from launch. Onyx.Worker moved to Phase 1.** |
| 3.1 | Owner decisions: **News Hub and the Admin Analytics Dashboard are in Phase 1 (launch)**, with credential-dependent providers inactive until credentials exist. **Worker mechanism approved:** built-in `BackgroundService` + DB-backed job/outbox tables; no Hangfire or Quartz.NET. No architecture decisions block scaffolding any more. |
| 3.2 | Owner decisions: **integration tests use the local SQL Server with an isolated, disposable test database per run** (portable to Testcontainers later; never the dev/prod DB). **Git is initialized as part of the initial scaffolding.** Dev-environment findings recorded (§3.1, §29). *(Database parts superseded by Rev 4.)* |
| 4 | Owner decisions: **database changed from SQL Server to PostgreSQL** (EF Core + Npgsql); all SQL Server and SQL Server Full-Text assumptions removed; search redesigned on PostgreSQL full-text + trigram (§18); integration tests use a disposable PostgreSQL database (§3.1); BFF tokens kept in the encrypted `HttpOnly` auth cookie (§25.1); Git commit after each verified stage; review checkpoints; starting packages approved. |

---

## 1. Approved decisions

| # | Decision |
|---|---|
| D1 | .NET 10 (LTS). ASP.NET Core Web API backend. ASP.NET Core MVC/Razor server-rendered frontend. **EF Core Code First + PostgreSQL** (Npgsql provider). Supersedes the earlier SQL Server decision. |
| D2 | Projects: Onyx.Domain, Onyx.Contracts, Onyx.Application, Onyx.Infrastructure, Onyx.API, Onyx.Web, **Onyx.Worker (Phase 1)**, plus test projects. |
| D3 | Onyx.Web talks to Onyx.API only over HTTPS REST. It never touches the DB, DbContext, repositories or Infrastructure. |
| D4 | Admin is an `Admin` area in Onyx.Web, built with MVC + light JavaScript. Admin is a **real CMS** with typed sections and no unrestricted page builder. |
| D5 | BFF authentication, permission-based authorization (roles SuperAdmin, ContentManager, Marketing, Sales), audit logging and the security baseline are unchanged. |
| D6 | Arabic is the default language. Routes are `/ar/...` and `/en/...`. **Arabic URLs use Arabic segments and slugs. English URLs use English segments and slugs (final).** |
| D7 | Portable deployment. No hosting-provider coupling. Owner handles deployment later. |
| D8 | Five pillars: Software & IT · AI Solutions · Automation · Cybersecurity · Digital Growth & Creative. |
| D9 | Regional GCC company. Saudi Arabia is the primary commercial and SEO market. Jordan has an operating presence. Other GCC countries are future markets. |
| D10 | Public offices: **Riyadh, Granada District (KSA)** and **Amman (Jordan)** only. |
| D11 | Audience is SMEs, mid-market and enterprise. |
| D12 | No public pricing in any form. |
| D13 | Proof policy: real, verified proof only. Product benchmarking is internal research and never a source of public claims (§9.3). |
| D14 | Corporate site + interactive technology experience + lead-generation platform. |
| D15 | Every inquiry is stored in Admin (system of record). Email is a notification channel only. |
| D16 | **No public WhatsApp CTA or button at launch.** WhatsApp stays a future integration point only. |
| D17 | Consultation booking at launch, behind a provider-agnostic calendar abstraction. |
| D18 | Email behind an abstraction (SMTP / Microsoft 365 / transactional provider later). Visitor emails are sent in the submission language. |
| D19 | Cookie consent at launch (Necessary / Analytics / Marketing), versioned and auditable. |
| D20 | Formal retention policy supported. Periods are set in the privacy policy before production. |
| D21 | Cloudflare Turnstile with **mandatory server-side verification**. |
| D22 | Admin Analytics & Marketing area. GA4 and Google Search Console are approved. Google/Meta/TikTok/Snapchat Ads become future pluggable providers. |
| D23 | AI Content Engine in Admin. **Never auto-publishes.** Human approval is mandatory. |
| D24 | Public automated News Hub from allowlisted sources. Auto-publish is allowed, with attribution and without copying. |
| D25 | Bilingual site search over published content. PostgreSQL full-text + trigram search first, with semantic/vector or external search possible later behind `ISiteSearchService`. |
| D26 | Admin Notification Center. |
| D27 | Expanded Admin Activity / Audit system. |
| D28 | Backup and recovery are architectural concerns. Implementation details are finalized for the chosen host. |
| D29 | Brand: white-first, minimal, premium. The Onyx stone from the real logo is a restrained signature element. **Replaces the Rev 2 "dark Onyx identity".** |
| D30 | Tool report gating: a useful free summary for everyone; the detailed report requires contact details and consent and creates an Inquiry/Lead. |
| D31 | AI pillar: core AI services may have their own pages. AI + WhatsApp / CRM / existing systems are **use-case sections**, not standalone pages, unless demand and differentiated content justify one later. |
| D32 | Industries approved for launch as a broad, extensible taxonomy, with indexable pages only when content is substantial. |
| D33 | SEO Analyzer integrates with the Onyx SEO platform behind an abstraction. No assumed endpoints or capabilities. |
| D34 | AI Advisor remains a later phase: bilingual, grounded, creates inquiries only with consent. |
| D35 | **News Hub and the Admin Analytics & Marketing dashboard (GA4 + Search Console) are Phase 1 / launch scope.** Their domain, application, API, Worker and Admin components are built in the initial implementation. Providers that need credentials (GA4, Search Console, LLM) ship as real adapters that stay **inactive** until credentials are configured (see §15.1). |
| D36 | **Onyx.Worker uses built-in .NET `BackgroundService` + DB-backed job and outbox tables with row leasing.** Hangfire and Quartz.NET are not used at this stage. |
| D37 | **Integration tests run against a local PostgreSQL server using an isolated, disposable database per test run**, behind a portable test-database abstraction so containerized PostgreSQL can be used later in CI. Tests never use, modify, migrate, truncate or drop development or production databases. |
| D38 | Git is initialized in Stage 0. **After each stage, once its build, tests and package checks pass, one descriptive commit is made.** Secrets, credentials, password-bearing connection strings and sensitive generated files are never committed. |
| D39 | **PostgreSQL is the primary relational database.** Onyx has its own dedicated database; no unrelated existing database is reused. Provider: `Npgsql.EntityFrameworkCore.PostgreSQL`. Conventions in §3.2. |
| D40 | **BFF token storage: option (a).** Access and refresh tokens live inside Onyx.Web's encrypted, `Secure`, `HttpOnly` authentication cookie. They are never exposed to browser JavaScript and never placed in localStorage/sessionStorage. Details in §25.1. |
| D41 | **Review checkpoints:** stop after Stage 1; then proceed to Stage 5 and stop for design approval; after Stage 5 stop after each stage unless the owner authorizes several. |

## 2. Positioning

Onyx is **one integrated technology partner**. The core message:

> **We understand the business, its processes and its problems first, then design the right technology around it, instead of forcing the business into a predefined product.**

- Five pillars connected as one system (the signature "How it all works together" section, labelled as illustrative).
- Works across industries. Industry pages show depth where Onyx has it, and never imply a closed list.
- Visitors can *experience* Onyx: tools now and later, the News Hub, smart search, and later the AI Advisor.
- Saudi-first and GCC-ready. Arabic is native-quality and English is equally complete.
- SEO is a flagship capability backed by Onyx's own SEO platform. Only real platform capabilities are claimed.

## 3. Solution structure

```
OnyxWebsite/
├── src/
│   ├── Onyx.Domain/          Entities, value objects, enums, domain rules, domain events
│   ├── Onyx.Contracts/       API DTOs, error contract, paging, permission constants, wire enums
│   ├── Onyx.Application/     Use cases, services, validators, mapping, ports
│   ├── Onyx.Infrastructure/  EF Core, Identity, repositories, interceptors, provider adapters, SafeOutboundHttp, engines
│   ├── Onyx.API/             REST controllers, auth, rate limiting, Turnstile verification, ProblemDetails, OpenAPI
│   ├── Onyx.Web/             Public site (/ar, /en) + Admin area (CMS), BFF, typed API clients, consent banner
│   └── Onyx.Worker/          Background host: scheduled jobs, outbox, ingestion, sync, AI drafting, retention, tools
├── tests/  Onyx.Domain.Tests · Onyx.Application.Tests · Onyx.API.IntegrationTests · Onyx.Web.Tests · Onyx.ArchitectureTests
└── docs/   architecture.md · product-research/ (internal only, never published)
```

| Project | May reference | Never |
|---|---|---|
| Domain | — | everything |
| Contracts | — | everything (BCL / DataAnnotations only) |
| Application | Domain, Contracts | Infrastructure, API, Web, Worker, EF Core, ASP.NET Core, any provider SDK |
| Infrastructure | Application, Domain | API, Web, Worker |
| API | Application, Contracts, Infrastructure (composition root only) | Web, Worker |
| Worker | Application, Infrastructure (composition root only) | API, Web |
| Web | Contracts | everything else, EF Core, any database driver (Npgsql) |

The system is one modular monolith with **two backend hosts** (API, Worker) sharing the same Application/Infrastructure code. **No microservices.**

### 3.1 Test strategy and test databases (D37)

| Test project | Scope | Database |
|---|---|---|
| Onyx.Domain.Tests | Entities, invariants, domain guards, slug normalization | none |
| Onyx.Application.Tests | Services and validators with hand-written fakes for ports | none |
| Onyx.API.IntegrationTests | HTTP-level tests through `WebApplicationFactory`, plus persistence, interceptor, Worker job and outbox tests against real PostgreSQL | disposable PostgreSQL DB |
| Onyx.Web.Tests | Routing/localization, SEO markup, BFF/antiforgery behaviour, typed clients against a test API | none, or via the API factory |
| Onyx.ArchitectureTests | Dependency rules (CLAUDE.md §5) | none |

**Disposable PostgreSQL database rules**
- **Abstraction:** test code depends only on `ITestDatabase`. The default implementation is `LocalPostgresTestDatabase`; a containerized implementation can be added later for CI without changing tests.
- **Dedicated test login:** the server comes from the test-only environment variable **`ONYX_TEST_POSTGRES`**, a connection string for a dedicated **test login role** (e.g. `onyx_test`) connecting to the `postgres` maintenance database. The role must be **`NOSUPERUSER` with `CREATEDB`**. Because PostgreSQL only lets a non-superuser drop databases it owns, the test role *physically cannot* drop or alter the development or production databases. Setup SQL is in `docs/development-setup.md`.
- **Per-run database:** each run creates its own database `onyx_test_{yyyyMMddHHmmss}_{8 random lowercase chars}`, owned by the test role. It applies the **real EF migrations** and **drops it** (`DROP DATABASE … WITH (FORCE)`) at the end. Between tests, data is reset by an in-house helper (`TRUNCATE … RESTART IDENTITY CASCADE` over application tables, excluding the migrations history), which only runs against a guarded test database.
- **Safety guards** (any failure aborts the run before touching anything):
  1. the configured connection string must target the `postgres` maintenance database and must not name any other database;
  2. the connected role must not be a superuser (`rolsuper = false`) and must have `CREATEDB`;
  3. every create, migrate, truncate or drop targets only a name matching `^onyx_test_\d{14}_[a-z0-9]{8}$` that **this run created**;
  4. the application's own connection-string settings are overridden in the test host, so the app under test can only reach the test database;
  5. stale-database cleanup only touches databases that match the pattern, are **owned by the test role**, and whose embedded timestamp is older than 24 h.
- **Skipping:** integration tests are **skipped with a clear message** (not failed) when `ONYX_TEST_POSTGRES` is not set.
- **Schema creation:** the application database schema is created **only by migrations**. Infrastructure tests that need a throwaway model (e.g. verifying interceptors against test-only entities) use a **test-only DbContext in its own separate disposable database**, created with `EnsureCreated`. That is never used for the application context.

**Local environment observed (2026-10-03):** PostgreSQL **18.3** running as Windows service `postgresql-x64-18` on `localhost:5432`, password authentication required (`psql` at `C:\Program Files\PostgreSQL\18\bin`, not on PATH). No Onyx database or roles exist yet. Docker is not installed.

### 3.2 PostgreSQL conventions (D39)

| Concern | Convention |
|---|---|
| Provider | `Npgsql.EntityFrameworkCore.PostgreSQL` (EF Core 10). No SQL Server packages. |
| Database | Dedicated Onyx database per environment (e.g. `onyx_dev` locally). Never shared with unrelated applications. PostgreSQL ≥ 17; created `UTF8` with `LOCALE_PROVIDER builtin`, `BUILTIN_LOCALE 'C.UTF-8'` (see §18). |
| Roles | Runtime app role (DML only on Onyx schema objects) · migration role (owner of schema objects, used only by the deployment pipeline / developer) · test role (`NOSUPERUSER CREATEDB`, tests only). |
| Naming | Tables and columns in `snake_case`, applied by an in-house EF model convention (no extra package), so raw SQL and DBA tooling never need quoted PascalCase identifiers. |
| Keys | **`uuid` primary keys holding UUIDv7, generated in the domain** (`Entity` base class), so domain events and audit records can reference new entities before they are saved, and time-ordered values keep B-tree indexes compact. Identifiers that must be unguessable (e.g. public `ToolRun` ids, signed-link tokens) use **random** values, never v7. *(Changed during Stage 1 from identity integers; see Stage 1 report.)* |
| Time | `timestamp with time zone` only, with UTC values (`DateTimeOffset` with offset 0 or `DateTime` of Kind Utc), via `TimeProvider`. |
| Text | `text` / `varchar(n)` where a hard limit is a business rule. Case-insensitive lookups use normalized columns (as ASP.NET Core Identity does), not provider-specific collations. |
| Structured data | `jsonb` for versioned structured answers, audit change sets, tool results and provider payloads. |
| Concurrency | Optimistic concurrency on admin-edited aggregates through PostgreSQL's **`xmin`** system column, mapped as a `uint Version` concurrency token (Npgsql's row-version mapping). Replaces SQL Server `rowversion`. |
| Queues and leasing | Job, outbox and email claiming use `SELECT … FOR UPDATE SKIP LOCKED` inside a short transaction, plus lease expiry and owner token, so multiple Worker instances are safe. |
| Search | Native full-text search (`tsvector`/`tsquery`, GIN indexes) + `pg_trgm` trigram similarity (§18). No external search engine in v1. |
| Extensions | Allowed: `pg_trgm` (contrib, available on standard distributions and mainstream managed PostgreSQL). Extensions are enabled by migrations run with the migration role. Any new extension needs approval. |
| Migrations | EF Core migrations in Onyx.Infrastructure (`dotnet ef`, local tool manifest). The design-time factory uses a credential-free connection string. Production migrations are applied by the pipeline as an idempotent script or bundle. **Never auto-migrate in production; never `EnsureCreated` for the application context.** |
| Health | API readiness check = in-house `IHealthCheck` calling `DbContext.Database.CanConnectAsync` (no extra package). Liveness has no dependencies. |
| Connection | Npgsql built-in pooling. SSL settings are configurable per environment (`SSL Mode=Require` or stricter in production). Passwords are never in committed configuration. |

## 4. Conceptual domain and content model

Concept names only. **Nothing is created until implementation is approved.** Translatable items follow `X` + `XTranslation(LanguageCode, text…, Slug, SeoTitle, SeoDescription)`.

| Module | Concepts |
|---|---|
| **Catalog** | `ServicePillar` (5) · `Service` (one pillar; related services across pillars; `ServiceUseCase` sections, FAQs, deliverables, process steps) |
| **Industries** | `Industry` (broad taxonomy; tags services, projects, insights; `PageStatus` = TaxonomyOnly or Published, see §10) |
| **Markets** | `Market` (ISO code; primary SA; publish only with real local content) · `Office` (Riyadh–Granada District, Amman; address and contact configurable) |
| **Portfolio** | `Project` (short presentation; market editions, e.g. KSA/Jordan editions of سجل الطبي) · `CaseStudy` (deep story for selected projects) · `Testimonial` · `ProofMetric` · `ProjectCapability` (each publicly claimed capability, verified) |
| **Proof verification** | `Verification {VerifiedBy, VerifiedAtUtc, SourceNote, ClientPermission}` on every proof item. The domain blocks publishing unverified proof. |
| **Integrations (showcase)** | `Integration`: public showcase of integrations Onyx has delivered or supports, verified. Distinct from system connections below. |
| **Content** | `Page` + typed `PageSection` · `CallToAction` · `Faq` · `SiteSettings` (contact email, phones, social) · `Redirect` |
| **Insights** | `Insight` (Article/Guide; real author; dates; `Origin` = Human or AiAssisted; approval record) · `Topic` · `GlossaryTerm` |
| **Content Engine** | `ContentOpportunity` · `ArticleProposal` · `ArticleDraft` (per language) · `EditorialReview` (§16) |
| **News** | `NewsSource` · `NewsItem` · `NewsCategory` · `NewsIngestionRun` (§17) |
| **Search** | `SearchDocument` (denormalized index of published content per language, §18) |
| **Tools** | `ToolRun` · `ToolReport` (summary + detailed) · `OpportunityQuestionnaire` (versioned) · `CheckDefinition` |
| **Leads** | `Inquiry` (immutable) · `Lead` · `LeadActivity`/`LeadNote` · `Attribution` · `ConsentRecord` |
| **Booking** | `ConsultationBooking` · `AvailabilityRule` / `AvailabilityException` (internal availability) (§13.4) |
| **Messaging** | `EmailMessage` (outbox: template key, language, recipient, status, attempts) · `EmailTemplate` (translatable, admin-editable copy, code-owned layout) |
| **Privacy** | `CookieConsentReceipt` · `RetentionPolicy` (per data category) · `DataSubjectRequest` (export/delete) |
| **Analytics** | `ProviderConnection` (GA4, GSC, future ad platforms; non-secret config, status, last sync) · `AnalyticsDailyMetric` / `SearchPerformanceDailyMetric` / `CampaignDailyMetric` (synced aggregates) |
| **Notifications** | `AdminNotification` (type, severity, link, required permission) · `NotificationReadState` (per user) |
| **Operations** | `JobDefinition` · `JobRun` (status, duration, error summary) · `OutboxMessage` (domain events for async handlers) |
| **Advisor (later)** | `AdvisorConversation` · `AdvisorMessage` · `AdvisorKnowledgeEntry` |
| **Identity / Audit** | Users, roles, permissions, refresh tokens · `AuditLog` (§20) |
| **Media** | `MediaAsset` + translated alt text |

## 5. Sitemap

Arabic uses Arabic segments and slugs. English uses English ones. Slugs are illustrative; final Arabic copy comes from native copywriting.

```
/ar/ (default)                                  /en/
├── الخدمات/                                      services/
│   ├── البرمجيات-وتقنية-المعلومات/{service}          software-it/{service}
│   ├── حلول-الذكاء-الاصطناعي/{service}               ai-solutions/{service}
│   ├── الأتمتة/{service}                             automation/{service}
│   ├── الأمن-السيبراني/{service}                     cybersecurity/{service}
│   └── النمو-الرقمي-والإبداع/{service}               digital-growth-creative/{service}
├── القطاعات/ · القطاعات/{industry}                 industries/ · industries/{industry}
├── أعمالنا/ · أعمالنا/{project}                    work/ · work/{project}
│   └── دراسات-الحالة/{case-study}                    case-studies/{case-study}
├── الأدوات/                                       tools/
│   ├── تحليل-الموقع                                 website-analyzer
│   ├── فحص-الأمان                                  security-check
│   └── اكتشف-الفرص                                 opportunity-finder
├── المعرفة/ · المعرفة/{slug} · المعرفة/مواضيع/{topic}   insights/ · insights/{slug} · insights/topics/{topic}
├── آخر-الأخبار/ · آخر-الأخبار/{category} · …/{item}   news/ · news/{category} · news/{item}
├── المصطلحات/{term}                               glossary/{term}
├── التكاملات/{slug}                               integrations/{slug}        verified only
├── الأسواق/{market}                               markets/{market}           future, real local content only
├── من-نحن/ · من-نحن/منهجيتنا · من-نحن/الثقة-والأمان     about/ · about/how-we-work · about/trust
├── مكاتبنا                                        offices                    Riyadh (Granada District), Amman
├── الوظائف/{job}                                  careers/{job}
├── ابدأ-مشروعك                                    start-a-project
├── احجز-استشارة                                   book-a-consultation
├── تواصل-معنا                                     contact
├── بحث                                           search                     noindex
└── الخصوصية · الشروط · سياسة-ملفات-الارتباط          privacy · terms · cookie-policy
```

## 6. Navigation

- **Desktop:** Services (5-column mega menu + "How it all works together") · Industries · Work · Tools · Insights · News · About · search icon · `العربية / English` · **[Start a project]** · secondary **Book a consultation**.
- **Mobile:** menu, search, language switch, sticky **Start a project** / **Book a consultation** bar. **No WhatsApp at launch.**
- **Footer:** pillars, tools, industries, offices, careers, trust, legal, cookie settings link.

## 7. Homepage (typed sections, admin-ordered)

| # | Section | Rule |
|---|---|---|
| 1 | `Hero` | Integrated promise + Onyx stone signature visual; CTAs: Start a project · one tool |
| 2 | `ProofStrip` | Hidden until verified proof exists |
| 3 | `PillarsOverview` | 5 pillars, outcome-led |
| 4 | `IntegratedSystem` | "How it all works together", interactive, **labelled illustrative** |
| 5 | `BusinessFirstApproach` | "We design technology around your business", with industry examples and an open-ended "any industry" message |
| 6 | `FeaturedWork` | **Max 3** featured verified projects (admin flag; limit enforced) |
| 7 | `ToolsShowcase` | Website analyzer · Security check · Opportunity finder (when live) |
| 8 | `ProcessSteps` | How Onyx works (no invented durations) |
| 9 | `LatestNews` | 3–4 recent News Hub items |
| 10 | `InsightsFeed` | Latest insights |
| 11 | `Offices` | Riyadh, Amman |
| 12 | `CtaBand` | Start a project · Book a consultation · Contact |

## 8. Service pages and the AI pillar

**Template:**
1. H1 phrased the way buyers search, answer-first summary, contextual CTA.
2. Problems it solves.
3. Deliverables.
4. **Use cases** (`ServiceUseCase` sections).
5. How it works (diagram or before/after; mandatory for Automation).
6. Process / engagement.
7. **One** contextual link to a related verified case study (not the full project repeated).
8. Verified integrations.
9. Security and data notes.
10. FAQs.
11. Related services, insights and tool.
12. CTA block.

**AI pillar:** pages for core AI services that Onyx actually offers (AI Agents, Customer Service Agents, Sales Agents, Business Assistants, AI Business Automation, Custom AI, AI Integration, Intelligent Chatbots, AI-Powered Websites).
- **AI + WhatsApp / CRM / existing systems** are `ServiceUseCase` sections, mainly on AI Integration and the Automation pages.
- **Promoting a use case to a standalone page** requires: genuine search/business demand (from real data), differentiated content, and no duplication.
- Note that "AI + WhatsApp" as a *service description* is allowed (it is a capability), while a *public WhatsApp contact button* is not (D16).

## 9. Work / portfolio

### 9.1 Confirmed portfolio (facts as supplied by the owner; everything else is pending)

| Project | Confirmed facts | Public-claim boundaries |
|---|---|---|
| **سجل الطبي / Medical Record System** | Clinic/medical management system. Market-specific editions for Saudi Arabia and Jordan. | Internal benchmarking first (KSA, Jordan, global). Capabilities published only once they exist. No "most complete" claims. Competitors never named publicly. |
| **Creative Talent Marketplace** (final name pending) | Marketplace connecting photographers, models and creative talent. | Only implemented functionality. Competitors not discussed publicly. |
| **TireZone** (mytirezone.com) | US tire/automotive platform. AI embedded in the customer journey: search, compare, reason about suitable options for the user's vehicle, answer tire/vehicle questions, interpret user-described issues with **possible explanations / preliminary guidance**, guide toward the relevant product/service, continue toward booking. | Never "definitive mechanical diagnosis". Positioned as AI embedded in the business journey. Not a competitor comparison. Discount Tire is an internal benchmark only. |
| **DealGo** | Real-time auction platform; user balance/wallet business logic; non-traditional auction/purchase flow. | Highlight real-time architecture and workflows once verified. No invented performance, volume, user or outcome numbers. |
| **Al Baraka** | B2B industrial website/platform for tissue/paper manufacturing machinery. | Only implemented functionality (discovery, specs, quotation flows only if they exist). |
| **إيثاق العقارية / Ethaq Real Estate** | Real estate project/platform. | No details until supplied. |

### 9.2 Presentation rules
- Two levels: **Projects** (short) and **Case Studies** (deep, only where enough verified information exists).
- **Full project content lives only in Work.** Service and industry pages show at most a small contextual card linking to the case study. The homepage shows max 3 featured projects. No repeated full project blocks across the site.
- Each publicly stated capability is a verified `ProjectCapability`. Technologies, integrations, dates and screenshots are published only when supplied and confirmed.
- Client names and logos need recorded client permission.

### 9.3 Internal product-research process
`Benchmark strong products → identify useful features/workflows → compare with actual implementation → decide what to build → implement approved functionality → only then describe it publicly.`
- Research lives in `docs/product-research/{project}.md` (internal; never rendered on the website).
- Competitor names never appear in public content unless the owner explicitly approves it.
- **Planned research tasks** (not started; each is a separate task): clinic-management systems (KSA, Jordan, global); creative-talent marketplaces; tire platforms (incl. Discount Tire); industrial machinery B2B sites.

## 10. Industries

- `Industry` is a **broad, extensible taxonomy** (healthcare, real estate, automotive, manufacturing, retail, e-commerce, logistics, hospitality, education, professional services, construction, …). It is used for tagging and filtering, never presented as a closed list.
- **Two states:**
  - `TaxonomyOnly`: used for tags and filters; no indexable page.
  - `Published`: a full landing page. It needs minimum content (industry context, business problems, Onyx approach, relevant services/use cases, optional related verified work), checked by the domain before publishing, plus editorial review.
- The **Industries hub** leads with the business-first message, lists published industries, and ends with "Your industry isn't listed? Tell us about your business" → Opportunity Finder / Start a project.
- No thin pages. No page per industry × service × city combination.

## 11. Interactive tools (unchanged pipeline; gating decided)

- **Pipeline:** `POST /api/v1/tools/{tool}/runs` (Turnstile, rate limits per IP and per target) → `ToolRun` → Worker job → poll → **free summary** → optional detailed report.
- **Report gating (D30):**
  - Free summary: a real, useful subset of actual findings, with severity/priority and plain-language explanations.
  - Detailed report: the full findings, explanations, recommendations, and any comparison the engine actually supports. It requires contact details, Turnstile and consent. It creates an `Inquiry` (type `ToolFollowUp`) linked to the `ToolRun`, and the report is delivered (shown and emailed in the submission language).
  - **No fabricated finding counts, no artificial blurring of everything.**
- **SafeOutboundHttp** is mandatory for visitor-supplied (and admin-supplied) external URLs.
- **Security assessment:** passive checks only (lists unchanged from Rev 2, mirrored in CLAUDE.md §8). Deeper testing requires ownership verification + signed authorization handled by staff.
- **SEO Analyzer:** `ISeoAnalysisEngine` is an adapter to the Onyx SEO platform. The engine **declares its own capabilities** and the UI renders only those. **No endpoints, metrics or capabilities are assumed** until the platform API is defined. Until then, the adapter is an interface with no public tool.
- **Opportunity Finder:** rule-based, versioned questionnaire. Recommends areas and services. No numbers or guarantees.
- **AI Advisor (later):** see §16.6.

## 12. Calls to action

`CallToAction` action types at launch: **StartProject · BookConsultation · Contact · RequestConsultation · Tool (Analyze / Security / Opportunities) · Url**. `WhatsApp` exists only as a reserved future type, disabled.
Resolution order: item override → pillar default → page-type default → global default.

## 13. Inquiries, leads, booking and email

### 13.1 Inquiry types (sources)
StartProject · Contact · Consultation (booking) · RequestConsultation · ServiceInquiry · SimilarProject · ToolFollowUp · OpportunityFinder · ContextualCta · Advisor (later). WhatsApp is a future source only.

### 13.2 Model (Rev 2 model kept)
- **`Inquiry`** (immutable): contact fields (no WhatsApp channel option at launch), needs, structured answers (versioned JSON), links (service, project, ToolRun, booking, conversation), **`Attribution`**, **`ConsentRecord`**, Turnstile result.
- **`Lead`** (pipeline): stage, source, primary pillar, owner, priority, rule-based score, activities, linked inquiries.
- **Attribution:**
  - first touch and last touch;
  - `utm_source/medium/campaign/content/term`;
  - referrer, landing page, submission page;
  - language, market, device class;
  - tool source and `ToolRunId`;
  - GA client id (only with analytics consent, for funnel joins).
- **Lead detail in Admin** shows an "Origin" panel with the full attribution trail and linked tool run, booking and pages.

### 13.3 Submission flow (system of record + notification)
```
Web form → API (Turnstile verify, validation, rate limit, consent)
  → transaction: Inquiry + Lead (create/match) + OutboxMessage(InquirySubmitted)
  → 201 to visitor (success does NOT depend on email)
Worker: InquirySubmitted → EmailMessage(internal notification → configured info address)
                         → EmailMessage(visitor confirmation, submission language)
                         → AdminNotification(new lead)
```
Email failures are retried with backoff and surface as notifications. They never lose the inquiry.

### 13.4 Consultation booking
```
Book a consultation → short qualification form → choose available slot → confirm
  → ConsultationBooking + Inquiry(Consultation) + Lead (one transaction, slot uniqueness enforced)
  → internal email + visitor confirmation email (with .ics) + AdminNotification
```
- **Ports:** `IAvailabilityProvider` (free slots) and `IBookingCalendarProvider` (create/cancel calendar events, optional meeting link). Domain/Application never reference Google, Microsoft 365, Calendly or any provider.
- **Default implementation: internal availability.** Admin-managed `AvailabilityRule` (weekdays, hours, slot length, buffer, lead time, horizon) and `AvailabilityException` (holidays, blocked days). Calendar sync is added later as another provider.
- **Time zones:** slots stored in UTC. Business time zone is configurable (Asia/Riyadh default). The visitor sees their local time with the zone stated.
- **Double booking:** a unique constraint on the slot plus a concurrency check. A slot is held only during confirmation.
- **States:** Requested / Confirmed / Rescheduled / Cancelled / Completed / NoShow. Cancel/reschedule uses a signed visitor link.
- **[OPEN]:** slot length, working days/hours, consultation format (online/in-person/phone), who attends, meeting-link provider.

### 13.5 Email architecture
- **Ports:** `IEmailSender` (transport) and `IEmailTemplateRenderer`. Infrastructure adapters: SMTP first (also covers Microsoft 365 SMTP), with transactional-provider adapters later. **No provider SDK in Application.**
- **Templates:** code-owned layout; translatable admin-editable copy; separate Arabic (RTL) and English (LTR) versions; plain-text alternative.
- **Outbox:** every email is an `EmailMessage` row sent by Worker. Idempotent, with retries and a dead-letter state.
- **Configuration:** recipient addresses (info email per inquiry type) live in settings, not code. Sending domain needs SPF/DKIM/DMARC (deployment task).
- **Content:** visitor emails never echo unsanitized input as HTML. Internal emails contain only what the receiving role may see. Instead of full PII, they include a link to the lead in Admin (configurable).

## 14. Consent, privacy and retention

- **Cookie categories:** Necessary (session, antiforgery, language choice, consent record) · Analytics (GA4) · Marketing (future ad pixels).
- **Banner:** Accept all / Reject non-essential / Customize. Choices can be changed anytime from the footer link. Non-essential scripts load **only after** consent. Google Consent Mode can mirror the choices.
- **Consent record:** a `CookieConsentReceipt` (anonymous consent id, policy version, categories, timestamp, IP hash, language) is stored server-side. A policy version bump re-prompts the visitor.
- **Attribution and consent:** persistent first-touch attribution cookies require Analytics consent. Without consent, only the context of the submitting session/page is recorded. **[OPEN — legal confirmation]**
- **Form consent:** `ConsentRecord` on every inquiry (contact consent required; marketing consent optional and separate).
- **Retention:** a `RetentionPolicy` per data category (inquiries/leads, tool runs, bookings, consent receipts, email outbox, notifications, advisor conversations, analytics aggregates, audit logs). Each policy has an action (delete / anonymize) and a period that is **not hard-coded**; it is configured from the approved privacy policy. A Worker job enforces it and records its runs.
- **Data-subject requests:** `DataSubjectRequest` supports export (machine-readable) and deletion/anonymization across related records, audited.
- A banner does not by itself make the site compliant. Legal texts and the processing register are owner/legal deliverables before production.

## 15. Admin Analytics & Marketing

- **Ports (Application):** `IWebAnalyticsProvider` (GA4 Data API), `ISearchPerformanceProvider` (Search Console API), `IAdPlatformProvider` (future: Google Ads, Meta, TikTok, Snapchat). These are provider-neutral DTOs. Domain has no Google or ad-platform types.
- **Sync, not live calls:** Worker syncs daily or hourly aggregates into local tables (`AnalyticsDailyMetric`, `SearchPerformanceDailyMetric`, `CampaignDailyMetric`). Admin dashboards read local data: fast, quota-safe, and still usable when a provider is down.
- **Analytics views:** users, sessions, page views, top and landing pages, sources/channels, devices, countries/markets, campaigns/UTM, conversion events (inquiry submitted, booking confirmed, tool run, report requested).
- **Search views:** queries, pages, clicks, impressions, CTR, average position, per language and market.
- **Funnel correlation:** `Campaign → Visit → Landing page → Service/Tool interaction → Conversion → Lead → Qualified → Won/Lost`.
  - Joined on UTM/campaign keys and (with consent) the GA client id stored on inquiries.
  - Lead stages come from the local DB.
  - Ad-platform spend joins in once those providers are added.
- **Conversion events** are sent to GA4 client-side only after analytics consent. Future offline-conversion uploads to ad platforms require marketing consent.
- **Credentials:** OAuth/service-account secrets live in the secret store. `ProviderConnection` holds only non-secret config and status. Connection changes are audited, with no secret values.
- Displayed metrics always state their source and date range. **Nothing is estimated or fabricated.**

### 15.1 Inactive-until-configured providers (applies to GA4, Search Console, LLM; later ad platforms, calendar, email providers)
- Each provider has a `ProviderConnection` with status **NotConfigured → Configured → Active → Error / Disabled**. A provider becomes Active only after its credentials exist in the secret store and a connection test passes.
- **NotConfigured / Disabled:** the provider's sync jobs are skipped (recorded as `Skipped` in `JobRun`, no error spam). Admin screens show an explicit "Not connected" state with setup guidance. **No sample, mock or placeholder metrics are ever displayed.**
- **Error:** the last good data stays visible with a "stale since …" notice, and an `AdminNotification(IntegrationFailed)` is raised.
- Activating a provider is a configuration and secrets task, with no code change or redeploy of business logic. Enabling or disabling is audited.
- Development and tests use fake adapters registered only in Development/Test environments.

## 16. AI Content Engine (Admin, editorial)

### 16.1 Workflow
```
Scheduled research (Worker) → ContentOpportunity → ArticleProposal (reasoning) → Arabic draft + English draft
  → Pending Review → human Edit / Approve / Reject / Request changes / Schedule → Publish (human-approved only)
```
- **Domain rule:** an `Insight` with `Origin = AiAssisted` cannot be published or scheduled without a recorded human approval (`ApprovedBy`, `ApprovedAtUtc`). There is **no auto-publish path**, enforced in the domain and covered by tests.

### 16.2 Opportunity sources (pluggable `IContentOpportunitySource`)
- Phase 1: configured topic seeds per pillar/market, and News Hub trends.
- Later: Search Console (e.g. a query/page with rising impressions but weak CTR or position → an improvement suggestion with reasoning) and the Onyx SEO platform.
- Optional external research/search API **[OPEN]**.

### 16.3 Proposal content shown to editors
- Why the topic was selected, target market, primary and secondary keywords, search intent, related pillar/service.
- Suggested title, slug (normalized per language), meta title and description.
- Internal-link opportunities (from `SearchDocument`), content brief, and sources with URLs.

### 16.4 Integrity rules
- **No fabricated search volume, keyword difficulty, ranking probability or traffic forecasts.** Quantitative metrics appear only when they come from an integrated source, and their provenance (source + date) is shown. Otherwise the field is empty.
- Arabic and English drafts are **generated natively per language** from the same brief (not translated), then edited by humans.
- Drafts must not contain invented Onyx facts (clients, results, capabilities). The prompt is grounded in approved Onyx content. Claims about the outside world need sources.
- Generated HTML is sanitized. Editors see a source list and a checklist before approving.

### 16.5 Ports
`ITextGenerationService` (LLM abstraction, shared with News and the Advisor), `IContentOpportunitySource`, `ISearchPerformanceProvider`. LLM provider **[OPEN]**.

### 16.6 AI Advisor (later phase, unchanged intent)
- Bilingual. Grounded only in published, approved Onyx content (the `SearchDocument` corpus + `AdvisorKnowledgeEntry`).
- Never invents prices, capabilities, clients, case studies, results, timelines, certifications or partnerships.
- Helps understand the visitor's business and scope needs. Creates an `Inquiry(Advisor)` only after the visitor reviews the summary and consents.
- Hands off to a human through the inquiry or booking flow (not WhatsApp at launch).

## 17. News Hub (public, automated)

### 17.1 Pipeline (Worker, scheduled)
```
Allowlisted NewsSource (RSS/Atom/API) → fetch via SafeOutboundHttp → normalize (canonical URL, title, publisher, published time)
  → deduplicate (canonical URL + normalized-title similarity → cluster) → classify (category, language)
  → generate original concise summary (ar + en, length-capped, no verbatim reproduction)
  → attach attribution → auto-publish (allowlisted source + checks pass) or hold for review → SearchDocument update
```

### 17.2 Data
- **`NewsSource`:** name, homepage, feed URL, type, language, default category, **status Allowed/Blocked**, licensing/terms note (reviewed before allowlisting), auto-publish flag.
- **`NewsItem`:** source, original URL (unique), original title, publisher, original published time, ingested time, category, Onyx summaries per language, status (Published / Held / Hidden / Unpublished / Deleted), `AutoPublished`, edit history, cluster key.

### 17.3 Rules
- Store metadata + Onyx summary only. **Never full third-party article text** (processed transiently). Never copy source images; use category artwork.
- Each item displays the publisher, original time, a **link to the original**, and a clear label: "Summary by Onyx — reported by {source}".
- The summary must not add facts that are not in the source and must not editorialize as if it were Onyx reporting.
- Admin can hide, edit, unpublish or delete items, and can allowlist/blocklist sources. All of this is audited.
- **Indexing:** the News hub and category pages are indexable. **Individual auto-published items default to `noindex, follow`**, to avoid scaled thin-content risk; an editor can mark an enriched item indexable. Recommended default **[OPEN — confirm]**.
- **Categories at launch:** Artificial Intelligence · Software & Technology · Digital Marketing · Cybersecurity (admin-extensible).
- Ingestion failures → `JobRun` error + `AdminNotification`.
- **Launch scope (D35).** The full pipeline ships in Phase 1. **Until an LLM provider is active**, ingestion still fetches, normalizes, deduplicates and classifies (rule-based, from source category and keywords). Items are stored as **Held**, never auto-published, because auto-publish requires an Onyx summary. Editors may write summaries manually and publish held items. Once the LLM provider is active, automatic summarization and auto-publish switch on for allowlisted sources.

## 18. Smart site search

- **`SearchDocument`:** one row per published item per language (type, id, language, title, summary, body text, URL, market/industry/pillar tags, published date, type boost).
  - Kept in sync by domain events on publish, unpublish and edit.
  - **Only published, public content.** Never drafts, leads, tool runs or held news.
- **Engine v1: PostgreSQL native search.** No SQL Server Full-Text, no external engine.
  1. **Arabic normalization in application code** (one shared `ArabicTextNormalizer`), applied identically at index time and query time:
     - Unicode NFKC; strip tashkeel (diacritics) and tatweel;
     - unify alef forms `أ إ آ ٱ → ا`, `ة → ه`, `ى → ي`, and `ؤ → و`, `ئ → ي`;
     - Arabic-Indic and Persian digits → Western digits; punctuation → spaces.
     
     English text is lowercased and NFKC-normalized. Normalized text is stored in dedicated columns, while the original text is kept for display.
  2. **Weighted `tsvector`** (stored generated column, GIN index):
     - weights: title **A**, summary **B**, body **C**;
     - text-search configuration chosen per row's language: `arabic` (PostgreSQL's built-in Snowball configuration) for `ar`, and `english` for `en`;
     - plus a `simple` (unstemmed) vector over titles and keywords, so exact names and brands (project names, technology names) always match.
     
     Stage 10 verifies that the `arabic` configuration exists on the target server. The fallback is `simple` over normalized text.
  3. **Trigram similarity (`pg_trgm`)** with a GIN index on normalized titles. It gives typo tolerance, partial-word matching and a fallback when the full-text query returns nothing.
  4. **Query parsing:** `websearch_to_tsquery(config, normalizedQuery)`, which safely parses free text, quotes, `OR` and `-`. Prefix matching (`:*`) is added for the last term in search-as-you-type. User input is always a bound parameter, never concatenated into SQL.
- **Ranking:** `ts_rank_cd` (weighted) × content-type boost × freshness (news only) + trigram title similarity as a secondary signal. Results are grouped by type (Services, Industries, Work, Insights, News, Pages), with a cap per group.
- **Database locale:** Onyx databases are created with `ENCODING 'UTF8'`, `LOCALE_PROVIDER builtin`, `BUILTIN_LOCALE 'C.UTF-8'` (PostgreSQL ≥ 17). This gives Unicode-correct, OS-independent character classification, so Arabic letters count as word characters for full-text and trigram processing. Code-point collation is acceptable because ordering is by explicit sort fields; ICU collations can be applied per column if linguistic sorting is ever needed. Test databases use identical settings. Verified in Stage 10.
- **Port:** `ISiteSearchService`. A later semantic/vector implementation (e.g. embeddings over the same `SearchDocument` corpus, `pgvector` subject to approval) or an external engine can replace or augment it without domain changes. This is also the retrieval corpus for the AI Advisor.
- **Safety:** the search page is `noindex`. Queries are rate-limited and length-capped (e.g. 100 characters). Search terms are logged only in aggregate (no PII).
- **Portability:** `pg_trgm` and the built-in text-search configurations ship with standard PostgreSQL distributions and mainstream managed PostgreSQL services. Confirm on the production host at deployment **[OPEN, non-blocking]**.

## 19. Admin Notification Center

- **`AdminNotification`:** type (NewLead, NewBooking, ArticleReadyForReview, JobFailed, NewsIngestionFailed, IntegrationFailed, ToolAttention, …), severity, title/message (admin UI language), link to the Admin resource, **required permission**, created time.
- **`NotificationReadState`** per user: read/unread, read time.
- **Visibility:** a user sees a notification only if they hold its required permission (e.g. `Leads.View` for NewLead, `System.Jobs.View` for JobFailed). Messages contain no sensitive detail beyond what that permission allows. The link is re-authorized on open.
- **Delivery:** in-Admin badge plus list (polling). Optional email digests later.
- Created by Worker/Application handlers from domain events and job failures.

## 20. Admin Activity / Audit

- **Captured:**
  - entity changes (interceptor, `IAuditable`);
  - explicit actions: publish/unpublish, AI article approve/reject/schedule/publish, news edits/hide/delete, source allow/block, lead stage/owner changes, lead export;
  - data-subject requests;
  - permission/role/user changes, login/security events;
  - integration/provider configuration changes;
  - retention runs.
- **Record:** user, action, category (Content · Publishing · Leads · Security · Integrations · Privacy · System), entity type and id, before/after diff (JSON), UTC time, IP, correlation id.
- **Redaction:** `[SensitiveData]`/`[NotAudited]` fields, secrets, tokens and credentials are never stored. For integration config, record "secret changed", never values. For PII-heavy records, store field names changed rather than values where appropriate.
- **Protection:** append-only, no update/delete API. Viewing requires `AuditLogs.View`. Audit retention is governed by its own `RetentionPolicy`.
- **Admin UI:** an "Activity" screen filterable by user, category, entity and date, plus an activity tab on key records (lead, service, article).

## 21. Onyx.Worker

**Status:** part of the Phase 1 architecture (D2). Not implemented yet.

| Responsibility | Trigger |
|---|---|
| Outbox: domain events → handlers (emails, notifications, search index, advisor re-index later) | continuous |
| Email sending with retries | continuous |
| Scheduled publishing (insights, pages) | every minute |
| News ingestion pipeline | scheduled per source |
| AI content research and drafting | scheduled |
| GA4 / Search Console sync (ad platforms later) | scheduled |
| Retention enforcement, expired ToolRun cleanup | daily |
| Sitemap regeneration / cache warm-up | on publish events + daily |
| Interactive tool execution (Phase 2) | queued |

**Boundaries**
- No public HTTP surface (health endpoint only, internal).
- Executes Application services; no business logic in job classes.
- Every job is **idempotent** and recorded as a `JobRun`. Failures create a notification.
- **Mechanism (approved, D36):** built-in `BackgroundService` + DB-backed job/outbox tables with row leasing, so it is safe with multiple instances and portable to any host. **Hangfire and Quartz.NET are not used at this stage.**
  - **Recurring jobs:** `JobDefinition` holds schedules (interval or simple cron-like expression parsed in-house), the next-run time and an enabled flag. A scheduler loop claims due jobs in a short transaction with `SELECT … FOR UPDATE SKIP LOCKED`, sets the lease expiry and owner token, and records a `JobRun`.
  - **Outbox:** `OutboxMessage` rows are written in the same transaction as the business change. A dispatcher loop claims them in batches and runs the handlers. Retries use exponential backoff; messages over the attempt limit go to a dead-letter state and raise a notification.
  - **Queued work** (emails, later tool runs) uses the same lease-and-claim pattern.
  - Graceful shutdown honours `CancellationToken`. An expired lease is reclaimed by another instance.
- Least-privilege DB login and secrets. Visitor- or admin-supplied external URLs go through SafeOutboundHttp only. Calls to fixed provider APIs (Google, LLM, email) use typed clients with resilience.
- **Web and API requests never run long news, AI, integration or tool work synchronously.**

## 22. Multilingual and GCC

- **URLs (final):**
  - Arabic: `/ar/{Arabic segments}/{Arabic slug}`, e.g. `/ar/الخدمات/حلول-الذكاء-الاصطناعي`.
  - English: `/en/{English segments}/{English slug}`, e.g. `/en/services/ai-solutions`.
  - Per-language route table.
- **Slug normalization:** NFC; strip tashkeel/tatweel; hyphens; Arabic letters, a–z, 0–9 (Western digits); ≤ 60 characters recommended. Emitted UTF-8 percent-encoded.
- **Slug changes** create a 301 `Redirect` automatically. Redirects are admin-managed.
- **Root and language:** `/` → 302 `/ar/` unless the visitor explicitly chose English (cookie, Necessary category). No `Accept-Language` auto-redirect; a dismissible suggestion is shown instead.
- **SEO markup:** `hreflang` ar/en/x-default (→ Arabic), per-language canonical, sitemaps per language (+ news sitemap for indexable items) + index, localized metadata and JSON-LD.
- **Copy:** native Arabic copy, equally complete English. Separate keyword research per language.
- **Markets:** language and market are separate dimensions. No country copies of the site. Market pages only with genuine local content. Saudi content woven into the main site at launch.
- **Structured data:** `Organization` with addresses for Riyadh (Granada District) and Amman only; `areaServed` lists served countries.
- Emails, tool reports, booking confirmations and notification copy are localized.

## 23. Admin CMS and permissions

- **Editable by authorized users without developers:**
  - pillars, services (+ use cases, FAQs), industries, projects, case studies, capabilities (with verification);
  - insights, glossary, news (items + sources), pages and typed sections, CTAs, FAQs;
  - media, page and SEO metadata, redirects, offices, site settings, email template copy;
  - availability rules, check-definition copy, questionnaire versions;
  - draft/publish/schedule, both languages.
- **Code-driven (not CMS-editable):** tool workflows and engines, security logic, SafeOutboundHttp, permission definitions, application architecture, layouts and templates' structure.
- **Permissions** (constants in Contracts; granular, extensible):
  - **Content:** `Pillars.*`, `Services.*`, `Industries.*`, `Projects.*`, `CaseStudies.*`, `Testimonials.*`, `Proof.Verify`, `Integrations.*`, `Insights.*` (+`Insights.Publish`), `ContentEngine.View`, `ContentEngine.Review`, `ContentEngine.Approve`, `News.*`, `NewsSources.Manage`, `Glossary.*`, `Pages.*`, `Ctas.*`, `Media.*`, `Seo.Edit`, `Redirects.Manage`, `Offices.Manage`, `Markets.*` (+`Markets.Publish`), `Settings.Manage`, `EmailTemplates.Manage`.
  - **Leads:** `Inquiries.View`, `Leads.View`, `Leads.ViewAll`, `Leads.Manage`, `Leads.Assign`, `Leads.Export`, `Bookings.View`, `Bookings.Manage`, `Availability.Manage`.
  - **Analytics:** `Analytics.View`, `SearchPerformance.View`, `ProviderConnections.Manage` (GA4/GSC/LLM/email/calendar/ad-platform connections). *(Renamed in Stage 1 from `Integrations.Configure`, which collided with the `Integrations.*` showcase group.)*
  - **Tools:** `Tools.Configure`, `ToolRuns.View`.
  - **Privacy and operations:** `Privacy.Manage` (retention, data-subject requests), `System.Jobs.View`, `AuditLogs.View`.
  - **Identity:** `Users.Manage`, `Roles.Manage`.
- **Default role bundles (editable later):**
  - **SuperAdmin:** all.
  - **ContentManager:** content, insights, news, content-engine review/approve, media, SEO, redirects.
  - **Marketing:** analytics, search performance, SEO, CTAs, content-engine view/review, insights draft.
  - **Sales:** inquiries, leads, bookings, availability.
  - Code never checks role names (except SuperAdmin protection).

## 24. Request flows and API resources

- **Public page:** Web → typed client → API public endpoint → service → repository projection → DTO → SSR with canonical, hreflang and JSON-LD.
- **Admin write:** antiforgery → BFF token → `[HasPermission]` → validator → domain rules (proof guard, AI-approval guard, industry content minimum) → unit of work → audit → outbox events.
- **Forms, booking and tools:** Turnstile token → API verifies server-side → validation → persist → outbox → Worker.
- **API (v1):**
  - **Public:** `services`, `service-pillars`, `industries`, `projects`, `case-studies`, `insights`, `news`, `news-categories`, `glossary`, `integrations`, `offices`, `markets`, `pages`, `ctas`, `settings`, `search`, `inquiries` (POST), `consultations/availability`, `consultations` (POST), `consents` (POST), `tools/{tool}/runs`, `tools/runs/{id}`, `tools/runs/{id}/report-requests` (POST).
  - **Admin (`/api/v1/admin/...`):** the same content resources plus `content-opportunities`, `article-drafts`, `news-sources`, `leads`, `inquiries`, `bookings`, `availability`, `analytics`, `search-performance`, `provider-connections`, `notifications`, `audit-logs`, `retention-policies`, `data-subject-requests`, `jobs`, `redirects`, `media`, `email-templates`, `users`, `roles`.
  - Still one controller per business resource.

## 25. Security addendum (Rev 3)

- **Turnstile** on contact, start-project, consultation request/booking, tool runs, report requests, and any other abuse-sensitive anonymous action. The API verifies every token server-side (secret in the secret store; expected hostname/action checked; single use). Client-side checks are never trusted. On failure → 400 ProblemDetails, logged without PII.
- **Booking abuse:** per-IP and per-email limits, a slot hold timeout, signed cancel/reschedule links.
- **News ingestion:** allowlisted sources only, fetched via SafeOutboundHttp, response-size caps, HTML stripped/sanitized before summarization, and **feed content treated as untrusted data** in LLM prompts (prompt-injection defence).
- **LLM usage (content engine, news, advisor):** no secrets or lead PII in prompts. Outputs are sanitized. Prompts and providers sit behind an abstraction. Data-residency decision **[OPEN]**.
- **Analytics and integrations:** provider secrets only in the secret store. Admin dashboards read local aggregates. CSP allows analytics domains, and the scripts are injected only after consent.
- **Data Protection keys** (BFF cookies, signed links, encrypted values) are persisted to durable storage shared by instances and **included in the backup plan** (§26).
- All Rev 1–2 rules remain (BFF, CSRF, CORS deny, CSP nonces, headers, upload validation, SSRF gateway, ProblemDetails, logging hygiene, least privilege).

### 25.1 Admin BFF session and token protection (D40, option a)

| Aspect | Design |
|---|---|
| **Login** | The Admin login form posts to Onyx.Web (antiforgery + Turnstile + API rate limits). Onyx.Web calls the API server-to-server (`POST /api/v1/auth/login`, then the 2FA step) over HTTPS. The browser never talks to the API. |
| **Tokens** | **Access token:** a JWT, lifetime ≈ 10 min (configurable), signed by the API, with minimal claims: `sub`, session id, security-stamp version, `iss`, `aud`, `iat`, `exp`. **No permission list in the token** (permissions are resolved server-side). **Refresh token:** an opaque 256-bit random value, stored **hashed** in the API DB, rotating, in a token family per login session. |
| **Where tokens live** | Inside Onyx.Web's authentication ticket, stored in the **auth cookie**. The ticket is **encrypted and signed by ASP.NET Core Data Protection** (authenticated encryption, purpose-isolated). The cookie value is opaque. Tokens are never rendered into HTML, never sent to browser JavaScript, never placed in localStorage/sessionStorage, never logged (the `Authorization` header is redacted from HttpClient logging). |
| **Cookie attributes** | Name `__Host-onyx.admin` (so `Secure`, `Path=/` and no `Domain` are guaranteed by the browser); `HttpOnly`; `Secure`; `SameSite=Strict`; browser-session cookie (no persistent expiry); issued only after successful 2FA where 2FA is required. |
| **Session lifetime** | **Absolute limit** ≈ 12 h (ticket `ExpiresUtc` + refresh-family absolute expiry, enforced by the API). **Idle limit** ≈ 60 min (refresh token idle expiry). Both are configurable. Expiry → redirect to login. |
| **Refresh** | When the access token is within 60 s of expiry, or the API returns 401, Onyx.Web calls `POST /api/v1/auth/refresh` server-to-server with the refresh token. The API rotates it and returns a new pair. Web replaces both in the ticket and re-issues the cookie. Parallel requests: a per-session lock in Web **plus** a short API grace window (e.g. 30 s), during which the immediately preceding refresh token returns the *same* successor instead of triggering reuse detection. A refresh token reused outside the grace window → **the whole family is revoked** and the session ends. |
| **Revocation** | **Logout:** Web calls `POST /api/v1/auth/logout` (revokes the family) and deletes the cookie. **Admin actions** (disable user, password reset, role/permission change, 2FA reset) revoke the user's refresh families and bump the security stamp. The API checks the stamp version on every request (cached briefly), so outstanding access tokens stop working immediately, not after their expiry. A Web session whose refresh the API refuses is signed out; if the API is merely unreachable, the session is kept and the refresh is retried on the next request. |
| **Stolen-cookie exposure** | Mitigated by `HttpOnly`, `Secure`, `SameSite=Strict`, CSP, short absolute and idle lifetimes, and server-side revocation (a stolen cookie is useless once the family is revoked or the stamp changes). Admin "sessions" view with revoke-all comes later. |
| **Cookie size** | The minimal JWT (~0.5 KB) + refresh token + a few claims gives an encrypted ticket of ≈ 1.5–2.5 KB, under the 4 KB per-cookie limit. ASP.NET Core's chunking cookie manager handles overflow. A test asserts the cookie stays within the budget (Stage 4). |
| **Data Protection keys (Web)** | Fixed application name `Onyx.Web`. Keys persisted to a configurable shared directory (portable; Web has no DB access), protected at rest where the host supports it, rotated on the default schedule (90 days), and **included in backups** (§26). Losing the keys signs everyone out; it does not expose data. |
| **CSRF** | Cookie auth in Web → global antiforgery on all state-changing requests. `SameSite=Strict` is defence in depth. The API never accepts cookies (bearer only). |

**API-side implementation (Stage 3)**
- **Grace window:** the successor refresh token is stored encrypted (AES-256-GCM) under a key derived (HKDF) from the *predecessor's raw value*, which only the client holds. A concurrent refresh within 30 s receives the same successor; the database alone never yields a usable token.
- **Two-step sign-in:** the password step returns a hashed, single-use, 5-minute challenge (invalidated after 5 wrong codes). If an authenticator is mandatory but not yet enrolled, it returns an enrolment challenge instead, and no token is issued before enrolment.
- **Enrolment binding:** each enrolment start generates a fresh key bound to that enrolment challenge (stored as a user token). Completion requires the same challenge, so a key from an abandoned, stale, concurrent or reset attempt can't complete enrolment. The binding is removed on completion and on an admin 2FA reset.
- **Break-glass recovery:** the `reset-two-factor` operator command (command line only, no HTTP surface) resets the last enrolled SuperAdmin's second factor. It requires a typed email confirmation and an audit reason, and refuses while another active, enrolled SuperAdmin could reset the account through the admin API. It removes the key, recovery codes and enrolment binding, closes sign-in steps, revokes sessions, clears lockout, rotates the stamp and writes a `TwoFactorRecoveredByOperator` audit record (`docs/development-setup.md` §7).
- **TOTP:** in-house RFC 6238 verifier with replay protection (a code is accepted only for a later time step than the last one used).
- **Encryption at rest:** authenticator keys and recovery codes are encrypted with Data Protection, through a custom user store.
- **Mandatory 2FA:** SuperAdmin, anyone holding `Users.Manage`, `Roles.Manage`, `ProviderConnections.Manage`, `Privacy.Manage` or `Leads.Export`, or everyone when `IdentitySecurity:RequireTwoFactorForAllUsers` is true.
- **API Data Protection key ring:** stored in PostgreSQL (`data_protection_keys`) by an in-house `IXmlRepository`, shared by all API instances and covered by database backups.
- **Secure default:** every matched endpoint requires authentication unless it declares `[AllowAnonymous]`; a test fails if any endpoint lacks explicit authorization metadata.
- **Escalation guard:** admins can only grant, remove or act on permissions they hold; only a SuperAdmin can act on SuperAdmin; the last active SuperAdmin cannot be disabled or demoted.

**Web-side implementation (Stage 4)**
- **Ticket contents:** three claims (id, display name, preferred language) plus the access token, refresh token and access expiry. The ticket expiry equals the API session's absolute expiry. The cookie is session-only (`IsPersistent=false`), with no sliding expiration. A test keeps it under 4 KB without chunking.
- **Refresh:** done in `OnValidatePrincipal` 60 s before the access token expires. Concurrent refreshes in one instance are collapsed into one API call (keyed by a hash of the refresh token). Across instances, the API's grace window applies. POSTs to the API are never retried by the resilience pipeline, so a retry can't look like token reuse.
- **Session ended mid-request:** a 401 from the API raises `AdminSessionEndedException`. A global filter signs out and redirects to sign-in with a local `returnUrl`. The admin shell (name, permission-filtered menu, unread count) is loaded in an action filter, so this also works before the view renders.
- **Sign-in steps:** 2FA challenge and enrolment tokens are kept only in the encrypted TempData cookie (`__Host-onyx.tmp`), never in markup. Recovery codes are shown once, in a `no-store` response.
- **Emailed links:** a reset or invitation link (`?userId=&token=`) is validated for shape, moved into encrypted TempData, and the browser is immediately redirected to the clean URL. The token therefore never stays in the address bar or history, and never reaches a referrer. Account pages send `Referrer-Policy: no-referrer` and `Cache-Control: no-store`, and load no third-party resources.
- **Return URLs:** after sign-in, only local paths under `/admin` are followed. The public language switch and consent redirects accept only local paths in the chosen language.
- **Client address:** Onyx.Web resolves the visitor IP through its own forwarded-headers middleware (trusted `ForwardedHeaders:KnownProxies`/`KnownNetworks` only, loopback by default). It sends that address to the API as `X-Forwarded-For`, together with `X-Correlation-ID` and `Accept-Language`. The API trusts the header only from its own configured proxies, which is loopback in development; in production these must list Onyx.Web's address.
- **API outage:** transport failures (connection, timeout, open circuit) become an "unavailable" result. Forms show a service message; pages that need the API return a 503 error page; sessions are kept.

## 26. Backup and recovery

Host-agnostic requirements; the concrete implementation is finalized for the selected production environment.

| Concern | Requirement |
|---|---|
| PostgreSQL | Automated physical base backups (e.g. `pg_basebackup` or the host's snapshot facility) + **continuous WAL archiving for point-in-time recovery**; periodic logical dumps (`pg_dump`) for portability; defined retention; encrypted, off-host copies |
| Media / file storage | Separate automated backups or versioned storage; retention aligned with the DB so restores are consistent |
| Configuration and secrets | Secrets in a secret store with its own recovery procedure; never in backups of source control |
| Data Protection key ring | Backed up and restorable; loss would invalidate sessions and signed links |
| Restore testing | Periodic restore drills to a non-production environment, documented results |
| Recovery procedure | Written runbook: RPO/RTO targets **[OPEN]**, order of restore (secrets → DB → media → apps), verification steps |
| Retention interplay | Backups respect deletion requests within the backup retention window (documented in the privacy policy) |

## 27. Brand and design direction (design phase later; nothing designed yet)

- **Logo (owner-supplied reference):** a lowercase geometric wordmark "onyx". The "o" holds a faceted onyx stone/crystal inside a black circle. The "n" and "y" use sharp breaks and cuts. The palette is monochrome black/grey on light. The final asset (vector) will be provided and is the primary brand reference.
- **Direction:** premium, elegant, modern enterprise technology; minimal; open and spacious; **white-first** with strong whitespace; black/near-black typography; restrained neutral tones; maximum one restrained accent if any (to be defined from the brand).
- **Signature:** the Onyx stone, used as a refined abstract/3D stone in the hero, subtle geometric facets derived from the logo cuts, occasional section transitions, and selective interactive moments. **Not repeated in every section.**
- **Avoid:** clutter, heavy gradients, "AI startup" visuals, robots, glowing brains, AI faces, card grids everywhere, excessive animation, auto-opening widgets.
- **Arabic:** RTL quality is critical. Choose an Arabic typeface that harmonizes with the geometric wordmark. **[OPEN]** whether an Arabic logo lockup exists.
- **Non-negotiable:** WCAG 2.2 AA; Core Web Vitals LCP < 2.5s, INP < 200ms, CLS < 0.1; 3D/motion lazy-loaded, with a static fallback and `prefers-reduced-motion` support.
- Earlier mockups are not mandatory. The design system is created from scratch in the design phase.

## 28. Phasing (approved)

| Phase | Scope |
|---|---|
| **1. Launch** | Solution scaffold incl. Worker (BackgroundService + DB job/outbox); bilingual CMS site (Arabic default, localized URLs); 5 pillars with confirmed services; industries (taxonomy + qualified pages); Work (confirmed projects; case studies where verified); insights; glossary; offices; start-a-project, contact, **consultation booking**; email notifications/confirmations; Turnstile; cookie consent; retention and data-subject request foundations; Inquiry/Lead + attribution; site search; notification centre; activity/audit; admin CMS with roles/permissions; technical SEO; **News Hub** (full pipeline; auto-summaries/auto-publish activate with the LLM provider); **Admin Analytics & Marketing dashboard** (GA4 + Search Console adapters, sync jobs, local aggregates, funnel views; inactive until credentials) |
| **2. Tools and editorial AI** | Opportunity Finder → Preliminary Security Assessment → SEO Analyzer (after the SEO platform API is defined); AI Content Engine |
| **3. Intelligence and growth** | AI Advisor; ad-platform providers; market pages; CRM; WhatsApp integration (if approved) |

## 29. Open items

### A. Needed before scaffolding begins
**None.** Phasing (D35), Worker mechanism (D36), test database (D37), PostgreSQL (D39) and BFF token storage (D40) are resolved.

### A2. Environment prerequisites (owner action)
- **Create the PostgreSQL test role and set `ONYX_TEST_POSTGRES`.** Needed to *run* integration tests; without it they are skipped. SQL and steps are in `docs/development-setup.md`.
- **Create the development database and roles** (`onyx_dev`, owner and app roles), needed to run the API locally (`docs/development-setup.md`).
- Recommended: update the .NET runtime/SDK to the latest 10.0 patch (runtime 10.0.10 is installed; packages use 10.0.12).

### B. Needed before a specific feature is *activated* or finalized (non-blocking for building it)
3. **Booking:** slot length, working days/hours, format (online/in-person/phone), attendees, meeting-link provider. (Built with admin-configurable availability, so these are data entry, not code.)
4. **Email:** info address(es) per inquiry type, sending domain, initial transport (SMTP/M365/provider).
5. **LLM provider and data residency/PDPL position:** for the News Hub, Content Engine and Advisor. The provider-neutral `ITextGenerationService` is built in Phase 1. Until a provider is chosen, news items are held for manual summaries (§17).
6. **News Hub:** initial source allowlist with licensing review; confirm the `noindex` default for auto-published items.
7. **GA4 property and Search Console property access** (service account) for Analytics.
8. **Content-engine research source:** whether an external search/research API is used.
9. **SEO platform API definition** and real capability list.
10. **Attribution cookie under consent:** legal confirmation of the approach in §14.

### C. Needed before production launch (content/legal/ops)
11. Confirmed services per pillar and their final names (Arabic/English).
12. Portfolio details per project (capabilities, technologies, screenshots, client permissions), which projects get case studies, the **3 featured projects**, and the final marketplace name. Confirm the exact Arabic product name «سجل الطبي» (the standard definite form would be «السجل الطبي»; keep the brand spelling if intentional).
13. Office addresses and contact details (Riyadh, Granada District; Amman).
14. Privacy policy, cookie policy, retention periods, tool terms of use, security-testing authorization template (KSA + Jordan legal review).
15. Backup RPO/RTO and the host-specific backup implementation.
16. Final logo vector, an Arabic lockup if any, and typography licences.
17. Lead routing/ownership and visibility scoping.
18. Verify that the production PostgreSQL host runs **PostgreSQL ≥ 17** (builtin `C.UTF-8` locale provider) and supports `pg_trgm`, the built-in `arabic`/`english` text-search configurations, and WAL archiving / point-in-time recovery.
