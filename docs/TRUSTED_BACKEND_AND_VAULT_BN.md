# Revision 6: trusted Farm backend এবং নিজের vault

## কী বদলেছে

Farm-এর SQL connection, Redis cache connection, আলাদা Redis SignalR backplane connection এবং Data Protection certificate password এখন shared `TrustedInfrastructure` adapter দিয়ে trusted backend startup-এ OpenBao থেকে নেওয়া যায়। Production/Staging-এ vault configure না থাকলে startup বন্ধ হয়। আগের Development demo চালু থাকে; সেটি vault-protected বলে দাবি করা হয় না।

নিজের OpenBao service এবং management API/UI, operator authentication ও secret-free `PublicGateway` আলাদা **Trusted Platform** package-এ আছে। এই Farm package-এ reviewed shared adapter source আছে; gateway বা vault server duplicate করা হয়নি। আলাদা package ছাড়া gateway deployment instructions সম্পূর্ণ হবে না।

## আসল security boundary

1. Farm MVC backend trusted server-এ থাকবে। Identity, tenant/branch permissions, CSRF, SQL, accounting/stock rules এবং SignalR authorization সেখানেই চলে।
2. PublicGateway-এ Farm source/business execution, SQL password, OpenBao bootstrap credential, shared Data Protection key বা master API key দেবেন না। এটি fixed backend-এর authenticated HTTP/WebSocket transport।
3. Gateway cookie/Authorization relay করে। যে operator gateway বদলাতে পারে, সে user-এর active session ও যে data gateway দিয়ে যায় তা দেখতে/চুরি করতে পারে। Vault এই ঝুঁকি দূর করে না। Sensitive ব্যবহারকারীর জন্য trusted TLS/frontend operator প্রয়োজন।
4. Backend/OS/DB administrator code ও memory inspect করতে পারে। নিজের হাতে দেওয়া executable-এ password লুকিয়ে তাকে সেই password থেকে নিরাপদ রাখার দাবি নেই।
5. Production bootstrap script-এর আগে ASPNETCORE_ENVIRONMENT এবং DOTNET_ENVIRONMENT দুটিই Production সেট করুন। Production network rule, gateway-to-backend TLS, service identity, certificate/private-file permissions, OpenBao role/policy এবং actual secret provisioning operator-এর আলাদা কাজ। এই source package এগুলো স্বয়ংক্রিয়ভাবে তৈরি, grant, deploy বা বদলায় না।

## Exact bindings

| Typed secret | Farm backend configuration |
|---|---|
| SqlConnectionString | ConnectionStrings:DefaultConnection |
| RedisConnectionString | Scaling:Cache:Connection |
| RedisBackplaneConnectionString | Scaling:SignalR:Connection |
| DataProtectionCertificatePassword | Security:DataProtection:CertificatePassword |

Backend কেবল `secret/data/apps/farm/runtime` পড়ে। Request/query/user input দিয়ে vault path বদলানো যায় না। JSON/environment-এর পুরোনো secret fallback, typed vault field absent থাকলে, clear হয়। Farm-এ SMTP/OpenAI API key integration নেই; এই revision সেটি আছে বলে দাবি করে না। Local/explicitly configured Ollama আগের read-only সীমাতেই আছে।

OpenBao non-secret options: `TrustedVault:Mode=OpenBao`, HTTPS `Address`, mount/auth method, absolute `RoleIdFile` + `SecretIdFile` অথবা mTLS certificate/private-key paths। Bootstrap files publish/web root-এর বাইরে trusted server-এ রাখুন। Token/role secret/password chat, source, appsettings, command line বা evidence log-এ দেবেন না। Platform guide-এর approved operator provisioning ব্যবহার করুন। SQL encryption mandatory এবং certificate validation bypass নিষিদ্ধ। Runtime secret version-এ UTC expiry লাগে; maximum 24 hours।

`LegacyLocal` হলো স্পষ্ট unprotected migration option; Production-এ সেটি নিতে `RequireInProduction=false`-ও দিতে হয়। সাধারণ production setup-এর shortcut হিসেবে ব্যবহার করবেন না। `Synthetic` এবং `Disabled` শুধু Development-এর জন্য।

## Gateway root-host profile

প্রতি project-এর নিজস্ব public hostname এবং `/` root path ব্যবহার করুন; nested IIS virtual application/base-path rewriting এখানে verified নয়। Backend-এর non-secret configuration:

