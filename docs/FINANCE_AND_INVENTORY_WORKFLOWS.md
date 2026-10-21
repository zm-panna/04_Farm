# Finance and inventory workflow guide

## Screens and permissions

- `/Inventory`: on-hand balances, displayed weighted average cost, exact carrying value, stock adjustment entry. `Inventory.View` to view; `Inventory.Move` to post.
- `/Purchase`: purchase receipts and purchase returns. `Purchase.View` to view; `Purchase.Post` to post.
- `/Sales`: stock sales, sales returns and livestock sales. `Sales.View` to view; `Sales.Post` for stock entries; `Livestock.Sell` for livestock sale entry.
- `/Accounts`: document-linked payment/refund, bank statement lines, reconciliation, manual journal, ledger, trial balance and management statements. `Accounts.View` to view; `Accounts.Post` for settlement/journal; `Accounts.Reconcile` for bank entries and matching.
- `/animals/new`: `Livestock.Create` registers an animal and, when purchase cost is nonzero, posts acquisition and payable together. Editing a master cannot change historical acquisition cost.
- `/stock-ledger`: movement history. Use the product link on the inventory screen to filter.

All queries and postings use the authenticated company's selected branch, verified through company and branch grants. All forms use anti-forgery tokens. Automatic journals are integral to the operational permission; a purchase poster does not need separate permission to write its mandatory accounting legs.

## Accounting mappings

| Operation | Debit | Credit |
|---|---|---|
| Stock purchase | 1300 Inventory | 2100 Payables |
| Stock sale | 1100 Receivables; 5100 Cost of sales | 4100 Product revenue; 1300 Inventory |
| Animal acquisition | 1400 Livestock assets | 2100 Payables |
| Animal sale | 1100 Receivables; 5200 Livestock cost | 4200 Livestock revenue; 1400 Livestock assets |
| Feed consumption | 5100 Feed/cost expense | 1300 Inventory |
| Stock found/opening adjustment | 1300 Inventory | 3900 Adjustment equity |
| Stock loss adjustment | 6100 Adjustment expense | 1300 Inventory |
| Customer receipt | 1000 Cash/bank | 1100 Receivables |
| Supplier payment | 2100 Payables | 1000 Cash/bank |

Sales returns reverse revenue and restore original carrying cost. Purchase returns reverse the original payable amount, remove current weighted carrying value, and post any price difference to 6100. Refunds reverse the relevant cash/control-account settlement legs. Confirm these mappings and opening balances with the customer's accountant before statutory reporting; this is not an automated tax or statutory filing system.

## Exact decimal and inventory rules

`BusinessInput` rejects quantities, prices and amounts with more than three decimals, negative values where disallowed, values outside SQL decimal(18,3), and positive transactions whose rounded total would be zero. References are trimmed before uniqueness checks. Transaction references are capped to leave room for their journal prefixes.

`InventoryCostBalance.Value` stores exact three-decimal carrying value separately from the rounded display unit cost. A receipt adds its posted total. A partial removal allocates carrying value proportionally; final depletion removes the entire remaining value. Thus 1 unit at 1 plus 2 units at 2 yields value 5 and displayed cost 1.667, without a residual inventory journal balance after sale of all 3 units.

Feed records store `CostAmount`, the exact inventory cost allocated to that animal. Feed journals use the entered feed date at midnight UTC. Other trading journals use their actual posting time. This is current weighted-average costing; retrospective re-costing is not implemented. `/Accounts/periods` requires `Accounts.Close` and closes the selected company/branch through a past UTC date. The close can only move forward, and the application unit-of-work rejects new posted vouchers dated on or before it. Posted vouchers and their existing lines cannot be edited, deleted or extended. This interface does not reopen closed books.

Existing nonzero stock without an approved `InventoryCostBalance` is blocked. Existing nonzero-cost animals without an acquisition `FinanceDocument` are blocked from sale. Do not synthesize these baselines silently: reconcile the old stock/asset subledger to the posted GL and migrate approved opening documents/values in a controlled migration. New zero-stock masters initialize valuation on their first receipt or adjustment.

## Linked payments, returns and refunds

Every nonzero purchase, stock sale, animal acquisition and animal sale has a `FinanceDocument`. It retains the source row ID, voucher ID, original total and exact cost.

Outstanding = original amount − return credits − signed payment allocations.

- Positive outstanding: customer receipt or supplier payment can be recorded up to that amount.
- Negative outstanding: the document needs a refund; the same form posts the correct customer/supplier refund direction.
- Zero outstanding: additional settlement is blocked.
- Payments cannot be unallocated, use the wrong party type, over-settle, or cross company/branch.
- Returns cannot exceed the remaining original quantity. Cumulative rounding allocates the original amount and cost exactly across partial returns.
- Prior paid invoices remain traceable after return; their balances become refund due, rather than silently erasing the original payment.

Postings use serializable transactions, uniqueness constraints and inventory concurrency controls. Failures roll back both operational rows and journals. Reload before retrying a conflict, and reuse the original business reference to detect a previously completed request.

## Bank reconciliation

Enter a signed statement amount (receipts positive, withdrawals negative). One line matches one posted voucher with equal net cash/bank 1000 movement. A unique filtered index prevents a voucher from being reused. A concurrency token on `MatchedVoucherId` prevents stale updates overwriting a match. Split/group matching and reconciliation undo require a separately reviewed workflow and are not offered by this screen.

## Reports and limits

The trial balance and management statement include posted vouchers through the selected inclusive UTC date. The ledger screen displays the most recent 500 matching lines; filter dates/account to inspect older lines. The inventory reconciliation indicator always compares current carrying value to current inventory GL. The payment/refund history still displays the newest 100 entries. Source records remain in the database; view limits do not delete them.

