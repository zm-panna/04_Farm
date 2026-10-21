# নিরাপদ Demo চালু করা: SQLite / SQL Server

এই demo শুধু আলাদা development database-এ ব্যবহার করুন। এতে বাস্তব customer, payment বা veterinary তথ্য নেই। কোনো password source/ZIP/SQL-এ দেওয়া নেই। Demo নিজে থেকে app startup-এ চালু হয় না।

## Default core-only demo-তে কী তৈরি হবে

নিচের counts `SEED_DEMO.ps1` / `--init-demo`-র core-only mode-এর। Packaged SQL এবং optional enterprise mode-এর current counts আলাদা: 93 tables, 1,273 rows, 100 balanced vouchers এবং 10 synthetic users। চারটি নতুন module-এর জন্য নিচের enterprise opt-in ব্যবহার করুন।

- ৫টি demo company, প্রতিটিতে ১টি farm ও ২টি branch (main + store); তাই company isolation পরীক্ষা করা যাবে।
- মোট ৫টি করে প্রযোজ্য business record: purchase, product sale, animal sale, feed, health, milk, expense, fattening plan, weight, breeding, support, subscription, return ইত্যাদি।
- ১৫টি animal: main branch-এ ৫টি active ও ৫টি sold, store branch-এ opening balance-সহ আরও ৫টি active animal। Sold animal দিয়ে milk/feed/health entry করা হয়নি।
- ৭০টি balanced voucher, ১৭০টি voucher line, ৪৫টি stock movement। Main stock-এর quantity = ১০০ purchase − ২ sale − ১ feed − ১ purchase return = ৯৬ KG। Store branch-এ প্রথম opening-এ stock ১০ KG, cash ও একটি animal-এর value capital-এর বিপরীতে post হয়। ৫ KG transfer ও ৫ KG reverse transfer-এর পরে main/store stock আবার ৯৬/১০ KG থাকে। Exact inventory value, paired transfer journal এবং GL inventory balance মেলে।
- UserManager দিয়ে নিরাপদ password hash-সহ ৫টি login। পূর্ণ তালিকা: [DEMO_SEED_MANIFEST.md](DEMO_SEED_MANIFEST.md)।

## ১. দ্রুত local demo: SQLite

.NET 10 SDK install করুন। Project root থেকে PowerShell-এ:

```powershell
.\scripts\SEED_DEMO.ps1
```

Script password গোপন prompt-এ নেবে। অন্তত ১২ অক্ষর, uppercase, lowercase, digit ও symbol দিন। Script Development-এ `demo-data/farm-demo.db` তৈরি ও seed করে; environment variables শেষে আগের অবস্থায় ফিরিয়ে দেয়। Existing customer database overwrite বা delete করে না।

App চালানোর সময় একই provider/database দিন:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:DOTNET_ENVIRONMENT='Development'
$env:Database__Provider='Sqlite'
$env:ConnectionStrings__DefaultConnection="Data Source=$((Get-Location).Path)\demo-data\farm-demo.db"
dotnet run --no-launch-profile --project .\EnterpriseERP.Web\EnterpriseERP.Web.csproj
```

Console-এ দেখানো local address-এ `/Account/Login` খুলুন। শুধু loopback/local demo-তে Development HTTP ব্যবহার করা যায়; shared/public host-এ HTTPS প্রয়োজন। HTTPS certificate trust/setup প্রয়োজন হলে .NET-এর official development certificate নির্দেশনা অনুসরণ করুন; certificate warning bypass করবেন না। SQLite হলো local development demo; SQL Server deployment-এর প্রমাণ নয়।

## ২. SQL Server demo

আলাদা empty database-এর নামে `demo` থাকতে হবে, `prod` থাকা যাবে না; যেমন `FarmERP_Demo`। Customer/live database ব্যবহার করবেন না। Provider `SqlServer` করে এই release-এর reviewed EF migration আগে apply করুন। Migration তৈরি বা apply করার আগে connection target পুনরায় যাচাই করুন।

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:DOTNET_ENVIRONMENT='Development'
$env:Database__Provider='SqlServer'
$env:ConnectionStrings__DefaultConnection='Server=localhost;Database=FarmERP_Demo;Trusted_Connection=True;Encrypt=True;'
# প্রতিষ্ঠানের অনুমোদিত trusted SQL Server certificate ব্যবহার করুন।
dotnet ef database update --project .\EnterpriseERP.Web\EnterpriseERP.Web.csproj --startup-project .\EnterpriseERP.Web\EnterpriseERP.Web.csproj
.\scripts\SEED_DEMO.ps1 -Provider SqlServer
```

