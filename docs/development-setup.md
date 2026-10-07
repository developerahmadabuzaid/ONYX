# Development Setup

Local prerequisites for building, running and testing Onyx. **Never commit passwords.** Real values go in environment variables or `dotnet user-secrets`.

## 1. Tooling
- .NET SDK 10.0.x (pinned in `global.json`). Keep the runtime on the latest 10.0 patch.
- PostgreSQL **17 or newer** (18.x recommended). `psql` lives in `C:\Program Files\PostgreSQL\<version>\bin`.
- Restore local tools once: `dotnet tool restore` (provides `dotnet ef`).

## 2. PostgreSQL roles and databases (run once, as a PostgreSQL superuser)

Choose strong passwords and replace the placeholders. Run in `psql` connected to the `postgres` database:

```sql
-- Owner / migration role: owns the Onyx schema objects; used for `dotnet ef database update` locally.
CREATE ROLE onyx_owner LOGIN PASSWORD '<owner-password>' NOSUPERUSER NOCREATEDB NOCREATEROLE;

-- Runtime application role: data access only (no DDL).
CREATE ROLE onyx_app LOGIN PASSWORD '<app-password>' NOSUPERUSER NOCREATEDB NOCREATEROLE;

-- Test role: may create databases (and therefore drop only the ones it owns). Never a superuser.
CREATE ROLE onyx_test LOGIN PASSWORD '<test-password>' NOSUPERUSER CREATEDB NOCREATEROLE;

-- Development database (dedicated to Onyx).
CREATE DATABASE onyx_dev
  OWNER onyx_owner
  ENCODING 'UTF8'
  LOCALE_PROVIDER builtin BUILTIN_LOCALE 'C.UTF-8'
  LC_COLLATE 'C' LC_CTYPE 'C'
  TEMPLATE template0;
```

Then, connected to `onyx_dev` **as `onyx_owner`**:

```sql
GRANT CONNECT ON DATABASE onyx_dev TO onyx_app;
GRANT USAGE ON SCHEMA public TO onyx_app;
ALTER DEFAULT PRIVILEGES FOR ROLE onyx_owner IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO onyx_app;
ALTER DEFAULT PRIVILEGES FOR ROLE onyx_owner IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO onyx_app;
```

## 3. Integration tests

Integration tests create and drop their **own** disposable databases (`onyx_test_<timestamp>_<random>`) using the `onyx_test` role. They never touch `onyx_dev` or any other database. Because `onyx_test` is not a superuser, PostgreSQL itself refuses to let it drop databases it doesn't own.

Set the connection string (maintenance database `postgres`; **no other database name is allowed**):

```powershell
# PowerShell (current user, persistent)
[Environment]::SetEnvironmentVariable('ONYX_TEST_POSTGRES', 'Host=localhost;Port=5432;Database=postgres;Username=onyx_test;Password=<test-password>', 'User')
```

Restart the terminal/IDE, then run `dotnet test`. If `ONYX_TEST_POSTGRES` is not set, database-backed tests are **skipped** (reported as skipped, not passed).

The test fixture refuses to run if:
- the connection string targets any database other than `postgres`;
- the role is a superuser or lacks `CREATEDB`;
- any operation would target a database not created by the current run.

## 4. Running the API locally

```powershell
cd src/Onyx.API
dotnet user-secrets set "ConnectionStrings:Onyx" "Host=localhost;Port=5432;Database=onyx_dev;Username=onyx_app;Password=<app-password>"
```

Apply migrations with the owner role (never the app role):

```powershell
dotnet ef database update --project src/Onyx.Infrastructure --startup-project src/Onyx.Infrastructure --connection "Host=localhost;Port=5432;Database=onyx_dev;Username=onyx_owner;Password=<owner-password>"
```

The API never migrates the database on startup.

## 5. API secrets and the first administrator

The JWT signing key is a secret (≥ 32 random bytes, base64). It is never committed. Generate and store it in user secrets:

```powershell
$bytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
cd src/Onyx.API
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($bytes))
```

Seed the roles, then create the first SuperAdmin. The password is read from an environment variable, never from the command line, and is never printed:

```powershell
dotnet run --project src/Onyx.API -- seed-identity

$env:ONYX_BOOTSTRAP_PASSWORD = '<choose a long passphrase (12+ characters)>'
dotnet run --project src/Onyx.API -- bootstrap-superadmin --email <your admin email> --name "<your name>" --language ar
Remove-Item Env:ONYX_BOOTSTRAP_PASSWORD
```

The command refuses to run if a SuperAdmin already exists. At first sign-in the SuperAdmin must enrol an authenticator app.

