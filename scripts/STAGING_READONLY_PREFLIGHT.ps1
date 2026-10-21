[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ApplicationDll,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$IisSiteName,
    [string]$IisApplicationPool
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Deliberately no connection-string/password parameter, build, publish, migration, seed, restart or ACL operation.
if ($env:OS -ne 'Windows_NT') { throw 'This operator wrapper requires Windows; use the direct CLI for authorized source-side validation.' }
if ([IO.Path]::GetPathRoot($ApplicationDll) -notmatch '^[A-Za-z]:\\$' -or $ApplicationDll.StartsWith('\\') -or -not [IO.Path]::IsPathRooted($ApplicationDll) -or -not (Test-Path -LiteralPath $ApplicationDll -PathType Leaf) -or [IO.Path]::GetFileName($ApplicationDll) -ne 'EnterpriseERP.Web.dll') {
    throw 'Select the existing published EnterpriseERP.Web.dll using an absolute local path.'
}
if ([IO.Path]::GetPathRoot($OutputPath) -notmatch '^[A-Za-z]:\\$' -or $OutputPath.StartsWith('\\') -or -not [IO.Path]::IsPathRooted($OutputPath) -or [IO.Path]::GetExtension($OutputPath) -ne '.json' -or (Test-Path -LiteralPath $OutputPath)) {
    throw 'Select a new absolute private .json evidence path. Existing reports are never overwritten.'
}
if ([string]::IsNullOrWhiteSpace($IisSiteName) -ne [string]::IsNullOrWhiteSpace($IisApplicationPool)) {
    throw 'Optional IIS inspection requires both IisSiteName and IisApplicationPool.'
}
$dotnet=Get-Command dotnet -CommandType Application -ErrorAction Stop
# The application validates ALL inputs before connecting, reserves a new private output, and exits before host initialization.
& $dotnet.Source $ApplicationDll --verify-staging-readonly $OutputPath
$collectorExit=$LASTEXITCODE
if ($collectorExit -eq 4) { exit 4 }
if ($IisSiteName -and (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
    $iisPath=[IO.Path]::ChangeExtension($OutputPath, '.iis.json')
    & (Join-Path $PSScriptRoot 'STAGING_IIS_READONLY.ps1') -SiteName $IisSiteName -ApplicationPool $IisApplicationPool -ApplicationDll $ApplicationDll -OutputPath $iisPath
    # The sidecar has its own status. Never substitute its limited metadata checks for live TLS/restart/UAT evidence.
}
Write-Warning 'Read the collector and optional IIS sidecar statuses. ProductionApproved is always false; manual deployment and customer acceptance gates remain open.'
exit $collectorExit
