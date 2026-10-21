param([string]$ConnectionString)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'VERIFY_SOURCE.ps1')
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Source verification failed' }
Write-Host 'No database was modified. Review evidence/schema-reviewed.sql, backup staging, and run the committed migration under an authorized deployment identity.'
Write-Host 'dotnet ef database update --project EnterpriseERP.Web --configuration Release --no-build'
if ($ConnectionString) { Write-Warning 'Avoid passing secrets on the command line. Set ConnectionStrings__DefaultConnection using your secret manager.' }
