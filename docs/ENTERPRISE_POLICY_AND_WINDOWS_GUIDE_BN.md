# Enterprise configuration and Windows installation

**Revision 6 secure hosting:** আগে [trusted backend/vault guide](TRUSTED_BACKEND_AND_VAULT_BN.md) পড়ুন। Production/Staging এখন OpenBao configuration ছাড়া fail-closed; পুরোনো direct-connection instructions কেবল reviewed operator migration/preflight context। PublicGateway ও management UI আলাদা Trusted Platform package-এ।

এই source revision-এর Windows PC এবং Windows/IIS deployment দুটো পথ আছে। এখানে Windows, IIS বা আসল SQL Server-এ execution করা হয়নি। স্থানীয় Linux/SQLite পরীক্ষা staging acceptance-এর বিকল্প নয়। কোনো production approval বা statutory compliance certificate দেওয়া হচ্ছে না।

## কোম্পানি, মুদ্রা ও সময়

1. `/business-policy` থেকে প্রতিটি company-র exact country, state/region, base currency, currency decimal places এবং IANA/Windows timezone দিন। US-এর state আবশ্যক। Arab country বলতে কোন দেশ তা আলাদা করে লিখতে হবে।
2. বিদ্যমান ledger, stock value, documents ও subscription amounts-এর currency accountant দিয়ে review করুন। প্রথম policy সেগুলোকে convert করে না; একই currency ছিল কি না তা explicit confirmation চাইবে।
3. প্রস্তাবকারী ছাড়া অন্য authorized reviewer approve করবেন। Company base currency এবং precision এরপর locked। পুরনো amounts-কে অন্য currency-তে relabel করার route নেই। আলাদা currency-র জন্য আলাদা company বা separately approved migration দরকার। FX conversion/combined multi-currency consolidation নেই।
4. নতুন effective-dated policy পুরনো approved version বদলায় না। New enterprise documents তাদের policy/currency snapshot রাখে। Audit timestamps UTC থাকে; business-date calculations company timezone ব্যবহার করে।
5. Tax policy-তে explicit reviewed exclusive percentage, tax liability account, effective dates এবং source/basis দিন। Zero মানে implicit exemption নয়: `Exempt` অথবা `OutOfScope` এবং কারণ দরকার। Independent approval ছাড়া storefront tax policy ব্যবহার হয় না। এই engine tax jurisdiction নির্ধারণ, আইন যাচাই, tax filing, compound tax বা tax-inclusive calculations করে না।
6. Payroll-এর নিজস্ব reviewed policy ও original snapshot বজায় থাকে। Rate, deduction, employer contribution ও proration rules বাস্তব jurisdiction অনুযায়ী approve করতে হবে। Payroll posting liability/accrual record করে; salary transfer করে না।
7. Fixed asset workflow book carrying cost এবং reviewed depreciation-এর জন্য। Recoverable VAT, statutory capital allowance, impairment ও revaluation স্বয়ংক্রিয়ভাবে নির্ণয় হয় না।

## Windows PC: loopback HTTPS + SQL Server

- Microsoft-এর official .NET 10 runtime/Hosting Bundle এবং অনুমোদিত SQL Server instance operator provision করবেন। Software install, credentials, certificates বা network permissions এই source package নিজে গ্রহণ/পরিবর্তন করে না।
- Verified publish output, reviewed migrations এবং encrypted SQL connection ব্যবহার করুন। Connection string বা password chat, source, command history অথবা shared config file-এ রাখবেন না।
- `Security__DataProtection__KeyDirectory` হবে private persistent directory, publish/wwwroot-এর বাইরে। Windows user identity স্থির রাখুন এবং DPAPI protection ব্যবহার করুন।
- `Kestrel__Certificates__Default__Path` এবং certificate password secure local configuration-এ provision করুন। Certificate hostname/trust সঠিক না হলে warning bypass করবেন না।
- `Security__DataProtection__ApplicationName` স্থির রাখুন। `Reporting__QuestPdfLicense` approved license eligibility অনুযায়ী configure করুন।
- `scripts/RUN_WINDOWS_LOCAL.ps1 -PublishDirectory <verified-publish>` loopback `https://localhost:7443` চালানোর operator helper। LAN exposure বা public endpoint খোলে না। এই PowerShell helper এখানে executed হয়নি।
- বাস্তব SQL migration, fresh login, restart/key persistence, backup/restore ও UAT না হওয়া পর্যন্ত live নয়।

## Windows IIS server

বিদ্যমান `LIVE_DEPLOYMENT_RUNBOOK_BN.md` এবং `WINDOWS_PRODUCTION_EVIDENCE.ps1` অনুসরণ করুন। আলাদা app-pool identity, `LoadUserProfile=true`, trusted hostname certificate, encrypted SQL, restricted key directory, correct folder ACLs, backup/restore ও recycling acceptance লাগবে। Production database-এ demo seed নয়। Windows PC-তে pass মানে IIS pass নয়; দুই target-এর evidence আলাদা রাখুন।

## Fictional enterprise demo

- `--init-demo` আগের core-only pristine fixture রাখে। `--init-enterprise-demo` আলাদা empty isolated Development SQLite database-এ পাঁচটি fictional company-র enterprise examples যোগ করে। একই database-এ modes বদলে upgrade নয়।
- `SEED_ENTERPRISE_DEMO.ps1` helper explicit `-ConfirmFictionalPolicies` চায়। Company/branch tables-এর পাশাপাশি HR/Payroll, CRM, Fixed Assets, E-commerce-এর meaningful applicable tables-এ কমপক্ষে পাঁচটি linked sample row থাকে।
- Policy review, consent, invoice amounts, 5% example tax এবং payroll examples সম্পূর্ণ fictional। এগুলো দেশের আইন বা genuine accountant/customer approval-এর প্রমাণ নয়। নতুন production company-তে কোনো policy/rate seed হয় না।
- `farmdemo1@example.invalid` থেকে `farmdemo5@example.invalid` makers এবং `farmreviewer1@example.invalid` থেকে `farmreviewer5@example.invalid` synthetic reviewers। কোনো known/default password shipped হয় না। Operator-generated initialization secret discard করতে হবে অথবা demonstration-এ ব্যবহার করলে private রাখতে হবে।
- Exported SQL-এ actual synthetic Identity password hashes থাকে: private রাখুন। কোনো password plaintext, MFA secret, recovery code বা real provider token নেই। Financial/payment status সবসময় truthful; no card charge, no real fund transfer, no outbound send।

## Remaining acceptance

দেশ/state-specific real policies, accountant/native-speaker/customer UAT, browser/mobile rendering, SQL Server provider concurrency/migrations, Windows/IIS security/configuration, backup restore, real payment/AI provider এবং production load evidence এখনও আলাদা gates।
