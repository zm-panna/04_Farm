# Smart Farm ERP — Staging verification and customer release runbook

**Status: NOT PRODUCTION APPROVED.** This repo has unimplemented enterprise module screens. Production should not be released merely because a packaging script exits successfully.

## Prerequisites on a Windows staging host
- .NET 10 SDK, ASP.NET Core Hosting Bundle 10.x, IIS and SQL Server, with approved licenses and Windows updates.
- A non-production DB, least-privilege SQL login, secure connection string in environment/user secrets (never in ZIP).
- Code signing / package scanning and an HTTPS staging hostname.
- Create database backup and document the tested restore procedure before any schema update.

## Commands
```powershell
# From extracted project root; use a STAGING database only
$env:FARM_STAGING_CONNECTION = 'Server=STAGING-SQL;Database=SmartFarm_Staging;Integrated Security=True;Encrypt=True;TrustServerCertificate=False'
dotnet --version
dotnet restore .\EnterpriseERP.sln
dotnet build .\EnterpriseERP.sln -c Release
dotnet test .\EnterpriseERP.sln -c Release
# Only if migrations are absent: create and review them in source control first
# dotnet ef migrations add InitialFarmSchema --project .\EnterpriseERP.Web --startup-project .\EnterpriseERP.Web
# dotnet ef migrations script --idempotent --project .\EnterpriseERP.Web --startup-project .\EnterpriseERP.Web -o migration-review.sql
# After DBA approval and backup: 
.\scripts\VERIFY_STAGING_AND_PACKAGE.ps1 -StagingUrl 'https://staging.example.com' -RunMigrationOnStaging -Publish
```

## Security/UAT requirements
- Verify login, company switching, permission denial, CSRF, cross-tenant record access and subscription restrictions in both UI and API.
- Test purchase/receipt, stock movement, milk entry, feed consumption, animal sale, finance postings and PDF/Excel figures against hand-calculated examples.
- Check job retry/idempotency, expired approvals, concurrency, backups/restore, logging, secrets and scheduled job isolation.
- English, বাংলা and Banglish user flows; mobile viewport tests. Record real customer UAT signatures.
- IIS app pool, HTTPS certificate, ACL, request logs, error logs and real browser sessions need manual acceptance. The script cannot certify them.

## Rollback
Stop new writes, preserve audit logs, use an approved database backup restore strategy and the previous deployment artifact. Schema rollback must be DBA-reviewed; don't assume a code-only rollback is safe.

## Release decision
Production approval requires: compilation success, migrations reviewed and applied on staging, end-to-end testing, customer UAT, security review, and IIS staging approval. `evidence.json` alone is not production approval.
