# QR ও barcode ব্যবহার: Farm Revision 7

এই সংযোজন বিদ্যমান Farm Revision 6-এর HR/Payroll, CRM, Fixed Asset, E-commerce, Redis cache, server-side tables, internal messaging ও trusted backend/vault ব্যবস্থা অক্ষুণ্ণ রেখে তৈরি হয়েছে। বর্তমান পরীক্ষার সংখ্যা ও সঠিক evidence: `CURRENT_VERIFICATION.json`। প্রথম live rollout-এর সম্মত scope: বাংলাদেশ, একটি company। প্রকৃত payroll/tax policy, Windows PC/IIS + SQL Server deployment এবং ব্যবহারকারীর অনুমোদন এখনও আলাদা go-live ধাপ।

## ব্যবহার

1. Administrator প্রয়োজনমতো `Scanning.Use` এবং label তৈরি/বাতিলের জন্য `Scanning.ManageLabels` দেবেন। একই সঙ্গে সংশ্লিষ্ট Inventory, Livestock, Purchase বা FixedAssets-এর বিদ্যমান permission লাগবে। শুধু scanner permission কোনো business access দেয় না।
2. নির্বাচিত company/branch-এ “Scan workspace” খুলুন। Product, Animal, Asset, PurchaseOrder বা Document context বেছে নিন। Label manager-এ অনুমোদিত record খুঁজে label তৈরি করুন। Product-এর exact SKU, Code128, EAN13/EAN8, UPC-A/UPC-E mapping দেওয়া যায়। অন্য record-এর label server তৈরি করা opaque ID।
3. Keyboard scanner দিয়ে value লিখুন অথবা manual input ব্যবহার করুন। Camera ব্যবহার করতে নিজের ইচ্ছায় Start চাপুন এবং browser permission দিন। Production camera-র জন্য HTTPS প্রয়োজন। Camera frame বা audio server-এ পাঠানো হয় না; ছবি সংরক্ষণ করা হয় না। Stop/Cancel, tab hidden বা page ছাড়লে camera বন্ধ হয়। Browser permission এখনও pending থাকলেও Cancel কার্যকর।
4. Scan প্রথমে বর্তমান অনুমোদিত record দেখায়। Scan করলেই stock, journal, weight, treatment বা custody বদলায় না। Open record দিয়ে বিদ্যমান screen খোলা যায়।
5. কোনো পরিবর্তন চাইলে action ও মান লিখে draft তৈরি করুন। Source/destination, quantity, business date, অর্থ/স্টকের প্রভাব, custody এবং operator-এর notes দেখে explicit confirmation checkbox দিন; তারপর Confirm করুন। Draft 10 মিনিট বৈধ। Form বদলালে নতুন review দরকার।
6. Confirm অনিশ্চিত হলে একই draft আবার খুলুন বা retry করুন। একই draft-এর replay নতুন posting করে না। History-তে নিজের reference/status অনুসন্ধান ও page করা যায়। Cancelled/expired draft post হয় না। Stock, reservation, policy, business date, label বা access বদলালে পুরোনো draft পুনরায় review করতে হবে।

## কোন কাজ বাস্তবভাবে যুক্ত আছে

| Context | Scan / preview | Explicit confirmed action | সীমা |
|---|---|---|---|
| Product / feed SKU | Branch-scoped exact mapping, name, unit, available quantity | Stock quantity adjustment; source/destination branch transfer | Adjustment signed delta, পূর্ণ physical count reconciliation নয়। Branch stock model; rack/bin/serial module নেই |
| Animal tag | Opaque label থেকে authorized animal | Weight record; operator-entered health observation; selected feed item consumption | কোনো diagnosis, medicine choice বা automatic clinical decision নেই |
| Purchase order | Approved/part-received order, remaining quantity | Existing receiving service দিয়ে partial/final receipt | বিদ্যমান single-line PO model; scanner supplier price বদলায় না |
| Fixed asset | Approved/current asset record | Existing custody service দিয়ে custodian/location পরিবর্তন | Acquisition, depreciation বা disposal scanner দিয়ে automatic হয় না |
| Accounting document | Accounts.View সাপেক্ষে reference, kind, recorded amount/currency | Lookup only | Payment/refund/settlement বা নতুন invoice post হয় না |
| Printable labels | Authorized record-এর opaque ID বা configured product barcode | QR এবং Code128 print page | Arbitrary URL fetch নেই; animal/asset label-এ PII, clinical data, cost বা secret নেই |

