# Farm enterprise source revision

The enterprise workflow inventory below began in Revision 3. For the current Bangladesh one-company corrective revision, also read `FIRST_LIVE_BANGLADESH_REVIEW.md`; current test evidence is exclusively indexed by `CURRENT_VERIFICATION.json`.

This revision adds the four explicitly requested first-live modules to the preserved verified Core Revision 2. It is a source candidate, not deployment approval. Exact completed verification and artifact hashes are in `CURRENT_VERIFICATION.json`; historical evidence is not evidence for this revision.

## Implemented workflows

- **HR/payroll**: branch staff, separately authorized rates, dated attendance, reviewed leave, explicit effective-dated payroll policies, ended-month draft calculation, independent review, balanced accrual posting, payroll register and strictly own posted payslips/leave. Rates, attendance, policies and calculations are snapshotted. No salary bank transfer or statutory filing is performed. Daily/monthly/mixed contractual methods with immutable effective-dated rate history, proration, overtime and fixed percentage deduction/contribution rules require explicit policy review. The UI explains the exact arithmetic; this is not an automatic jurisdiction rules engine. Compensation changes take effect at a month boundary or hire date; future-effective scheduling and arbitrary mid-period rate changes are not supported.
- **CRM**: scoped leads, opportunities, tasks/activities, explicit consent history, reviewed internal quotes, independent acceptance and idempotent conversion to a customer/sales-ready handoff. Consent is never inferred or copied as a marketing grant. Quotes are net internal proposals, not tax invoices. No email, SMS or customer outreach is dispatched.
- **Fixed assets**: acquisition proposals, independent approval and balanced capitalization, asset register, custody history, monthly straight-line or explicitly no depreciation, reviewed depreciation posting and disposal with balanced gain/loss journal. Residual floors, cumulative rounding, unique asset-period postings and disposed-asset guards are enforced. Tax depreciation, impairment, revaluation and statutory books are not supplied.
- **E-commerce**: published stock-linked catalog, authenticated customer profile links, own cart and orders, expiring server-priced checkout, explicit order confirmation, atomic reservations, staff review/fulfillment, cancellation and linked partial/full returns with original tax allocation. Reservations protect every existing stock deduction path. Payments remain manually pending/unverified; no card processing, paid-state toggle, payment webhook or real-money transfer exists. Current delivery mode is branch collection. Customer self-registration and remote provider integration are separate work.
- **Company policies**: company-level jurisdiction, region, accounting currency, 0–3 currency decimal places, timezone and effective-dated independently reviewed policy versions. One accounting denomination locks at first approval. Existing balances require explicit acknowledgement; currency is never converted or relabelled by a later policy. Explicit exclusive per-line sales tax configuration supports positive Standard rates or reviewed Exempt/OutOfScope treatment with a reason. Country/state labels do not prove legal compliance.
- **AI Manager**: permission-scoped branch aggregate facts and deterministic review drafts/checklists. It does not expose payroll amounts or customer identities, execute a posting, approve, pay or send a message. Follow-up actions must be performed through the authorized reviewed business workflow.

All modules have genuine MVC forms, CSRF validation, scoped lists/details/search/paging, role checks and relational workflow tests. Current verification distinguishes real localhost HTTP from deterministic DOM tests and real browser testing.

## Production acceptance still required

1. Operator-approved real policy values for the first Bangladesh company; actual accountant/payroll/legal review, chart of accounts and opening balances. Named Arab jurisdictions and US states/localities are later expansion inputs, not requirements for the confirmed Bangladesh-only launch.
2. Actual SQL Server migration and isolated demo import, provider-specific concurrency/rollback tests, backup/restore and SQL permissions.
3. Windows PC and IIS deployment, TLS certificate trust, persistent protected Data Protection keys, app-pool identity, restart/recovery and least-privilege review.
4. Real browser/mobile and Bengali/Banglish/Arabic native-speaker acceptance, including RTL and printed reports/payslips.
5. Authorized staging access and approved external providers before any live integration or transaction.

Hotel and LC/import-export were not confirmed for this release. Warehouse bins/lots/expiry, FX conversion, statutory filing engines and unrestricted autonomous actions are not included.

## Fictional demonstration

`--init-enterprise-demo` is an explicit opt-in for a new isolated Development SQLite database. It creates five synthetic companies and additional synthetic reviewer identities, linked module examples, balanced journals and at least five rows in applicable enterprise tables. Every policy and simulated approval is labelled fictional. No production bootstrap policy is silently configured. See `DEMO_SEED_MANIFEST.md` for table counts and justified empty security tables.

`SEED_DEMO.ps1` and `EXPORT_DEMO_SQL.ps1` accept `-IncludeEnterprise -ConfirmFictionalPolicies`; the caller chooses a private local password. No known/default password is shipped, and the archive's sample initialization password is discarded. The SQL contains synthetic Identity hashes and must be handled privately.