`IdentitySecurity:AdminPortalBaseUrl` (the Onyx.Web admin URL, used in invitation and password-reset links) is `https://localhost:7200` in Development and must be set for production.

## 6. Running Onyx.Web with the API

Onyx.Web talks only to Onyx.API over HTTPS. It never touches the database. Trust the development certificate once (`dotnet dev-certs https --trust`), then start both hosts in separate terminals:

```powershell
dotnet run --project src/Onyx.API --launch-profile https   # https://localhost:7180
dotnet run --project src/Onyx.Web --launch-profile https   # https://localhost:7200
```

- Public site: `https://localhost:7200/` (redirects to `/ar/`). Admin: `https://localhost:7200/admin`.
- Development settings (`src/Onyx.Web/appsettings.Development.json`):
  - `OnyxApi:BaseUrl = https://localhost:7180`;
  - `Site:PublicBaseUrl = https://localhost:7200`;
  - `DataProtection:KeysDirectory = ../../.local/web-keys`, resolved from the project directory to `<repo>/.local/web-keys` (git-ignored).
- Onyx.Web needs no secrets in development. Its Data Protection keys encrypt the admin cookie. Deleting them only signs admins out.
- **Media (API):** `Media:StorageRoot = ../../.local/media` in `src/Onyx.API/appsettings.Development.json`, resolved from the API project directory to `<repo>/.local/media` (git-ignored). Uploaded images are stored there under random names; they are served only through the API (live content or a valid preview token).
- **CMS routes:** every `/{lang}/...` path that is not a code route is resolved as CMS content by the API (200, 301 to the current URL, 410 or 404). `/sitemap.xml`, `/sitemaps/{lang}.xml` and `/robots.txt` are generated; until `Site:AllowIndexing` is `true`, robots.txt disallows everything and every page carries `noindex, nofollow`.
- **Client address:** Onyx.Web sends the visitor IP to the API as `X-Forwarded-For`. In development both run on loopback, which ASP.NET Core trusts by default, so the API's rate limits and audit records see the real visitor address.

**Production settings (no defaults; set per environment):**

| App | Setting | Value |
|---|---|---|
| Onyx.Web | `OnyxApi:BaseUrl` | the API's internal HTTPS address |
| Onyx.Web | `Site:PublicBaseUrl` | the public site origin (used for canonical/hreflang) |
| Onyx.Web | `Site:AllowIndexing` | `true` only at launch |
| Onyx.Web | `DataProtection:KeysDirectory` | a directory shared by all Web instances and **included in backups** |
| Onyx.Web | `ForwardedHeaders:KnownProxies` / `KnownNetworks` | the reverse proxy / load balancer in front of Web |
| Onyx.Web | `AllowedHosts` | the public host name(s) |
| Onyx.API | `ForwardedHeaders:KnownProxies` / `KnownNetworks` | **Onyx.Web's address(es)**, so only Web can supply the visitor IP |
| Onyx.API | `IdentitySecurity:AdminPortalBaseUrl` | the public Web origin (for emailed admin links) |
| Onyx.API | `Privacy:IpHashKey` (optional secret) | enables keyed hashing of IP addresses on consent receipts; without it, no IP is stored |
| Onyx.API + Onyx.Worker | `Media:StorageRoot` | the **same** directory **outside any web root**, shared by all API and Worker instances and **included in media backups** (uploads fail until it is set; the Worker removes files of deleted media) |
| Onyx.API | `Content:PreviewTokenMinutes` | draft preview link lifetime (default 30) |
| Onyx.API + Onyx.Worker | `Site:PublicBaseUrl` / `Site:AllowIndexing` | **the same values as Onyx.Web** (public https origin; `true` only at launch) |
| Onyx.API + Onyx.Worker | `IndexNow:Key` | 8–128 letters/digits/hyphens; leave empty until launch (IndexNow stays inactive while empty or while indexing is off) |
| Onyx.API | `Seo:OrganizationStructuredDataEnabled` | `true` only after verifying the Brand Kit logo and website |
| Onyx.API | `Media:MaxFrames` | most frames per animated GIF (default 1000) |

## 7. Recovery: SuperAdmin lost their authenticator (`reset-two-factor`)

Break-glass operator command for when a SuperAdmin can no longer complete the second factor (lost device, exposed setup secret) **and no other SuperAdmin can reset them** through the admin portal. Like `bootstrap-superadmin`, it runs on the operator's machine or server against the configured database. It has **no HTTP endpoint**.

```powershell
dotnet run --project src/Onyx.API -- reset-two-factor --email <superadmin email> --confirm <same email> --reason "<why; stored in the audit log>"
```

