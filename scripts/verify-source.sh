#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p evidence
dotnet --info | tee evidence/sdk.txt
dotnet restore EnterpriseERP.slnx -m:1 | tee evidence/restore.log
dotnet build EnterpriseERP.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false | tee evidence/build.log
dotnet test Tests/EnterpriseERP.UnitTests/EnterpriseERP.UnitTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory evidence/test-results | tee evidence/test.log
dotnet tool restore | tee evidence/tool-restore.log
dotnet ef migrations has-pending-model-changes --project EnterpriseERP.Web --configuration Release --no-build | tee evidence/model-check.log
dotnet ef migrations script --idempotent --project EnterpriseERP.Web --configuration Release --no-build --output evidence/schema-reviewed.sql | tee evidence/migration-script.log
dotnet publish EnterpriseERP.Web -c Release --no-restore --output artifacts/publish -m:1 -p:UseSharedCompilation=false -nodeReuse:false | tee evidence/publish.log
echo 'SOURCE VERIFICATION PASSED. SQL Server execution, IIS/TLS and customer UAT remain separate live gates.'
