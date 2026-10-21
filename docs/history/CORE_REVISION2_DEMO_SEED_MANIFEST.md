# Demo seed manifest (FARM-DEMO-V3)

Source of truth: `EnterpriseERP.Web/DemoBusinessSeeder.cs` and `DemoIdentitySeeder.cs`.
Entry point: explicit `--seed-demo`; Development SQLite initialization uses `--init-demo`. No startup seeding.

## Exact initial record counts

Counts are across five isolated companies, not five records in every company. A role/account has access only to its own company and that company’s two demo branches.

| Table / group | Initial count | Meaning |
|---|---:|---|
| Companies / Farms | 5 each | One farm per demo company |
| Branches | 10 | Main operational branch and a store/opening branch per company |
| Animals | 15 | Five main active + five sold + five opening-valued store-branch animals; sale state agrees with animal sale ledger |
| StockItems / InventoryCostBalances | 10 each | Same-SKU KG stock in both branches, with exact carrying value |
| StockMovements | 45 | 20 operational movements, five opening movements, 20 paired transfer movements |
| OpeningBalances / OpeningStockValues / OpeningAnimalValues | 5 each | Genuine first entry in each store branch: cash, itemized stock and livestock against capital |
| StockTransfers | 10 | Five main-to-store transfers and five opposite-direction returns; snapshots, movements and paired vouchers remain traceable |
| MilkYields / AnimalSales | 5 each | Milk only for active animals; sales only for sold animals |
| PurchaseReceipts / ProductSales / StockDocumentReturns | 5 each | Original source document and distinct posted return |
| TradingParties | 10 | One Supplier and one Customer in each main branch; finance documents link to the correct party type |
| PurchaseOrders / PurchaseOrderReceipts | 5 each | Synthetic Received demo order and its actual fixture receipt link; 100 ordered/received, without reopening after the 1-unit return |
| StockReorderPolicies | 5 | One policy per main-branch stock item: reorder point 5, target 10; no generated replenishment order |
| Vouchers / VoucherLines | 70 / 170 | Fourteen balanced vouchers, 34 lines per company |
| FinanceDocuments | 25 | Purchase, Sale, two AnimalPurchase, one AnimalSale per company |
| FinancePayments / FinancePaymentAllocations | 5 each | Product-sale receipt allocated to the exact sale document |
| BankStatementLines | 5 | Each matches its payment voucher's cash/bank movement |
| AnimalHealthRecords / AnimalFeedRecords | 5 each | Active animal observations and accounted feed consumption |
| FatteningPlans / AnimalWeightRecords | 5 each | Initial weight matches animal's current weight |
| FarmExpenses | 5 | Veterinary-category sample expense with its own cash voucher |
| BreedingCycles / BreedingCycleEvents | 5 each | Open Bred cycle and corresponding initial event |
| SupportTickets / SupportTicketActivities | 5 each | Created ticket and real fixture-creation activity |
| SubscriptionInvoices / SubscriptionPayments | 5 each | 1,000 amount fully paid; never ages into demo suspension |
| CompanySubscriptionPolicies | 5 | One policy per company, not duplicate policies |
| AutomationRules | 5 | Inactive Manual/Review examples only |
| Audits | 6 | Five actual seed-completed events plus one content-fingerprint event, committed with the data |
| AspNetUsers / AspNetRoles / AspNetUserRoles | 5 each | FarmAdmin, Manager, Accounts, Inventory, Viewer |
| UserCompanyAccess / UserBranchAccess | 5 / 10 | Exactly one company and its two demo branches per user; no cross-company access |
| UserPermissions | Explicit grants | Natural role-specific count; not five arbitrary permissions |

## Deliberate exceptions to “five rows in every table”

- AccountingPeriodCloses: **zero**. Demo books remain open so operators can practice posting; a close is created only by an actual authorized accounting action.
- ScheduledJobs, AutomationApprovals, AutomationExecutions and Notifications: **zero**. No fabricated authorizations, executed actions or pending background jobs. Inactive rule examples can be reviewed safely.
- Identity token, login, user-claim and role-claim tables: **zero**. These tables are populated only by actual Identity workflows. Password hashes and security stamps come from `UserManager`; no raw SQL password/hash insertion. MFA authenticator secrets and recovery codes are never fabricated or shipped in the fixture; the user enrolls through the real security workflow.
- No PlatformAdmin demo accounts. Only the explicit empty-database trusted bootstrap may create that initial role.
- A company subscription policy and an inventory cost balance are logical singletons per company/item. They are not duplicated to satisfy a numeric target.
- Audit records reflect the actual seeding operation. The seed does not fabricate automation approvals or automation execution history. Purchase-order approval/receipt fields are clearly synthetic demo scenario data; they do not represent a real supplier order or user authorization.
- EF migration history contains the migrations actually applied. It is not application fixture data.
- A module with only a marker class and no implemented entity/table receives no invented table or placeholder rows. This manifest makes no claim that marker modules are complete.

## Ledger invariants

