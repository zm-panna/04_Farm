[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [ValidateRange(1024,65535)][int]$HttpsPort = 7443
)
$ErrorActionPreference='Stop'
if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') { throw 'Run this operator script on the authorized Windows PC.' }
$publish=[IO.Path]::GetFullPath($PublishDirectory)
$dll=Join-Path $publish 'EnterpriseERP.Web.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Choose the verified dotnet publish output folder.' }
if ($env:TrustedVault__Mode -ne 'OpenBao') { throw 'This production launcher requires TrustedVault__Mode=OpenBao and reviewed trusted-server bootstrap files. See docs/TRUSTED_BACKEND_AND_VAULT_BN.md.' }
if ([string]::IsNullOrWhiteSpace($env:Security__DataProtection__KeyDirectory)) { throw 'Provision a persistent private key directory outside the publish/wwwroot folder.' }
if ($env:Kestrel__Certificates__Default__Path -or $env:Kestrel__Certificates__Default__Password) { throw 'The vault-protected Windows launcher uses a pre-provisioned Windows certificate store, not a PFX path/password.' }
if ([string]::IsNullOrWhiteSpace($env:Kestrel__Certificates__Default__Subject) -or $env:Kestrel__Certificates__Default__Store -ne 'My' -or $env:Kestrel__Certificates__Default__Location -notin @('CurrentUser','LocalMachine')) { throw 'Configure the reviewed localhost certificate Subject, Store=My and Location=CurrentUser or LocalMachine. Its private key access and trust must already be provisioned.' }
if ($env:Kestrel__Certificates__Default__AllowInvalid -eq 'true') { throw 'Invalid TLS certificates are not accepted.' }
$env:ASPNETCORE_ENVIRONMENT='Production'
$env:DOTNET_ENVIRONMENT='Production'
$env:Database__Provider='SqlServer'
$env:AllowedHosts='localhost'
$env:Security__DataProtection__ProtectionMode='Dpapi'
if ([string]::IsNullOrWhiteSpace($env:Security__DataProtection__ApplicationName)) { throw 'Set a stable application name for this installation before startup.' }
Write-Host 'Starting the verified application on this Windows PC, loopback HTTPS only. SQL Server migrations, certificate trust, key-folder permissions and backups must already be reviewed.'
Push-Location $publish
try { & dotnet $dll --urls "https://localhost:$HttpsPort"; if ($LASTEXITCODE -ne 0) { throw "Application exited with code $LASTEXITCODE." } }
finally { Pop-Location }
