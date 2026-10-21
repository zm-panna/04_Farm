# Smart Farm AI ERP — .NET 10

**Revision 6 secure hosting:** আগে [trusted backend/vault guide](docs/TRUSTED_BACKEND_AND_VAULT_BN.md) পড়ুন। Production/Staging এখন OpenBao configuration ছাড়া fail-closed; পুরোনো direct-connection instructions কেবল reviewed operator migration/preflight context। PublicGateway ও management UI আলাদা Trusted Platform package-এ।

এই source package-এ Farm-এর business workflow, multi-company/branch permission, Identity login, হিসাব/stock integration, AI Manager এবং local demo রয়েছে। মূল uploaded ZIP-এর অসম্পূর্ণ source থেকে সংশোধন করা হয়েছে। **Customer production approval আলাদা:** actual SQL Server, Windows IIS, configured provider এবং customer UAT-এর evidence ছাড়া live-ready দাবি করা হয়নি।

প্রথম live scope: বাংলাদেশের একটি company; HR/Payroll, CRM, Fixed Assets ও E-commerce চারটিই থাকবে। আগের corrective revision-এ onboarding, explicit payroll policy input, সম্পূর্ণ শেষ হওয়া payroll month এবং company-local accounting date সংশোধন করা হয়েছে। যাচাইয়ের বর্তমান অবস্থা নিচের machine-readable report-এ আছে।

বর্তমান Revision 7 পরীক্ষায় 923 .NET, 1,333 localhost HTTP, 84 JavaScript এবং 6 fail-closed startup probe পাস করেছে। Actual Farm gateway transport-ও পরীক্ষা হয়েছে; real OpenBao/SQL Server/IIS customer deployment আলাদা gate।

## QR ও barcode scanner

`/scan`-এ product/feed, animal, asset, purchase order ও document lookup; সাতটি বিদ্যমান business action-এর পৃথক reviewed draft/confirmation; QR+Code128 print; manual/keyboard/camera input আছে। Scanner নিজে scan থেকে posting করে না। [ব্যবহার ও scope](docs/SCANNING_WORKFLOWS_BN.md) এবং [পরীক্ষার সীমা](docs/SCANNING_VERIFICATION.md) পড়ুন। Physical camera, SQL Server/IIS ও customer UAT এখনও আলাদা gate।

## Redis / server-side tables / internal messaging

নতুন realtime/large-data checkpoint-এর জন্য [Revision 5 handoff](docs/REVISION5_REALTIME_BN.md) এবং [technical/deployment guide](docs/REALTIME_AND_LARGE_DATA.md) আগে পড়ুন। `/data` ও `/Messaging` এখন কার্যকর permission-scoped UI। Redis default-এ বন্ধ; local evidence production rollout নয়।

## প্রথমে কোন ফাইল পড়বেন

- [Revision 4-এর সংক্ষিপ্ত handoff](docs/REVISION4_HANDOFF_BN.md)

- [Bangladesh first-live review](docs/FIRST_LIVE_BANGLADESH_REVIEW.md), [email ownership review](docs/EMAIL_OWNERSHIP_REVIEW_BN.md) ও [read-only SQL/IIS evidence guide](docs/STAGING_READONLY_PREFLIGHT.md)
- [Enterprise revision-এর পরিবর্তন ও সীমা](docs/ENTERPRISE_REVISION_CHANGES.md)
- [বর্তমান requirement matrix](docs/REQUIREMENT_MATRIX.md) এবং [machine-readable verification](docs/CURRENT_VERIFICATION.json)
- [নিরাপদ demo ও login](docs/DEMO_LOGIN_AND_SQL_BN.md)
- [Company policy এবং Windows PC/IIS নির্দেশনা](docs/ENTERPRISE_POLICY_AND_WINDOWS_GUIDE_BN.md)
- [SQL Server / IIS deployment runbook](docs/LIVE_DEPLOYMENT_RUNBOOK_BN.md)
- [বাস্তব staging/UAT checklist](docs/LIVE_UAT_CHECKLIST_BN.md)
- [Opening balance](docs/OPENING_BALANCE_WORKFLOW.md), [branch transfer](docs/STOCK_TRANSFER_WORKFLOW.md), [MFA ও recovery](docs/MFA_AND_LOCAL_ACCOUNT_RECOVERY.md) এবং [চার ভাষার reports](docs/MULTILINGUAL_REPORTS.md)
- `evidence/`: এই revision-এ বাস্তবে চালানো build/test/dependency/migration-generation logs; পুরোনো status `docs/history`-এ থাকলে সেগুলো historical।

