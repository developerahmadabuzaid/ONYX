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
