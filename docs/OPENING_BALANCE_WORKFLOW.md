# Reviewed opening balances

`/Accounts/opening` is a one-time, tenant-scoped opening workflow for a branch with no previous financial or stock transactions. It requires both `Accounts.Opening` and `Permissions.Manage`; a normal posting grant alone cannot authorize it. A trusted wildcard administrator satisfies both checks. All POSTs require antiforgery protection.

## Supported inputs

- Fixed-chart opening balance-sheet lines: 1000 cash/bank, 1500 other current assets, 1600 other fixed assets, 2500 other liabilities, 3000 capital and 3100 retained earnings/accumulated loss.
- Exact itemized opening inventory quantities and total carrying values for zero-quantity products belonging to the active branch. The system creates the stock movements and exact valuation balances, derives rounded informational unit cost, and automatically debits 1300 for the exact total.
- Exact itemized opening values for active-branch unsold animals with zero recorded cost. The system records the reviewed opening cost basis, stores its original source value and automatically debits 1400. It does not fabricate a purchase invoice or supplier payable. A later animal sale recognizes this explicit opening source as its cost basis.
- Unique reference, source-review note, opening date and an explicit reviewed confirmation. Stock/animal arrays support up to 100 items each; the UI can add input rows up to that limit.

Every account requires one side only. Assets must be debit balances; liabilities/capital credit balances. Retained earnings can be debit or credit. Total debits, including derived inventory/livestock entries, must equal credits. Values use at most three decimal places. The system does not infer any opening value.

## Safety and integrity

One opening per company/branch and a tenant-unique business reference are enforced by database indexes. A serializable transaction commits the opening, its source rows, stock movements, valuation balances, animal cost bases, balanced posted voucher and actor audit entry together. Previously closed dates are rejected. Existing vouchers, stock movements, purchase/sale/feed/expense/payment/document/bank activity prevents an opening. Existing nonzero master stock, animal costs or valuation balances require a separately reviewed migration, not this form.

Opening batches and itemized source rows are immutable through the context save guard, and source rows cannot be appended later. The posted voucher has the existing journal immutability guard. Ordinary animal editing cannot rewrite existing weight or acquisition cost; dated weights require the fattening permission and history workflow.

Receivables/payables control accounts 1100/2100 and profit/loss accounts are not accepted in this opening workflow. Party-level opening receivable/payable document import is outside this feature. Other assets in 1500/1600 are opening GL balances only, not an implemented fixed-asset lifecycle/depreciation register. Correcting an opening requires a separately authorized accounting procedure; this interface does not delete or silently rewrite it.

## Verification

`OpeningBalanceWorkflowTests.cs` covers exact ledger/subledger alignment, subsequent feed depletion and livestock sale, empty-branch and duplicate guards, permissions, tenant isolation, reviewed/balanced input, closed periods, injected rollback and immutability. `AnimalWeightMasterGuardTests.cs` checks the separate master-edit bypass fix. The central 19 October 2026 TRX records all 20 opening tests and all 3 weight-master guard tests passing. Use the final aggregate test report for the full application result; SQL Server concurrency and customer accountant review remain part of acceptance.
