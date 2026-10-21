# Smart Farm ERP — delivery assessment (18 October 2026)

This is a **source enhancement** built on the attached Starter package, not the missing original full ERP project. No .NET SDK, SQL Server or IIS runtime is available in this environment. **Do not deploy to production without building, migrating, and testing.**

| Feature | Status | Scope |
|---|---|---|
| .NET 10 modular solution | Partially Implemented | 23 module scaffolds; many only contain markers/UI |
| Company/branch permission | Partially Implemented | Verified access table plus specific per-operation grants; onboarding UI and dynamic role editor absent |
| Purchase + Inventory + Accounts | Implemented in source, Not Tested | Receipt with atomic weighted-average stock and payable voucher |
| Sales + Inventory + Accounts | Implemented in source, Not Tested | Stock deduction, receivable, revenue and COGS in one transaction |
| Central stock ledger | Partially Implemented | Receipt, sale and manual stock movement ledger; warehouse transfers/batches absent |
| Double-entry vouchers | Partially Implemented | Balanced voucher validation + integrated product transactions, no comprehensive GL/close/reversal |
| Dairy/Livestock | Partially Implemented | Add animal, milk entry and sale foundation; expenses/breeding/health incomplete |
| Subscription | Partially Implemented | Unpaid oldest invoice notice/alert/suspension calculation and payment allocation; enforcement missing |
| AI | Partially Implemented | Permission checked allowlisted deterministic farm summary; Ollama UI/streaming missing |
| Automation | Partially Implemented | Tenant/permission checked dry run only; no scheduler/job/outbox |
| Reporting | Partially Implemented | Summary + CSV; PDF/Excel/full reports absent |
| PDF | Pending | Not implemented |
| Dynamic permissions | Partially Implemented | Database grants checked; no role management/approval screens |
| Remaining Enterprise modules | Pending | Marker scaffolds only, not business implementations |
| SQL Server migration | Not Tested | Generate and apply EF migration using scripts/BUILD_AND_MIGRATE.ps1 |
| Build/test/IIS | Not Tested | Actual SDK/runtime unavailable here |

## Security and operational caveats
- Company/branch are supplied by request headers but checked against authenticated user access lists. Do not trust headers on their own.
- Permission rows need to be provisioned by a trusted administrator; the installer does not auto-grant wide privileges.
- Integrated purchase and sale use SQL Server Serializable isolation and unique reference constraints; external document numbering, reversal/refunds and settlement remain to implement.
- Subscription status is informational, not an access/suspension policy enforcement layer.
- AI assistant is a fixed read-only reporting tool, not Ollama integration. The automation dry run causes no action.
- UI is a starter scaffold; acceptance screenshots and end-to-end workflows are not validated.
- Never import this database into production until migrations are reviewed, rollback/backups and authorization tests pass.
