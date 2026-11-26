# SaaS platform: customers, products, subscriptions, invoices and AI demos

The public site now works as a SaaS storefront next to the corporate site and the staff Admin. Visitors browse products,
plans and demos without an account; customers sign up, verify their email and manage their account; staff run the catalogue,
subscriptions, customers and invoices in the Admin. Design and rules: `docs/architecture.md` §24.8 and CLAUDE.md §2.4 and §12.

## What is implemented

| Area | Status |
|---|---|
| Customer accounts | Sign-up (Turnstile, versioned terms consent, optional separate marketing consent), email verification by a single-use link, sign-in with lockout, sign-out, forgot/reset password, change password, profile, delete account (erases personal data, cancels open subscriptions). No account enumeration. |
| Isolation from staff | Separate table (`customer_accounts`), opaque server-side sessions, separate API scheme (`Authorization: OnyxCustomer …`) and policy, separate Web cookie (`__Host-onyx.customer`). Staff endpoints authenticate only the staff JWT scheme; customer endpoints only the customer scheme. Customers never have roles or permissions. |
| Products | Admin catalogue (Arabic and English copy written separately, slugs, trial days, demo type and link, sort order), plans with optional prices, verification record, publish/unpublish. Public list and detail pages at `/ar/المنتجات` and `/en/products`. |
| Pricing | A plan price is shown publicly only when staff entered a real amount, a currency, a non-custom period and ticked “public price”. Otherwise “priced to your needs”. Nothing is seeded. |
| Intent preservation | Product buttons lead to `/{lang}/subscribe?product=…&intent=trial\|subscribe` and `/{lang}/ai-demo/{product}`. Signing in, signing up and confirming the email all carry a validated local `returnUrl` back there. |
| Subscriptions and trials | Trial starts immediately (once per customer and product, enforced by a unique index); a subscription request waits for staff to activate or reject it. One open subscription per product. Staff are notified (notification centre); customers are emailed on decisions and trial expiry. The Worker expires trials (`trial-expiry`, every 15 minutes). |
| Invoices | Manual invoicing by staff: draft (lines as “description \| quantity \| unit price”), issue (numbered `INV-YYYY-NNNNN` from a database sequence, bill-to snapshot), mark paid, void. Customers see only their own issued, paid or void invoices. |
| Customer dashboard | Overview (subscriptions, recent invoices, usage), profile, subscriptions (cancel), invoices (list and detail). |
| AI demo | For products whose demo type is “AI assistant”: verified customers only, Turnstile, prompt ≤ 1,000 characters, per-customer and global daily limits, switch `AiDemo:Enabled` (off by default). Runs are queued and processed by the Worker (`ai-demo-processing`, every minute) through `ITextGenerationService`, grounded only in the product's published copy. No LLM provider is connected yet, so runs fail visibly as “not connected”. |
| Admin | Menu group “Products & customers”: SaaS products, Customers (disable/enable), Subscriptions (by status, decide), Invoices. |

## Permissions

`Products.View`, `Products.Edit`, `Products.Publish` (verification and publishing), `Customers.View`, `Customers.Manage`,
`Subscriptions.View`, `Subscriptions.Manage`, `Invoices.View`, `Invoices.Manage`. SuperAdmin holds all of them; give them to
other roles in Admin → Roles.

## Database migration

`20261027165830_SaasPlatform` creates 11 tables (`customer_accounts`, `customer_tokens`, `customer_sessions`, `saas_products`,
`saas_product_translations`, `saas_product_plans`, `saas_product_plan_translations`, `customer_subscriptions`, `invoices`,
`invoice_lines`, `ai_demo_runs`), the `invoice_number_seq` sequence and their indexes. It does not alter existing tables.

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `Site:PublicBaseUrl` | API | Required for customer email links; sign-up answers “unavailable” until it is an https origin. |
| `Turnstile:SecretKey` / `Turnstile:SiteKey` | API secret store / Web | Customer forms fail closed without Turnstile (like the lead forms). |
| `Email:FromAddress` | API and Worker | e.g. `info@onyxvanguardsolutions.com`. |
| `Email:Transport`, `Email:Smtp:*` | Worker | `Transport` = `Smtp`; host, port, security and username in configuration; **password only in user-secrets or the host's secret store**. |
| `CustomerAccounts:*` | API | Terms version, token lifetimes, session idle/absolute days, lockout. |
| `AiDemo:*` | API | `Enabled` (default false), per-customer and global daily limits, max output tokens. |

Set the sender password locally with:

```bash
dotnet user-secrets set "Email:Smtp:Password" "<the mailbox password>" --project src/Onyx.Worker
```

Publish SPF, DKIM and DMARC records for the sending domain so verification emails are delivered.

## Tests

- `tests/Onyx.Domain.Tests/Commerce/CommerceDomainTests.cs`: account, token and session rules, publish and price guards,
  subscription and invoice lifecycles, demo limits.
- `tests/Onyx.Application.Tests/Customers/CustomerReturnPathTests.cs`: return paths (no open redirect), password policy.
- `tests/Onyx.API.IntegrationTests/Commerce/*`: isolation in both directions, sign-up without enumeration, single-use
  verification, lockout, reset and sign-out, disable and deletion, products and prices, trials and requests, staff decisions,
  invoices, AI demo limits and Worker processing.
- `tests/Onyx.Web.Tests/CustomerSiteTests.cs`: browsing, intent kept through sign-in, cookie and `OnyxCustomer` header, no
  cross-access between customer and staff sign-ins, no off-site redirects, Admin menu by permission.

## Known limitations and next steps

1. **Payments:** no payment provider; invoices are recorded and marked paid by staff. Choosing a provider (and approving its
   package) adds online payment, webhooks and receipts.
2. **AI provider:** the demo stays “not connected” until an LLM provider is approved, configured and passes a connection test.
3. **Product delivery:** products run as separate applications; there is no single sign-on into them yet.
4. **Retention:** add retention policies for unverified accounts, customer sessions and AI demo runs (periods are owner
   decisions, CLAUDE.md §10). Data-subject export does not yet include customer accounts.
5. **Search and sitemap:** product pages are not yet in the site search index or the sitemap.
6. **Customer 2FA:** optional by design and not built yet.
