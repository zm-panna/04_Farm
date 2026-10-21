# Branch stock transfers and traceability

## Scope and access

`/stock-transfers` lists the active branch's posted transfers. `Inventory.View` and current access to both branches are required to inspect a transfer. `Inventory.Transfer` permits a new transfer; generic adjustment permission does not. The active branch is always the source. The destination must be a different branch in the same company, with an explicit current user branch grant. IDs supplied by the browser never bypass these checks.

Choose the source product and destination branch, load matching destination products, then supply the quantity and unique transfer reference. Source and destination must have the same normalized SKU and use the same physical stock unit. This screen does not convert packs, kilograms, litres or units. `StockItem.UnitOfMeasure` stores a normalized unit code (up to 30 letters/numbers/hyphens), such as KG, L, EA or BAG25KG. A unit is immutable once assigned, and all branches using the same company SKU must share the same unit. Unknown legacy/API units remain empty and block transfers. The catalog requires explicit unit entry; assigning it to existing stock or movement history requires an explicit review checkbox and `Product.UnitReviewed` audit. That review labels quantities already in the stated unit; it never converts them.

## Atomic financial and physical posting

The service runs under a serializable transaction. It checks both branch grants, products, source availability and existing exact valuations. Nonzero legacy balances without reviewed valuation cannot be transferred in either direction. No negative stock is allowed. Quantities must be positive and fit decimal(18,3).

Exact cost uses the source's current `InventoryCostBalance`, not the rounded displayed unit cost. Partial transfers allocate value proportionally to three decimals; final depletion transfers the entire remaining source value. The destination receives that same value and recalculates weighted average cost.

Each nonzero-value transfer creates two balanced posted vouchers:

| Branch | Debit | Credit |
|---|---|---|
| Source | 1290 Interbranch clearing | 1300 Inventory |
| Destination | 1300 Inventory | 1290 Interbranch clearing |

Clearing nets to zero at company level. Individual branches retain their interbranch balance. The chart mapping needs the customer's accountant's review before statutory reporting. There is no sale, revenue, payable or tax inferred from an internal transfer. Free stock still creates both physical movements and a transfer record, without inventing zero-valued journal lines.

The two balance changes, movements, journals, transfer record and audit commit or roll back together. The transfer reference is trimmed and uppercased, unique across the company and limited to 80 characters. Journal and movement references are `XFER-OUT-{REFERENCE}` and `XFER-IN-{REFERENCE}`. Reload after a conflict and search the original reference before retrying.

## Immutable evidence and corrections

A transfer retains branch and product names/SKUs as they were at posting, both stock item IDs, stock unit, quantity, exact cost, before/after quantity and value on both sides, both movement IDs, both voucher IDs, posting time, actor and note. Detail screens expose the paired accounting entries. Catalog renames cannot rewrite these snapshots.

Stock movements, posted transfers and posted vouchers cannot be edited or deleted through the application's unit-of-work boundary. Correction means a **new opposite-direction transfer** from the receiving branch, with a new unique reference and the original reference recorded in its note. The original remains visible. Current available stock and carrying value are checked again; if stock has since been blended or consumed, this is a current-cost physical correction, not a retroactive rewrite or guaranteed reversal of the original financial amount. There is no automatic negative-stock override or destructive cancellation.

## Deliberate boundaries

Locations in this workflow are real existing branches. The current purchase, sale, feed and return subledgers do not carry physical warehouse/bin, batch/lot, expiry, or serial allocations. Those features need a shared allocation model across **all** stock flows and an approved opening allocation of existing stock. A transfer-only marker warehouse or editable lot label would give false traceability and is not implemented. Do not claim warehouse-bin or batch recall coverage from this release.

Posted transfers are immediate, single-product movements. Dispatch/in-transit/receipt approval, cross-company trade, pack conversion, freight capitalization and multi-line transfer documents are not represented by this screen.

## Verification

`StockTransferTests` covers partial/final exact-cost conservation, destination weighted averaging, paired balanced journals, stable historical names, normalized duplicates, both-branch grants, company isolation, transfer-specific permission, invalid/oversized quantities, negative-stock rejection, missing legacy valuation, injected failure after physical/journal writes with full rollback and retry, immutable movements and transfers, opposite-direction correction, zero-value stock, read scope, independent Create/Edit product permissions, unit snapshot/immutability, cross-branch unit consistency, unknown/mismatched units, and explicit legacy unit review with an audit and unchanged quantity/value. The parent release suite coordinates compilation, SQLite relational execution and SQL Server additive migrations. SQL Server concurrent load/deadlock behavior still requires staging validation.

## Display languages

Transfer screens, unit-review labels and posting/error messages register English, Bengali, Banglish and Arabic strings with the shared language engine. Arabic uses the shared RTL layout. References, SKU codes, recorded actor IDs, units and user-entered notes remain unchanged as business evidence.
