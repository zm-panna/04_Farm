# Revision 5: বড় ডেটার তালিকা, Redis ও অভ্যন্তরীণ মেসেজ

এই পরিবর্তন আগের Bangladesh first-live source-এর ওপর করা হয়েছে। HR/Payroll, CRM, Fixed Assets, E-commerce এবং আগের হিসাব/stock workflow রাখা হয়েছে। বাস্তব payroll/tax policy বা production অনুমোদন তৈরি করা হয়নি।

## কী ব্যবহার করতে পারবেন

- `/data` খুলে অনুমতিপ্রাপ্ত ৪৭টি register-এর তালিকা পাবেন। Search, filter, sort ও page বদলালে server শুধু প্রয়োজনীয় page পাঠায়। Company/branch/নিজের record-এর সীমা server যাচাই করে। সংশ্লিষ্ট আগের workflow page-এ **Interactive server tables** খুলেও ব্যবহার করা যায়।
- Inventory ও party directory-এর পুরোনো action-সহ তালিকাতেও search/paging আছে। Inventory-র balance, suggestion ও action selector একই নির্বাচিত page-এর জন্য। বড় product register খুঁজতে `/data/products` ব্যবহার করুন।
- Redis cache ঐচ্ছিক। বর্তমানে products, animals ও salary-বিহীন staff summary-তে bounded cache আছে। Permission, financial posting, payroll amount, message content বা login session cache করা হয় না। Redis বন্ধ থাকলে SQL থেকে পড়া হয়। সফল পরিবর্তনের সঙ্গে SQL generation বদলায়, তাই পুরোনো cache আর ব্যবহার হয় না।
- `/Messaging`-এ internal inbox, sent list, recipient search, explicit confirmation-সহ message পাঠানো এবং read acknowledgement আছে। `Messaging.View` ও `Messaging.Send` আলাদা অনুমতি। Offline থাকলেও database-এ message থাকে; reconnect করলে আবার authorized fetch হয়।
- SignalR-এর live event শুধু নতুন inbox তথ্য আছে এমন ছোট signal দেয়। Message content REST API থেকে নতুন permission যাচাই করে আসে। Message database-এ থাকা, live signal পৌঁছানো এবং ব্যবহারকারী পড়া আলাদা অবস্থা। কোনো external SMS/email/WhatsApp পাঠানো হয় না।
- Redis backplane আলাদা optional setting। একাধিক server-এর মধ্যে live signal পাঠাতে এটি ব্যবহার করা যায়। Data cache চালু করলেই backplane চালু হয় না।

## Configuration ও staging

Default configuration-এ Redis cache এবং Redis backplane দুটোই বন্ধ। বাস্তব endpoint, credential বা certificate source-এ দেওয়া নেই। Operator নিজে approved secret store থেকে configuration দেবেন। Production-এ TLS দরকার। Password/connection string chat, screenshot বা log-এ দেবেন না।

বিস্তারিত contract, setting-এর নাম, Windows PC/IIS topology, sticky sessions, shared Data Protection key, migration/index review এবং benchmark পদ্ধতি: [REALTIME_AND_LARGE_DATA.md](REALTIME_AND_LARGE_DATA.md)। মেসেজের permission, সীমা ও recovery: [INTERNAL_MESSAGING.md](INTERNAL_MESSAGING.md)।

Local test-এ actual Redis, HTTP, WebSocket ও দুই-node backplane/outage recovery আলাদা evidence হিসেবে আছে। SQL Server query translation এবং migration SQL তৈরি করা actual SQL Server-এ চালানোর সমান নয়। এই executor-এ Chromium-এর প্রয়োজনীয় local socket blocked হওয়ায় real-browser execution হয়নি; actual DataTables library-এর DOM + live HTTP test আলাদাভাবে চালানো হয়েছে। Windows/IIS/SQL Server/TLS/load এবং customer/native-language UAT এখনও বাস্তব staging gate।

বর্তমান exact test count ও hash: [CURRENT_VERIFICATION.json](CURRENT_VERIFICATION.json)। পুরোনো report-এর সংখ্যা বর্তমান ফলাফলের সঙ্গে যোগ করবেন না।

## Synthetic demo

Enterprise demo-তে পাঁচটি স্পষ্টভাবে fictional internal message, inbox/outbox, read sample এবং notification acknowledgement যুক্ত হয়েছে। এগুলো বাস্তব কর্মী/গ্রাহককে পাঠানো যোগাযোগ বা বাস্তব অনুমোদন নয়। Production startup-এ কোনো demo seed হয় না। Packaged SQL-এর sample password সংরক্ষণ করা হয়নি; নিজের private demo তৈরি করতে আগের hidden-password script ব্যবহার করুন।

## পরবর্তী আলাদা কাজ

নিজস্ব vault management এবং trusted backend API-এর নতুন অনুরোধ এই Redis/DataTables/SignalR checkpoint-এর বাইরে আলাদা implementation হিসেবে চলছে। এই source-কে সেই নতুন security architecture সম্পূর্ণ হয়েছে বলে গণ্য করবেন না।
