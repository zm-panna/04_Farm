# Independent source review: Bangladesh first-live safeguards

Reviewed 2026-10-19, approximately 17:59 UTC. Scope: current source, scoped offline email-ownership review, read-only staging collector, early CLI dispatch, and shared migration-history readiness checks. This is a targeted source review, not a full security certification or deployment acceptance.

## Findings and source-verified corrections

1. **Email-ownership audit exceeded the SQL Server storage bound.** The original expanded JSON could exceed `Audits.Reference` (`nvarchar(200)`), although SQLite tests would accept it. `AdministrationService.cs:181–192` now writes a compact envelope with the complete user/branch identifiers, full normalized-email SHA-256, explicit method code and complete external reference. A serialized-length guard runs before changing the target account; nothing is truncated. The two methods remain explicitly out-of-band, and the UI does not claim email delivery.
2. **Wrong-password lockout and password-length consistency.** `AdministrationService.cs:147–156` now permits the existing 1024-character password limit, enables failure accounting when lockout was disabled, and records incorrect-password failures outside the business transaction. A subsequent business rollback cannot undo those attempts.
3. **Physical column comparison omitted value-generation flags.** An `Id` column missing SQL `IDENTITY` could originally pass the type/nullability comparison while ordinary inserts failed. Expected and observed column definitions now include identity and computed flags (`StagingPreflightSchema.cs:26–29,66–67`; `StagingPreflightSql.cs:19`; `StagingPreflight.cs:172–174`). Focused lost-identity/unexpected-computed regressions are present.
4. **Attached-value preflight spelling could start the normal host.** `--verify-staging-readonly=/private/report.json` originally missed early dispatch. `StagingPreflight.cs:48–52` now intercepts this reserved spelling and rejects it before normal startup. Mixed arguments are also rejected. The regression checks both dispatch and rejection.

No additional blocking defect was identified in the following reviewed corrections:

- **Offline identity scope and atomicity:** both permissions are demanded again inside the transaction; only an account provisioned in the current company, with no other company or unauthorized branch access, is eligible. Self-review, privileged targets, changed email/stamp, locked accounts and replay are rejected. Scope checks precede target-privilege disclosure. Confirmation, security-stamp change and audit share the transaction. The controller requires antiforgery validation and removes attempted password values before redisplay (`AdministrationService.cs:133–195`; `Controllers/AdministrationController.cs:32–47`).
- **No normal host or mutation path in the collector:** `Program.cs:7–9` dispatches before `WebApplication.CreateBuilder`. The collector uses fixed parameterized SELECT statements and model inspection; no migration, repair, business save, hosted worker or Data Protection initialization was found. Its intentional filesystem write is a newly reserved private JSON evidence file. Settings/exceptions do not print connection strings or passwords.
- **Fail-closed evidence:** required process/SQL identities are compared; absent metadata visibility, unsupported reads, timeouts and exhausted row budgets cannot become PASS. Enabled row-level security prevents an all-clear on scoped company data. `productionApproved` remains false; IIS/TLS/keys, effective host configuration, full security review, CHECK semantics, restore rehearsal and customer acceptance stay explicitly NOT_VERIFIED.
- **Current policy selection:** `StagingPreflight.cs:203–213` captures one UTC instant, orders approved versions exactly as the application does, and evaluates each candidate's effective start in that candidate's own timezone. It then requires the applicable Bangladesh/BDT/2-decimal policy and matching currency lock. No tax/payroll law is inferred from country selection.
- **Shared migration history:** `StagingPreflightMigrationHistory.cs` rejects empty, missing, duplicate, blank, case-mismatched and unknown future IDs. Both the CLI and `DeploymentReadinessController.cs:28–39` use that assessment. Readiness remains `productionReady=false`, and a matching history is expressly not physical-schema or production acceptance.

## Authored payroll changes, not an independent review

This reviewer authored the payroll changes in `HRPayrollService.cs`, `Controllers/HRPayrollController.cs`, `Views/HRPayroll/Policies.cshtml`, and `HRPayrollFirstLiveGuardsTests.cs`. They must not be described as independently audited by this report.

- Policy POST requires explicit, singular, nonblank posted basis/proration/precision/rate/cap/date fields; reviewed zero and No remain valid choices. New forms do not silently preselect the consequential choices.
- Calculation and source revalidation require the payroll month to have fully ended in company-local time. A pure date guard supports month-length and Bangladesh-midnight boundary regressions.
- The first aggregate run exposed four new-test routing-fixture failures. The fixture was corrected without weakening assertions or changing runtime rules. Final aggregate results belong in the separate test evidence, not this source-review conclusion.

## Acceptance boundary

No SQL Server, IIS, real browser, customer policy approval or first-live acceptance was performed by this review. SQL Server catalog behavior, deployed runtime permissions and identity, protected key persistence, restart, backup restoration and customer UAT remain release gates. No independent cold test run is claimed here.

Subsequent verification: the independent cold-copy build, 805 release tests, separate 20 legacy probes and 47-check onboarding HTTP journey are recorded in [independent-current/REPORT.md](independent-current/REPORT.md). That execution evidence does not remove the external acceptance gates above.
