# Bangladesh one-company first-live review

Confirmed deployment scope: Bangladesh, one company, retaining HR/Payroll, CRM, Fixed Assets and E-commerce. The multi-country configuration capability remains; unspecified future Arab countries and US states are not blockers for this Bangladesh-only launch.

## Concrete source defects found in Revision 3

1. New ordinary Identity accounts were unconfirmed, while employee/customer linking required confirmation and no supported production verification path existed. A bounded audited offline mailbox-ownership review is now implemented. See EMAIL_OWNERSHIP_REVIEW_BN.md.
2. Omitted payroll form fields could silently inherit a daily basis, 2-decimal precision and zero deductions. The form now requires explicit choices and accepts reviewed zero/false only when actually posted.
3. Payroll could calculate on the last local day before the month had fully ended. The whole calendar month must now be in the past in the company's reviewed timezone.
4. Core date guards, automatic financial posting dates and report cutoffs used UTC inconsistently with company-local policies. The corrected paths use the reviewed business date; actual audit timestamps remain UTC and historic rows are not rewritten.
5. The readiness endpoint accepted unknown applied migrations, and existing staging evidence wrappers only checked anonymous login/source results. Unknown/duplicate/missing histories now fail closed. A separate read-only SQL/host evidence path now distinguishes actual checks from NOT_VERIFIED gates, without applying migrations, seeding, changing keys/ACLs or restarting IIS.

Current corrective-source verification has passed: clean Release build, 805 .NET tests, 902 real localhost HTTP checks (including 47 fresh-account onboarding checks), 44 JavaScript checks, three fail-closed CLI invocation checks, model consistency, unchanged idempotent SQL and publishing. The new tests cover the fixes above. Independent cold-copy verification is recorded separately in CURRENT_VERIFICATION.json. Prior Revision 3 counts remain historical. None of these local checks executes Windows/IIS or an actual SQL Server.

## Operator/environment gates, distinct from code work

- Authorized connected Windows PC/IIS and SQL Server staging target. The user confirms staging exists; this execution environment has no connection or credentials for it.
- Approved Bangladesh company currency/timezone and payroll/tax values, effective dates, reviewer responsibilities, chart of accounts and opening balances. BD/BDT/Asia-Dhaka examples are configuration choices, not statutory payroll/tax rules or compliance certification.
- Read-only metadata/security preflight, followed by approved isolated-staging migration, rollback/restore, SQL concurrency and full authenticated business UAT. Read-only checks cannot establish these write-path properties.
- Two properly authorized real operators where independent policy/payroll/asset reviews are required. Synthetic makers/reviewers and fictional demo policy percentages are not real approvals.
- Browser/mobile and language acceptance, backup/restore, monitoring, password/MFA handover and license eligibility.

## Existing workflow limits to accept or extend explicitly

- Payroll supports reviewed daily/monthly/mixed contractual calculations and accrual postings, without automatic salary transfers or statutory filing.
- CRM quotes are internal net proposals and reviewed customer/sales-ready handoffs, without external message sending or automatic tax invoices.
- Fixed assets support reviewed book straight-line/None depreciation and disposal, without tax depreciation/revaluation/impairment.
- Ecommerce currently requires authenticated linked customers, branch collection and pending/unverified payment; no public self-registration, delivery integration or card gateway is implied.
- Legacy purchase/sales are single-line documents; advanced tax/discount/multi-line invoice requirements remain separate specification. No such expansion is silently claimed by this correction.
