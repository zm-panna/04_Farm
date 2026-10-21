param(
    [ValidateSet('Sqlite','SqlServer')][string]$Provider = 'Sqlite',
    [string]$ConnectionString,
    [Security.SecureString]$Password,
    [switch]$IncludeEnterprise,
    [switch]$TenTenants,
    [switch]$ConfirmFictionalPolicies
)
$ErrorActionPreference = 'Stop'
if ($IncludeEnterprise -and (-not $ConfirmFictionalPolicies -or $Provider -ne 'Sqlite')) { throw 'Enterprise demo requires explicit fictional-policy confirmation and an isolated SQLite Development fixture. SQL Server import is a separate reviewed step.' }
if ($TenTenants -and -not $IncludeEnterprise) { throw 'TenTenants requires IncludeEnterprise.' }
$root = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK is required.' }
if (-not $ConnectionString) {
    if ($Provider -eq 'SqlServer') {
        $ConnectionString = $env:ConnectionStrings__DefaultConnection
        if (-not $ConnectionString) { throw 'Supply a connection string for an isolated migrated SQL Server DEMO database.' }
    } else {
        $folder = Join-Path $root 'demo-data'
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        $ConnectionString = 'Data Source=' + (Join-Path $folder $(if ($TenTenants) { 'farm-ten-demo.db' } else { 'farm-demo.db' }))
    }
}
if (-not $Password) { $Password = Read-Host 'Unique DEMO password: 12+ characters, uppercase, lowercase, digit, symbol' -AsSecureString }
$names = @('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','Database__Provider','ConnectionStrings__DefaultConnection','FARM_ALLOW_DEMO_SEED','FARM_DEMO_PASSWORD')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$pointer = [IntPtr]::Zero
try {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
    $env:FARM_DEMO_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $env:FARM_ALLOW_DEMO_SEED = 'YES'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:Database__Provider = $Provider
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
    $command = if ($IncludeEnterprise) { '--init-enterprise-demo' } elseif ($Provider -eq 'Sqlite') { '--init-demo' } else { '--seed-demo' }
    $extra = if ($TenTenants) { @("--ten-tenants") } else { @() }
    dotnet run --no-launch-profile --project (Join-Path $root 'EnterpriseERP.Web/EnterpriseERP.Web.csproj') -- $command @extra
    if ($LASTEXITCODE -ne 0) { throw 'Demo initialization failed. Read the error above; no automatic data deletion or retry is performed.' }
    if ($TenTenants) { Write-Host 'Ten-tenant fixture: farmdemo1..10@example.invalid and farmreviewer1..10@example.invalid.' }
    Write-Host 'Default five-tenant demo accounts: farmdemo1@example.invalid through farmdemo5@example.invalid.'
    if ($IncludeEnterprise) { Write-Host 'Additional synthetic reviewers: farmreviewer1@example.invalid through farmreviewer5@example.invalid. Policy values and reviews are fictional and must never be treated as actual approvals.' }
    Write-Host 'Only your supplied password is valid. Reruns preserve existing passwords and business data.'
}
finally {
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    $Password = $null
}