এই release-এ তিনটি committed migration এবং matching snapshot আছে। সেগুলো DBA দিয়ে review ও staging-এ validate করুন; `EnsureCreated` দিয়ে production migration bypass করবেন না। SQL Server seed command schema তৈরি/পরিবর্তন করে না।

আগের `SEED_FIVE_ROWS_PER_TABLE_DEMO.sql` insertion script অবসর দেওয়া হয়েছে। সেটি এখন নিরাপদভাবে error দেখায়; পুরোনো version চালাবেন না। EF command-ই business data ও Identity একই transaction-এ তৈরি করে। সরাসরি ব্যবহার করতে হলে `FARM_ALLOW_DEMO_SEED=YES` ও গোপন environment-supplied `FARM_DEMO_PASSWORD` দিয়ে `--seed-demo` চালান। পুরোনো `--seed-demo-users` এখন একই combined seed-এর compatibility alias।

## Login ও permission

| Login | Role | Scope |
|---|---|---|
| farmdemo1@example.invalid | FarmAdmin | Demo company 1: tenant-scoped `*` |
| farmdemo2@example.invalid | Manager | Demo company 2: farm operations, stock movement, reports, support |
| farmdemo3@example.invalid | Accounts | Demo company 3: finance, reconciliation, expenses |
| farmdemo4@example.invalid | Inventory | Demo company 4: stock, purchase, product sale |
| farmdemo5@example.invalid | Viewer | Demo company 5: read-only farm/report/support views |

Password হলো initialization-এর সময় আপনার দেওয়া secret। Demo accounts-এর কোনো PlatformAdmin role বা platform billing authority নেই। ফলে platform readiness endpoint-এ demo administrator-ও অনুমতি পাবে না; নতুন real installation-এর trusted `--bootstrap-admin` workflow প্রথম PlatformAdmin তৈরি করে। Demo database-এ সেই bootstrap চালাবেন না।

## পুনরায় চালালে কী হবে

- সফল seed marker থাকলে আবার চালানো read/validation-only no-op। Password, permission, business balance বা পুরোনো date বদলাবে না।
- মাঝপথে Identity বা business validation ব্যর্থ হলে সব business/Identity/role/grant পরিবর্তন rollback হবে।
- Existing non-demo data, incomplete/old seed, ভুল provider/name বা Production environment হলে seed বন্ধ হবে। কোনো force/overwrite option নেই। এই fixture-এর version V3; আগের V2 demo থাকলে নতুন আলাদা database/file (যেমন `farm-demo-v3.db`) নির্বাচন করুন। আগের data মুছে বা বদলে দেওয়া হয় না।
- ভুল password দিয়ে login হলে seed rerun password reset করবে না। অনুমোদিত password recovery ব্যবহার করুন, অথবা শুধু disposable demo database-এর ক্ষেত্রে operator নিজে নতুন আলাদা demo database বেছে নিন।
- পাঁচটি user কেবল নিজেদের company-র দুইটি demo branch-এ access পান; অন্য company-তে নয়। MFA secret/recovery code আগে থেকে বানিয়ে রাখা হয় না; নিজের account-এর security page থেকে প্রকৃত enrollment করুন।
- Automation rules inactive। Scheduler, approval বা execution history বানিয়ে পূরণ করা হয় না। Subscription demo invoice paid, তাই পুরোনো demo চালালে বকেয়ার কারণে স্বয়ংক্রিয় suspension হবে না।

