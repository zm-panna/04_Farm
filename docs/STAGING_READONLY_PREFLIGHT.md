# Bounded SQL Server and IIS staging evidence

## Status and purpose

This is a read-only evidence collector for the one-company Bangladesh first-live gate. It is **not a production approval**. Actual SQL Server and Windows/IIS execution has not been performed in the source-build environment: no staging server, credentials, Windows identity or customer acceptance was provided.

The existing anonymous login-page smoke test proves HTTP reachability only. This collector adds real SQL metadata and selected-company policy evidence when an authorized operator runs it against the intended staging target. It does not start the web host, workers, Identity provisioning or Data Protection, and does not create/migrate/seed a database, generate a key, change permissions, restart IIS or write business records. The only deliberate write is a new local evidence JSON file (plus an optional new IIS sidecar).

## Before running

1. Use the existing published `EnterpriseERP.Web.dll` from the exact release under review. Complete source tests and release manifest checks separately. Do not point this tool at an arbitrary DLL downloaded from an unknown source.
2. An operator must arrange a local process running under the actual application's existing Windows/runtime identity, using its existing SQL runtime connection. Do not use an administrator/migration login and call that runtime evidence. The tool compares the local process identity and actual SQL original login/database user with explicit expectations. It does not create an impersonation token or provision any account.
3. Independently confirm the expected SQL `SERVERPROPERTY('ServerName')`, database name, `ORIGINAL_LOGIN()` and `USER_NAME()` with the DBA. A connection alias is not necessarily SQL Server's actual server name. An unexpected name stops further inspection.
4. Reuse a locally available secure connection through the process environment variable `ConnectionStrings__DefaultConnection`. Never put a password in CLI arguments, transcripts, screenshots, chat, an evidence report, this guide or source control. The wrapper deliberately has no connection-string/password parameter. It does not fetch, save or echo secrets. Arrange injection through your existing deployment/secret mechanism; do not paste credentials into shell history.
5. The connection must select the expected database, have `Encrypt=Mandatory` (or `Strict`) and `TrustServerCertificate=false`, and use a trusted hostname/certificate. SQLite, attached-file/user-instance databases and `ApplicationIntent=ReadOnly` routing are rejected. Certificate errors must be fixed by the operator; the tool never bypasses them.
6. Choose an already provisioned private local evidence directory outside the published application directory and every exposed webroot. Confirm its existing Windows ACLs restrict access to the intended operator/reviewers. This tool makes no ACL changes. Unix directories must already be owner-only; symlink/junction ancestors of evidence, application or webroot directories, UNC/device/alternate-stream output paths and existing output files are rejected. Declared application/webroot directories must already exist and refer to their actual non-linked local paths. Do not serve or broadly share the evidence directory. Identity hashes are identifiers, not anonymization guarantees.
7. The existing runtime principal needs sufficient SELECT access and database `VIEW DEFINITION` to collect a complete catalog. If those are absent, expect `NOT_VERIFIED`. **Do not elevate the runtime principal merely to make this tool pass.** Arrange a separately authorized DBA metadata/security review instead. The tool never grants permissions.

## Explicit non-secret environment settings

Set these through your local process/deployment environment. Example values below are placeholders; substitute independently verified values. Only the selected one-company ID is inspected for business-policy/demo checks.

```powershell
$env:Database__Provider = 'SqlServer'
$env:FARM_PREFLIGHT_EXPECTED_SERVER = 'YOUR-SQL-SERVER\INSTANCE'
$env:FARM_PREFLIGHT_EXPECTED_DATABASE = 'YOUR_REVIEWED_STAGING_DATABASE'
$env:FARM_PREFLIGHT_EXPECTED_SQL_LOGIN = 'YOUR_EXISTING_RUNTIME_LOGIN'
$env:FARM_PREFLIGHT_EXPECTED_SQL_USER = 'YOUR_EXISTING_DATABASE_USER'
$env:FARM_PREFLIGHT_EXPECTED_PROCESS_IDENTITY = 'IIS APPPOOL\YOUR_EXISTING_POOL'
$env:FARM_PREFLIGHT_COMPANY_ID = 'YOUR_POSITIVE_COMPANY_ID'
$env:FARM_PREFLIGHT_EXPECTED_TIMEZONE = 'Asia/Dhaka'
$env:FARM_PREFLIGHT_WEBROOT = 'C:\YourExistingPublishedApp\wwwroot'
$env:FARM_PREFLIGHT_RUNTIME_IDENTITY_CONFIRMED = 'YES'
$env:FARM_PREFLIGHT_PRIVATE_OUTPUT_CONFIRMED = 'YES'
```