Sales checkout, ecommerce dispatch/returns, milk collection scanning, batch/expiry/serial/rack/bin, label-printer drivers, offline financial posting এবং bulk count reconciliation এই সংযোজনে যুক্ত হয়নি। বিদ্যমান সাধারণ workflows চালু আছে। এই সীমাগুলো নতুন module হিসাবে “complete” দাবি করা হয়নি।

## নিরাপত্তা ও হিসাব

- প্রতিটি resolve/draft/detail/print/confirm-এ authentication, company, branch ও permission যাচাই হয়। Draft শুধু তার creator-এর। Customer/employee portal পরিচয় scanner business permission নয়।
- Transfer-এ দুই branch-এর membership এবং দুই label commit-এ আবার যাচাই হয়। Feed-এ animal ও feed label দুটিই যাচাই হয়।
- Label বদলানো যায় না। পুরোনো label কারণ লিখে revoke করে নতুন label দিন। Revoked value পুনরায় অন্য record-এ বরাদ্দ হয় না। Draft-এর পরে revoke করলেও confirm বন্ধ হয়।
- SKU-এর leading zero অপরিবর্তিত থাকে। কেবল checksum-valid configured retail barcode-এ UPC-A, leading-zero EAN13 ও UPC-E সমতুল্যতা প্রযোজ্য। একাধিক record-এর ambiguity হলে lookup ব্যর্থ হয়।
- Draft-এর source snapshot, reviewed data ও request key অপরিবর্তনীয়। Existing service-এর serializable transaction-এর ভেতরে final recheck, draft outcome, business write ও audit একসঙ্গে commit হয়। ব্যর্থ হলে rollback হয়।
- Financial scan action-এর আগে approved company business policy প্রয়োজন। Carrying value-এর existing 3-decimal precision রাখা হয়; actual receipt amount approved currency precision অনুসরণ করে। Policy/rate এই feature invent করে না।
- All state-changing HTTP calls require anti-forgery. Scanner endpoints have per-user bounds; draft creation also has a bounded per-owner rate. UI renders record text as text, not HTML.
- Scanner route-এ কেবল same-origin camera এবং narrow `wasm-unsafe-eval` CSP capability। অন্য routes-এর camera denial অপরিবর্তিত। Microphone disabled। Decoder self-hosted, version-pinned; internet/CDN camera processing নেই।

## Migration ও deployment

নতুন `ReviewedScanningWorkflows` migration শুধু ScanLabels ও ScanIntents table, scoped foreign keys, immutable-state constraints এবং bounded lookup/history indexes যোগ করে। `sql/03-SCANNING_ADDITIVE_REVIEW.sql` কেবল R6-এর শেষ migration থেকে নতুন migration-এর review script; `sql/01-SCHEMA_REVIEW.sql` পূর্ণ idempotent script। এগুলো generate/review করা হয়েছে, live SQL Server-এ চালানো হয়নি।

Operator backup, restore test, staging review এবং approval ছাড়া production migration চালাবেন না। এই package নিজে SQL migration, demo seed, IIS restart, ACL change বা credential grant করে না। আগের trusted backend/vault এবং Windows PC/IIS নির্দেশিকা বহাল: `TRUSTED_BACKEND_AND_VAULT_BN.md`, `ENTERPRISE_POLICY_AND_WINDOWS_GUIDE_BN.md`। Customer frontend-এ database/vault secret যাবে না।

Demo seed-এ 25টি fictional label এবং 5টি cancelled fictional draft আছে; এগুলো genuine user approval নয়। Production নতুন label বা scanner grant স্বয়ংক্রিয়ভাবে পায় না। Demo password package-এ রাখা হয়নি।

## পরীক্ষা ও অবশিষ্ট যাচাই

Automated tests cover all seven actual confirmed workflows, owner/company/branch isolation, stale/revoked labels, duplicate/replay, cancellations, rollback, reservations, concurrent contexts, server pagination, CSRF/flood rejection, generated label decoding and real localhost HTTP. Camera helper tests decode synthetic QR, Code128, EAN13/EAN8, UPC-A/UPC-E frames with the actual pinned WASM library. The actual served UI is exercised with jsdom and authenticated localhost APIs, including four languages and Arabic RTL; this is not physical-camera or real-browser acceptance.

Live SQL Server concurrency/migration, actual Windows/IIS/PowerShell, HTTPS device permission behavior, physical camera/handheld scanner/label printer, browser rendering, native-speaker language acceptance, operational load targets এবং customer UAT এখনও unverified। No production approval is implied.
