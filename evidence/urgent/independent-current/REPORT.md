# Independent cold-copy verification

Completed 2026-10-19T18:09:22.989853+00:00.

## Results

- Fresh Release solution build: **passed, 0 warnings and 0 errors**.
- Current release tests: **805/805 passed**, 0 failed, 0 skipped. See `logs/release-results/cold-release-tests.trx`.
- Existing independent audit probes: **20/20 passed**, 0 failed, 0 skipped. These ran in a separate audit-only project and are **not additional release-source tests**. See `logs/probe-results/legacy-audit-probes.trx`.
- New account onboarding journey: **47/47 real localhost HTTP checks passed**. It used a disposable synthetic SQLite fixture, exercised actual forms/account creation/offline ownership review/HR and customer linking, and shut down its temporary host. See `http-onboarding/results.json`.

## Source and binary identity

All **428** runtime/configuration/test/script manifest entries matched before execution and again afterward in both the independent copy and authoritative source.

Final manifest SHA-256: `6ef20f228615674716543701c45d9b73669a48f5345a7e2ea9b4517cbb965dfe`.

Cold-built web DLL SHA-256: `4b534a62897989d2e08c464ce2a578793d564738c00bca0be55ee7f6d852d765`. The HTTP fixture confirmed the DLL did not change during its run. This is the independent copy's binary hash, not a claim of bit-for-bit identity with the coordinator's separate build.

The independent source copy started without any `bin`, `obj`, `evidence`, `runtime`, runtime database or log artifacts. It included `.config`. The shared .NET SDK (10.0.402) and NuGet cache were reused; application binaries were freshly built. The only post-checkpoint synchronization was `scripts/http-enterprise-onboarding.py`; comparison of the initial and final manifests confirmed no compiled-source changes. The old probe source was kept outside the release source tree.

## Reproduction

Use the existing writable SDK/NuGet environment with the source copy as working directory:

- `dotnet restore EnterpriseERP.slnx --disable-parallel -m:1`
- `dotnet build EnterpriseERP.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false`
- `dotnet test Tests/EnterpriseERP.UnitTests/EnterpriseERP.UnitTests.csproj -c Release --no-build --no-restore -m:1`
- Restore/build the separate audit-only project, then `dotnet test AuditProbes.csproj -c Release --no-build --no-restore -m:1 --filter FullyQualifiedName~EnterpriseIndependentAuditTests`.
- From the cold source root, `python3 scripts/http-enterprise-onboarding.py`.

The supplied audit-only project assumes it is a sibling of the cold `source` directory; arrange that layout before reproducing. Exact executed output and TRX files are included. Two initial environment-only restore attempts failed before compilation: the first used an unwritable default runtime path; the second omitted single-node MSBuild and its worker named pipes were denied. Existing supported environment settings and single-node flags resolved both, without escalation or source changes. Those attempt logs are retained.

## Limits

This independent run did not rerun the other 855 HTTP checks. No SQL Server, IIS/TLS, real browser, customer policy acceptance or backup restore rehearsal was performed. These results establish local code/test evidence only. **Production approval remains false.**