`Bangladesh Standard Time` is also supported, but the configured expectation must exactly match the approved company's policy. Both identifiers must resolve on the executing host. This gate expects BD, BDT and two currency minor units. A different jurisdiction/currency requires a separately reviewed gate; it must not be coerced to Bangladesh or applied globally.

For an IIS virtual account, launching an appropriate process may require an operations-approved method. This collector does not provide impersonation or use administrator credentials as a substitute. A SQL-authenticated connection still requires the actual expected local process identity and matching SQL login/user.

## Run the already-published app

No build/publish/migration is performed by this wrapper:

```powershell
.\scripts\STAGING_READONLY_PREFLIGHT.ps1 `
  -ApplicationDll 'C:\YourExistingPublishedApp\EnterpriseERP.Web.dll' `
  -OutputPath 'C:\YourPrivateEvidence\staging-20261019-unique.json'
```

Or invoke the early CLI route directly:

```powershell
dotnet 'C:\YourExistingPublishedApp\EnterpriseERP.Web.dll' `
  --verify-staging-readonly 'C:\YourPrivateEvidence\staging-20261019-unique.json'
```

The only CLI arguments accepted by the early route are the switch and one new absolute JSON path. The alternate `--verify-staging-readonly=...` spelling is intercepted and rejected; it cannot fall through into normal application startup. Combining it with seeding/export/provisioning flags fails before host startup. The connection opens only after input checks and successful reservation of the new output file. Existing evidence is never overwritten. A failed/partial output must not be treated as a valid report; choose a fresh filename for an authorized retry.

Optional local IIS configuration inspection:

```powershell
.\scripts\STAGING_READONLY_PREFLIGHT.ps1 `
  -ApplicationDll 'C:\YourExistingPublishedApp\EnterpriseERP.Web.dll' `
  -OutputPath 'C:\YourPrivateEvidence\staging-20261019-another-unique.json' `
  -IisSiteName 'YOUR_EXISTING_SITE' -IisApplicationPool 'YOUR_EXISTING_POOL'
```

This reads the installed Windows IIS management assembly and selected root-site/pool configuration using current access, then creates a `.iis.json` sidecar beside the SQL report. It never calls `CommitChanges`, starts/recycles a pool or changes its identity. If the runtime account cannot inspect IIS, the sidecar is `NOT_VERIFIED`; an independent operations reviewer should inspect configuration without widening the application account's access. No PowerShell/IIS execution was available in the source-only environment, so this wrapper remains a Windows operator validation step.

## What the SQL evidence means

