# Farm go/no-go — 26–27 October 2026 Bangladesh time

Decision: **NO-GO for production writes/deployment until the gates below pass.** A local publish candidate is available; this does not certify the live server.

## Confirmed target

| Item | Confirmed value |
|---|---|
| Windows host | Windows Server 2022, `192.168.1.12` |
| Public Farm hostname | `farm.baligaonbandhan.com` |
| Existing database | `Projectfarm` — preserve data; no reset, reseed or automatic creation |
| Workspace | `D:\Web Final\Working Software\Updatedoc\ICT BD\Project list\Test Project\04_Farm` |
| Git baseline and verified remote main | `3473cd2774c325ec14bfa1afd47d0992707d47b5` |

Fetch and remote-head comparison found only `main`, with ahead/behind `0/0`. The four prior preparation files were preserved. The parent reports the inspected cloud Codex setup task `01a17435-6a9f-7081-86f6-6cdc6d55a04a` made no source changes, so it provides no newer code to copy. Any other unpublished Farm update still needs its exact task/artifact/branch/commit identified; no guessed ZIP is substituted.

## Work completed in this continuation

- Fixed enterprise HTTP evidence overwriting earlier runs: every run now has a unique `artifacts/http-verification/` directory.
- Fixed false-success exit behavior when teardown detects leaked fixture passwords or changed binaries.
- Fixed Windows SQLite file-handle leaks by explicitly closing fixture query connections while retaining commit/rollback semantics.
- Fixed results being marked passed before cleanup: cleanup failure is now recorded and fails the command.
- Added the pinned Python timezone dependency and an early onboarding timezone check. An isolated virtual environment was restored under `artifacts/http-test-env-20261026`; global Python and host security settings were not changed.
- Seven focused regression tests pass. No Farm application DLL/source or database schema change was needed for these verification-harness defects.

Fresh Windows localhost HTTP verification: **414 passed, 0 failed**, with successful cleanup and matching published Farm/TrustedInfrastructure DLL hashes in every accepted run:

| Journey | Passed | Evidence under `artifacts/http-verification/` |
|---|---:|---|
| Fresh-account onboarding and own HR/customer access | 48 | `onboarding-21pbryt9/results.json` |
| HR/payroll daily, monthly, mixed and independent posting | 118 | `hr-k7wzzx6a/results.json` |
| CRM consent, quote acceptance and scope isolation | 78 | `crm-cpw6gp1t/results.json` |
| Fixed assets acquisition, depreciation and disposal | 76 | `assets-c0juqbjt/results.json` |
| Ecommerce reservations, fulfillment, returns and currency precision | 94 | `ecommerce-ze4zhncy/results.json` |

Combined machine-readable index: `artifacts/http-verification/READINESS_20261026.json`. These are real localhost HTTP/SQLite workflows, not SQL Server or IIS tests. All 335 files in the existing publish manifest were rehashed with zero mismatches. No application rebuild was necessary because this continuation changed the verification harness and documentation only.

The earlier local Release build/publish and 922 passed .NET tests / 2 Redis skips / 84 JavaScript checks remain evidence from the preceding run, not newly rerun results. The existing candidate is `artifacts/readiness-20261025T180754-8656b94d/publish/`, with its `PUBLISH_SHA256.txt` and `RESULTS.json` alongside. Runtime hashes must match the new HTTP results before combining this evidence.

Earlier attempts remain visible but are NOT accepted passing runs: `onboarding-qx99u3i3` lacked Python tzdata; `onboarding-b75eon62` and `hr-684znp3r` completed their business checks but failed Windows cleanup. Their pre-fix JSON success fields are superseded by the nonzero command outcomes and this note; raw evidence has not been rewritten.

## Critical path and owners

| Gate | Owner / action | Status |
|---|---|---|
| Source verification wrapper and EF tooling | Authorized administrator reviews/signs/trusts files using the organization's approved process; then rerun canonical verification, model consistency and fresh SQL generation | BLOCKED by unsigned PowerShell and downloaded-tool-manifest policies; no denied operation retried or bypassed |
| Server access | Operator supplies/authorizes an existing management route to `192.168.1.12`; use secure login mechanisms, never credentials in chat | No established authenticated server session in this task |
| SQL target identity | Confirm SQL Server instance/port hosting existing `Projectfarm`, migration history and schema compatibility; do not reuse another project's connection settings | Pending read-only preflight |
| Existing data protection | DBA provides dated successful backup AND restore rehearsal, rollback owner and expected data-loss window | Pending; no writes allowed yet |
| Migration review | Compare all five committed migration IDs, including `20261020094521_ReviewedScanningWorkflows`, with target history; review fresh idempotent SQL and incompatibilities | Pending EF gate + DBA review; explicit application approval required |
| Dedicated IIS target | Inspect existing sites/pools/bindings read-only; agree unused Farm-specific site/pool and versioned physical path; preserve every other site | Proposed names `Farm-Staging` / `Farm-Staging-Pool` only, not created |
| TLS/topology | Confirm DNS destination, valid certificate for `farm.baligaonbandhan.com`, gateway/backend topology and exact trusted proxy address | Not verified; no DNS, firewall, TLS or IIS changes made |
| Vault/keys | Operator configures approved OpenBao path/identity and persistent protected Data Protection; matching separate gateway package required | Pending secure provisioning; no bypass of fail-closed production mode |
| Staging acceptance | Authorized isolated staging migration/write-path, SQL concurrency, restart/key persistence, rollback/restore, browser/device UAT and four-module acceptance | Not executed against real infrastructure |
| Business inputs | Approved Bangladesh policies, payroll/tax values, chart/opening balances, independent reviewers and owner sign-off | Pending; synthetic test data is not business approval |
| Optional integrations | Real Redis, Ollama and external provider acceptance if enabled | Unverified; keep disabled until separately accepted |

