# Farm business workflows added in source

## Working routes

- `/Fattening`: create one growth plan per animal, record dated weights, inspect weight history, current growth and per-animal recorded cost/margin. `/Fattening/Home` and `/Fattening/Home/Index` are aliases to the implemented screen.
- `/Expenses`: post an already-paid cash expense with an optional animal assignment, view/filter the expense ledger and category totals. `/Expenses/Home` and `/Expenses/Home/Index` are aliases.
- `/breeding`: record service details, expected calving date and append-only pregnancy/calving/outcome history.

These routes use real database-backed MVC forms, antiforgery tokens, server validation and verified company/branch context. They are not the general enterprise module scaffold pages.

## Authorization

- `Fattening.View`, `Fattening.Create`
- `Expenses.View`, `Expenses.Create`; posting also requires `Accounts.Post`
- `Breeding.View`, `Breeding.Create`, `Breeding.Update`

The grants must be assigned through the protected permission workflow. Existing company/branch access is still required. No request body may select another tenant or branch.

## Data and accounting

Five additional entities are configured in `AppDbContext.FarmBusiness.cs`: `FatteningPlan`, `AnimalWeightRecord`, `FarmExpense`, `BreedingCycle`, `BreedingCycleEvent`. A reviewed schema migration is required.

- Existing animal master editing cannot change the current weight; weight updates require the dated Fattening workflow and its permission.
- Weight history has unique tenant/reference and tenant/animal/date constraints. A backdated weight does not replace a newer current weight. A plan can reuse an identical existing starting-day weight; conflicting starting-day weights are rejected.
- Each expense, its voucher and audit entry commit in one serializable transaction. Stored money and weights must have at most three decimal places. Positive/range checks run before storage. Duplicate references cannot post twice.
- Expense accounts: Feed 6100; Veterinary 6200; Transport 6300; Labor 6400; Utilities 6500; Maintenance 6600; Other 6900. Cash/bank is credited to 1000. These are preliminary account mappings and require the customer's accountant to review them.
- Expense forms do not update stock. They record cash-paid services or directly consumed purchases; inventory purchase/issue workflows must be used for stocked goods. Do not record the same feed issue or supplier payment as an expense again.
- Fattening cost combines animal purchase cost, exact allocated `AnimalFeedRecord.CostAmount` and animal-linked cash expenses. Feed cost uses the actual inventory carrying-cost allocation rather than recomputing rounded average unit costs. Shared branch costs are excluded. Historical animal costs before the plan are included. Expected margin uses the entered expected sale amount; realized margin uses an actual recorded sale.
- Breeding cycles have a database-enforced single open cycle per animal. Supported transitions: Bred → Pregnant, NotPregnant, Calved or Aborted; Pregnant → Calved or Aborted. Final outcomes close the cycle. Closed cycles cannot be changed. New service dates cannot precede a completed cycle's last event. No outcome silently creates a calf, valuation or health diagnosis.
- Recorded dates are limited to 2000 through today in UTC. Future target/calving dates are planning values, not actual events.

## Localization and usability

Responsive pages reuse the existing visual design. The shared `farm-i18n.js` engine and `farm-core-translations.js` dictionary cover English, Bangla, Banglish and Arabic form labels, actions, headings, columns, explanatory notices, known status values, common server-validation errors and native browser form-validation messages. As of the source verification pass, 2034 combined dictionary keys and 1873 explicitly marked static elements are checked by `scripts/TEST_UI_LOCALIZATION.js`. Arabic sets RTL layout with an LTR reset for other languages. The selection persists with localStorage and cookie fallback; keyboard navigation, a skip link and a live language-change announcement are supported. User-entered notes, names, codes and numeric values are not translated. Unknown server/infrastructure errors remain unchanged rather than having their meaning guessed. Customer linguistic and full browser UAT remain required.

## Tests

`FarmBusinessWorkflowTests.cs` uses a real isolated SQLite relational store and Identity fixture to cover expense balance/linkage, storage-failure rollback, permissions, tenant isolation, duplicate normalization, invalid precision, weight history, per-animal costs, breeding lifecycle and controller antiforgery/authorization metadata.

The central test report verified 33 FarmBusinessWorkflowTests, 10 DairyWorkflowRegressionTests, 10 AnimalHealthWorkflowTests, 3 AnimalWeightMasterGuardTests and 20 OpeningBalanceWorkflowTests passing. The independent Node localization suite passed all 14 checks (`evidence/localization-tests.log`). Actual final build/test results are recorded in the release verification output. SQLite tests do not prove SQL Server deadlock handling, filtered-index/rowversion behavior, IIS deployment or browser acceptance.

## Remaining boundaries

- No expense reversal, purchase invoice matching, multi-currency conversion, tax, payment reconciliation or overhead allocation UI was added here.
- Fattening has one plan per animal; a plan amendment/restart workflow is not implemented.
- Breeding eligibility (sex, maturity, species or veterinary advice) is not inferred from the current minimal Animal model. Operators must choose an eligible animal and enter an appropriate expected date. Calves are registered through the existing Livestock workflow.
- Outcomes on sold animals are rejected; historical sold-animal corrections require an authorized administrative correction process.
- Complete operational registers are loaded for the selected branch; weight history display shows the most recent 100 entries. High-volume pagination/archiving is a further improvement.
- These implementations do not complete unrelated HR, payroll, hotel, ecommerce, import/export or other enterprise scaffolds and do not establish production readiness.

## Health follow-up lifecycle and shared dairy validation

`AnimalHealthRecord.FollowUpCompletedDate` records a staff-confirmed completion without changing the original event date, due date or notes. `/animal-health/follow-up/complete` requires login, antiforgery, active company/branch access and `AnimalHealth.Create`. The service uses a serializable transaction, rejects duplicate completion, foreign records, missing scheduled follow-up, a date before the original event or a future date, and appends an actor/date audit entry. Completing earlier than the planned due date is allowed if the work was actually performed after the originating event. Completion is a staff record of work performed, not a medical decision.

Health due lists, the home dashboard and HealthDue notification jobs now exclude completed follow-ups and sold animals. Historical due dates and completion dates remain visible in the health register. Health creation checks normalized duplicate references and writes the record and audit atomically. Breeding keeps its separate append-only lifecycle; closed cycles are excluded from upcoming/overdue counts.

Milk MVC now calls the same FarmService.RecordMilk used by the API, so positive quantities, at most three decimals, dates from 2000 through today, available tenant-scoped animals, duplicate-session rejection and audit creation cannot be bypassed by skipping client validation.