- `configuration`: explicit target, runtime-identity and private-output attestations, encryption and timeout configuration accepted. Windows ACLs and secret injection are still operator responsibilities.
- `sqlConnection` / `targetIdentity`: actual encrypted connection and expected server/database/original login/database principal matched. Only SHA-256 fingerprints of identities are emitted, not connection strings, passwords or raw exception details. Read-only databases/replicas are rejected as primary staging evidence.
- `metadataVisibility`: database `VIEW DEFINITION` established. Hidden metadata, permission/read failures and row-limit overflow never count as healthy.
- `migrations`: compiled migration IDs, recognized applied IDs, pending IDs and fingerprints of unknown IDs. Missing history, pending/unknown/duplicate history and model-snapshot drift fail; no migration is applied. “Committed” means compiled into this release, not independently authenticated source-control provenance.
- `modelSchema`: all mapped SQL Server tables/columns (including identity/computed flags), primary/alternate keys, unique indexes and foreign-key column/target/delete/update rules compared with the compiled model. CHECK names and enforcement state are checked. Ordinary non-unique performance indexes, default/computed expressions, collations, triggers, stored procedures and complete schema equivalence are not certified. Simple SQL Server index-filter parenthesization is normalized conservatively; complex predicate differences fail rather than being guessed equivalent.
- `constraintsState`: visible user-table foreign/CHECK constraints and unique indexes checked for disabled/untrusted/not-for-replication enforcement. Actual CHECK definition hashes are retained for an independent semantic review; the collector does not claim text fingerprints prove the definitions match the release. No DBCC/full-table validation occurs.
- `runtimePrivileges`: bounded risk screen for privileged fixed roles and effective elevated server/database/schema/table permissions. Any detected elevated capability fails; unresolved permission results are `NOT_VERIFIED`. This is not a complete cross-database, ownership, delegation, nested-group or operating-system security audit.
- `productionSafetyConfiguration` is always `NOT_VERIFIED`: effective `AllowedHosts`, protected persistent key path/mode and complete host configuration require independent runtime evidence. The early CLI deliberately does not load/start the host or validate a key by writing one.
- `companyPolicy`: selected company's applicable approved policy on its Bangladesh-local business date, BD/BDT/2-decimal denomination, matching approved timezone, currency lock and independent reviewer metadata. It does not authenticate the human review or certify legal/tax suitability. Enabled SQL row-level security on application tables prevents a data all-clear because rows may be hidden.
- `fictionalMarkers`: known demo company name/slug, approved-policy review text, tax-policy text and seed-audit markers for the selected company. Free text is inspected as booleans and is not exported. A marker fails even if surrounding records say “Approved.” This bounded check cannot establish that every business record, Identity account or other company is free of fictional content.

Limits: connection timeout 5 seconds, each SQL command 5 seconds, overall cancellation budget 120 seconds, no connection retries, at most 12,000 catalog rows per query and 200 selected-company policy/audit/tax rows before an inconclusive result. Company queries use parameterized IDs, index seeks and `MAXDOP 1`; if no suitable seek plan is available, the query fails safely as `NOT_VERIFIED`. No NOLOCK/dirty-read “evidence,” broad ledger scans, mutations, repair attempts or load tests are run. Reads are not a transactionally consistent snapshot; concurrent changes require a coordinated observation window and a fresh report.

## Status and exit codes

Each check independently records `PASS`, `FAIL` or `NOT_VERIFIED`. Failures do not turn unexecuted dependent checks green. `collectorStatus` summarizes automated checks only; manual checks remain separate.

- `0`: all automated SQL checks passed. This does **not** approve production.
- `2`: at least one automated check failed.
- `3`: automated evidence remains inconclusive/unexecuted.
- `4`: invalid invocation/configuration/output or failed evidence write. A complete report may not exist.

The IIS sidecar has its own status and is not folded into the SQL exit code. A sidecar `PASS` covers only selected configuration metadata. Certificate trust/expiry/hostname, live external TLS, Hosting Bundle functionality, security headers, real worker identity, ACLs, secret handling, key persistence and session survival across an approved restart remain `NOT_VERIFIED`.

Every report has `productionApproved: false`, including a completely green automated metadata run. Do not change it to true or rename this report as customer acceptance.

## Separate required evidence

Retain dated independent evidence for: reviewed CHECK semantics and SQL runtime least privilege; reviewed schema-application and rollback; verified backup/restore; IIS/TLS and encrypted persistent key-ring operation; company-local real policy and opening-balance approval; authenticated permission/tenant-isolation tests; maker-checker rejection and duplicate-submit/concurrency testing; stock/financial reconciliation; and customer sign-off for the licensed first-live scope. Mutation-based business UAT belongs on an explicitly approved disposable staging clone with its own test plan and consent, never as an automatic live “smoke test.”

Do not assume a ready login page, existing database, successful source test, approved-looking fictional fixture or this metadata collector proves any of those independent gates.
