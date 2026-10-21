# Bangladesh first-live: Revision 4 handoff

প্রথম live scope বাংলাদেশের একটি company। HR/Payroll, CRM, Fixed Assets এবং E-commerce চারটি module-ই রাখা হয়েছে। Windows PC এবং IIS + SQL Server দুটো deployment path-এর নির্দেশনা package-এ আছে।

## কী ঠিক হয়েছে

- নতুন staff/customer account-এর mailbox ownership বাইরে যাচাই করার পর অনুমোদিত administrator নিজের password দিয়ে explicit review record করতে পারবেন। কে/কখন/কোন পদ্ধতিতে review করেছেন তার bounded audit থাকে; application email পাঠানোর দাবি করে না। তারপর employee/customer link আলাদাভাবে confirm করতে হবে।
- Payroll policy form-এ basis, proration, precision, deduction/contribution এবং cap স্পষ্টভাবে বেছে দিতে হয়। Omitted field থেকে নীরবে কোনো policy অনুমান করা হয় না। Company-local calendar month পুরো শেষ হওয়ার আগে payroll calculation/approval হয় না।
- Farm validation, operational posting date, accounting close এবং report cutoff reviewed company timezone ব্যবহার করে। পুরোনো entry ও UTC audit timestamp বদলানো হয় না।
- Read-only SQL/IIS preflight evidence helper যোগ হয়েছে। Unknown migration history, missing SQL identity-column metadata বা malformed preflight command-এ ভুল ready result পাওয়া যায় না। এই helper database/keys/permissions/IIS settings পরিবর্তন করে না।

## যাচাই

- Clean Release build: ০ warning, ০ error
- .NET: 805/805
- Real localhost HTTP: 902/902; এর মধ্যে genuine new-account onboarding 47 checks
- JavaScript/source-DOM: 44/44; actual browser testing হিসেবে গণ্য নয়
- Negative preflight CLI: 3/3; malformed/missing configuration-এ host বা key file তৈরি হয়নি
- EF model drift নেই; generated idempotent SQL আগের committed schema-এর সঙ্গে byte-for-byte মেলে; publish সফল
- Independent cold-copy build/test এবং অতিরিক্ত audit checks-এর আলাদা evidence `CURRENT_VERIFICATION.json`-এ আছে

এই ফল local Linux ও disposable SQLite fixture-এর। SQL Server বা Windows/IIS-এ চালানো হয়েছে এমন দাবি নয়। Real tax/payroll approval, payment, email dispatch বা customer production sign-off দেওয়া হয়নি।

## এখন operator-এর প্রয়োজন

1. নির্দিষ্ট অনুমোদিত Windows/IIS + SQL Server staging target-এ exact release ব্যবহার করে runtime identity, schema এবং deployment settings যাচাই করুন। Password/connection secret chat বা command history-তে দেবেন না। শুরু করুন `STAGING_READONLY_PREFLIGHT.md` দিয়ে।
2. বাস্তব Bangladesh company policy, effective dates, chart of accounts ও opening balances প্রস্তুত করে আলাদা authorized reviewer দিয়ে approve করুন। Demo percentage/approval fictional; বাস্তব policy হিসেবে ব্যবহার করবেন না।
3. আলাদা অনুমোদিত staging copy-তে migration/rollback, backup restore, concurrent posting, permission isolation, stock/ledger reconciliation এবং চার module-এর user acceptance চালান। তারপর launch decision নিন।

## বর্তমান operational সীমা

Payroll accrual আছে, automatic salary transfer বা statutory filing নেই। CRM quote internal net proposal। Assets straight-line/None book depreciation সমর্থন করে। Ecommerce authenticated linked customer ও branch collection ব্যবহার করে; payment pending/unverified, কোনো card gateway/carrier নয়। Legacy purchase/sales single-line operational document। Multi-line statutory invoice বা অন্য workflow প্রয়োজন হলে launch-এর আগে scope অনুযায়ী কাজ যোগ করতে হবে।

Future Arab/USA expansion-এর exact country/state এখন Bangladesh-only launch-এর blocker নয়। Bangladesh-এর বাস্তব approved inputs এবং actual staging/customer acceptance এখনও প্রয়োজন।
