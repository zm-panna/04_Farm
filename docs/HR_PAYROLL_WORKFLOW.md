# HR, daily/monthly payroll and employee self-service

## What is implemented

The MVC workflow under `/HRPayroll` supports branch-scoped staff search, filters, 20-row paging, employment edits/deactivation, protected identity linking, confidential pay rates, attendance entry/correction, leave requests and independent approval, reviewed policy versions, monthly calculation/recalculation, independent payroll approval, separate accrual posting, payroll registers and employee-owned payslips/leave.

This supports explicitly reviewed **daily-rated and monthly prorated payroll**. It is not an implementation of statutory Bangladesh, US-state, or unspecified Arab-country payroll. No statutory deduction, tax rate, benefit entitlement, weekend calendar, monthly salary divisor, FX conversion, bank transfer, tax filing, or remittance is assumed. Progressive-tax or jurisdiction-specific payroll rules beyond the explicit percentages and reviewed daily/monthly formulas require an independently reviewed extension before use. A reviewed Mixed policy supports Daily and Monthly contracts in one branch/month run, using each staff member’s explicitly reviewed contractual basis. Non-Mixed policies reject mismatched staff bases instead of inferring them.

## Setup and roles

1. Approve a Company Business Policy for the exact country/state or region, base currency/minor units, timezone and effective period. The company base denomination is locked by the shared accounting policy. There is no production payroll default.
2. `HR.Manage` creates staff without an account or compensation. `HR.View` reads only safe directory fields; it cannot access rates or payroll amounts.
3. `Payroll.Rates` separately selects the staff contractual Daily/Monthly basis and sets reviewed daily rates, monthly salary and overtime-hour rates. Input rates allow three decimal places; each calculated amount is rounded to the reviewed company currency precision.
4. `HR.IdentityLink` optionally links an exact existing identity ID after explicit ownership verification. The account must have verified email and explicit company and branch membership, and cannot already belong to another staff record in the company. It creates neither an identity nor access grants. A link cannot be reassigned through the ordinary HR UI.
5. `Payroll.Policy` proposes an immutable versioned policy with no currency default, explicit percentages (including reviewed zero), effective dates, jurisdiction/calculation basis and expense/liability accounts. The entire policy period must match one approved Company Business Policy. A different `Payroll.Policy.Approve` user confirms all values and locks the policy.
6. `Employee.Self` is a separately granted permission. Account linkage alone never grants it or general HR/payroll privileges.

Compensation is append-only effective-dated history. The rate form requires an explicit month boundary or hire date and a review note. Calculation selects the latest authorized version effective at the employee’s first employed date in the month; it never falls back to current staff convenience fields. Missing history or a mid-period change blocks calculation. Corrections before approval append a new version with the same effective date; a version cannot predate the latest configured date or rewrite an approved/posted period. Later contract changes use a later effective month; this release accepts effective dates no later than the company business date and does not schedule future versions. Mid-period contractual changes require a separately reviewed extension, not an invented split formula.

Full permission list: `HR.View`, `HR.Manage`, `HR.IdentityLink`, `HR.Attendance`, `HR.Leave.Approve`, `Payroll.View`, `Payroll.Rates`, `Payroll.Policy`, `Payroll.Policy.Approve`, `Payroll.Calculate`, `Payroll.Approve`, `Payroll.Post`, `Employee.Self`.

## Attendance and leave

- One attendance row per staff/date. Paid-day units are exactly 0, 0.5 or 1. Overtime is 0–24 hours with up to three decimals. Every entry carries an explanatory note and actor audit.
- Dates must be inside employment. Future attendance is blocked using the company's business timezone.
- Leave spans inclusive calendar dates, has an explicit paid/unpaid request and requires a different reviewer. The requester and linked employee cannot approve their own request.
- Pending or approved leave cannot overlap another pending/approved request. Approved leave and attendance cannot cover the same day.
- Payroll requires exactly one attendance row or approved leave for **every employment date** in the month. This prevents missing attendance from silently reducing pay. Explicitly record contracted paid rest days as one paid day and unpaid days as zero. Pending leave blocks calculation.
- Leave's paid/unpaid treatment is explicitly reviewed; the module does not calculate legal entitlements or balances.
- Approved and posted payroll freeze overlapping attendance and leave. Ordinary edits cannot rewrite historical accrual sources.

## Deterministic payroll