## Local demo: সবচেয়ে ছোট পথ

.NET 10 SDK install করা Windows-এ ZIP extract করুন। যে folder-এ `EnterpriseERP.slnx` আছে সেখানে PowerShell খুলুন।

```powershell
dotnet restore EnterpriseERP.slnx
.\scripts\SEED_DEMO.ps1
```

Password script-এর hidden prompt-এ নিজে দিন: কমপক্ষে ১২ অক্ষর, uppercase/lowercase/digit/symbol। কোনো public default password নেই। Seed script শুধু আলাদা Development database-এর জন্য। আবার চালালে existing password/data পরিবর্তন হয় না।

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:DOTNET_ENVIRONMENT='Development'
$env:Database__Provider='Sqlite'
$env:ConnectionStrings__DefaultConnection="Data Source=$((Get-Location).Path)\demo-data\farm-demo.db"
dotnet run --no-launch-profile --project EnterpriseERP.Web --urls http://127.0.0.1:5099
```

নিজের browser-এ `http://127.0.0.1:5099/Account/Login` খুলুন। Login `farmdemo1@example.invalid`; password initialization-এর সময় আপনার দেওয়া secret। আরও চারটি ভিন্ন permission/company account demo guide-এ আছে। UI থেকে অনুমোদিত company/branch নির্বাচন করুন। Header/URL বদলে অন্য company access দেওয়া হয় না।

SQLite এখানে isolated local evaluation/test-এর জন্য। Public deployment-এ Development HTTP নয়; production-এ SQL Server ও HTTPS ব্যবহার করুন। এই demo run SQL Server বা IIS verification নয়।

## চারটি নতুন module-সহ fictional demo

নতুন খালি demo file ব্যবহার করুন; পুরোনো core demo একই file-এ upgrade করা হয় না।

```powershell
New-Item -ItemType Directory -Force demo-data
.\scripts\SEED_DEMO.ps1 -ConnectionString "Data Source=$((Get-Location).Path)\demo-data\farm-enterprise-demo.db" -IncludeEnterprise -ConfirmFictionalPolicies
```

Run-এর সময় একই `ConnectionStrings__DefaultConnection` ব্যবহার করুন। অতিরিক্ত reviewers: `farmreviewer1@example.invalid` থেকে `farmreviewer5@example.invalid`; password আপনার privately দেওয়া demo secret। HR/Payroll, CRM, Fixed Assets এবং E-commerce-এর sample policy/rates/reviews সব fictional। Production-এর জন্য নিজের অনুমোদিত country/state, currency, timezone ও effective-dated policy আলাদাভাবে configure/review করতে হবে।

## Source verification

```powershell
.\scripts\VERIFY_SOURCE.ps1
```

Linux-এ equivalent `scripts/verify-source.sh`। Canonical verification script restore/build/tests, model/migration consistency এবং publish পরীক্ষা করে; error হলে থামে। নিজে চালিয়ে output/evidence review করুন। `FINAL_RELEASE_VERIFY` source pass-কে SQL/IIS/UAT approval হিসেবে ধরে না।

## SQL Server এবং আসল admin

1. আলাদা staging database ও approved encrypted SQL connection provision করুন। Config key `ConnectionStrings__DefaultConnection`; source/appsettings-এ secret নয়। Provider `Database__Provider=SqlServer`।
2. এই release-এর committed migrations থেকে SQL generate করে DBA review করুন। Existing database-এ আগে backup/restore rehearsal এবং schema compatibility review দরকার।
3. Empty migrated installation-এ `--bootstrap-admin` চালাতে secure local environment-এ `FARM_BOOTSTRAP_EMAIL`, `FARM_BOOTSTRAP_PASSWORD`, `FARM_BOOTSTRAP_COMPANY`, `FARM_BOOTSTRAP_BRANCH` provision করতে হয়। Bootstrap-এর পরে secret সরিয়ে দিন। Demo database-এ bootstrap চালাবেন না।
4. Permission অনুযায়ী staff/branch access provision করুন; demo account-এ PlatformAdmin অধিকার নেই।
5. SQL Server demo দরকার হলে শুধু আলাদা demo-named database-এ `SEED_DEMO.ps1 -Provider SqlServer` ব্যবহার করুন। Customer database-এ demo rows নয়।

