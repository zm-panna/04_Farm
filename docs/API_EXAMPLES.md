# Farm API and authentication contract

Current revision: 2026-10-19. This document replaces historical header-based examples.

## Session and tenant selection

1. GET `/Account/Login`, then POST its form with the returned anti-forgery token and the locally provisioned Identity credentials. Never commit credentials or session cookies.
2. GET `/Context`, select a company/branch returned for that authenticated user, and POST `/Context/Select` with the form anti-forgery token.
3. The server issues authenticated active-company/branch claims and rechecks current database membership on operations. `X-Company-Id` and `X-Branch-Id` are **not** tenant selectors and do not grant access.
4. All unsafe MVC/API actions require anti-forgery validation. For JSON POSTs, use the token from an authenticated same-origin form as `X-CSRF-TOKEN` and keep the corresponding anti-forgery/session cookies. For form POSTs send `__RequestVerificationToken`.

There is no public bearer-token/mobile API provisioning in this release. Use same-origin authenticated UI; do not disable CSRF to integrate a third party.

## Principal endpoints

| Endpoint | Input and permission |
|---|---|
| POST `/api/farm/stock` | Form `sku` (2–60 characters), `name` (2–160), `cost` (nonnegative, three decimals), `unitOfMeasure` (explicit stock unit code, e.g. `KG`); `Inventory.Create`. Creates a zero-quantity product with audited unit assignment; units cannot be guessed or converted. |
| POST `/api/farm/animals` | Form `tag` (2–70 characters), `breed` (2–100), `cost` (nonnegative, three decimals); `Livestock.Create`. A nonzero cost creates a linked acquisition journal/document atomically. |
| POST `/api/operations/purchase` | Form `itemId`, `quantity`, `price`, `reference`; `Purchase.Post`. Direct receipt, not the optional PO approval flow. |
| POST `/api/operations/sale` | Same form; `Sales.Post`; stock, receivable/revenue/COGS posted atomically. |
| GET `/api/operations/summary` | `Reports.View`; selected company/branch, all-time summary. |
| GET `/api/operations/summary.csv` | Same scope and permission; CSV download. |
| GET `/api/advanced/ledger?from=…&to=…&account=…` | ISO UTC dates; `Accounts.View`. |
| GET `/api/advanced/trial-balance?through=…` | ISO UTC date; `Accounts.View`. |
| GET `/api/advanced/trial-balance.xlsx?through=…` | Same permission; Excel. |
| GET `/api/advanced/farm-summary.pdf` | `Reports.View`; explicit production QuestPDF license configuration required. |
| POST `/api/advanced/payment` | JSON `{ "reference":"PAY-UNIQUE", "partyType":"Customer", "amount":10, "documentId":123 }`; `Accounts.Post`; selected outstanding document required. Records bookkeeping only, never transfers money. |
| POST `/api/advanced/reconcile` | JSON `statementLineId`, `voucherId`; `Accounts.Reconcile`; exact cash amount and one-to-one matching. |
| POST `/api/advanced/ai/chat` | JSON `question`; `AI.Chat` and scoped report access; provider disabled by default. |
| GET `/api/operations/ai/summary` | Deterministic read-only allowlisted facts; scoped permissions. |
| GET `/api/operations/automation/{id}/dry-run` | `Automation.Review`; preview only. |
| POST `/api/advanced/automation/request` | JSON `ruleId`, `payload`; `Automation.Request`; immutable tenant-bound hash. |
| POST `/api/advanced/automation/approve` | JSON `approvalId`, `payload`; `Automation.Approve`; separate approver, matching hash. No arbitrary financial execution. |
| GET `/api/operations/subscription` | Current calculated company subscription state. |
| POST `/api/operations/subscription/payment` | Form `invoiceId`, `reference`, `amount`; `Subscription.Pay` **and** trusted PlatformAdmin/platform.billing authority. Tenant wildcard alone cannot clear SaaS debt. No payment gateway charge. |
| GET `/ops/readiness` | Trusted PlatformAdmin or `permission=ops.readiness`; database connectivity, committed/applied migrations and model-snapshot consistency. Does not certify IIS, business readiness, backups or UAT. |

Quantities, prices and money use at most three decimal places and bounded decimal(18,3) values. References are normalized and unique in their documented tenant/branch scope. Expected validation conflicts are returned as safe 400/409 responses; authentication/permission failures as 401/403 on APIs.

Use the UI for party-linked receipts/sales, returns, refunds, purchase orders, stock policies, period close, staff administration and health follow-up. See `FINANCE_AND_INVENTORY_WORKFLOWS.md` and `FARM_BUSINESS_WORKFLOWS.md` for exact supported transitions and limitations.

Export labels follow the authenticated UI language cookie or explicit `?lang=en|bn|ar|banglish`. Arabic PDF/XLSX use RTL. Static account codes and numeric values are not translated.

Product metadata edits require the original `RowVersion` from the edit form; stale or missing tokens return HTTP 409 without saving. New animal UI acquisition cost accepts the same three-decimal range as the service. Later costs and weights remain immutable in master edits; use dated weight records for new measurements.
