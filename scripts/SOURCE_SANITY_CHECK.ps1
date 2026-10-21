$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$checks=@('EnterpriseERP.Web/Program.cs','EnterpriseERP.Web/TenantContext.cs','EnterpriseERP.Web/Controllers/SupportController.cs','EnterpriseERP.Web/Views/Support/Tickets.cshtml','EnterpriseERP.Web/wwwroot/js/support-tickets.js','EnterpriseERP.Web/SupportService.cs','EnterpriseERP.slnx')
foreach($path in $checks){ if(-not(Test-Path $path)){throw "Missing $path"}; Write-Host "OK $path" }
if(-not (Get-Command dotnet -ErrorAction SilentlyContinue)){throw 'Install the .NET 10 SDK before build and testing'}
& (Join-Path $PSScriptRoot 'VERIFY_SOURCE.ps1')
