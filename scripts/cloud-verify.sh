#!/usr/bin/env bash
# Build and test without starting the application or contacting a database.
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
mkdir -p .cloud/results
dotnet --info > .cloud/results/sdk.txt
dotnet restore EnterpriseERP.slnx -m:1 2>&1 | tee .cloud/results/restore.log
dotnet build EnterpriseERP.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false 2>&1 | tee .cloud/results/build.log
dotnet test Tests/EnterpriseERP.UnitTests/EnterpriseERP.UnitTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory .cloud/results 2>&1 | tee .cloud/results/test.log
dotnet tool restore 2>&1 | tee .cloud/results/tool-restore.log
dotnet ef migrations has-pending-model-changes --project EnterpriseERP.Web --configuration Release --no-build 2>&1 | tee .cloud/results/model-check.log
dotnet ef migrations script --idempotent --project EnterpriseERP.Web --configuration Release --no-build --output .cloud/results/migration-review.sql 2>&1 | tee .cloud/results/migration-script.log
dotnet publish EnterpriseERP.Web -c Release --no-build --no-restore --output .cloud/publish 2>&1 | tee .cloud/results/publish.log
echo 'Cloud source verification passed. Production hosting is a separate release gate.'
