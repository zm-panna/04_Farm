param([string]$StagingUrl='', [switch]$RunTests, [switch]$Publish)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=Split-Path -Parent $PSScriptRoot
$folder=Join-Path $root 'release-evidence'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$result=[ordered]@{TimestampUtc=[DateTime]::UtcNow.ToString('o');Source='NOT_RUN';Staging='NOT_RUN';SqlServer='NOT_VERIFIED';IIS='NOT_VERIFIED';UAT='NOT_VERIFIED';ProductionApproved=$false;Decision='BLOCKED';Blockers=@()}
try {
    # Tests and publishing are mandatory in the canonical source gate; switches retained for legacy callers.
    & (Join-Path $PSScriptRoot 'VERIFY_SOURCE.ps1')
    $result.Source='PASS'
    if ($StagingUrl) { & (Join-Path $PSScriptRoot 'STAGING_SMOKE_TEST.ps1') -BaseUrl $StagingUrl; $result.Staging='ANONYMOUS_HTTP_ONLY' }
    $result.Blockers=@('Reviewed SQL Server application/rollback/concurrency evidence required','IIS/TLS/secret and backup-restore evidence required','Accepted licensed-module scope and signed customer UAT required')
} catch { $result.Blockers+= $_.Exception.Message }
$result | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $folder 'FINAL_RELEASE_RESULTS.json')
Write-Warning 'Source verification does not approve live release. Complete the separate dated deployment and UAT checklist.'
exit 2