Per company, unit cost is 46–50 and purchase quantity is 100. Product sale consumes 2, feed consumes 1 and purchase return consumes 1. Remaining main-branch quantity is 96; carrying value is `96 × unit cost`, equal to that branch’s net 1300 inventory GL balance. Each store branch starts from zero-valued masters and its first posted opening records cash 10,000, stock 10 KG at the same unit cost, and an unsold animal at 26,000–30,000, balanced against capital account 3000. There is no invented AnimalPurchase document for these opening-valued animals.

Two real 5-KG transfers per company then move main→store and store→main. Exact before/after snapshots show 96/10 → 91/15 → 96/10. Their four journals use source Dr1290/Cr1300 and destination Dr1300/Cr1290; transfer clearing returns to zero in both branches. Main and store stock values remain fully reconciled.

Each operational source has its own voucher: two animal acquisitions (1400/2100), stock purchase (1300/2100), product sale (1100/4100 and 5100/1300), feed (5100/1300), customer receipt (1000/1100), animal sale (1100/4200 and 5200/1400), farm expense (6200/1000) and purchase return (2100/1300). Acquisition documents exist for both purchased main-branch animals; the additional store-branch animal has an immutable OpeningAnimalValue instead. The sold animal's asset is removed; the active animal remains at cost. Animal sale remains receivable; the product sale is fully settled. Purchase payable is reduced by the return. Subscription records are the application's separate service-billing ledger and are not mixed into farm GL.

Every voucher has positive equal total debit and credit. Invoice paid amount equals its payment sum. Bank match amount equals its voucher's net 1000 movement. Source IDs, user IDs, company IDs and branch IDs refer to actual seeded records.

## Safety / rerun contract

The seed requires explicit `FARM_ALLOW_DEMO_SEED=YES`, a unique environment-supplied password (12+ characters and Identity complexity), a supported provider, a database/file name containing `demo` and excluding `prod`, and a non-Production environment. SQLite is Development-only. The command is intentionally forbidden against existing customer data. Demo name checks are a guard, not a substitute for an operator verifying the actual destination.

An empty database is populated in one serializable transaction shared by EF and Identity. An execution-strategy retry obtains a fresh scope/context. Identity failure or integrity failure rolls back all rows. Five completion audits and then one SHA-256 content-fingerprint audit are written last, before commit. A completed rerun validates its tenant/accounts and leaves passwords, grants, dates, added user work and balances unchanged. An old or partial fixture is rejected rather than silently repaired. V3 does not silently adopt an old V2 database: choose a new isolated demo database/file for the expanded opening/transfer scenario; no existing database is automatically deleted or upgraded by this seed.

For credentials, login commands, and local/SQL Server setup see [DEMO_LOGIN_AND_SQL_BN.md](DEMO_LOGIN_AND_SQL_BN.md). Automated seed tests use isolated SQLite files; SQL Server execution and reviewed migration validation remain separate release checks.

## Private SQL script with real synthetic data

`DemoSqlExporter` exports an untouched V3 fixture through `--export-demo-sql <absolute-local-output.sql>`. It requires Development, `FARM_ALLOW_DEMO_EXPORT=YES`, and a demo-named source database/file. `scripts/EXPORT_DEMO_SQL.ps1 -OutputDirectory <new-private-local-folder>` securely prompts for a chosen login password, creates a fresh isolated SQLite fixture, exports `02-demo-data.sql`, and copies the separately generated/reviewed committed-migration SQL as `01-schema-reviewed.sql`. It never applies either script or connects to a target SQL Server. The private source database is retained in the folder.

The data script contains actual synthetic rows and the real `UserManager`-generated password hashes, preserving the five usable demo logins with the password chosen by the operator. It never writes or prints the plaintext password, authenticator key, recovery code, external login or token. Password hashes/security stamps are sensitive: keep the output folder private, outside public source control/sharing. Do not use a real account password for demo initialization. The wrapper requires an operator-selected private folder and does not change ACLs or system security settings. Windows PowerShell execution and effective folder ACL/privacy verification remain unverified release checks; the operator must verify that other users or web hosting cannot read the folder.

Export checks the exact initial table counts and five known synthetic companies/accounts/roles, requires all token/claim/MFA/system exceptions to remain empty, and verifies the original content fingerprint. Every mapped data value is included in the canonical fingerprint except native rowversions and the fingerprint audit itself. Any additional row or changed business/Identity value rejects export. This includes many security/context operations after login, so export immediately after seeding. Older V3 fixtures without the fingerprint marker are rejected; a seed rerun does not add or refresh a missing/changed fingerprint. Create a fresh isolated file. The digest detects accidental changes; a database administrator with direct write access could forge it, so it is not an authenticity guarantee against database-owner tampering.

The generated data SQL uses Unicode/invariant typed literals, dependency-ordered inserts, preserved numeric identities, and SQL Server-generated native rowversions. It begins one serializable transaction, refuses a target name without `demo` or containing `prod`, and checks every application/Identity table is empty under locking. Errors roll back the transaction. It does not disable FK/check constraints, drop/truncate/delete tables, or reseed an existing installation. The export file must be a new absolute local `.sql` path; overwrite, symbolic-link destinations and paths inside the application public web root are rejected. Schema application and SQL Server runtime verification remain separate operator-reviewed steps.