In production, run the published API with the same arguments (`dotnet Onyx.API.dll reset-two-factor …`), using that environment's normal configuration and secrets.

**Safeguards. The command refuses to run when:**
- `--confirm` doesn't repeat `--email`, an option is unknown or repeated, or `--reason` isn't 10–500 characters. These checks happen before anything touches the database.
- The account doesn't exist, isn't a SuperAdmin, or is disabled. Other users are reset in the admin portal (`POST /api/v1/admin/users/{id}/two-factor/reset`).
- Another **active SuperAdmin with 2FA enabled** exists. That person should reset the account through the audited admin portal instead.

**Effect (one transaction):**
- 2FA disabled; authenticator key, recovery codes and any pending enrolment binding removed;
- open sign-in steps closed; all sessions revoked;
- lockout and failed-attempt counter cleared;
- security stamp rotated;
- a `TwoFactorRecoveredByOperator` security audit record written (reason, operator account `user@host`, counts).

The password is **not** changed. The account owner signs in with their password and must enrol a new authenticator straight away. The command prints only the user id and counts, never keys, codes, tokens or passwords.

**Before running it:** verify the account owner's identity out of band, and record who authorized the recovery in `--reason`.

## 8. Leads, booking and Turnstile (Stage 8)

**Turnstile (Cloudflare) is the only external service the conversion forms use, and nothing enables it automatically.**
- **Production requires Turnstile (Stage 9):** Onyx.API does not start in Production without `Turnstile:SecretKey` (and a hostname from `Turnstile:AllowedHostnames`, `Site:PublicBaseUrl` or `IdentitySecurity:AdminPortalBaseUrl`); Onyx.Web does not start without `Turnstile:SiteKey`. Development and Testing still run without keys.
- **Public forms fail closed.** Without keys, the contact, project and consultation forms say that online submission is unavailable, and the API refuses submissions with 503 `Captcha.Unavailable`.
- **Admin sign-in and password reset** require a Turnstile token whenever Turnstile is configured. Without keys they keep their existing protections (rate limit, lockout, mandatory 2FA).
- **Keys:** create a Turnstile widget in the Cloudflare dashboard that allows the site hostnames (add `localhost` for development), then store the keys:

```powershell
dotnet user-secrets set "Turnstile:SecretKey" "<secret key>" --project src/Onyx.API   # secret: never in appsettings or git
# The site key is public (it is rendered into the form pages); set it in Onyx.Web's user secrets or environment:
dotnet user-secrets set "Turnstile:SiteKey" "<site key>" --project src/Onyx.Web
```

- **What the API checks:** the token is verified server-side against Cloudflare's siteverify endpoint. The provider must report success for this secret; the token must be solved for the form's action (`inquiry`, `booking`, `admin-login`, `password-reset`), on an allowed hostname, within `Turnstile:MaxTokenAgeSeconds` (default 300); and each token is accepted once.
  - **Allowed hostnames:** `Turnstile:AllowedHostnames` lists them. When it's empty, the hosts of `Site:PublicBaseUrl` and `IdentitySecurity:AdminPortalBaseUrl` are used.
- **Logging:** the token, the secret and the visitor IP are never logged.

**Business settings (no invented values):**

| App | Setting | Value |
|---|---|---|
| Onyx.API + Onyx.Worker | `Leads:DefaultRecipients` | internal notification address(es); empty means inquiries are stored and shown in Admin, but no internal email is sent |
| Onyx.API + Onyx.Worker | `Leads:RecipientsByType:{InquiryType}` | optional per-type addresses (e.g. `Consultation`) |
| Onyx.API | `Leads:ConsentVersion` | identifies the form-consent wording; change it whenever that wording changes |
| Onyx.API | `Leads:ManageLinkHoursAfterSlot` | how long the visitor booking link still serves the calendar file after the slot (default 24) |
| Onyx.Worker | `IdentitySecurity:AdminPortalBaseUrl` | admin origin for the lead link in internal emails (same value as the API) |
| Onyx.API + Onyx.Worker | `Site:PublicBaseUrl` | public origin for the visitor booking-management link (https) |
| Onyx.API | `Turnstile:SecretKey` (secret) | see above |
| Onyx.Web | `Turnstile:SiteKey` | see above |

- **Consultation booking** is configured in Admin → Availability:
  - the settings are the business time zone (default Asia/Riyadh), slot length, buffer, minimum notice, horizon, and the on/off switch;
  - weekly hours and blocked dates are added there too.
  - Nothing is preset. Online booking stays closed, and visitors get the consultation request form instead, until the hours are entered and booking is enabled.
