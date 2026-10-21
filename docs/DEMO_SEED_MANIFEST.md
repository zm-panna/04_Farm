# Optional enterprise demonstration manifest

Packaged fixture: **FARM-ENTERPRISE-DEMO-V1**, layered on the guarded FARM-DEMO-V3 core fixture. Source: `DemoBusinessSeeder.cs`, `EnterpriseDemoSeeder*.cs`, `MessagingDemoSeeder.cs`, `ScanningDemoSeeder.cs` and `DemoSqlExporter.cs`. The core-only command remains available; this archive’s SQL data uses the explicitly requested enterprise demonstration.

## Verified generated sample

- 95 mapped tables, 1,345 total rows; every applicable non-security/workflow table has at least five rows globally.
- Twenty-five opaque fictional scan labels and five explicitly cancelled fictional scan drafts. These are lookup/training fixtures, not genuine business approvals.
- Five fictional internal messages, inbox/outbox/read samples, ten SQL rate buckets and five automation acknowledgements. Fifteen derived cache-generation rows are metadata; their random tokens are excluded from the business fixture fingerprint.
- Five companies, ten branches, five synthetic maker users and five synthetic reviewer users.
- 100 exactly balanced vouchers and ten stock items reconciled to their movement histories; no outstanding demo reservation.
- Five rows in each new enterprise table, except20 chronological order-event rows. Compensation history is immutable and linked to each payroll-line snapshot.
- SQLite integrity check passed and foreign-key violations were zero. SQL Server schema/data import was **not** executed.
- All tax percentages, payroll rates and company policy examples are conspicuously **FICTIONAL**. Simulated maker/checker records are demonstration scenarios, never actual business/legal approval.
- No known or default password is shipped. The unique sample initialization password was discarded; exported synthetic Identity password hashes require private handling.

Counts below are across the five scoped companies, not five rows per company. Each maker and reviewer is restricted to their configured company and branches. Production/bootstrap never receives these policies or approvals.

## Exact generated table counts