## Read-only preflight sequence

### Desktop access check — 2026-10-26

- Local computer: `DESKTOP-2GLIULR`, Wi-Fi IPv4 `192.168.68.101`. None of its IPv4 addresses matches the confirmed server `192.168.1.12`; they are separate hosts.
- No running standard Windows RDP clients (`mstsc` / `msrdc`) were found. This does not rule out an uninspected third-party management route, but no authenticated server session is established in this task.
- One TCP connection probe to the specified server's standard RDP port `192.168.1.12:3389` received no response within five seconds. This does not prove the server is off; routing, a VPN requirement, filtering, or a different approved RDP port may explain it. No other ports were scanned and no login was attempted.
- The desktop DNS lookup for `farm.baligaonbandhan.com` returned “DNS name does not exist.” HTTPS could not be checked through that hostname. This is the current local resolver result, not an independent public-DNS audit.
- Consequently no remote IIS site/app-pool metadata, SQL `Projectfarm` metadata or backup records were accessed. No credentials were requested/read, and no network, DNS, TLS, IIS or database settings were changed.
- Next concrete operator action: open the already-approved network/VPN route and an authenticated RDP session to the Farm server, or identify the organization's existing management route/port if it is different. The domain administrator should separately verify the Farm hostname's DNS record through the approved change process. Do not paste passwords or tokens into chat. After the operator makes the authorized session available, continue the read-only steps below before requesting any deployment or database write.

1. Establish the approved server session and verify the host identity; do not treat this desktop as the server.
2. Inspect .NET Hosting Bundle, IIS site/app-pool inventory, existing Farm binding/certificate metadata and candidate directory hashes. Do not install, restart, rebind or alter ACLs during inspection.
3. With an approved read-only SQL identity, verify the exact instance/database `Projectfarm`, migration history (including unknown/duplicate/missing entries), schema metadata and current backup/restore evidence. Do not run `database update`, seed, bootstrap or export customer rows.
4. Review non-secret runtime configuration and actual trusted gateway/vault/key topology with the operator. Do not expose credential files, connection passwords, tokens or private keys in reports.
5. Use the approved existing published Farm DLL and `STAGING_READONLY_PREFLIGHT.md` procedure only after script policy is resolved, writing to a new private evidence path. That collector does not apply migrations or approve production.
6. Produce a dated go/no-go report. Request specific approval for the reviewed staging migration and site configuration only after backup, schema and topology checks pass. Production switch-over requires a separately confirmed window and rollback plan.

## Deployment order after explicit operator approval

1. Resolve approved access and the script/tool policy gates; complete read-only preflight and fresh EF model/SQL checks. Stop on an unknown migration or incompatible schema.
2. DBA verifies a backup of existing `Projectfarm` by restoring into an isolated staging database. Record backup time, restore result, recovery objectives and rollback owner. Never reset or seed the existing database.
3. Review the exact migration SQL and its compatibility with both candidate and rollback binaries. Obtain specific approval for staging writes, then apply only the reviewed changes to the isolated staging copy.
4. Stage the hash-verified publish in a new Farm-only versioned directory and dedicated approved IIS site/pool. Configure the approved gateway/vault/key profile and HTTPS certificate/binding for `farm.baligaonbandhan.com`; preserve other projects' sites, pools and directories. These configuration changes require their own approval.
5. Complete actual SQL transaction/concurrency checks, authenticated four-module UAT, scanner/report/browser checks, TLS, vault revocation, key persistence after restart, monitoring and restore rehearsal. Record business-owner sign-off; any failed required gate means NO-GO.
6. Agree a maintenance window and explicit production switch-over approval. Take the approved current backup, retain the existing publish/configuration, apply only approved database changes and switch only the Farm binding/path. Recheck readiness, login, stock/ledger reconciliation and business workflows before reopening access.
7. Roll back on failed health, authentication, reconciliation or required UAT. Stop new Farm writes under operator control; restore the previous application/configuration only if compatible with the resulting schema. If a database restore is required, obtain explicit restore approval covering loss/reconciliation of writes since backup. Never assume reverting binaries reverses migrations. Record the outcome and keep other sites running.

The sequence above is a reviewable operator plan, not an executed deployment or authorization to perform its writes. Current safe local preparation is complete for the identified defects; further server work is waiting on the listed operator gates, not running in the background.

Hotel, LC/import-export and advanced bins/lots remain unspecified scope in `REQUIREMENT_MATRIX.md`; they are not silently completed or dropped. Actual SQL/IIS/TLS/OpenBao, physical scanners, live AI and customer acceptance remain distinct from local HTTP evidence. No GitHub push, deployment or live database write was performed.
