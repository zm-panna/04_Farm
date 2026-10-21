# Advanced integration status — 2026-10-18

## Source additions (NOT build-verified)
- Finance: ledger, trial balance, preliminary financial statements, customer/supplier payment posting and bank matching. Financial statement classification based on account prefixes must be validated against an approved chart of accounts; not statutory statements.
- Exports: ClosedXML trial balance Excel and QuestPDF Latin-label summary PDF. Bangla font embedding not implemented/tested.
- Ollama: HTTP read-only assistant using report-service facts and independent permission checks. No unrestricted SQL or tool execution. End-to-end Ollama test not run.
- Automation: approval requests with proposal hash and dual-person approval; scheduler worker moves due jobs to manual-review queue only. It intentionally does NOT execute approved actions, provider notifications or retry dispatch.
- Subscription: API middleware blocks protected /api routes after configurable days, with billing/report exception. Full role/route coverage and timezone not established.
- Data models: finance payments, bank lines, automation approvals, scheduled jobs and subscription policy, with DbContext additions.

## Outstanding requirements
Full dynamic role management UI, full Enterprise module business CRUD, financial reconciliation import, secure automated execution after approval, RBAC admin, tenant-scoped complete reports, subscription timezone handling, full migrations, complete AI assistant UI, approval UI, payment gateway and external messaging integrations, security acceptance, cross-tenant concurrency and IIS deployment.

## Verification
ZIP integrity: checked. C# compilation: NOT TESTED (dotnet SDK unavailable). SQL Server integration: NOT TESTED. IIS: NOT TESTED. Tests: NOT TESTED.

## Setup
Restore NuGet, install .NET 10 SDK, configure SQL Server connection string via secret/environment, run `dotnet ef migrations add AdvancedUpdate --project EnterpriseERP.Web --startup-project EnterpriseERP.Web` from repository root, then `dotnet ef database update --project EnterpriseERP.Web --startup-project EnterpriseERP.Web`. Check generated migration carefully and take a database backup first. Do not use this as a production release without full testing.
