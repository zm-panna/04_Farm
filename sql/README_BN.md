# SQL schema + synthetic demo data

এই folder-এর SQL **শুধু আলাদা demo database-এর জন্য**। কোনো SQL Server-এ এই scripts এখানে execute করা হয়নি। Source-generated SQL এবং actual SQL Server import/rollback/concurrency acceptance আলাদা বিষয়।

1. DBA-র সঙ্গে `01-SCHEMA_REVIEW.sql` review করুন। এটি committed EF Core migrations থেকে তৈরি idempotent SQL। সঠিক নতুন, empty SQL Server demo database-এ schema apply করতে হবে। Existing/customer database-এ এই sample ব্যবহার করবেন না।
2. একই database-এ `02-DEMO_DATA.sql` review করে apply করুন। Script database name-এ `demo` থাকা এবং `prod` না থাকা পরীক্ষা করে; সব application/Identity table empty না হলে বন্ধ হয়। FK/check constraints বন্ধ করা হয় না। Inserts এক transaction-এ হয়; error হলে rollback হয়।
3. Initial row counts ও generated file hash: `../evidence/scanning/final/demo-export.json`। পাঁচ company, দশ branch, দশ synthetic Identity user (পাঁচ maker ও পাঁচ reviewer) এবং coherent linked business records আছে। Security authorization, tokens এবং other system tables-এ fabricated পাঁচ rows বসানো হয়নি; exceptions `../docs/DEMO_SEED_MANIFEST.md`-এ আছে।

## Login password

এই pre-generated sample-এর random initialization password discard করা হয়েছে। **কোনো জানা/default login password দেওয়া হয়নি**। SQL-এ বাস্তব synthetic Identity password hashes আছে, plaintext password নেই। এগুলো sensitive; file private রাখুন, public repository/site-এ publish করবেন না। নিজের real account-এর password demo-তে ব্যবহার করবেন না।

নিজের জানা secret দিয়ে usable demo logins চাইলে, extracted source root থেকে নতুন private local folder-এ regenerate করুন:

```powershell
.\scripts\EXPORT_DEMO_SQL.ps1 -OutputDirectory 'C:\PrivateFarmDemoExport' -IncludeEnterprise -ConfirmFictionalPolicies
```

Folder আগে থেকে থাকলে script overwrite করে না। Hidden prompt-এ নতুন demo password দিন। Script নতুন isolated SQLite fixture তৈরি করে schema ও data SQL export করে; SQL Server-এ connect/apply করে না। Generated folder-এ পাঁচ maker login `farmdemo1@example.invalid` থেকে `farmdemo5@example.invalid` এবং পাঁচ reviewer `farmreviewer1@example.invalid` থেকে `farmreviewer5@example.invalid`; password আপনার prompt-এ দেওয়া secret। Folder-এ source SQLite-ও থাকে, তাই Windows permissions দিয়ে authorized operator ছাড়া কারও access না থাকার বিষয়টি নিজে যাচাই করুন।

Exporter used/modified fixture, production environment, missing opt-in, public web-root/reparse-point destination, existing output এবং unexpected tenant/MFA/token/platform data প্রত্যাখ্যান করে। Export seed-এর পরপরই করতে হয়; login/context selection বা business edits-এর পরে export করার চেষ্টা করবেন না। Fingerprint accidental changes শনাক্ত করে; database owner-এর tampering-এর বিরুদ্ধে authenticity proof নয়।

Live installation-এর জন্য demo users/data import নয়: reviewed migrations, secure bootstrap, actual SQL/IIS tests ও customer UAT অনুসরণ করুন।

Enterprise sample: 95 tables / 1,345 rows / 100 balanced vouchers। HR, CRM, assets ও ecommerce-এর linked examples আছে। Tax/payroll percentages এবং approvals সব fictional; real company-তে copy করবেন না। Additional reviewers farmreviewer1@example.invalid থেকে farmreviewer5@example.invalid; actual policy approval নয়।

Demo SQL export web host চালু বা login করার আগে করুন; host-এর outbox worker demo delivery-state পরিবর্তন করতে পারে। এই fixture বাস্তব message বা provider send করে না।

Revision 7-এর `03-SCANNING_ADDITIVE_REVIEW.sql` R6-এর শেষ migration থেকে ScanLabels/ScanIntents যোগ করার আলাদা review script। এটি কোনো deployment approval নয়; DBA backup/restore ও staging validation ছাড়া customer database-এ চালাবেন না। Seed SQL customer database-এ ব্যবহারযোগ্য নয়। Scanner sample-এ 25 fictional label ও 5 cancelled fictional draft আছে।
