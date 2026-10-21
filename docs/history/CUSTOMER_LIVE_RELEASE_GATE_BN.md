# Smart Farm ERP — Production Release Gate (19 October 2026)

## বর্তমান সত্যিকারের অবস্থা

এই ZIP একটি development source package, **সম্পূর্ণ 100% ERP নয়**। Enterprise module-গুলোর বড় অংশ শুধু ModuleMarker ও placeholder Area page। .NET SDK, SQL Server, IIS ও customer UAT এখানে পাওয়া যায়নি। Compile সফল হয়েছে এমন evidence নেই।

## এই আপডেটে যা পরিবর্তন

- SubscriptionState calculation-এ policy-based SuspendDays, GraceDays এবং active OverrideUntil প্রয়োগ করে middleware-এর সঙ্গে status consistent করা হয়েছে।
- নতুন offline source QA script যোগ করা হয়েছে। এটি compilation test নয়।
- `AUDIT_LIVE_READINESS.ps1` unfinished project count করে `release-evidence/live-readiness.json` দেয় এবং `-Strict` দিলে incomplete code block করে।
- আগের starter/source, English/Bangla/Banglish selector, transaction posting ও automation code অপরিবর্তিত রেখেছি।

## Windows staging-এ বাধ্যতামূলক workflow

1. .NET 10 SDK, SQL Server staging instance এবং উপযুক্ত dotnet-ef 10.x ইনস্টল করুন।
2. বাস্তব connection string environment variable-এ দিন; source বা ZIP-এ credentials লিখবেন না।
3. `python scripts/source_quality_audit.py` চালান।
4. `powershell -ExecutionPolicy Bypass -File scripts/AUDIT_LIVE_READINESS.ps1 -Strict` চালান। বর্তমান placeholder থাকায় ইচ্ছাকৃতভাবে release block হবে।
5. প্রতিটি placeholder module-এর real CRUD/services/validation/tenant security সম্পন্ন করুন।
6. `scripts/BUILD_RELEASE_WINDOWS.ps1 -GenerateInitialMigration -Publish` staging-এ চালিয়ে log/SQL migration review করুন।
7. QA: purchase→receipt→stock→AP→payment→GL; sales→reservation→delivery→AR→ledger; dairy, livestock, health, feed; automation approval/retry; tenant isolation; all languages; reports/PDF; subscription 14/15/29/30; backup/restore।
8. IIS HTTPS staging-এ deploy করে customer sign-off নিন। এর আগে Live নয়।

## Report interpretation

Static checks passing ≠ compilation passing ≠ user acceptance passing ≠ production ready.
