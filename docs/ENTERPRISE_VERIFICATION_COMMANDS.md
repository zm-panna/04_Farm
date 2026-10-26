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

## Windows evidence preservation (2026-10-25)

`VERIFY_SOURCE.ps1` now places each run in a new `artifacts/source-verification/<UTC>-<unique-id>/` directory, including its `publish/`, logs, generated SQL and `test-results/`. An optional `-EvidenceDirectory <new-directory>` selects the destination; an existing destination is rejected. Historical `evidence/` and existing `artifacts/publish/` are preserved. Use the directory printed by the script, not older recorded results. This output-path change does not approve deployment.

The current Windows host blocks unsigned PowerShell scripts and blocks local tool restore for the downloaded `.config/dotnet-tools.json`. Do not change execution policy or remove file protection as a workaround. An authorized operator must review the files and use the organization's approved signing/trust process before the canonical script and EF gate can be completed. See `LOCAL_READINESS_20261025.md` for fresh results and remaining gates.

## HTTP verification on Windows (2026-10-26)

The enterprise HTTP fixtures now save each run under a new `artifacts/http-verification/<suite>-<unique-id>/` directory and print its exact path. They preserve committed historical `evidence/http-enterprise-*` files. Query connections are explicitly closed after transaction completion so Windows can remove the temporary database. A failed teardown password-leak, binary-integrity or cleanup check now fails the command as well as its JSON report. Read both exit status and `results.json`; do not infer success from individual PASS lines. Current accepted evidence and the target-specific operator gates are indexed in `FARM_GO_LIVE_20261026.md`.

Use an isolated Python virtual environment and install `scripts/requirements-http.txt` before the onboarding journey. Windows Python does not provide the IANA timezone database required by `ZoneInfo('Asia/Dhaka')`. For a new, unused environment directory:

```powershell
python -m venv artifacts/http-test-env
./artifacts/http-test-env/Scripts/python.exe -m pip install -r scripts/requirements-http.txt
./artifacts/http-test-env/Scripts/python.exe -m unittest discover -s Tests/python -p 'test_*.py' -v
./artifacts/http-test-env/Scripts/python.exe scripts/http-enterprise-onboarding.py
```

No activation or PowerShell policy change is required. These are independent Python test commands, not substitutes for the blocked PowerShell or EF gates. Each business journey still creates and cleans its own synthetic database and localhost process; never redirect it to customer data or a remote target.
