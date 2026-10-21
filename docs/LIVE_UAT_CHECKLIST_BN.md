# Smart Farm ERP — বাস্তব staging acceptance

এই তালিকা পরীক্ষার পরিকল্পনা; কোনো ঘরে PASS আগে থেকে দেওয়া নেই। Source test, SQLite test, SQL Server test এবং IIS test আলাদা evidence। ব্যবসার মালিক/দায়িত্বপ্রাপ্ত reviewer গ্রহণ না করা পর্যন্ত customer production release অনুমোদিত নয়।

## Evidence record
প্রতিটি পরীক্ষায় সংরক্ষণ করুন: release ZIP SHA256, environment (secrets ছাড়া), UTC সময়, test ID, synthetic input/reference, expected result, actual result, PASS/FAIL, sanitized log/screenshot এবং reviewer। কোনো password, token, connection string বা আসল ব্যক্তিগত তথ্য evidence-এ রাখবেন না।

## ১. Installation ও পুনরুদ্ধার
- SETUP-01: নতুন আলাদা staging SQL Server database-এ reviewed committed migrations চালিয়ে login ও readiness পরীক্ষা। Pending migration থাকলে healthy দেখানো চলবে না।
- SETUP-02: আগের schema থাকলে backup নিয়ে clone-এ upgrade; original row count, financial totals ও foreign keys মিলিয়ে দেখা। নতুন initial migration existing database-এ অন্ধভাবে চালানো যাবে না।
- SETUP-03: একই setup আবার চালালে duplicate migration/demo/security records যেন না হয়।
- SETUP-04: encrypted/offline database backup আলাদা test database-এ restore করে login, ledger totals ও representative documents যাচাই। শুধু backup command success যথেষ্ট নয়।
- SETUP-05: app recycle/restart ও SQL connection সাময়িক বন্ধ করে failure handling যাচাই; recovery-তে duplicate financial posting নয়।

## ২. Identity ও company/branch isolation
- AUTH-01: নতুন admin bootstrap, ভুল password lockout, logout, password পরিবর্তন ও প্রয়োজনমতো প্রথম login পরিবর্তন। Demo password production-এ রাখা যাবে না।
- AUTH-02: দুই company এবং একই company-র দুই branch তৈরি। নিষিদ্ধ ID URL/body/header-এ দিয়ে list/detail/edit/export/report/AI access প্রত্যাখ্যান যাচাই। শুধু UI menu লুকানো যথেষ্ট নয়।
- AUTH-03: সীমিত operator-এর সরাসরি privileged endpoint access; CSRF token ছাড়া mutation; revoked access দিয়ে পুরনো session ব্যবহার। প্রত্যেকটি deny হতে হবে।
- AUTH-04: role/permission পরিবর্তনের audit actor ও target যাচাই; reviewer নিজের approval দিতে পারবে কি না নীতির সঙ্গে মিলিয়ে দেখা।

## ৩. Stock ও financial integrity
- FIN-01: opening stock → purchase receipt → sale → receipt/payment → report। Quantity, weighted cost, inventory value, payable/receivable এবং debit-credit মিলিয়ে দেখা।
- FIN-02: একই reference/form দুবার submit; network timeout-এর পরে retry; দুই operator একসঙ্গে শেষ stock বিক্রি। একবারই posting, stock negative নয়।
- FIN-03: decimal quantity/price, zero/negative/অতিরিক্ত বড় value ও rounding সীমা পরীক্ষা। Validation failure-এ partial document/stock/ledger থাকবে না।
- FIN-04: rollback করার মতো error ঘটিয়ে transaction atomicity; reversal/cancellation original history রাখবে এবং দ্বিতীয়বার reverse করবে না।
- FIN-05: আলাদা branch/company stock, party, bank/account ব্যবহার করে cross-tenant posting প্রতিরোধ। রিপোর্টের total এবং underlying ledger এক হতে হবে।
- FIN-06: stock adjustment, transfer, animal purchase/sale এবং farm expense-এর অনুমোদিত workflow থাকলে source ও destination/reversal দুদিক যাচাই। বাস্তবে অনুপস্থিত workflow PASS দেওয়া যাবে না।

## ৪. Farm workflows
- FARM-01: animal registration/tag uniqueness, status/history ও permitted lifecycle changes। বিক্রি/মৃত/নিষ্ক্রিয় animal-এ অনুপযুক্ত নতুন transaction বন্ধ।
- FARM-02: milk collection → available milk → sale; duplicate date/session, over-sale ও cancellation। Health withdrawal/restriction থাকলে milk-sale policy যাচাই।
- FARM-03: health/vaccination/feed entry; feed consumption-এর stock deduction; duplicate submission ও insufficient stock rollback।
- FARM-04: breeding/pregnancy/birth, weight/fattening, expenses ও corresponding dashboard/report বাস্তব business policy অনুযায়ী পরীক্ষা। চিকিৎসার সিদ্ধান্ত AI-কে অর্পণ নয়।

