$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$config=Get-Content (Join-Path $root 'EnterpriseERP.Web/appsettings.json') -Raw | ConvertFrom-Json
if($env:ASPNETCORE_ENVIRONMENT -ne 'Production'){throw 'Set ASPNETCORE_ENVIRONMENT=Production for production checks'}
if($env:DOTNET_ENVIRONMENT -and $env:DOTNET_ENVIRONMENT -ne 'Production'){throw 'DOTNET_ENVIRONMENT must not override Production'}
if($env:FARM_ALLOW_DEMO_SEED -eq 'YES' -or $env:FARM_DEMO_PASSWORD){throw 'Demo seed credentials must never be present in production'}
if($config.ConnectionStrings.DefaultConnection){throw 'Do not embed production database credentials in appsettings.json; use secret store or environment'}
if($env:TrustedVault__Mode -ne 'OpenBao'){throw 'Set TrustedVault__Mode=OpenBao and reviewed trusted-server bootstrap files for the secure production profile'}
if($env:ASPNETCORE_FORWARDEDHEADERS_ENABLED -eq 'true'){throw 'Automatic forwarded-header trust is prohibited. Use exact TrustedProxy IP/public origin when using PublicGateway.'}
Write-Host 'Static configuration gates passed. This is NOT a runtime, security or UAT approval.'