Only a fully ended calendar month can be calculated. The exact independently reviewed policy must cover the full month and still match the effective company business policy. Each staff member employed during the month is included, including someone deactivated with an employment end in that month.

Per employee:

- Daily base pay = round((paid attendance days + approved paid leave calendar days) × daily rate)
- Monthly policy explicitly chooses `ActualCalendarDays` (28/29/30/31 for the selected month) or `FixedPaidUnits` (reviewed positive denominator). It also explicitly chooses whether paid units are capped at that denominator.
- Monthly base pay = round(monthly salary × paid units used / reviewed denominator). Without the cap, a fixed denominator smaller than paid units deliberately produces more than the contractual monthly amount; the reviewer sees and must approve this choice.
- Part-month employment includes only recorded paid attendance/leave within employment, while the chosen monthly denominator stays as reviewed. No implicit 30-day rule, weekday calendar, absence deduction or cap is invented.
- Overtime pay = round(overtime hours × overtime hourly rate)
- Gross = base pay + overtime pay
- Employee deduction = round(gross × configured deduction % / 100)
- Net salary payable = gross − deduction
- Employer contribution = round(gross × configured employer contribution % / 100)

All rounding uses decimal arithmetic, the reviewed currency's 0–3 minor units and midpoint-away-from-zero. Rates and percentage inputs are validated before persistence. Monthly salary must fit the company currency precision. Compensation ID/version/effective date, pay basis, monthly salary, denominator mode/value, paid units used, salary components and employee names/codes/rates are snapshotted in immutable lines. Policy IDs refer to immutable reviewed snapshots of company jurisdiction, timezone and denomination.

A deterministic SHA-256 source fingerprint covers the exact ordered policy, staff, selected immutable compensation versions, attendance and leave source rows. Any source change requires draft recalculation before approval. Recalculation can explicitly select a corrected independently reviewed policy ID covering the same month. It replaces only draft lines atomically, rotates the original-version token, records the last calculator, and forces independent approval of the new result.

## Approval, posting and payslips

`Payroll.Approve` must be a different user from the last calculator and must explicitly confirm review. Approved runs and lines cannot be edited. `Payroll.Post` then posts a balanced voucher on a valid, open accounting date on or after month end:

- Debit wage expense for gross
- Credit employee salary payable for net
- Credit deduction payable for deductions
- Debit employer-contribution expense and credit its separate payable

Zero journal lines are omitted. A posting is a GL accrual only; there is no money transfer and no claim that the employee was paid. Closed-period validation is shared with all accounting writes. The run keeps the voucher ID, posting date and actors. Posted payslips state that salary is accrued, not confirmed transferred.

The unique company/branch/month run, unique company reference, unique run/staff line and original tokens reject repeated submissions. Approved/posted lines, policies and posted runs are protected by DbContext save guards, in addition to operation-level checks.

Employees access `/HRPayroll/me` and `/HRPayroll/me/payslip/{lineId}`. The service derives staff ownership from the authenticated user and current branch. It never trusts a submitted staff ID for own leave. Other employees' payslips and general payroll registers remain inaccessible. Payslips only become available after posting. HR/payroll administrative payslip access requires `Payroll.View`.

## Persistence and operational boundaries

Every write executes in a serializable transaction; failures roll back and clear EF's tracker. Audit writes share that transaction. Provider-independent revision strings are checked against the originally submitted token and set as EF original concurrency values. Tenant/branch composite foreign keys protect staff, compensation history, leave, attendance, policy, run and line associations. Staff are deactivated rather than deleted.

Source implementation includes isolated real SQLite relational tests covering the complete posting path, precision 0/1/2/3, independent review, permissions/privacy, forged identity and staff IDs, duplicate actions, missing attendance, stale revisions/source fingerprints, cross-tenant/branch constraints, closed periods, immutable snapshots and forced audit-failure rollback. SQL Server execution, concurrent production load, IIS deployment and customer UAT remain separate live verification gates.

## Disposable real HTTP acceptance

After the release binary is built, `python3 scripts/http-enterprise-hr.py` starts its own ephemeral localhost synthetic enterprise database, exercises rendered MVC forms and antiforgery tokens, validates independent review and accrual, and removes temporary credentials/cookies/database at exit. Its JSON report records whether it actually passed. This is real HTTP validation, not browser rendering or IIS/SQL Server proof.