| Table | Rows | Exception when empty |
|---|---:|---|
| AccountingPeriodCloses | 0 | No real accounting period has been closed; demo remains open for evaluation. |
| AnimalFeedRecords | 5 |  |
| AnimalHealthRecords | 5 |  |
| AnimalSales | 5 |  |
| AnimalWeightRecords | 5 |  |
| Animals | 15 |  |
| AspNetRoleClaims | 0 | No fabricated extra security claims. |
| AspNetRoles | 5 |  |
| AspNetUserClaims | 0 | No fabricated user claims or external assertions. |
| AspNetUserLogins | 0 | No external identity provider or real account is connected. |
| AspNetUserRoles | 5 |  |
| AspNetUserTokens | 0 | No credentials, MFA seeds, recovery codes or tokens are fabricated/exported. |
| AspNetUsers | 10 |  |
| Audits | 118 |  |
| AutomationApprovals | 0 | No genuine human authorization is implied by a seed. |
| AutomationExecutions | 0 | No external or background consequential execution occurred. |
| AutomationRules | 5 |  |
| BankStatementLines | 5 |  |
| Branches | 10 |  |
| BreedingCycleEvents | 5 |  |
| BreedingCycles | 5 |  |
| CRMActivities | 5 |  |
| CRMConsentEvents | 5 |  |
| CRMLeads | 5 |  |
| CRMOpportunities | 5 |  |
| CRMQuoteLines | 5 |  |
| CRMQuotes | 5 |  |
| CRMSalesReadyRecords | 5 |  |
| Companies | 5 |  |
| CompanyBusinessPolicies | 5 |  |
| CompanyCurrencyLocks | 5 |  |
| CompanySubscriptionPolicies | 5 |  |
| EcommerceCartLines | 5 |  |
| EcommerceCarts | 5 |  |
| EcommerceCustomers | 5 |  |
| EcommerceFulfillments | 5 |  |
| EcommerceOrderEvents | 20 |  |
| EcommerceOrderLines | 5 |  |
| EcommerceOrders | 5 |  |
| EcommerceProducts | 5 |  |
| EcommerceReturns | 5 |  |
| EcommerceStockReservations | 5 |  |
| FarmExpenses | 5 |  |
| Farms | 5 |  |
| FatteningPlans | 5 |  |
| FinanceDocuments | 30 |  |
| FinancePaymentAllocations | 5 |  |
| FinancePayments | 5 |  |
| FixedAssetCustodies | 5 |  |
| FixedAssetDepreciationLines | 5 |  |
| FixedAssetDepreciationRuns | 5 |  |
| FixedAssetDisposals | 5 |  |
| FixedAssets | 5 |  |
| HRPayrollAttendance | 5 |  |
| HRPayrollCompensations | 5 |  |
| HRPayrollLeaves | 5 |  |
| HRPayrollLines | 5 |  |
| HRPayrollPolicies | 5 |  |
| HRPayrollRuns | 5 |  |
| HRPayrollStaff | 5 |  |
| InternalMessages | 5 |  |
| InventoryCostBalances | 10 |  |
| MessagingAutomationReads | 5 |  |
| MessagingOutbox | 5 |  |
| MessagingRateBuckets | 10 |  |
| MilkYields | 5 |  |
| Notifications | 5 | No fabricated dispatched outcome or recipient notification. |
| OpeningAnimalValues | 5 |  |
| OpeningBalances | 5 |  |
| OpeningStockValues | 5 |  |
| ProductSales | 10 |  |
| PurchaseOrderReceipts | 5 |  |
| PurchaseOrders | 5 |  |
| PurchaseReceipts | 5 |  |
| ScaleCacheEpochs | 15 |  |
| ScanLabels | 25 | Opaque synthetic labels across five contexts/companies. |
| ScanIntents | 5 | Cancelled synthetic review drafts; no scanner business postings. |
| ScheduledJobs | 0 | No background action is queued by the demonstration. |
| StockDocumentReturns | 10 |  |
| StockItems | 10 |  |
| StockMovements | 55 |  |
| StockReorderPolicies | 5 |  |
| StockTransfers | 10 |  |
| SubscriptionInvoices | 5 |  |
| SubscriptionPayments | 5 |  |
| SupportTicketActivities | 5 |  |
| SupportTickets | 5 |  |
| TaxPolicies | 5 |  |
| TradingParties | 10 |  |
| UserBranchAccess | 15 |  |
| UserCompanyAccess | 10 |  |
| UserInboxNotifications | 5 |  |
| UserPermissions | 237 |  |
| VoucherLines | 285 |  |
| Vouchers | 100 |  |

## Safe local regeneration

Use a **new isolated Development SQLite file**, an operator-chosen unique demo password, and explicit `-IncludeEnterprise -ConfirmFictionalPolicies` with `scripts/SEED_DEMO.ps1` or `scripts/EXPORT_DEMO_SQL.ps1`. No startup seeding occurs. No automatic production/data conversion or real money/message action is performed. SQLite is for this local test/demo path; production uses separately verified SQL Server.

The seed runs within one serializable transaction with Identity, validates linked stock/accounting data, and records completion plus a content fingerprint. Reruns preserve existing passwords/data and reject switching between core-only and enterprise fixture modes. Partial or used/altered fixtures are not silently repaired or exported. Export is a new private local file only; existing output, network/reparse/public-web-root paths, missing opt-in, production and non-demo targets are rejected. Constraints are not disabled. The SQL refuses nonempty target application tables.

Usable local demo identities are `farmdemo1@example.invalid` through `farmdemo5@example.invalid`, and enterprise-only reviewers `farmreviewer1@example.invalid` through `farmreviewer5@example.invalid`, with the password chosen privately during regeneration. Do not reuse any real account password. The archive’s generated SQL cannot be used with an advertised shared password because none was retained.

Exact current file hashes and generated counts: `evidence/scanning/final/demo-export.json`. The fingerprint detects accidental change, not malicious database-administrator forgery. Windows PowerShell, private-folder ACLs and SQL Server imports remain operator verification gates.
