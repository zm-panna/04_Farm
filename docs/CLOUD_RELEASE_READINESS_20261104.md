# 04 Farm cloud release readiness — 2026-11-04

Decision: **source candidate verified; production release blocked by missing target access and operational acceptance evidence**.

Application source tested: cloud branch commit `f366697e3755e76ad4c0bbacd6eb8b122a981fb2`.
The readiness follow-up changes only verification tooling and documentation; it does not change application binaries, schema or security configuration.

## Fresh cloud evidence

- .NET SDK 10.0.100; dependency restore passed.
- Release solution build: zero warnings, zero errors; publish passed.
- Main test run: 922 passed, zero failed, two Redis tests skipped.
- Follow-up targeted Redis run: both skipped cases passed, zero failed/skipped.
  This is 924 passed cases across two runs, not a single 924-case run.
- Redis 7.2.16 built from the official Redis GitHub tag; loopback only, persistence
  disabled, synthetic keys and in-memory SQLite. Process stopped, port closed,
  temporary data, source and binaries removed after verification.
- EF tool restore passed; model consistency passed (no pending model changes).
- Idempotent SQL Server migration script generated successfully without connecting
  to a database. It has **not** been reviewed/applied against a target database.
- No customer database, deployment, cloud resource, credentials or production
  security settings were changed.

Evidence remains private/ignored in `/workspace/04_Farm/.cloud/results/`:
`build.log`, `test.log`, `tests.trx`, `publish.log`, `redis/test.log`,
`redis/redis-tests.trx`, `redis/cleanup.json`, and `release/` model/script logs.
Published output: `/workspace/04_Farm/.cloud/publish/`.

SHA-256:

| Artifact | Hash |
|---|---|
| Published EnterpriseERP.Web.dll | `6122ed55a9d4846f7237b1b3b8781b4a2d214f8faed1aeeeda62ce567fd6b594` |
| release/migration-review.sql | `f3e0370b28d99cb858ee1091fdb71b9a206753d4c8fb38e3995aba1e508f5619` |

## Exact conditions still needed for release

1. **Hosting target and access:** approved cloud provider/account, region, cost
   limit, actual staging and production hostname, authorized deployment identity,
   and intended Windows/IIS or explicitly reviewed alternative topology. The
   current managed development environment has no configured outbound identities,
   secret bindings, runtime variables or customer-specific network destinations.
   It is not evidence of a provisioned 24/7 host. Runtime/Hosting Bundle, supervisor,
   filesystem permissions and approved restart behavior require target verification.
2. **SQL Server:** exact server/database, actual runtime OS identity and SQL
   login/user, securely connected credential delivery, trusted encrypted connection,
   separate migration authority, schema/history comparison, reviewed migration and
   rollback. Existing data must be inventoried before migration. Runtime least
   privilege, SQL concurrency and financial/stock consistency remain unverified.
3. **Vault and gateway:** production requires OpenBao, its HTTPS address and
   approved identity/policy/bootstrap paths, typed runtime secret version/expiry,
   renewal/restart procedures and recovery. The separate Trusted Platform
   PublicGateway/vault management package was not present in this workspace.
   If that topology is selected, supply its reviewed source/artifact and exact
   trusted backend/gateway host/IP configuration. Do not use LegacyLocal or turn
   off RequireInProduction to evade the gate.
4. **TLS and persistent keys:** approved public DNS/certificate, AllowedHosts,
   trusted proxy origin, restricted persistent Data Protection key directory and
   stable app identity. IIS uses reviewed DPAPI identity/profile; Linux requires
   protected RSA certificate configuration. Verify actual session/CSRF survival
   across an authorized restart and key recovery. Do not reuse temporary test keys.
5. **Backup and rollback:** dated successful restore rehearsal for SQL, protected
   keys/certificates and vault state, defined retention/storage, acceptable data
   loss/downtime, rollback owner and previous compatible app package. No such
   operational evidence was provided here.
6. **Business acceptance:** approved real Bangladesh company ID, BDT denomination,
   timezone, payroll/tax/accounting inputs and opening balances; independent real
   maker/reviewer operators; accepted module limits and signed customer UAT.
   Test login/permissions, tenant isolation, approval flows, duplicate/concurrent
   submission, stock/ledger reconciliation, browser/mobile and languages on an
   explicitly authorized staging clone. Confirm QuestPDF license eligibility,
   MFA/recovery ownership and monitoring/alert owners. Synthetic data is not approval.

## Next executable operator action

After the target is connected and runtime identity/private output are confirmed,
use the existing bounded **read-only** staging collector with the tested DLL:

```text
dotnet /approved/publish/EnterpriseERP.Web.dll --verify-staging-readonly /private/new-evidence.json
```

The operator must supply the exact environment settings in
[STAGING_READONLY_PREFLIGHT.md](STAGING_READONLY_PREFLIGHT.md), including explicit
server/database/login/user/process expectations, company ID, timezone and webroot.
Do not place passwords in chat, source or command arguments. This collector does
not migrate, seed, provision an admin, change keys, restart the app or approve
production. Follow with separately authorized staging migration/UAT/restore and
review the [deployment runbook](LIVE_DEPLOYMENT_RUNBOOK_BN.md).

No new business feature was started in this follow-up: same-day release readiness
took priority. Farm history pagination and fattening plan amendments remain
documented development opportunities, not silently completed scope.
