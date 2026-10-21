# Farm enterprise modules: independent review

Completed 19 October 2026, 05:18 UTC.

## Result

No open blocker was found in the reviewed source and local verification scope. The independently rebuilt, frozen source passed:

- Release build: zero warnings and zero errors.
- .NET: **670/670**, comprising 650 release tests and 20 independent regression probes; none skipped.
- Real localhost HTTP: **855/855**: core 407, HR/payroll 117, CRM 77, fixed assets 75, company/tax policy 86, Ecommerce 93.
- JavaScript: **44/44**: localization 31, support UI 8, browser-security contract checks 5.
- Entity Framework: no pending model change; SQL Server idempotent migration script generated successfully. The script was not executed against SQL Server.

All **383** final runtime inputs matched the supplied release manifest after testing. Manifest SHA-256: `b99b17a5ceb7c0e5cdd0de100d85b6b710fb855ec9c7778861958336fe349347`.

The audit build DLL is `537fcf8b97f4040f6895b79578762a7dcf63ae0f4f650d5c67ca953fde2af1ee`; this is an independently built artifact in a different source path, not a claim of binary identity with the release publisher's DLL. Source identity is recorded separately.

A final packaging correction changed the unused Features.Ecommerce metadata flag to true. Source search found no runtime reader. The 93-check Ecommerce HTTP suite passed again with that final configuration; the repeat is not counted twice.

## Scope and corrected findings

The review covered tenant and branch boundaries, employee compensation and customer privacy, explicit grants and CSRF, maker/checker approvals, immutable policy and money snapshots, journal balance, transactional rollback, replay protection, reservation-aware stock deductions, CRM consent and conversion deduplication, asset residual rounding and disposal, payroll daily/monthly/mixed formulas, effective-dated compensation, company-local business dates and configured currency precision.

Four early findings were corrected before the frozen run:

1. Current company policy selection at local midnight no longer requires UTC-date coverage first.
2. Ecommerce current policy checks and accounting posting dates use the company-local business date.
3. CRM acceptance resolves the current policy's local date while preserving the original quote snapshot.
4. Customer order service responses no longer include internal fulfillment cost or raw staff review/event notes.

Legacy actual cash/document/payment values enforce configured currency precision without silently revaluing historical fractional balances. Internal carrying costs and noncash book corrections preserve supported precision. Posted financial history cannot be rewritten by later policy or compensation changes.

The 20 additional probes covered depreciation at 0/2/3 currency decimals and exact residuals; reservation protection across existing stock deduction routes; CRM consent rollback; compensation privacy; real cross-company asset/CRM IDs; leap-month and partial-employment monthly pay; fixed-denominator caps; draft-only payroll policy replacement; mixed historical compensation selection; and representable revenue with fractional internal cost.

## Limits and release gates

These results do not grant production approval. Actual SQL Server migration/runtime, backup/restore and rollback rehearsal, Windows/IIS/TLS and network configuration, staging access, approved country/state policies, native-speaker and actual-browser acceptance, and customer UAT remain unverified. Bangladesh, USA and unspecified Arab jurisdictions were mentioned; exact Arab countries and US state still require confirmation. No statutory compliance is implied by configurable tax/payroll settings.

Ecommerce payment remains explicitly unverified, with no trusted gateway webhook or automatic charge. Payroll creates reviewed accruals, not external disbursements. No real account, payment, deployment or external message was performed during this audit.

## Evidence index

- `results/SUMMARY.json`: machine-readable counts, source identity and verification boundaries.
- `results/final-build.log`, `results/final-independent-all.trx`: cold build and complete .NET results.
- `results/TEST_UI_LOCALIZATION.log`, `TEST_SUPPORT_TICKETS_UI.log`, `TEST_BROWSER_SECURITY.log`: JavaScript results.
- `frozen/evidence/http*/results.json`: per-journey HTTP assertions and build identity; matching console logs are in `results`.
- `results/http-ecommerce-final-config.log`: additional final-config rerun.
- `results/final-runtime-manifest-verification.json`, `SOURCE_RUNTIME_SHA256.txt`: source fingerprint verification.
- `results/ef-model-drift.log`, `ef-script-generation.log`, `schema-reviewed.sql`: migration checks and generated SQL.
- `EnterpriseIndependentAuditTests.cs`: independent additional probes.

Earlier intermediate failures were audit-fixture/cache issues and were resolved before the final cold run: lowercase stock-unit fixture values violated an established normalized-unit rule; timestamp-preserving file copies retained a stale build reference until generated build caches were removed. The original intermediate evidence is retained. No product fix was made for those audit-only issues.