## ৫. AI Manager ও automation
- AI-01: AI বন্ধ/timeout/unreachable হলে safe fallback; fabricated stock, balance বা medical advice-কে verified fact হিসেবে দেখাবে না।
- AI-02: company/branch/permission অনুযায়ী data scope; prompt দিয়ে permission বদলানো, অন্য company পড়া বা secrets প্রকাশের চেষ্টা ব্যর্থ।
- AI-03: source data ও model-generated narrative আলাদা; model output দিয়ে স্বয়ংক্রিয় financial posting বা permissions পরিবর্তন নয়।
- AUTO-01: approved job restart/retry, duplicate claim, expired lease, failed-job review এবং disabled automation পরীক্ষা। যেসব external integration সংযুক্ত হয়নি সেগুলোর status NOT TESTED।
- AUTO-02: subscription day-boundary, unpaid/partially paid/paid status ও permitted support access; suspended company অন্য company-কে প্রভাবিত করবে না।

## ৬. IIS ও ব্যবহারকারীর গ্রহণযোগ্যতা
- WEB-01: Windows IIS-এ published exact release, matching Hosting Bundle, HTTPS certificate, restricted host, least-privilege app-pool এবং runtime DB user পরীক্ষা। Schema-changing credential runtime app-এ নয়।
- WEB-02: persisted protected Data Protection keys দিয়ে recycle-এর পরে intended session behavior; keys/backups public webroot-এর বাইরে।
- WEB-03: desktop/mobile-এ login থেকে মূল workflow; সব configured language, Arabic enabled হলে RTL, validation, pagination/search, export/PDF fonts ও print পরীক্ষা।
- WEB-04: unauthorized file URL, error-page তথ্য ফাঁস, security headers, upload size/type এবং logs-এ credential নেই যাচাই।
- UAT-01: customer-এর বাস্তব কাজ synthetic data দিয়ে সম্পন্ন; দায়িত্বপ্রাপ্ত ব্যক্তি লিখিতভাবে accepted scope, known exclusions এবং release version নিশ্চিত করবেন।

## ৭. Confirmed enterprise modules ও policy acceptance
- POL-01: প্রতিটি company-র exact country/state, currency minor units (0/2/3), timezone এবং effective-dated policy maker/checker দিয়ে configure করুন। নিজের approval প্রত্যাখ্যান, currency lock, expired policy, local-midnight date এবং পুরোনো posting অপরিবর্তিত থাকা যাচাই করুন। Existing ledger-এর currency অনুমান নয়; accountant-এর reviewed confirmation প্রয়োজন।
- POL-02: tax policy-তে approved rate অথবা কারণসহ Exempt/OutOfScope treatment পরীক্ষা। Country বেছে নিলেই আইনগত compliance ধরে নেওয়া যাবে না। Fractional historical amount silently round/relabel নয়।
- HR-01: staff → dated attendance/leave → Daily/Monthly/Mixed payroll draft → অন্য reviewer-এর approval → accrual posting → register/payslip। বাস্তব approved method অনুযায়ী মাসের denominator, proration, overtime, deduction এবং employer contribution মিলিয়ে দেখুন। Salary transfer করা হয় না।
- HR-02: HR-only user salary দেখতে পাবে না; staff অন্যের payslip পাবে না। আগের period-এর compensation version অক্ষত, approved run আর edit নয়, double-submit একবারই ledger posting।
- CRM-01: lead → opportunity/task → consent record → reviewed quote → acceptance → customer/sales-ready conversion। Repeated/concurrent conversion এক customer; consent না থাকলে granted নয়। Quote একটি internal net proposal; send, stock reservation বা tax invoice হয় না।
- ASSET-01: acquisition → অন্য reviewer-এর approval → capitalization → custody → straight-line/None depreciation → disposal। Residual floor, 0/2/3 decimal rounding, একই asset/period এক posting এবং disposed asset-এর পর depreciation প্রত্যাখ্যান যাচাই।
- SHOP-01: verified customer account → নিজের cart → server-priced confirmed checkout → reservation → staff fulfillment → linked return। Price expiry, insufficient available stock, cancel release এবং original tax allocation যাচাই। Branch collection এবং pending/unverified payment সীমা পরিষ্কার থাকতে হবে।
- SHOP-02: reservation থাকা stock-এর legacy sale/feed/adjustment/transfer অতিরিক্ত deduction ব্যর্থ; client-supplied paid state বা unsigned callback গ্রহণ নয়। Customer অন্য customer-এর order/internal cost/staff note পড়তে পারবে না।
- AI-04: enterprise facts/checklists-এ permission/branch scope এবং sensitive salary/customer data redaction যাচাই; draft থেকে consequential action আলাদা অনুমোদিত workflow-তে করতে হবে।
- WEB-05: Windows PC-তে loopback HTTPS এবং IIS server-এ approved host/TLS দিয়ে পৃথক test evidence নিন। PowerShell/ACL/certificate setup বাস্তবে পরীক্ষা ছাড়া PASS নয়।

## Go / No-Go
Unresolved authorization/data-loss/accounting defect, failed backup restore, unexecuted required workflow, অথবা SQL Server/IIS failure থাকলে NO-GO। সব relevant test-এর genuine evidence, migration review, customer acceptance এবং rollback দায়িত্ব নির্ধারণ হওয়ার পরই deployment decision। Source build সফল হওয়া একা live approval নয়।
