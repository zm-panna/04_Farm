param([string]$Configuration='Release', [string]$EvidenceDirectory='')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location (Join-Path $PSScriptRoot '..')
# Keep each run separate: committed historical logs and existing publish output are never overwritten.
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path (Get-Location) ('artifacts/source-verification/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
}
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'EvidenceDirectory must be a new directory; existing evidence is never overwritten.' }
New-Item -ItemType Directory -Path $EvidenceDirectory -ErrorAction Stop | Out-Null
Write-Host "Source evidence and publish output: $EvidenceDirectory"
function Invoke-Dotnet([string[]]$Arguments,[string]$Log) {
    & dotnet @Arguments 2>&1 | Tee-Object -FilePath $Log
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed ($LASTEXITCODE). See $Log" }
}
Invoke-Dotnet -Arguments @('--info') -Log (Join-Path $EvidenceDirectory 'sdk.txt')
Invoke-Dotnet -Arguments @('restore','EnterpriseERP.slnx','-m:1') -Log (Join-Path $EvidenceDirectory 'restore.log')
Invoke-Dotnet -Arguments @('build','EnterpriseERP.slnx','-c',$Configuration,'--no-restore','-m:1','-p:UseSharedCompilation=false','-nodeReuse:false') -Log (Join-Path $EvidenceDirectory 'build.log')
Invoke-Dotnet -Arguments @('test','Tests/EnterpriseERP.UnitTests/EnterpriseERP.UnitTests.csproj','-c',$Configuration,'--no-build','--no-restore','--logger','trx;LogFileName=tests.trx','--results-directory',(Join-Path $EvidenceDirectory 'test-results')) -Log (Join-Path $EvidenceDirectory 'test.log')
Invoke-Dotnet -Arguments @('tool','restore') -Log (Join-Path $EvidenceDirectory 'tool-restore.log')
Invoke-Dotnet -Arguments @('ef','migrations','has-pending-model-changes','--project','EnterpriseERP.Web','--configuration',$Configuration,'--no-build') -Log (Join-Path $EvidenceDirectory 'model-check.log')
Invoke-Dotnet -Arguments @('ef','migrations','script','--idempotent','--project','EnterpriseERP.Web','--configuration',$Configuration,'--no-build','--output',(Join-Path $EvidenceDirectory 'schema-reviewed.sql')) -Log (Join-Path $EvidenceDirectory 'migration-script.log')
Invoke-Dotnet -Arguments @('publish','EnterpriseERP.Web','-c',$Configuration,'--no-restore','--output',(Join-Path $EvidenceDirectory 'publish'),'-m:1','-p:UseSharedCompilation=false','-nodeReuse:false') -Log (Join-Path $EvidenceDirectory 'publish.log')
Write-Host 'SOURCE VERIFICATION PASSED. SQL Server execution, IIS/TLS and customer UAT remain separate required live gates.'
