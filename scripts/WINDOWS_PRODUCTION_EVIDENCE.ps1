param([Parameter(Mandatory=$true)][string]$StagingUrl,[string]$StagingConnection,[switch]$ApplyStagingMigration)
$ErrorActionPreference='Stop'
if($ApplyStagingMigration){throw 'Automatic migration application is disabled in this evidence collector. Review the committed SQL, back up staging and execute the explicit DBA-approved database update command.'}
if($StagingConnection){Write-Warning 'Do not pass secrets as command-line arguments; use the secure ConnectionStrings__DefaultConnection environment setting.'}
& (Join-Path $PSScriptRoot 'FINAL_RELEASE_VERIFY.ps1') -StagingUrl $StagingUrl -RunTests -Publish
exit $LASTEXITCODE
