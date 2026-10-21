# Smart Farm ERP — SQL Server ও Windows IIS deployment

**Revision 6 secure hosting:** আগে [trusted backend/vault guide](TRUSTED_BACKEND_AND_VAULT_BN.md) পড়ুন। Production/Staging এখন OpenBao configuration ছাড়া fail-closed; পুরোনো direct-connection instructions কেবল reviewed operator migration/preflight context। PublicGateway ও management UI আলাদা Trusted Platform package-এ।

এই নির্দেশনা deployment করার পথ; deployment সম্পন্ন হওয়ার certificate নয়। একই release-এর verification report-এ যে test বাস্তবে চালানো হয়েছে শুধু সেটিই verified। `LIVE_UAT_CHECKLIST_BN.md`-এর relevant ধাপগুলো staging-এ শেষ করুন।

## নিরাপদ প্রস্তুতি

1. Customer production থেকে আলাদা staging hostname/database ব্যবহার করুন। Existing database থাকলে backup ও restore rehearsal করুন।
2. Build machine-এ .NET 10 SDK, IIS server-এ app-এর সঙ্গে সামঞ্জস্যপূর্ণ .NET Hosting Bundle দিন। IIS আগে চালু না থাকলে Hosting Bundle install/repair-এর vendor নির্দেশনা অনুসরণ করুন। IIS deployment এবং Data Protection key persistence-এর [Microsoft নির্দেশনা](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/) দেখুন।
3. Dedicated app pool identity দিন; published files read/execute, প্রয়োজনীয় log/data path-এ সীমিত write। পুরো source/webroot-এ সবার write access নয়।
4. SQL migration operator এবং runtime app user আলাদা রাখুন। Runtime user-কে schema পরিবর্তনের অধিকার দেবেন না।
5. Connection string, bootstrap password, API key user-secrets/secure environment বা secret store-এ provision করুন। Source, ZIP, screenshot, chat ও PowerShell history-এ আসল password লিখবেন না। Development User Secrets production secret store নয়।

## Build এবং publish

ZIP extract করা root folder থেকে commands চালান। প্রতিটি command-এর exit code সফল না হলে পরের ধাপে যাবেন না।

```powershell
dotnet --info
dotnet restore EnterpriseERP.slnx
dotnet build EnterpriseERP.slnx -c Release --no-restore
dotnet test EnterpriseERP.slnx -c Release --no-build
dotnet publish EnterpriseERP.Web/EnterpriseERP.Web.csproj -c Release --no-restore -o artifacts/publish
```

Published output deploy করুন; source folder IIS physical path হিসেবে ব্যবহার করবেন না। Installer/scripts-এর canonical পথ README-তে দেখুন। Automated test সফল হলেও SQL Server-specific concurrency, IIS এবং customer UAT আলাদা থাকে।

## SQL Server schema

