# Independent enterprise-module acceptance gates

Baseline reviewed: `farm_followup_revision` (2026-10-19). New implementation target: `farm_enterprise_revision`.

This is a local source/security audit. No staging connection, production SQL Server, customer payroll policy, payment-provider credentials, deployment, external messages, or payment operation is authorized/verified by these checks.

## Cross-cutting integration

- TenantContext checks authenticated user membership and active company/branch claims. There are no global EF tenant query filters: all new reads and writes must explicitly scope company and branch.
- New relational relationships must use scoped composite foreign keys. The baseline generic Integrity map recognizes only existing animal, stock, voucher, finance, and breeding relations.
- Every new controller/API action needs an explicit module permission. Separate sensitive payroll access from ordinary HR metadata. Anti-forgery applies to cookie-authenticated mutations.
- Financial/stock operations need a serializable transaction, unique scoped reference, validated precision and positive amounts, same-transaction audit and atomic rollback. A failed request must not leave tracked mutations that can later save.
- Baseline guards make posted vouchers/journal lines immutable and reject closed-period postings; new snapshots and outcome records need equivalent final-state protection.
- Adding entities requires a migration and drift check. Existing demo seeder/exporter coverage must not silently claim the old entity count includes new modules.

## HR/payroll

- Employee PII and compensation never appear under unrelated module permissions or another company/branch.
- Payroll period/run and employee uniqueness; maker cannot approve own run; permissions distinct for drafting, approving, posting, and paying if payout recording exists.
- Approved values are durable snapshots, including staff identity/rates/deductions. Editing employee master cannot alter approved obligations; approved/posting journal cannot be altered or duplicated.
- Payroll arithmetic validates precision, negative/zero boundaries and deductions greater than gross. Statutory policy is configurable or explicitly excluded until jurisdiction and approved policy are supplied.
- Draft, approval, posting, and payment transitions reject invalid order/replay; money journals are balanced and period-aware; rollback contains no partial state.

## CRM

- Branch/company scoped leads, contacts, history and exports. Conversion creates or links one customer, with repeat/concurrent conversion unable to duplicate.
- Existing trading-party integration verifies that the linked party is an active customer in the same company/branch.
- Marketing consent defaults false, is explicit/audited, and is not inferred from a purchase or conversion. No automatic external outreach.

## Fixed assets

- Acquisition/date/cost/residual/useful life validation. Depreciation cannot exceed cost minus residual; positive amounts and last-period rounding are exact.
- One asset-period posting, duplicate/replayed request rejected. No pre-acquisition or post-disposal depreciation.
- Disposal executes once, derecognizes cost and accumulated depreciation, balances gain/loss and proceeds, rejects invalid sequence, and is immutable.
- Closed-period guard prevents all acquisition/depreciation/disposal back-posting where applicable.

## Ecommerce

- Server-resolved catalog pricing; caller cannot override totals, discounts, paid state, customer identity, branch or tenant.
- Reservation/create/cancel/fulfil is atomic and retry-safe. Reservations constrain every existing stock deduction (sales, supplier returns, feed, adjustments and transfers), not just other ecommerce orders.
- Orders are scoped to staff tenant/branch, or authenticated customer identity when a real customer-auth flow is enabled; guessed IDs cannot access orders.
- No success redirect, browser input or unsigned callback can mark paid. Unconfigured payment provider remains disabled; signed webhook handling, if implemented, validates amount/currency/order/provider and replay.
- Price/order snapshots, sale/finance document references and stock movement ledger remain consistent across master-price changes and cancellation retries.

## Evidence required

- Run frozen-source release build, full relational xUnit suite, JavaScript suites and relevant HTTP acceptance suites independently.
- Add focused negative/replay/rollback probes based on the delivered module surface. Report failed, passed and unrun environments distinctly.
- Retain source hashes and test logs. A local SQLite test result is not SQL Server migration/execution evidence or staging/customer acceptance.

## Jurisdiction/policy update

The user identified Bangladesh, unspecified Arab country/countries and USA, with Windows PC and IIS. Exact Arab jurisdictions and US state are pending; actual server access is unavailable. Audit currency, timezone and country policy isolation and durable posting snapshots. Never mix unlike currencies into one aggregate or retroactively reinterpret posted history after policy changes. Jurisdiction-specific tax, withholding, social insurance, labor, e-invoicing, ecommerce and asset-tax compliance remain unverified until approved country/state policy and implementation evidence exist.
