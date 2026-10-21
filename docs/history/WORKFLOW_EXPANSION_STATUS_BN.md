# Workflow expansion verification status

Implemented as source: `/animals` Animal Registry (list, create, edit; prevents editing sold animals, enforces tenant scope/permissions, audit events, duplicate tag detection, stale RowVersion check) and `/stock-ledger` read-only movement history filtered by company and branch.

**Not tested:** no .NET SDK, SQL Server or IIS available in authoring runtime. Existing Product Master, Finance, AI and scaffold modules remain unverified. Migration not created; these new views/controllers do not add database entities.

**Known constraints:** Animal registry cannot replace acquisition/animal costing workflows, existing accounting postings and all external integration services require staging testing. Trilingual copy on these screens is partial; full RESX/localization is pending. No production release approval.

## Windows acceptance commands

```powershell
dotnet restore .\EnterpriseERP.Web\EnterpriseERP.Web.csproj
dotnet build .\EnterpriseERP.Web\EnterpriseERP.Web.csproj -c Release
dotnet test .\Tests\EnterpriseERP.UnitTests\EnterpriseERP.UnitTests.csproj -c Release
```

Check authenticated permission failures and company/branch isolation with two real tenants. Confirm create/edit animal, duplicate tag rejection, sold lock and stock ledger paging. Provision database and migrations per the existing documentation; deploy to staging before IIS production.
