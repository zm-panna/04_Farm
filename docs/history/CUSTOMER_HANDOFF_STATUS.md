# Customer support handoff — 2026-10-18

## Implemented in source (NOT runtime verified)
- CustomerCare SupportTicket + SupportTicketActivity EF entities, tenant-scoped list/create/update/history APIs.
- Concurrent ticket updates guarded by SQL rowversion supplied by client.
- Company-scoped permission grant/revoke Razor screen with anti-forgery protection and audit entries.
- Existing finance, PDF/Excel, read-only AI summary, subscription, and review-only scheduler retained from prior source ZIP.
- Support ticket APIs exempted from subscription suspension so customers can ask for help while suspended.

## Known release blockers
1. dotnet SDK, SQL Server, IIS unavailable in this environment: build, migrations and deployment NOT TESTED.
2. Existing solution has basic scaffolds for many modules; they do not represent complete business functionality.
3. No fully functional CustomerCare MVC CRUD UI. Ticket API requires an API client/integrated UI and authorized company/branch headers.
4. Tenant/bootstrap admin provisioning, row-level tenant authorization across all modules and permissions policy need an end-to-end security review.
5. Subscription grace/override/timezone logic and suspension tests remain incomplete.
6. Existing worker only queues human review; does not execute actions automatically.
7. No end-to-end Ollama agent/action flow, SQL-generated migration artifact, automated integration testing, or customer acceptance evidence.
8. ASP.NET role hierarchy and permissions UX are not fully integrated; screen manages allowlisted user grants only.

## Windows verification commands
Install .NET 10 SDK + SQL Server 2022+ and provide connection string through user-secrets / environment variable (do NOT place credentials in repository):

```powershell
cd .\EnterpriseERP.Web
dotnet restore
dotnet build -c Release
dotnet ef migrations add CustomerSupportAndPermissions --project . --startup-project .
dotnet ef database update --project . --startup-project .
dotnet test ..\Tests\EnterpriseERP.UnitTests -c Release
dotnet publish -c Release -o ..\publish
```

Manual tests after migration: tenant A cannot see tenant B ticket; branch A cannot edit branch B ticket; duplicate ticket number blocked; stale RowVersion yields HTTP 409 after error handling; unauthorized permission changes blocked; suspended customer can access support; all financial postings reconcile and exports match ledger.

**Release decision:** NOT APPROVED for customer production use until blockers are resolved and verification evidence captured.
