param([string]$Configuration='Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location (Join-Path $PSScriptRoot '..')
New-Item -ItemType Directory -Path 'evidence' -Force | Out-Null
function Invoke-Dotnet([string[]]$Arguments,[string]$Log) {
    & dotnet @Arguments 2>&1 | Tee-Object -FilePath $Log
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed ($LASTEXITCODE). See $Log" }
}
Invoke-Dotnet -Arguments @('--info') -Log 'evidence/sdk.txt'
Invoke-Dotnet -Arguments @('restore','EnterpriseERP.slnx','-m:1') -Log 'evidence/restore.log'
Invoke-Dotnet -Arguments @('build','EnterpriseERP.slnx','-c',$Configuration,'--no-restore','-m:1','-p:UseSharedCompilation=false','-nodeReuse:false') -Log 'evidence/build.log'
Invoke-Dotnet -Arguments @('test','Tests/EnterpriseERP.UnitTests/EnterpriseERP.UnitTests.csproj','-c',$Configuration,'--no-build','--no-restore','--logger','trx;LogFileName=tests.trx','--results-directory','evidence/test-results') -Log 'evidence/test.log'
Invoke-Dotnet -Arguments @('tool','restore') -Log 'evidence/tool-restore.log'
Invoke-Dotnet -Arguments @('ef','migrations','has-pending-model-changes','--project','EnterpriseERP.Web','--configuration',$Configuration,'--no-build') -Log 'evidence/model-check.log'
Invoke-Dotnet -Arguments @('ef','migrations','script','--idempotent','--project','EnterpriseERP.Web','--configuration',$Configuration,'--no-build','--output','evidence/schema-reviewed.sql') -Log 'evidence/migration-script.log'
Invoke-Dotnet -Arguments @('publish','EnterpriseERP.Web','-c',$Configuration,'--no-restore','--output','artifacts/publish','-m:1','-p:UseSharedCompilation=false','-nodeReuse:false') -Log 'evidence/publish.log'
Write-Host 'SOURCE VERIFICATION PASSED. SQL Server execution, IIS/TLS and customer UAT remain separate required live gates.'
