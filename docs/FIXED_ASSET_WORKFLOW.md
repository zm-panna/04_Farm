# Fixed Asset register: reviewed book accounting

## What is implemented

The live MVC workflow is `/fixed-assets`; monthly runs are `/fixed-assets/depreciation`. Every read and write is restricted to the authenticated active company and branch, using the existing company/branch access grants. No tenant selector is accepted from the browser. Composite relational foreign keys bind assets, custody, run lines, disposal, policy and journals to their permitted scope.

1. **Acquire:** prepare a draft containing the source reference, gross carrying cost, acquisition date, recorded funding, explicit depreciation method, useful life, residual, starting date and evidence/rationale. A different authorized user reviews it. A separate post action activates the asset and creates a balanced acquisition journal. Zero-cost acquisitions deliberately create no fake zero journal. Cost and policy are immutable; cancel and recreate an unposted draft to correct source facts.
2. **Maintain:** edit only name, category, serial and descriptive notes with the form's original row-version token. Custody changes use their own dated, append-only handover history. No cross-branch transfer is implied. Disposed assets cannot change custody.
3. **Depreciate:** choose a completed calendar month-end. The server saves an exact preview of every eligible asset in that branch. A different user approves. Posting revalidates both the eligible asset set and each financial snapshot, then posts all asset changes, the ledger and audit as one serializable transaction. No partial asset run is committed. Cancelled drafts remain visible and their references remain reserved; a new reference may be used for a replacement run.
4. **Dispose:** prepare a dated preview of proceeds, receiving account, counterparty, cost, accumulated depreciation, carrying value and gain/loss. A different user approves. Posting rechecks that snapshot and atomically derecognizes the asset with a balanced disposal journal. The same asset cannot be disposed twice. The historical gross cost and depreciation are retained for inspection, while active-register book value becomes zero.

All money operations are accounting records only. The software never initiates a bank payment or transfer or collects proceeds.

## Explicit policies, currency and jurisdiction

A separately reviewed, effective-dated `CompanyBusinessPolicy` must exist. Each acquisition, depreciation run and disposal snapshots the business-policy ID/version, currency code and supported minor-unit precision. Dates are validated against today in that policy's company time zone. Currency changes require a separately reviewed migration/new company; this workflow does not perform FX or relabel historical amounts.

No BDT, USD, Arabian-country rate, tax jurisdiction or statutory useful life is assumed. Examples in tests are synthetic, not production accounting policy. The local accountant must approve acquisition cost, capitalization, useful life, residual estimate and method. The acquisition input is the reviewed carrying cost, including only the capitalization costs and non-recoverable tax approved by the accountant. This module does not infer VAT recoverability, depreciation tax deductions, US federal/state capital allowances, or local statutory filing treatment.

Supported book methods:

- `StraightLineMonthly`: explicit life 1–1,200 months, explicit starting date on the first day of a month on or after acquisition. Full calendar-month convention; no prorating is inferred.
- `None`: explicitly reviewed non-depreciable classification, life 0, residual equal to cost and mandatory rationale.

There is no editable statutory percentage and no implicit default method or useful life. Revaluation, impairment, componentization, estimate changes after posting, tax depreciation, historical asset migration/opening imports, partial-month allocation and interbranch asset transfers require a separate reviewed workflow and are not represented as supported here. Existing aggregate opening fixed-asset ledger balances are not automatically imported into this register; do not enter them as new acquisitions and double count them. Posted-asset errors require a separately reviewed accounting correction outside this non-reversing register workflow.

## Exact depreciation

For the nth posted month, target accumulated depreciation is:

`round((cost − residual) × min(n, lifeMonths) / lifeMonths, companyCurrencyMinorUnits, AwayFromZero)`.

The current charge equals that cumulative target less the previously posted accumulated amount, capped at the remaining depreciable basis. This cumulative calculation distributes rounding differences across periods and reaches the exact residual floor. For cost 1.00, residual 0.02 and life 3, charges are 0.33, 0.32 and 0.33. A tiny zero-rounded month can advance its reviewed period with no zero-value voucher. Assets already at the residual floor, zero-cost assets, non-depreciable assets and disposed assets are excluded.

