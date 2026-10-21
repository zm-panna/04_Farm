# নতুন staff/customer account-এর email ownership review

Account তৈরি করলেই email ownership verified হয় না। HR নিজের payslip এবং Ecommerce customer profile link করার আগে Identity account-এর ownership যাচাই প্রয়োজন। Provider সংযুক্ত না থাকলে এই release-এ একটি সীমিত, স্পষ্ট out-of-band review পথ আছে। এটি email পাঠায় না এবং SMTP delivery বা automated email challenge হয়েছে বলে দাবি করে না।

## অনুমোদিত operator-এর ধাপ

1. Administration থেকে নতুন account তৈরি করে প্রয়োজনীয় branch ও ন্যূনতম operational permissions দিন। Customer-এর জন্য শুধু `Ecommerce.View`, employee portal-এর জন্য `Employee.Self` প্রয়োজন হতে পারে; financial/admin permissions অযথা দেবেন না। Initial password কেবল প্রতিষ্ঠানের অনুমোদিত নিরাপদ উপায়ে hand over করুন।
2. Application-এর বাইরে mailbox control বাস্তবে যাচাই করুন: reviewer নিজে mailbox control পর্যবেক্ষণ করেছেন, অথবা প্রতিষ্ঠানের অন্য approved system-এর email challenge সম্পন্ন হয়েছে। শুধু নাম/phone মিলে যাওয়া, পরিচয়পত্র থাকা বা operator account তৈরি করা email ownership-এর প্রমাণ নয়। Password বা mailbox content সংগ্রহ/রেকর্ড করবেন না।
3. Administration-এর সংশ্লিষ্ট account row-তে “Record out-of-band verification” খুলুন। Operator-এর `Permissions.Manage` ও `Identity.VerifyEmailOwnership` অনুমতি দুটোই প্রয়োজন; tenant wildcard administrator-এরও target restrictions একই থাকে। `Identity.VerifyEmailOwnership` সাধারণ staff creation বা permission-management screen থেকে দেওয়া যায় না; trusted existing wildcard administrator অথবা আলাদাভাবে অনুমোদিত administrative provisioning প্রয়োজন।
4. সম্পন্ন method, 5–48 অক্ষরের non-sensitive external review reference এবং operator-এর current password দিন। Exact account-এর attestation checkbox review করে submit করুন। Raw document, password, token বা mailbox message লিখবেন না। Serialized audit record বড় হলে ছোট reference চাইবে; কখনও truncate করবে না।
5. Success হলে HR বা Ecommerce-এর নিজস্ব identity-link form-এ গিয়ে exact staff/customer account আলাদাভাবে confirm করে link করুন। Email ownership alone কর্মী বা customer mapping অনুমোদন করে না। Original password, roles ও business permissions বদলায় না। Existing target sessions-এর security stamp refresh হয়।

## বাধ্যতামূলক সীমা

- নিজের account, PlatformAdmin, wildcard/Permissions.Manage-bearing privileged identity, অন্য company-র বা shared multi-company identity এই পথে verify করা যায় না।
- Target account অবশ্যই এই company-তে supported Create Staff workflow দিয়ে তৈরি হতে হবে। Target-এর সব branch operator-এর scope-এ থাকতে হবে। অন্য company থেকে attach করা account-এর global identity পরিবর্তনের shortcut নেই।
- Expected email ও concurrency stamp মিলতে হবে; changed, locked বা already verified account reject হয়। Duplicate submission নতুন review তৈরি করে না।
- Wrong operator-password attempts Identity lockout-এ গণনা হয়; transaction failure তা মুছে দেয় না। সফল business change এবং audit এক transaction-এ থাকে। Audit save ব্যর্থ হলে confirmation-ও rollback হয়।
- Long current administrator passwords সর্বোচ্চ 1024 অক্ষর পর্যন্ত সমর্থিত; submitted password HTML বা evidence-এ ফেরত দেওয়া হয় না।

Audit action `StaffEmailOwnershipVerifiedOffline`-এ actor ও UTC time থাকে। Existing 200-character SQL audit column-এর compact JSON keys: `u` target user ID, `b` branch ID, `e` পূর্ণ SHA-256 normalized email fingerprint, `m` method (`M` observed mailbox control, `E` externally completed email challenge), `r` সম্পূর্ণ review reference। এই evidence একটি operator attestation, application-delivered challenge-এর প্রমাণ নয়।

বাস্তব deployment-এ প্রতিষ্ঠানের অনুমোদিত reviewer ও evidence-handling policy লাগবে। Automated mail delivery, public self-registration বা privileged/shared-account recovery এই feature-এর অংশ নয়।