Actionable history has bounded, newest-first pages with reference-substring search (trimmed, at most 80 characters):

- Purchase orders: 200 per page, with an optional **Open orders only** filter covering Draft, Approved and PartiallyReceived. Approval, receipt and cancellation forms remain available when an old order is found on another page or through search. Receipt links are loaded for the listed orders.
- Purchase/sales documents: 200 per page. The search and page controls beside **Return stock** control both the original-document selector and document/return history. Search an older reference or use **Next page** before choosing its return. Sales history also includes livestock sales, which remain ineligible for stock returns.
- Bank statement lines: 100 per page, with an optional **Unmatched lines only** filter. The controls beside reconciliation drive both the unmatched-statement selector and bank history.
- Unmatched posted cash/bank vouchers: 200 per page, with a separate reference search and pager beside reconciliation. Already matched, unposted and non-cash vouchers are excluded before counting and paging.

Search and status filters run inside the authenticated company/branch scope before database pagination. Page numbers are clamped to the actual matching range before calculating an offset; empty results remain page 1 of 1. Each search resets only its own page, while preserving the other selector's search/page and the financial-report filters. Thus an older bank line and an older voucher can be loaded into the same reconciliation form. Counts and selector contents never include other companies or unselected branches. Search uses the configured database's text-comparison behavior.

Manual journals cannot directly change control accounts 1100, 1300, 1400 or 2100; use linked operational workflows. Other supported four-digit account codes must use classes 1xxx through 6xxx and follow the customer's reviewed chart. A statement refuses to publish when a historical posted account is outside those configured classes.

## Verification

`FinanceWorkflowTests.cs` exercises precision/overflow rejection, exact weighted cost and depletion, return rounding, acquisition and sale journals, linked payments and both refund directions, feed/stock adjustments, duplicate references, tenant/permission isolation, reconciliation uniqueness, stale concurrent matching and legacy-opening safeguards. `WorkspaceActionPagingTests.cs` adds 101-bank-line and 201-voucher/order/document regressions for older actionable selections, search, status filtering, safe page bounds, retained filter state, selected-record posting, and company/branch/permission isolation. These are SQLite relational/controller tests; rendered selector/query binding is checked separately by HTTP acceptance. SQL Server migration and production concurrency/load evidence must still be collected with the release gate before deployment.

## Trading parties and the low-stock purchase-order lifecycle

`/Parties` provides tenant/branch-scoped suppliers and customers with unique codes and optional contact details. `Parties.View` permits viewing; `Parties.Manage` permits creation and activation/deactivation. Deactivation blocks new orders/trades but leaves historical balances available for settlement. Immediate receipts and sales can explicitly be unassigned; purchase orders require an active supplier. Linked financial documents retain the party ID.

`/Inventory` compares physical stock with its saved reorder point (default 5) and target (default 10). It subtracts unreceived quantities on approved/partially received orders from suggested replenishment. The Create purchase order link selects that item and pre-fills the suggested quantity; it does not place or send an external order. `Inventory.Edit` can change reorder policies.

The purchase order is explicitly **single-line**: one supplier, stock item, ordered quantity and agreed unit price. It has its own reference, distinct from receipt references:

1. `Purchase.Post` creates a Draft. No stock, payable or journal changes occur.
2. `Purchase.Approve` approves the draft. Approval is recorded with user/time; there is no supplier email or electronic transmission.
3. `Purchase.Post` records an actual receipt against an Approved or PartiallyReceived order. Agreed price is locked. Receipt quantity cannot exceed the unreceived quantity.
4. The serializable receipt transaction posts physical stock, exact inventory value, payable, voucher, supplier-linked financial document, order-receipt link and cumulative order receipt quantities/amounts together. Partial receipts use cumulative amount rounding. A tiny partial receipt/return that would consume the full monetary amount while leaving physical quantity is rejected with an instruction to process the full remaining quantity together; zero-value residual quantities cannot become stranded. Unique receipt references and order concurrency protection prevent repeat/over-receipt.
5. Full receipt sets Received. `Purchase.Approve` may cancel a Draft, Approved or PartiallyReceived order, preserving already received history. Received and Cancelled orders cannot be received again.
6. A later purchase return credits its receipt/document; it does not silently reopen a completed purchase order.

Purchase-order tests cover draft/approval states, partial/full receipt, duplicate/over-receipt rejection, cancelled remaining quantities, supplier type/active status, approval permissions, linked party balances, reorder policy validation and tenant isolation.

## Reviewed company currency precision

An unconfigured legacy company remains explicitly unlabelled and retains the existing three-decimal book contract. Once its accounting currency has been independently approved and locked, new customer/supplier invoice totals, actual cash payments/refunds, bank entries, subscription receipts, animal consideration and cash expenses must be representable in the configured 0–3 minor units. Excess precision is rejected before persistence; an incompatible historical document or settlement residual requires a separately reviewed correction, not automatic rounding or relabelling.

Legacy stock unit prices may retain three decimals, but the resulting new document total must meet the currency contract. Purchase-order receipts and linked returns allocate cumulative customer amounts at the approved currency precision and preserve the exact final remainder. Inventory carrying cost and noncash book-correction journals retain three decimals; no inventory revaluation is performed. Manual cash account1000 journal lines obey currency precision; operational control accounts still require linked operational workflows. This is an accounting precision contract, not a statutory invoice-rounding or tax-compliance certification.

Accounts and trade screens label the locked company denomination, use the configured payment increment, and display representable document amounts at that precision. Any historical fractional amount remains fully visible at three decimals, never hidden by display rounding. Policy changes cannot change the locked currency or its minor units. Cross-company or foreign-currency conversion is not performed.