Periods cannot be skipped or repeated. Depreciation before the chosen start is not charged. A run with no eligible assets is rejected. Only one open draft/approved run exists for a branch/month. A supplemental run with a new reference may cover newly posted acquisitions due in an already-run month; previously posted asset-periods are excluded. A filtered relational unique index independently prohibits more than one posted line for the same scoped asset/period. A new eligible acquisition after preview invalidates the run: cancel and recreate it for fresh independent review.

Before disposal, depreciation must be current through the preceding month. Disposal does not silently add partial-month depreciation. A disposal cannot predate any already-posted depreciation. A run posted after disposal preview invalidates that preview; it must be cancelled and prepared again.

## Journal map

The existing chart uses four-digit classes. This module records:

| Action | Debit | Credit |
|---|---|---|
| Acquisition | 1600 fixed-asset cost | 1000 cash/bank recorded OR 2500 other payable recorded |
| Depreciation | 6800 book depreciation expense | 1690 accumulated depreciation |
| Disposal | 1000 cash/bank OR 1500 other receivable; 1690 accumulated depreciation; 6801 disposal loss if applicable | 1600 gross asset cost; 4800 disposal gain if applicable |

2500 and 1500 are explicitly other payable/receivable ledger balances. They do not create vendor/customer trade-document subledger entries. Select cash only when cash/bank settlement is actually recorded. Settlement of other payable/receivable balances is handled through reviewed general accounting, not through a fabricated trade document. The accountant must validate these account mappings before live use.

Zero journal lines are omitted. If both asset cost and proceeds are zero there is no monetary journal. All nonzero journals are constructed through the shared balance and precision validator and linked to their originating document. The UI shows the proposed and actual debit/credit entries.

## Review, permissions and safety

- `FixedAssets.View`: register, details and run history.
- `FixedAssets.Create`: prepare/cancel unposted acquisitions.
- `FixedAssets.Edit`: descriptive metadata only.
- `FixedAssets.Custody`: record within-branch custody changes.
- `FixedAssets.Depreciate`: prepare/cancel monthly runs.
- `FixedAssets.Dispose`: prepare/cancel disposals.
- `FixedAssets.Approve`: independent acquisition-policy, run and disposal approval.
- `FixedAssets.Post`: post already-approved records.

Creation permission does not confer approval or posting. Approval always requires a different user from the preparer, explicit review confirmation and a review note. The UI is anti-forgery protected, with authorization in both the controller and authoritative service. Original row-version tokens protect edits and workflow transitions. A stale form returns a conflict and preserves its original token rather than silently overwriting a newer record. Replayed terminal actions fail. Cancel/Back navigation without submitting changes nothing.

Acquisition, run and disposal transitions are audited with company, actor, branch/document reference and time. Custody history is append-only. Direct DbContext financial mutations/deletions are rejected outside the workflow, including synchronous and asynchronous saves. The closed-period guard is checked even for zero-value acquisitions/runs/disposals that generate no journal. Financial writes use serializable transactions; cancellation or exceptions roll back journals, asset balances, history and audits and clear aborted tracked changes.

## Verification

`Tests/EnterpriseERP.UnitTests/FixedAssetWorkflowTests.cs` uses real relational SQLite with Identity and the project's shared tenant/permission services. It exercises independent approval, explicit policy and currency precision, acquisition entries, cumulative rounding and residual floors, tiny zero charges, excluded assets, period sequencing, stale/replayed requests, new-asset preview invalidation, cancellation/replacement, metadata/custody concurrency, disposal gains/losses and zero proceeds, disposed-asset guards, closed dates, scoped reads/writes, composite foreign keys, immutability, cancelled requests, register search/filter/pagination, and injected failure after ledger insert for each posting type.

`Tests/EnterpriseERP.UnitTests/FixedAssetUiTests.cs` renders the compiled Razor forms to verify HTML encoding, anti-forgery controls, original row tokens, review/post targets and the absence of financial fields from metadata editing. `scripts/http-enterprise-assets.py` exercises the real MVC model binder, Identity sessions and anti-forgery middleware in a disposable opt-in fictional SQLite database, covering the complete acquisition/custody/depreciation/disposal journey plus stale, replay, cancel and cross-company attempts. HTTP verification is not browser/JavaScript or visual verification.

The release lead runs the final aggregate build/test and migration checks. SQL Server production rollout must still validate the generated migration and company-specific accountant policies against a restored staging copy before enabling the register.
