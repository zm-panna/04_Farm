# Farm local readiness and Windows Server 2022 preparation — 2026-10-25

Status: SOURCE CANDIDATE ONLY. Not approved for deployment.

**2026-10-26 update:** See [current go/no-go and confirmed target](FARM_GO_LIVE_20261026.md). The server is now confirmed as `192.168.1.12`, hostname `farm.baligaonbandhan.com`, existing database `Projectfarm`; the earlier unresolved-target notes below are historical. New HTTP verification has 414 passing checks with cleanup, after fixing the Windows harness. This does not resolve the host-policy, SQL/IIS or customer-acceptance gates.

Workspace: `D:\Web Final\Working Software\Updatedoc\ICT BD\Project list\Test Project\04_Farm`
Base/current Git commit: `3473cd2774c325ec14bfa1afd47d0992707d47b5` (uncommitted preparation changes listed below). Verified origin: `https://github.com/zm-panna/04_Farm.git`, branch `main`. Working tree was clean before this work. No push, deployment, database migration or host security change was performed.

## Changes

- Windows source verification now uses a new per-run output directory, rejects existing evidence destinations and publishes inside the same run directory. This prevents overwriting historical evidence or a previous publish candidate. PowerShell syntax passes; execution is blocked by the host's unsigned-script policy, so runtime verification of the wrapper remains pending.
- The trusted-backend staging checklist now identifies the current scanner migration and removes the obsolete Revision 6 no-new-migration assumption. There are five committed migrations; the latest is `20261020094521_ReviewedScanningWorkflows`.
- No confirmed business defect has yet been established by the completed checks. Hotel, LC/import-export, warehouse bins/lots and unrestricted AI actions are not completed by this preparation: they require the scope/inputs described in `REQUIREMENT_MATRIX.md`.

## Fresh local verification

.NET SDK: 10.0.402. Restore succeeded. Release solution build succeeded with zero warnings and zero errors (elapsed 2:29.82). Publish succeeded using the built Release assembly.

Candidate: `artifacts/readiness-20261025T180754-8656b94d/publish/`
Evidence: `artifacts/readiness-20261025T180754-8656b94d/`
The 335-file publish inventory is recorded in `artifacts/readiness-20261025T180754-8656b94d/PUBLISH_SHA256.txt`.
Web DLL SHA-256: `045F29761F8F90D8A0369BF301DB0475B29D71D2274CF2A2CD5F243E7BFA9827`

Fresh JavaScript checks: 84 passed (localization 32, support 8, browser-security source checks 5, messaging 12, server tables 10, scanner camera/real offline WASM decoding 17). These are deterministic Node tests, not real browser/device acceptance.

Fresh .NET run: **922 passed, 0 failed, 2 skipped; 924 total**, duration 5m 3s. The two skipped cases require an explicitly configured disposable Redis endpoint. The first sandboxed run aborted because testhost could not connect within 90 seconds; an approved unsandboxed retry completed successfully. Evidence: `artifacts/readiness-20261025T180754-8656b94d/test-retry/tests.trx` and `test-retry.log`. The original aborted run is preserved in `test-results/` and `test.log`. No old test counts are substituted for this result. A diagnostic read of the CodeIntegrity event log was denied; no access/security policy was changed.

The published IIS `web.config` exists and uses `dotnet .\EnterpriseERP.Web.dll` with in-process hosting. No database, PFX/PEM/key files or `secrets.json` were found by the output filename scan. This scan is not a complete secret audit. The bundled development settings use a local SQLite demo filename; the candidate is not production-configured and must never be launched in Development on a public host.

## Pending matrix

| Gate | Status / required action |
|---|---|
| Dependency restore, Release build, publish | Passed locally on Windows; not an IIS launch |
| .NET business/security regression | 922 passed, 0 failed, 2 Redis cases skipped; approved local retry |
| Node UI and offline scanner checks | 84 passed; browser, camera and physical scanner still unverified |
| Canonical PowerShell wrapper | Blocked: unsigned `scripts/VERIFY_SOURCE.ps1`; approved signing/trust review needed |
| EF tool restore / model consistency / fresh migration SQL | Blocked: downloaded-file protection on `.config/dotnet-tools.json`; no bypass or unblock performed |
| Real Redis integration | Requires explicitly authorized disposable Redis endpoint; historical Redis passes do not establish this candidate |
| HTTP/browser business journeys | Not rerun in this session; historical results are not fresh proof |
| SQL Server behavior/migration/concurrency | Not executed; target, existing migration history, backup/restore and approved operator needed |
| IIS/TLS/OpenBao/gateway/key persistence | Not executed; approved host topology, identities and secure configuration needed |
| Bangladesh company policies and four-module UAT | Accountant/business owner approval, actual policies, chart/opening balances and independent reviewers required |
| Ollama/payment/mail and other real providers | Not verified; named approved endpoints/contracts required; no secrets in chat |
| Additional Hotel/LC/advanced warehouse scope | Await explicit inclusion and business requirements; not silently declared complete |

## Windows Server 2022 staging plan (proposal only)

Windows Server 2022 is the confirmed target OS. The supplied address `192.1681.12` is ambiguous; do not assume `192.168.1.12`. Confirm the exact host and Farm domain/HTTPS binding or dedicated port before connecting. Existing sites, app pools and other project folders must remain untouched.

1. Confirm a dedicated Farm staging site name, app-pool name and new versioned publish root with the server operator. Proposed names: `Farm-Staging` and `Farm-Staging-Pool`; check for collisions before use. No site or app pool has been created.
2. Complete the blocked source gates through the organization's approved signing/trust process, rerun the canonical verification into a new folder and retain its logs. Do not disable execution policy, Application Control or downloaded-file protection to make the checks pass.
3. Confirm .NET 10 Hosting Bundle support on the server and the separate compatible Trusted Platform gateway/vault package. This task neither installs server components nor modifies that separate project.
4. The operator supplies the approved SQL Server/database and an isolated staging backup/restore plan, existing migration history, exact public hostname/proxy address, OpenBao configuration and protected persistent Data Protection profile. Provision credentials through the approved secure mechanism outside source/publish/webroot; do not paste them in chat. Keep optional Redis and Ollama disabled until individually approved and verified.
5. Review all five committed migrations against the real target. After tool restoration is approved, generate fresh idempotent SQL, review it with the DBA and request explicit authorization before applying it. The existing packaged SQL is historical evidence, not a newly verified migration artifact.
6. On an approved isolated staging target, verify read-only metadata first, then separately authorize migration/write-path tests. Verify login, tenant/branch isolation, payroll maker/reviewer, CRM handoff, asset ledger, ecommerce stock/reservations, scanner reviews, reporting and subscription restrictions. Reconcile stock and journals using synthetic staging data.
7. Verify actual HTTPS, gateway isolation, vault expiration/revocation, app-pool restart and key persistence, backup restore, monitoring, browser/mobile/native-language acceptance and expected load. Record the approved rollback artifact and database compatibility before scheduling production.
8. Obtain business-owner sign-off and explicit production target/deployment authorization. A publish directory and passing local tests alone do not authorize launch.

Use `LIVE_DEPLOYMENT_RUNBOOK_BN.md`, `STAGING_READONLY_PREFLIGHT.md`, `LIVE_UAT_CHECKLIST_BN.md` and the corrected `trusted-platform/TRUSTED_BACKEND_STAGING_CHECKLIST.md` for detailed operator procedures. Prefer the trusted-backend guide when older direct-connection examples conflict with the current fail-closed vault requirement.
