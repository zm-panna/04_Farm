param(
 [Parameter(Mandatory=$true)][string]$Email,
 [Parameter(Mandatory=$true)][string]$Company,
 [Parameter(Mandatory=$true)][string]$Branch,
 [string]$ConnectionString
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Set-Location (Join-Path $PSScriptRoot '..')
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK is required.' }
if ($ConnectionString) { throw 'Do not supply SQL credentials on the command line. Configure the trusted OpenBao backend instead.' }
if ($env:ASPNETCORE_ENVIRONMENT -ne 'Production' -or $env:DOTNET_ENVIRONMENT -ne 'Production') { throw 'Set both ASPNETCORE_ENVIRONMENT and DOTNET_ENVIRONMENT to Production before production administrator provisioning.' }
if ($env:TrustedVault__Mode -ne 'OpenBao') { throw 'Production bootstrap requires TrustedVault__Mode=OpenBao and reviewed trusted-server bootstrap files.' }
if ($env:Database__Provider -eq 'Sqlite') { throw 'Production bootstrap script requires SQL Server. Use SEED_DEMO.ps1 for a disposable demo.' }
$names=@('ConnectionStrings__DefaultConnection','Database__Provider','FARM_BOOTSTRAP_EMAIL','FARM_BOOTSTRAP_PASSWORD','FARM_BOOTSTRAP_COMPANY','FARM_BOOTSTRAP_BRANCH')
$previous=@{}
foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$secure=Read-Host 'Initial admin password (12+ characters, upper/lower/digit/symbol)' -AsSecureString
$pointer=[IntPtr]::Zero
try {
 $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
 $env:FARM_BOOTSTRAP_PASSWORD=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
 # SQL connection is supplied by the trusted vault during application startup.
 $env:Database__Provider='SqlServer'
 $env:FARM_BOOTSTRAP_EMAIL=$Email
 $env:FARM_BOOTSTRAP_COMPANY=$Company
 $env:FARM_BOOTSTRAP_BRANCH=$Branch
 dotnet run --no-launch-profile --project EnterpriseERP.Web -- --bootstrap-admin
 if($LASTEXITCODE -ne 0){throw 'Bootstrap failed. No automatic retries or database replacement are performed.'}
 Write-Host 'Initial administrator provisioned. Enroll authenticator MFA and record offline recovery codes through Account Security.'
}
finally {
 if($pointer -ne [IntPtr]::Zero){[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)}
 foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
 $secure=$null;$ConnectionString=$null
}
