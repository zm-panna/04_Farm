Farmerp: সব টেবিলের ২০-row sample INSERT script

Source schema: Farmerp221026at1204.sql
Target database: Farmerp_Demo (SQL Server)
Tables: 95

ব্যবহার:
1. SSMS-এ আপনার দেওয়া schema তৈরি করা খালি Farmerp_Demo database খুলুন।
2. Farmerp_ALL_TABLES_20_ROWS.sql খুলে Execute দিন।
3. শেষে প্রতিটি table-এর row count দেখা যাবে।

এই script কেবল INSERT করে; database/table DROP, DELETE বা TRUNCATE করে না। কোনো application/Identity table-এ আগে data থাকলে script বন্ধ হবে। সব INSERT একটি transaction-এ থাকে; error হলে rollback হবে।

প্রতিটি টেবিলে কমপক্ষে ২০টি sample row। Voucher, voucher lines, stock movements ও সম্পর্কযুক্ত detail table-এ হিসাব ও stock balance মেলানোর জন্য বেশি row আছে। Farmerp_20_ROWS_COUNTS.csv-তে প্রতিটি table-এর নির্দিষ্ট সংখ্যা আছে।

আপনার schema-র AspNetUsers.IdentityUserId NOT NULL কলামে user-এর Id-র একই মান দেওয়া হয়েছে। RowVersion/timestamp SQL Server নিজে তৈরি করবে। Foreign key dependency অনুযায়ী parent table আগে এবং child table পরে INSERT করা হয়।

সব ডেটা fictional demo। Tax/payroll policy-গুলো বাস্তব নীতিমালা নয়। Automation rules inactive, sample jobs cancelled ও sample approvals rejected; কোনো বাস্তব automation চালানো হয় না। External login/token নমুনাগুলো inert ও কোনো configured provider-এর credential নয়।

Demo login: farmdemo1@example.invalid থেকে farmdemo20@example.invalid
Fictional reviewer: farmreviewer1@example.invalid থেকে farmreviewer20@example.invalid
Demo password: FarmDemo!2026Twenty

SQL file-এ Identity password hash আছে; private রাখুন। Script আপনার SQL Server-এ execute করা হয়নি। Export করার আগে fixture database-এর integrity, foreign keys, required columns, string lengths এবং প্রতি table-এর INSERT count যাচাই করা হয়েছে।
