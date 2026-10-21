# Build repair and Live release gate — বাস্তব অবস্থা

এই source .NET 10 target করে। Container-এ .NET SDK এবং SQL Server নেই; তাই এখানে `dotnet restore`, `dotnet build`, `dotnet test`, EF migration বা IIS verification **চালানো হয়নি**। ZIP integrity check compilation-এর সমান নয়।

## Windows 11 / Server-এ চালান

1. Visual Studio 2022/.NET 10 SDK এবং SQL Server/SQL Express install করুন।
2. Empty, disposable staging database তৈরি করে `FARM_STAGING_CONNECTION` environment variable-এ তার connection string দিন। Password repo/ZIP-এ রাখবেন না।
3. ZIP extract করে root folder থেকে PowerShell:

```powershell
$env:FARM_STAGING_CONNECTION = 'Server=localhost;Database=FarmERP_Staging;Trusted_Connection=True;TrustServerCertificate=True'
./scripts/BUILD_RELEASE_WINDOWS.ps1 -GenerateInitialMigration -Publish
```

4. `release-evidence/<timestamp>/build.log`, `tests.log`, `migration-review.sql`, `publish-staging.zip` পরীক্ষা করুন। Initial EF migration **সোর্সে আগে থেকে ছিল না**; scaffold করা migration কোড/SQL DBA-এর review ছাড়া prod-এ apply করবেন না।
5. Database backup নিয়ে reviewed migration staging-এ apply করুন; তারপর `--bootstrap-admin` command দিয়ে প্রথম user/company provision করুন।
6. staging-এ publish করুন, IIS hosting bundle install করুন, HTTPS চালু করুন এবং `./scripts/STAGING_SMOKE_TEST.ps1 -BaseUrl https://your-staging.example.com` চালান।
7. Customer UAT-এ animal entry, purchase, inventory, sales, double-entry voucher, reversal, subscription due, user permissions, automation approvals, support ticket এবং তিন ভাষা যাচাই করুন।

## Release blockers

- বহু Enterprise module কেবল `ModuleMarker.cs` (full CRUD/UI/workflow নয়)।
- EF migrations committed নয়; staging review প্রয়োজন।
- Live payment/messaging, Ollama security, scheduling & subscription enforcement সম্পূর্ণ acceptance হয়নি।
- Multi-tenant isolation/authorization penetration tests, concurrency and backup/restore tests হয়নি।
- Production IIS deployment ও user acceptance হয়নি।

এই package **development/build remediation**; customer production-ready certification নয়।
