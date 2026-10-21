# Sign up, sign in এবং ১০-row demo

লগআউট অবস্থায় শুধু Sign up / Sign in থাকবে। লগইন সফল হলে Sign out এবং ERP menu দেখা যাবে। Sign up অ্যাকাউন্টকে administrator-এর মাধ্যমে company/branch access দিতে হবে।

## SQL files

- `sql/ALL_MODEL_TABLES.sql`: বর্তমান EF Core model-এর ৯৫টি টেবিল, primary key, foreign key, index ও constraint সহ SQL Server DDL। নতুন empty database-এর জন্য।
- `sql/ALL_TABLES_TEN_ROW_DEMO.sql`: একই schema-র জন্য সম্পর্কযুক্ত synthetic demo INSERT data। প্রতিটি table-এ অন্তত ১০টি row; voucher/ledger/detail table-এ বেশি row প্রয়োজন হয়।
- `sql/TEN_ROW_COUNTS.json`: SQLite fixture-এর যাচাইকৃত table counts।

প্রথমে schema, পরে demo INSERT script চালান। SQL Server demo database-এর নামের মধ্যে `demo` থাকতে হবে। INSERT script empty database ছাড়া চলবে না। Identity password hashes আছে, তাই SQL data file private রাখুন।

## Demo login

`farmdemo1@example.invalid` থেকে `farmdemo10@example.invalid` এবং `farmreviewer1@example.invalid` থেকে `farmreviewer10@example.invalid`। প্রথম account-এর নিজের demo company-তে সব permission আছে। Reviewers-ও কেবল নিজেদের fictional company-তে প্রবেশ করতে পারে। নতুন signup-এ এসব permission দেওয়া হয় না।

এই workspace-এর প্রস্তুত demo password: `FarmDemo!2026Ten`। এটি কেবল local demonstration-এর জন্য। নিজের demo fixture তৈরি করলে নিজের password দিন।

## Recreate independently

```powershell
./scripts/SEED_DEMO.ps1 -IncludeEnterprise -TenTenants -ConfirmFictionalPolicies
```

ডিফল্টে `demo-data/farm-ten-demo.db` তৈরি হয়। একই fixture পুনরায় চালালে password বা existing data বদলাবে না। ৫-tenant fixture থেকে ১০-tenant fixture-এ in-place upgrade হয় না; নতুন empty demo path ব্যবহার করুন।

সব টেবিলের schema regenerate করতে:

```powershell
dotnet run --no-launch-profile --project EnterpriseERP.Web -- --export-schema "$PWD/sql/NEW_SCHEMA.sql"
```

প্রস্তুত pristine SQLite fixture থেকে SQL Server data export করতে `ASPNETCORE_ENVIRONMENT=Development`, `Database__Provider=Sqlite`, `ConnectionStrings__DefaultConnection`-এ fixture-এর absolute path এবং `FARM_ALLOW_DEMO_EXPORT=YES` দিয়ে `--export-demo-sql` ও নতুন absolute `.sql` output path ব্যবহার করুন। Login/পরিবর্তনের আগে export করুন; fingerprint বদলালে export প্রত্যাখ্যান করবে।

Automation rules inactive, jobs cancelled, approvals rejected। External-login/token rows inert synthetic examples; এগুলো কোনো configured provider বা বাস্তব credential নয়। ১৯৯৯ সালের demo period close বর্তমান posting বন্ধ করে না। Tax/payroll policies fictional।