```json
{
  "AllowedHosts": "farm.example.invalid",
  "TrustedProxy": {
    "Enabled": true,
    "KnownProxyIp": "192.0.2.10",
    "PublicOrigin": "https://farm.example.invalid"
  }
}
```

এগুলো documentation placeholder, deployable host/IP নয়। Operator নিজের exact gateway IP ও public hostname বসাবেন। Known proxy network wildcard নেই; forwarding limit 1। Enabled profile-এ অন্য peer, missing/changed fixed Host/forwarded origin rejected। Gateway কেবল fixed origin header পাঠায়, inbound forged identity/forwarding headers relay করে না। Backend network access ও TLS operator verify করবেন; IP check অন্য process running on the same trusted host থেকে রক্ষা করে না।

Existing root routes, Account login/context, `/api`, `/ops`, `/data`, `/Messaging`, `/hubs/messaging` এবং static assets relay হয়। Business `/Admin` authorization backend-এ থাকে। Reserved vault/platform management paths public gateway-এ blocked। Route relay মানে সব MVC workflow নতুন REST API-তে rewrite হয়েছে এমন নয়। নতুন external API clients-এর পূর্ণ typed business API expansion আলাদা scope।

## Runtime revocation ও transaction safety

Vault expiry, unreadable runtime config বা version change হলে request/job gate fail-closed হয় এবং backend নিজের process stop করে। Approved supervisor restart করে নতুন config নিতে হবে। EF/Redis clients চালু থাকা অবস্থায় silently rotate হয়েছে বলে দাবি নেই।

Hosted Farm DbContext-এ interceptor প্রতিটি EF read/write/raw ExecuteUpdate command এবং transaction commit-এর ঠিক আগে gate check করে। Worker batch, outbox publish, hub Sync ও external AI call-এর আগেও explicit checks আছে। Commit-এর আগে gate unavailable হলে transaction commit rejected হয়; existing workflow disposal rollback করে। ইতিমধ্যে provider-এ চলমান call retroactively বাতিল করা যায় না; short transactions, cancellation, SQL/OpenBao-side revocation এবং operational drain দরকার।

Design-time migration generation এবং `--staging-preflight` read-only operator CLI web host bootstrap-এর আলাদা পথ। এগুলো vault deployment বা automatic database migration করে না। পুরোনো SQL/IIS scripts-এর direct environment credential handling trusted operator tooling; public gateway-এ চালাবেন না।

## Windows PC ও IIS

Windows PC-তে Development demo আগের documented isolated SQLite path-এ চলে। Vault-protected local HTTPS launcher আগে provision করা Windows certificate store-এর Subject/Store/Location ব্যবহার করে; Kestrel PFX path/password নেয় না। IIS-managed TLS-ও OS certificate store ব্যবহার করে। Kestrel PFX password এই v1 typed vault adapter-এর supported field নয়; কোনো অন্য bootstrap secret-কে vaulted বলা হয়নি। বাস্তব business data হলে trusted backend + SQL Server + reviewed vault configuration ব্যবহার করুন। IIS backend app pool-এর identity-র জন্য protected key storage এবং bootstrap file access operator সেট করবেন। Gateway আলাদা site/app/process ও publish directory; তার package-এ SQL বা backend credentials থাকা চলবে না। Shared server হলেও trust boundary weaker; administrators-এর capability সীমা উপরে বলা আছে।

Revision 5-এর Redis guide, sticky-session requirement এবং two-node protected Data Protection setup অপরিবর্তিত। IIS proxy/TLS/upgrade configuration, actual SQL Server, OpenBao service policy/rotation and restore, native browser/mobile language UAT ও load test customer staging-এ আলাদা acceptance gate।

## যাচাই

`docs/CURRENT_VERIFICATION.json` এবং `evidence/trusted/final/` current result index। Gate tests real SQLite transactions-এ revoked commit rollback, raw write/read denial, missing Production vault rejection, typed optional-secret clearing, independent Redis mappings ও spoofed proxy rejection দেখে। Separate gateway HTTP script real Farm Identity/CSRF/scoped REST/SignalR WebSocket flow চালায় disposable synthetic fixture-এ। এটি actual SQL Server/OpenBao customer deployment evidence নয়। Secret creation/provisioning এবং real financial action কোনো test-এর অংশ নয়।
