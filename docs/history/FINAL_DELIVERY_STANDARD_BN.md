# Smart Farm AI ERP — Final Delivery Standard (বাংলা)

**Release decision: BLOCKED / NOT PRODUCTION APPROVED (as of 2026-10-19).**

এই package development ও staging review-এর জন্য। Windows/IIS/SQL runtime access ছাড়া production acceptance সম্পন্ন করা হয়নি।

## Deliverables included
- .NET 10 source code এবং 23টি module project reference-সহ `EnterpriseERP.slnx` solution manifest।
- Existing Farm, Product, Dairy, Inventory, Customer Care, Finance, AI ও Automation source (অনেক অংশ এখনো incomplete/scaffold)।
- English/বাংলা/Banglish UI-এর বিদ্যমান অনুবাদসমূহ (সমস্ত form-এর অনুবাদ নয়)।
- `scripts/SEED_FIVE_ROWS_PER_TABLE_DEMO.sql` — **কেবল isolated demo database**।
- `BootstrapAdmin.cs` — empty database-এ secure Identity প্রথম admin provisioning, schema migration সম্পন্ন হওয়ার পরে।
- Staging verification ও production config validation PowerShell scripts।

## Strict acceptance criteria (সবকিছু PASS না হলে release নয়)
1. Windows .NET 10 SDK installed; restore, Release build এবং unit/integration tests PASS।
2. Reviewed EF Core migrations checked-in, database backup, dry run, deploy/rollback tests PASS।
3. First company/admin Identity creation এবং real browser login/logout/MFA policy review PASS; no demo seed on production।
4. Permissions, company/branch isolation, subscriptions, financial posting, negative stock, concurrency and audit tests PASS।
5. Every licensed and enabled module: actual CRUD, workflows, reports এবং scoped exports PASS। Scaffold module disable/complete করতে হবে।
6. Ollama unavailable হলে ERP চালু থাকে; AI tools least-privilege, approval and prompt-injection security tests PASS।
7. Three languages: Dashboard, all applicable forms, validators, PDF/exports and mobile accessibility reviewed।
8. Production HTTPS IIS hosting bundle, SQL connectivity, logs, backup restore, application pool permissions, staging smoke and customer UAT sign-off PASS।

## First steps on Windows staging
```powershell
# from extracted folder, .NET 10 SDK installed
.\scripts\FINAL_RELEASE_VERIFY.ps1 -RunTests -Publish -StagingUrl 'https://staging.example.com'
```
`release-evidence/FINAL_RELEASE_RESULTS.json` returns BLOCKED by default. A build/publish passing alone cannot approve go-live.

Database migration: generate and review using `scripts/GENERATE_REVIEWED_MIGRATION.ps1`, check migration code and SQL with your DBA, back up staging SQL DB and apply there first. **Do not apply demo seed to production**.

Set secrets using Windows/IIS encrypted configuration or managed secret store. For production configuration audit:
```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
# configure ConnectionStrings__DefaultConnection securely, never commit credentials
.\scripts\CHECK_PRODUCTION_CONFIG.ps1
```

## Limits observed
- No `dotnet`, Windows IIS, or SQL Server installed in the authoring runtime. Build, migration application, IIS & UAT **NOT TESTED**.
- No checked-in EF migration files in supplied baseline.
- 23 module marker projects exist; module project count is not evidence of finished features.
- Login credentials must be provisioned via ASP.NET Core Identity, never plaintext SQL passwords.

### Customer acceptance sign-off
Release owner, DBA, IT admin, security reviewer, and customer representative must retain dated evidence and approve. No production-approved ZIP or live deployment has been supplied in this package.