Production bootstrap/startup-এর আগে explicit AllowedHosts, valid encrypted SQL connection এবং persistent protected Data Protection keyring বাধ্যতামূলক। Windows IIS-এ `Security__DataProtection__KeyDirectory`, `ApplicationName` ও `ProtectionMode=Dpapi` configure করুন; পূর্ণ key names ও app-pool নির্দেশনা [deployment runbook](docs/LIVE_DEPLOYMENT_RUNBOOK_BN.md)-এ আছে। পুরোনো standalone demo INSERT script-এর বদলে typed transactional seeder এবং guarded SQL exporter ব্যবহার করা হয়েছে; Identity hashes UserManager দিয়ে তৈরি হয়। Blank/fake hash দিয়ে login সম্ভব নয়। Schema SQL generation এবং actual SQL Server data-seed execution আলাদা evidence।

### নিজের password-সহ SQL schema + data export

`scripts/EXPORT_DEMO_SQL.ps1 -OutputDirectory <new absolute private local folder>` চালান। Hidden prompt-এ নিজের demo password দিন। Script fresh synthetic fixture থেকে schema/data SQL তৈরি করে; real Identity hashes থাকে, plain-text password/MFA/token থাকে না। নতুন private output folder ব্যবহার করুন এবং export webroot/Git/public share-এ রাখবেন না। Altered/non-demo database বা existing output overwrite অনুমোদিত নয়।

Package-এর `sql/01-SCHEMA_REVIEW.sql` ও `sql/02-DEMO_DATA.sql` ব্যবহারের নিয়ম [SQL sample guide](sql/README_BN.md)-এ আছে। এই pre-generated sample-এর random initialization password discarded: সেটি দিয়ে জানা password-এ login দাবি করা হয়নি। ব্যবহারযোগ্য নিজের logins পেতে উপরের exporter/seed workflow নিজে চালাতে হবে। Exported SQL কেবল reviewed isolated demo SQL Server database-এ apply ও test করুন; customer/live database-এ নয়।

## AI, automation ও reports

- AI Manager permission-scoped data ব্যবহার করে। Model provider configure না করা থাকলে/ব্যর্থ হলে safe fallback যাচাই করুন; model output আর verified ledger fact এক নয়।
- Ollama endpoint/model নিজের approved configuration থেকে দিন। Customer/private data কোনো external AI service-এ পাঠানোর আগে প্রতিষ্ঠানের অনুমতি ও data policy স্থির করুন।
- Financial posting, access changes এবং sensitive automation human-controlled workflow-এর বাইরে AI-কে দেবেন না। Demo automation inactive।
- Production PDF-এর জন্য `Reporting__QuestPdfLicense` প্রতিষ্ঠানের eligibility অনুযায়ী configure করুন; [license guide](https://www.questpdf.com/license/guide.html) review প্রয়োজন।

## Scope ও live release

HR/Payroll, CRM, Fixed Asset এবং E-commerce প্রথম live release-এর জন্য নিশ্চিত করা হয়েছে এবং এই revision-এ বাস্তব workflow যোগ করা হয়েছে। Hotel ও LC/Import–Export অন্তর্ভুক্তির সিদ্ধান্ত হয়নি; warehouse bin/lot/expiry এবং অন্যান্য advanced scope আলাদা specification চায়। Bangladesh, USA এবং Arab country deployment-এর জন্য exact Arab country, US state/locality ও accountant-approved policy এখনও দিতে হবে। বর্তমান module/status matrix অনুযায়ী implemented workflow ও সীমা যাচাই করুন।

Failed accounting/security test, missing required workflow, unverified SQL/IIS deployment বা unresolved business policy থাকলে production launch করবেন না। Test logs বা ZIP integrity alone customer acceptance নয়। কোনো real integration-এ credentials/configuration বসানো বা message/payment পাঠানো এই ZIP delivery-র অংশ হিসেবে সম্পন্ন হয়েছে ধরে নেবেন না।