Configuration key: `ConnectionStrings:DefaultConnection`; environment variable: `ConnectionStrings__DefaultConnection`। SQL Server-ই live target। Local SQLite demo production-এর বিকল্প নয় এবং তার test SQL Server execute হয়েছে প্রমাণ করে না।

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent --project EnterpriseERP.Web --startup-project EnterpriseERP.Web -o artifacts/migration-review.sql
```

Generated script DBA review করবেন। Existing schema-তে migration history নেই মানেই database খালি নয়। Applied migrations, tables, constraints, affected rows ও compatibility মিলিয়ে তারপর backup নেওয়া staging-এ apply করুন। `InitialCreate` নতুন করে generate করে existing database-এ চালাবেন না। Idempotent script migration history অনুযায়ী missing migration চালায়; unknown legacy schema স্বয়ংক্রিয়ভাবে repair করে না। [EF Core migration deployment নির্দেশনা](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)।

Reviewed নতুন development/staging database-এ প্রয়োজনে:

```powershell
dotnet ef database update --project EnterpriseERP.Web --startup-project EnterpriseERP.Web
```

Demo seed production-এ চালাবেন না। Production bootstrap-এর সময় local secure provisioning ব্যবহার করুন; successful provisioning-এর পরে bootstrap secret সরিয়ে দিন। Database row-এ plain-text password বসাবেন না; Identity password hash একই app-এর UserManager দিয়ে তৈরি হতে হবে।

## IIS configuration ও acceptance

- Dedicated staging site ও HTTPS binding তৈরি করুন; certificate/hostname সঠিক হতে হবে। Security warning bypass নয়।
- `ASPNETCORE_ENVIRONMENT=Production`, SQLServer provider এবং approved host configuration ব্যবহার করুন। `AllowedHosts`-এ actual hostname রাখুন; reverse proxy headers trusted topology অনুযায়ী configure করুন।
- Data Protection keys app pool identity-এর নিরাপদ persistent location-এ provision করুন, webroot-এর বাইরে; recycle test করুন। Server farm হলে compatible shared key protection আলাদাভাবে review করুন।
- Publish-generated `web.config` রাখুন। ASP.NET Core app-এর app pool configuration Microsoft IIS নির্দেশনা অনুযায়ী দিন।
- Login, authorized `/ops/readiness`, company/branch selection, purchase, stock, sale, ledger এবং report চালান। Authorized readiness 503 হলে database/connectivity/migration issue সমাধান ছাড়া launch নয়।
- Unauthenticated readiness deny হওয়া health success নয়; operator-এর authenticated session ও সঠিক privilege দিয়ে পরীক্ষা করুন।
- Startup সমস্যা হলে Windows Event Viewer ও সীমিত সময়ের diagnostic logs দেখুন; sensitive details user-facing error page-এ প্রকাশ নয়। Diagnostic logging শেষে বন্ধ/retention সীমিত করুন।

## Go-live ও rollback

1. Tested release ZIP hash, publish hash, migration review, test evidence এবং customer accepted scope লিখিতভাবে রাখুন।
2. Maintenance window, expected downtime, database backup, rollback owner এবং previous publish package প্রস্তুত করুন।
3. Maintenance-এর পরে নতুন app+database compatibility যাচাই করুন। Old binaries ফিরিয়ে দিলেই incompatible schema rollback হয় না।
4. Database restore করলে backup-এর পরের write হারাতে পারে; business owner-এর নির্দিষ্ট সিদ্ধান্ত ও reconciliation ছাড়া destructive restore নয়।
5. Launch-এর পরে error rate, failed jobs, login failure, stock/ledger reconciliation এবং backup success monitor করুন। কোনো external automation চালুর আগে recipient/consent/policy ও real provider test আলাদা করে যাচাই করুন।

## Release আটকে রাখার কারণ

Failed security/financial tests, unverified required business workflow, incomplete schema migration, failed backup restore, real SQL/IIS test না থাকা বা unresolved critical defect থাকলে release blocked থাকবে। Pending code এবং environment-dependent pending verification আলাদাভাবে status report-এ থাকবে।

## PDF library license gate

Production PDF export-এর আগে প্রতিষ্ঠানের QuestPDF license eligibility যাচাই করে `Reporting__QuestPdfLicense` configure করতে হবে। Development evaluation-এর Community default থেকে customer-এর Community eligibility প্রমাণ হয় না। প্রযোজ্য license নির্বাচন/ক্রয়ের সিদ্ধান্ত প্রতিষ্ঠানের দায়িত্ব; এই deliverable কোনো paid license কেনা বা license agreement গ্রহণের প্রমাণ নয়। বর্তমান শর্ত: [QuestPDF license guide](https://www.questpdf.com/license/guide.html)। License configuration না থাকলে PDF feature-এর fail-closed আচরণ staging-এ যাচাই করুন।

## Production startup-এর বাধ্যতামূলক configuration

Production bootstrap এবং normal startup দুটিতেই security configuration আগে দিতে হবে:

| Key | প্রয়োজন |
|---|---|
| `Database__Provider` | `SqlServer` |
| `ConnectionStrings__DefaultConnection` | approved encrypted SQL connection; valid server certificate, `TrustServerCertificate=False` |
| `AllowedHosts` | প্রতিষ্ঠানের actual allowed hostname(s) |
| `Security__DataProtection__KeyDirectory` | আগে তৈরি private absolute directory; wwwroot-এর বাইরে |
| `Security__DataProtection__ApplicationName` | স্থির app identifier, যেমন `SmartFarmERP` |
| `Security__DataProtection__ProtectionMode` | Windows IIS-এ `Dpapi` |

IIS app pool-এর নির্ধারিত identity স্থির রাখুন এবং `LoadUserProfile=true` configure করুন। Key directory-তে শুধু প্রয়োজনীয় app-pool/administrative access দিন; deployment প্রতিবার directory মুছে ফেলবেন না। Identity/key পরিবর্তনে existing cookies/token ব্যবহার ব্যাহত হতে পারে; staging recycle test আবশ্যক। Keyring backup-ও সংবেদনশীল security material।

Non-Windows production host বেছে নিলে `ProtectionMode=Certificate`, `Security__DataProtection__CertificatePath` এবং secret-store provisioned `Security__DataProtection__CertificatePassword` প্রয়োজন; private RSA certificate ও restricted filesystem permission দিতে হবে। এগুলো local operator-এর secure provisioning-এর বিষয়; credential এখানে বা source-এ লিখবেন না। Windows IIS live target-এর বদলে অন্য host ব্যবহার করলেই IIS test passed হবে না।

ভুল configuration-এ startup বন্ধ হওয়া intentional safety gate। Gate পাশ কাটাতে TLS/certificate validation বন্ধ না করে certificate, SQL hostname, identity ও config ঠিক করুন।