## পরীক্ষা

```powershell
dotnet test .\Tests\EnterpriseERP.UnitTests\EnterpriseERP.UnitTests.csproj --filter FullyQualifiedName~DemoSeederTests
```

এই tests local SQLite-এ counts, balanced voucher, stock/value reconciliation, secured password hash, idempotence, target safety এবং rollback যাচাই করে। SQL Server deployment-এর migration/constraint/runtime পরীক্ষা আলাদাভাবে staging-এ করতে হবে। Demo figures বাস্তব financial reporting বা veterinary সিদ্ধান্তের জন্য ব্যবহার করবেন না।

## নতুন installation-এর admin ও staff setup

Demo database-এর বাইরে একেবারে নতুন, migrated database-এ `scripts/PROVISION_FIRST_ADMIN.ps1` দিয়ে প্রথম admin তৈরি করুন। এটি প্রথম account-কে `PlatformAdmin` role ও নিজের company-র প্রশাসনিক access দেয়; existing database-এ bootstrap পুনরায় চলে না। এই account অত্যন্ত গুরুত্বপূর্ণ, তাই credentials গোপন রাখুন।

Login → Company & Branch নির্বাচন → `/Administration`:

1. Tenant administrator নিজের অনুমোদিত branch-এ নতুন staff account এবং তালিকাভুক্ত operational permission দিতে পারেন। Initial password form-এ দিতে হয়; সফল/ব্যর্থ submission-এর পরে password আর দেখানো হয় না। কোনো email নিজে থেকে পাঠানো হয় না। Staff member-কে অনুমোদিত secure মাধ্যমে password দিয়ে প্রথম login-এর পরে বদলাতে বলুন।
2. Existing account-এর exact email lookup করে ফেরত আসা account ও company/branch যাচাই করুন; checkbox দিয়ে confirm করলে কেবল ওই access যোগ হয়। Existing password/role পরিবর্তন হয় না। Lookup ১০ মিনিট পরে শেষ হয়; অন্য admin বা company-তে ব্যবহার করা যায় না।
3. দরকার হলে staff তালিকা থেকে নির্দিষ্ট branch অথবা ওই company-র সব access ও operational permission revoke করুন। পুরোনো session-এর branch claim পরের request থেকেই access পাবে না। অন্য company, Identity account এবং business/audit history থাকে। নিজের/শেষ admin বা PlatformAdmin access এই UI থেকে বাদ দেওয়া যায় না। Company-wide revoke করতে target account-এর সব সংশ্লিষ্ট branch আপনার নিজের অনুমোদিত scope-এর মধ্যে থাকতে হবে।
4. শুধু প্রকৃত PlatformAdmin নতুন company + farm + branch অথবা বর্তমান company-তে নতুন farm/branch তৈরি করতে পারেন। নতুন company-তে নিজের administrative scope তৈরি হয়; অন্য staff-কে পরে আলাদা করে provision করতে হবে।

Tenant-level permission screen থেকে `*`, administrator privilege, PlatformAdmin বা platform billing privilege তৈরি করা যায় না। Demo account-এর tenant wildcard তাকে platform administrator করে না।

## ৩. Data-সহ SQL script তৈরি করা

প্রথমে committed migration থেকে `evidence/schema-reviewed.sql` generate ও review করুন। এরপর নতুন private local folder নির্বাচন করুন:

```powershell
.\scripts\EXPORT_DEMO_SQL.ps1 -OutputDirectory 'C:\PrivateFarmDemoExport'
```

