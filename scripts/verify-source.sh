#!/usr/bin/env bash
# Source-only verification: never start hosting, seed data, or apply migrations.
set -euo pipefail
cd "$(dirname "$0")/.."
if (( $# != 0 )); then
    echo 'Usage: bash scripts/verify-source.sh (each run creates a new evidence directory)' >&2
    exit 2
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
mkdir -p artifacts/source-verification
run_dir=$(mktemp -d "$PWD/artifacts/source-verification/$(date -u +%Y%m%dT%H%M%SZ)-XXXXXXXX")
printf 'Source evidence and publish output: %s\n' "$run_dir"
if git rev-parse HEAD > "$run_dir/source-commit.txt" 2>/dev/null; then
    git status --porcelain > "$run_dir/source-status.txt"
fi
# pipefail preserves failures even though tee succeeds; stderr is retained too.
dotnet --info 2>&1 | tee "$run_dir/sdk.txt"
dotnet restore EnterpriseERP.slnx -m:1 2>&1 | tee "$run_dir/restore.log"
dotnet build EnterpriseERP.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false 2>&1 | tee "$run_dir/build.log"
dotnet test Tests/EnterpriseERP.UnitTests/EnterpriseERP.UnitTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory "$run_dir/test-results" 2>&1 | tee "$run_dir/test.log"
dotnet tool restore 2>&1 | tee "$run_dir/tool-restore.log"
dotnet ef migrations has-pending-model-changes --project EnterpriseERP.Web --configuration Release --no-build 2>&1 | tee "$run_dir/model-check.log"
dotnet ef migrations script --idempotent --project EnterpriseERP.Web --configuration Release --no-build --output "$run_dir/schema-reviewed.sql" 2>&1 | tee "$run_dir/migration-script.log"
dotnet publish EnterpriseERP.Web -c Release --no-build --no-restore --output "$run_dir/publish" 2>&1 | tee "$run_dir/publish.log"
echo 'SOURCE VERIFICATION PASSED. SQL Server execution, IIS/TLS and customer UAT remain separate live gates.'
