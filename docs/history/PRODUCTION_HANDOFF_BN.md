# Smart Farm ERP — Production handoff (বাংলা)

**বর্তমান অবস্থা: BLOCKED / NOT PRODUCTION APPROVED**। এখানে .NET SDK, SQL Server ও Windows IIS পাওয়া যায়নি। আগের source-এ marker-only module ও checked-in EF migration নেই। তাই 100% complete বা verified বলা যাবে না।

## Windows staging run
1. ZIP extract করুন, .NET 10 SDK, SQL Server staging database, ASP.NET Core Hosting Bundle ও IIS setup করুন। SQL Server production database-এ সরাসরি migration চালাবেন না।
2. Windows PowerShell-এ project root-এ `python scripts/AUDIT_PRODUCTION_READINESS.py` চালান; missing items দেখাবে।
3. DB backup ও restore drill সম্পন্ন করুন; existing SQL schema যাচাই করুন। EF migration তৈরি করলে DBA দিয়ে generated SQL review করান এবং source control-এ commit করুন।
4. `$env:ConnectionStrings__DefaultConnection` নিরাপদভাবে staging connection হিসেবে দিন, actual secrets repository/ZIP-এ রাখবেন না।
5. `./scripts/WINDOWS_PRODUCTION_EVIDENCE.ps1 -StagingUrl https://STAGING-DOMAIN -StagingConnection $env:ConnectionStrings__DefaultConnection` চালান। `-ApplyStagingMigration` কেবল reviewed migration ও backup-এর পরে।
6. Restore/build/tests pass, migration applied, IIS app start, login/company isolation, full CRUD workflows, bilingual/trilingual forms, subscription, accounts reconciled, AI tool permissions, PDF and backup/restore verify করুন।
7. `release-evidence/` এর logs সংরক্ষণ করুন। Customer representative-এর signed UAT ও owner-authorized security signoff ছাড়া Live approval দেবেন না।

**Important:** PowerShell script build করার চেষ্টা করে; কোড অসম্পূর্ণ থাকলে script ঠিকই ব্যর্থ হবে। Script থাকা মানে build সফল নয়।

## Additional finding
The provided archive does not contain `EnterpriseERP.sln`. Verification commands now target the Web and UnitTests `.csproj` files directly. A checked-in solution file must still be created and reviewed for final handoff.
