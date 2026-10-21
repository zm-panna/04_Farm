param([string]$MigrationName='')
$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet tool restore
if($LASTEXITCODE -ne 0){throw 'EF tool restore failed'}
if($MigrationName){
    Write-Warning 'Generate a new migration only after reviewing intentional model changes. Do not regenerate the existing initial schema.'
    & dotnet ef migrations add $MigrationName --project EnterpriseERP.Web --output-dir Migrations
    if($LASTEXITCODE -ne 0){throw 'Migration generation failed'}
}
New-Item -ItemType Directory -Path evidence -Force | Out-Null
& dotnet ef migrations script --idempotent --project EnterpriseERP.Web -o evidence/schema-reviewed.sql
if($LASTEXITCODE -ne 0){throw 'SQL generation failed'}
Write-Host 'Review evidence/schema-reviewed.sql with the DBA. No database connection or schema change was performed.'