Folder আগে থেকে থাকলে script বন্ধ হবে। গোপন prompt-এ নতুন demo password দিন; কোনো real account-এর password দেবেন না। Script fresh local SQLite demo তৈরি করে `01-schema-reviewed.sql`, `02-demo-data.sql` ও নির্দেশনাসহ README দেয়। SQLite source-ও ওই private folder-এ থাকে। Schema/data script apply করা বা target SQL Server-এ সংযোগ দেওয়া হয় না।

Data SQL-এ প্রকৃত synthetic business rows ও `UserManager`-এর তৈরি Identity password hash থাকে। তাই import-এর পরে পাঁচটি demo login-এ আপনার বেছে নেওয়া password ব্যবহার করা যাবে। Plaintext password, MFA secret, recovery code বা external token লেখা/print করা হয় না। Hash-সহ SQL ও source database সংবেদনশীল: public repository, public ZIP বা অন্যের সঙ্গে অনুমতি ছাড়া share করবেন না।

Import করার আগে connection ও আলাদা empty database যাচাই করুন; নামের মধ্যে `demo` থাকতে হবে, `prod` থাকা যাবে না। আগে reviewed schema, পরে data script চালাবেন। Data script সব application/Identity table খালি আছে কি না পরীক্ষা করে এবং এক transaction-এ insert করে; error হলে rollback হয়। Existing/live database-এ ব্যবহার করবেন না। SQL Server runtime verification আলাদা release check।

Existing fresh demo থেকে সরাসরি export-এর contract:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:DOTNET_ENVIRONMENT='Development'
$env:FARM_ALLOW_DEMO_EXPORT='YES'
# একই demo provider ও connection environment-এ নির্বাচন করুন।
dotnet run --no-launch-profile --project .\EnterpriseERP.Web\EnterpriseERP.Web.csproj -- --export-demo-sql 'C:\PrivateFarmDemoExport\02-demo-data.sql'
Remove-Item Env:FARM_ALLOW_DEMO_EXPORT
```

Source-এ ৫টি পরিচিত demo company/user, exact initial counts ও seed-এর content fingerprint থাকতে হবে। নতুন row, edited data, MFA/token বা অন্য Identity পরিবর্তন হলে export বন্ধ হবে। Login বা demo ব্যবহার করার আগেই export করুন। Fingerprint ছাড়া পুরোনো V3-ও গ্রহণ করা হয় না; নতুন আলাদা demo file নিন। Rerun fingerprint/password/data বদলায় না। এখন seed audit মোট ৬টি: ৫টি completion ও ১টি প্রকৃত fingerprint event। Fingerprint accidental পরিবর্তন ধরার ব্যবস্থা; database administrator ইচ্ছাকৃতভাবে data ও digest দুটিই বদলালে এটি authenticity প্রমাণ নয়।

## চারটি enterprise module-এর optional fictional demo

নতুন আলাদা SQLite file ব্যবহার করে `SEED_DEMO.ps1 -ConnectionString "Data Source=<absolute private path>\farm-enterprise-demo.db" -IncludeEnterprise -ConfirmFictionalPolicies` চালান। একই file-এ পুরোনো core-only demo-কে upgrade করে না।

পাঁচ maker-এর সঙ্গে `farmreviewer1@example.invalid` থেকে `farmreviewer5@example.invalid` reviewer তৈরি হয়। HR/Payroll, CRM, fixed assets, Ecommerce এবং reviewed company/tax policies-এর linked rows দেখায়; rates, consent ও review evidence সব fictional। Package SQL-এর password discarded, তাই নিজের usable logins চাইলে private initialization/export workflow চালাতে হবে। Exact current counts ও empty security-table exceptions `DEMO_SEED_MANIFEST.md`-এ আছে। Enterprise seed helper শুধুমাত্র isolated Development SQLite; generated SQL-এর actual SQL Server import আলাদাভাবে যাচাই করতে হবে।

Demo SQL export web host চালু বা login করার আগে করুন; host-এর outbox worker demo delivery-state পরিবর্তন করতে পারে। এই fixture বাস্তব message বা provider send করে না।
