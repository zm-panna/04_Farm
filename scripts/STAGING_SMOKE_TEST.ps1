param([Parameter(Mandatory=$true)][string]$BaseUrl)
$ErrorActionPreference='Stop'
if($BaseUrl -notmatch '^https://'){throw 'HTTPS staging URL required'}
# This is explicitly anonymous HTTP reachability, not authenticated functional/UAT evidence.
$response=Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/')+'/Account/Login') -MaximumRedirection 0 -TimeoutSec 30
if([int]$response.StatusCode -ne 200 -or $response.Content -notmatch '__RequestVerificationToken'){throw 'Login page or antiforgery form missing'}
Write-Host 'Anonymous login page returned 200 and includes an antiforgery form.'
Write-Warning 'Authenticated browser workflow, SQLServer/IIS deployment and signed customer UAT remain separate requirements.'
