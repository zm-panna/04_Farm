[CmdletBinding()]
param([Parameter(Mandatory=$true)][switch]$ConfirmFictionalPolicies,[Parameter(Mandatory=$true)][string]$WebDll)
$ErrorActionPreference='Stop'
if (-not $ConfirmFictionalPolicies) { throw 'Explicit confirmation of fictional demo data is required.' }
if ($env:ASPNETCORE_ENVIRONMENT -ne 'Development' -or $env:Database__Provider -ne 'Sqlite') { throw 'This helper is only for an isolated Development SQLite demonstration. Production/bootstrap stays unconfigured.' }
if ([string]::IsNullOrWhiteSpace($env:FARM_DEMO_PASSWORD) -or $env:FARM_DEMO_PASSWORD.Length -lt 12) { throw 'Provision a unique temporary demo password through a secure local environment variable; never commit or print it.' }
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) { throw 'Select a new empty isolated database file with demo in its name and no prod in its name.' }
$dll=[IO.Path]::GetFullPath($WebDll)
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Verified Web DLL not found.' }
$previous=$env:FARM_ALLOW_DEMO_SEED
try {
 $env:FARM_ALLOW_DEMO_SEED='YES'
 & dotnet $dll --init-enterprise-demo
 if ($LASTEXITCODE -ne 0) { throw "Enterprise demo initialization failed ($LASTEXITCODE); inspect the non-secret error." }
 Write-Host 'Five fictional enterprise companies and synthetic maker/reviewer examples are ready. Rates, tax classifications and approvals are demonstration data, never statutory guidance. No payment or communication was sent.'
} finally { $env:FARM_ALLOW_DEMO_SEED=$previous }
