# Smart Farm ERP — English, বাংলা, Banglish UI update

## Implemented in this package
- 3-language selector in shared Razor layout, translation map for core navigation and dashboard labels.
- Browser preference stored in localStorage (UI only); English fallbacks.
- Bangla typography fallback and mobile-responsive selector.
- Shared layout supports other modules, but their bespoke form labels/messages are **not fully translated**.
- Source preserved; no accounting rules or tenant authorization were changed.

## Important: Not a production-ready release
- dotnet SDK and SQL Server unavailable in build environment; compile, migrations, tenant isolation, payments, AI jobs, security and IIS deployment NOT TESTED.
- Enterprise module scaffolds remain; no claim all are implemented.
- Figma connector was unavailable; design assets are code/design-preview only.
- Before live go-live: dotnet restore, build, test; SQL migrations on staging copy, install SSL and IIS Hosting Bundle, UAT with two companies, security assessment, backups/rollback.

## Commands on Windows
```powershell
dotnet --info
dotnet restore .\EnterpriseERP.Web\EnterpriseERP.Web.csproj
dotnet build .\EnterpriseERP.Web\EnterpriseERP.Web.csproj -c Release
dotnet test .\Tests\EnterpriseERP.UnitTests\EnterpriseERP.UnitTests.csproj -c Release
```
Check solution filename and run scripts/CUSTOMER_RELEASE_CHECK.ps1 in the extracted folder. Do not apply migrations without review and backup.
