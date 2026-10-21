# Reproduce enterprise source verification

Run in an isolated extracted source directory, with the pinned .NET 10 SDK, Node.js, Python 3 and Poppler tools available. The packaged results are actual local Linux/SQLite checks. Windows, SQL Server and browser acceptance have separate checklists and were not executed here.

1. Run `scripts/verify-source.sh` on Linux or `scripts/VERIFY_SOURCE.ps1` on Windows for restore, Release build, the current complete test suite (see `CURRENT_VERIFICATION.json` for the verified count), model consistency, migration SQL generation and publishing. The Windows PowerShell scripts are operator helpers reviewed in source, not executed in this Linux verification.
2. Run `node scripts/TEST_UI_LOCALIZATION.js`, `node scripts/TEST_SUPPORT_TICKETS_UI.js` and `node scripts/TEST_BROWSER_SECURITY.js`. Current totals are 31, 8 and 5 respectively. These are deterministic DOM/source checks, not browser automation.
3. After the Release build, run the disposable real-HTTP journeys separately:
   - `python3 scripts/http-smoke.py` (407)
   - `python3 scripts/http-enterprise-hr.py` (117)
   - `python3 scripts/http-enterprise-crm.py` (77)
   - `python3 scripts/http-enterprise-assets.py` (75)
   - `python3 scripts/http-enterprise-policy.py` (86; do not pass a checkpoint skip flag)
   - `python3 scripts/http-enterprise-ecommerce.py` (93)
   - `python3 scripts/http-enterprise-onboarding.py` (new corrective-revision journey; count recorded in current evidence)

The HTTP scripts create their own temporary synthetic Development SQLite database, random in-memory initialization secret and loopback listener. They accept no customer database or external target. The core journey additionally uses Poppler's `pdfinfo`, `pdffonts` and `pdftotext` for report checks. They save sanitized evidence under `evidence/http*`, then remove the temporary fixture. On Windows, use the installed Python command if it is named `python` rather than `python3`. Windows execution itself remains unverified.

Do not substitute a live database, public host, real staff/customer information or genuine payment credentials. Demo consent, rates and approvals are fictional test evidence only.

The historical Revision 3 independent audit rebuilt a separate frozen source copy and added 20 isolated regression probes, producing 670 passing .NET cases. Those results are historical until rerun on the corrective revision. `evidence/independent/EnterpriseIndependentAuditTests.cs` records those probes separately; it is not silently added to the release's 650-test project. The historical independent report explains Revision 3 source and binary identity. Current corrective-revision evidence is explicitly indexed by `CURRENT_VERIFICATION.json` and `evidence/urgent/`.

The original authoring build, independent cold build and a further clean-extraction build are recorded separately. Different source directory paths can yield different DLL hashes; the runtime source manifest, file hashes and per-run binary hashes establish their provenance without claiming byte-identical independent builds.
