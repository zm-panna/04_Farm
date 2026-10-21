# Farm ERP Functional Expansion — release truth

Implemented in source in this update:
- Company/branch and permission-scoped animal health CRUD **create/list** API.
- Feed consumption create/list with serializable transaction, available-stock check, stock ledger movement, idempotency reference guard, a two-line voucher and audit record.
- EF model and uniqueness constraints for these new tables.

**Accounting assumption requiring customer approval:** feed cost accounts `5100` expense and `1300` inventory are illustrative account mappings, NOT verified customer chart-of-accounts mappings. DO NOT apply to production without approval.

**Not verified:** .NET compilation, EF migrations, SQL Server transactions, IIS, UAT, concurrency behavior under real load. No SDK/SQL Server in this execution environment.

**Not complete:** 22+ other marker-only modules; dynamic roles across all paths; fully agentic execution; advanced reporting; all forms in 3 languages; production operations.

Recommended verification on Windows, with .NET 10 SDK and a throwaway SQL Server database:
```powershell
dotnet restore .\EnterpriseERP.Web\EnterpriseERP.Web.csproj
dotnet build .\EnterpriseERP.Web\EnterpriseERP.Web.csproj -c Release
dotnet test -c Release
dotnet ef migrations add FarmCareExpansion --project .\EnterpriseERP.Web\EnterpriseERP.Web.csproj --startup-project .\EnterpriseERP.Web\EnterpriseERP.Web.csproj
dotnet ef migrations script --idempotent --project .\EnterpriseERP.Web\EnterpriseERP.Web.csproj --startup-project .\EnterpriseERP.Web\EnterpriseERP.Web.csproj -o reviewed_farm_care.sql
```
Review the generated SQL before applying it to staging. Run end-to-end tests, security tests and stakeholder UAT before a live release.
