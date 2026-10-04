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
| Onyx.API | `Media:StorageRoot` | a directory **outside any web root**, shared by all API instances and **included in media backups** (uploads fail until it is set) |
| Onyx.API | `Content:PreviewTokenMinutes` | draft preview link lifetime (default 30) |

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