- **Emails** are queued by the Worker (visitor confirmations in the submission language; internal emails carry a link to the lead, never the visitor's contact details or message). The visitor booking confirmation, with its `.ics` file and signed manage link, is queued together with the booking. Wording is editable in Admin → Email templates.

## 9. Privacy operations (Stage 9)

Privacy work is done in Admin by staff holding `Privacy.Manage`. That permission is 2FA-privileged. There is no public request form.

**Data-subject request procedure** (Admin → Data-subject requests):
1. **Record the request.** Enter the type (Export or Erasure), the subject's email address and how the request arrived. The address entered here defines whose data is affected; it cannot be changed later.
2. **Verify the requester's identity** by the standard set in the approved privacy procedure (an owner/legal input), then record how it was verified. Nothing runs before this step.
3. **Carry out the request:**
   - **Export:** download the JSON file. It is generated on the spot and never stored by Onyx; hand it to the subject through a secure channel and do not keep copies.
   - **Erasure:** type the address again and confirm. Personal data is removed in one transaction: lead records (notes deleted; inquiries, leads and bookings anonymized), every email to the address, and any admin account with that address (disabled and anonymized). Erasure is refused for your own account and for the last active SuperAdmin.
4. **Or reject** with a reason. Every step is audited without the address or exported content.

Erasure cannot be undone. Data in backups ages out with the backup retention (architecture.md §26).

**Retention** (Admin → Retention policies):
- **Nothing is preset.** Enter each category's period and action from the approved privacy policy, and use **Preview** to see how many records would be processed.
- **Enabling:** the daily Worker job (`retention-enforcement`, 02:30) acts only on categories saved **enabled with a period**.
- **Job sizing:** `Privacy:RetentionBatchSize` (default 200) and `Privacy:RetentionMaxBatchesPerRun` (default 50) bound each run. Both are technical limits, not retention periods.
- **Results:** each run is recorded as a job run and audited per category (counts only).

## 10. Site search (Stage 10)

- **Server requirements:** PostgreSQL 17 or later, with the database created as in §2 (builtin `C.UTF-8` locale, UTF8). The built-in `arabic`, `english` and `simple` text-search configurations ship with PostgreSQL. The `SiteSearch` migration creates the `pg_trgm` extension. It is a trusted extension, so the database owner (`onyx_owner`) can create it without superuser rights. The search integration tests verify all of this on the server they run against.
- **Filling the index:** publishing, unpublishing and URL changes re-index pages through the Worker's outbox. The `search-index-reconcile` job (every 30 minutes) builds the index the first time and repairs anything missed. On a fresh database, run the Worker once, or wait for the job, before expecting results.
- **Rate limit:** `RateLimiting:Search:PermitLimit` / `WindowSeconds` (default 30 per 60 seconds per client IP) in Onyx.API settings.
- **Privacy:** search queries are never stored. Request logs record the path only, and Onyx.Web's outbound HttpClient logging stays at `Warning`. Keep it there, because lower levels log full request URIs, including the search text.

## 11. News Hub (Stage 11)

- **Nothing is preset.** The migration seeds the four launch categories but no news source. Add sources in Admin → News sources, staff holding `NewsSources.Manage`:
  - a new source is **blocked**;
  - record the reviewed licensing/terms note, then **Allow** it;
  - the Worker fetches it on the next `news-ingestion` run (every 15 minutes).
- **Feed URLs** must be public `https://` URLs on a domain name. Internal hosts, IP addresses, credentials and non-standard ports are refused, at registration and again when the Worker connects.
- **Summaries and publishing.** No summary service is connected, because Onyx's AI Agent is external, so every ingested item is **Held**. Editors write the Arabic and English headline and summary in Admin → News items and publish. Nothing is published automatically until an adapter for `INewsSummaryService` is registered *and* a source is set to auto-publish.
- **Settings (Onyx.Worker):**
  - `News:MaxSourcesPerRun` (10), `News:MaxEntriesPerSource` (50), `News:MaxSummariesPerRun` (20), `News:SummaryTimeoutSeconds` (30), `News:RunBudgetMinutes` (4), `News:SimilarWindowHours` (72);
  - `SafeOutboundHttp:TimeoutSeconds` (20), `ConnectTimeoutSeconds` (10), `MaxResponseBytes` (2 MB), `MaxRedirects` (3), `UserAgent` (`OnyxVanguard-NewsReader/1.0`; robots.txt rules are matched against its first part).
  
  All are technical bounds, not business facts.
- **Network:** the Worker host needs outbound HTTPS to the allowlisted publishers. The gateway refuses proxies and private addresses by design, so outbound traffic must reach the public internet directly.
