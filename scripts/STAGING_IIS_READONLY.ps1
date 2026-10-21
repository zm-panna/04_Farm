[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SiteName,
    [Parameter(Mandatory=$true)][string]$ApplicationPool,
    [Parameter(Mandatory=$true)][string]$ApplicationDll,
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Local configuration inspection only. No CommitChanges, key generation, permission changes, start/stop/recycle or HTTP requests.
function Hash-Value([string]$Value) {
    $sha=[Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value)))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function Is-Within([string]$Path,[string]$Root) {
    $rootPath=[IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    return [IO.Path]::GetFullPath($Path).StartsWith($rootPath,[StringComparison]::OrdinalIgnoreCase)
}
$stream=$null
try {
    if ($env:OS -ne 'Windows_NT') { throw 'Windows required.' }
    if ($env:FARM_PREFLIGHT_PRIVATE_OUTPUT_CONFIRMED -cne 'YES' -or [string]::IsNullOrWhiteSpace($env:FARM_PREFLIGHT_WEBROOT)) { throw 'Private output confirmation and explicit webroot required.' }
    if ([IO.Path]::GetPathRoot($OutputPath) -notmatch '^[A-Za-z]:\\$' -or -not [IO.Path]::IsPathRooted($OutputPath) -or $OutputPath.StartsWith('\\') -or [IO.Path]::GetFileName($OutputPath).Contains(':') -or [IO.Path]::GetExtension($OutputPath) -ne '.json') { throw 'New local JSON path required.' }
    $parent=Get-Item -LiteralPath ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) -ErrorAction Stop
    if ((Is-Within $OutputPath $env:FARM_PREFLIGHT_WEBROOT) -or (Is-Within $OutputPath ([IO.Path]::GetDirectoryName($ApplicationDll)))) { throw 'Evidence must be outside webroot and application directory.' }
    $roots=@($parent.FullName,$env:FARM_PREFLIGHT_WEBROOT,[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ApplicationDll)))
    foreach($root in $roots) {
        $rootDirectory=Get-Item -LiteralPath $root -ErrorAction Stop
        if (-not $rootDirectory.PSIsContainer) { throw 'Existing directories required.' }
        for ($dir=$rootDirectory; $null -ne $dir; $dir=$dir.Parent) {
            if (($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Evidence and public/application roots cannot contain reparse points.' }
        }
    }
    $stream=[IO.File]::Open($OutputPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
} catch { throw 'IIS evidence output validation failed. Use a new local file in an existing private non-web directory. No IIS settings were changed.' }
$result=[ordered]@{Format='farm-iis-readonly-v1';TimestampUtc=[DateTime]::UtcNow.ToString('o');ProductionApproved=$false;CollectorStatus='NOT_VERIFIED';Checks=@()}
$manager=$null
try {
    $assembly=Join-Path $env:windir 'System32\inetsrv\Microsoft.Web.Administration.dll'
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'IIS management assembly unavailable.' }
    Add-Type -Path $assembly
    $manager=New-Object Microsoft.Web.Administration.ServerManager
    $site=$manager.Sites[$SiteName]
    $pool=$manager.ApplicationPools[$ApplicationPool]
    if ($null -eq $site -or $null -eq $pool) { throw 'Expected site/pool unavailable.' }
    $app=$site.Applications['/']
    $physical=[Environment]::ExpandEnvironmentVariables($app.VirtualDirectories['/'].PhysicalPath)
    $correctTarget=$app.ApplicationPoolName -ceq $ApplicationPool -and ([IO.Path]::GetFullPath($physical).TrimEnd([IO.Path]::DirectorySeparatorChar) -ieq [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ApplicationDll)).TrimEnd([IO.Path]::DirectorySeparatorChar))
    $result.Checks+=@{Id='siteApplicationTarget';Status=$(if($correctTarget){'PASS'}else{'FAIL'});Summary='Configured root application, pool and published DLL directory match the selected target.';SiteSha256=(Hash-Value $SiteName);PoolSha256=(Hash-Value $ApplicationPool)}
    $identityType=$pool.ProcessModel.IdentityType.ToString()
    $identity=switch($identityType){'ApplicationPoolIdentity' {'IIS APPPOOL\'+$ApplicationPool};'SpecificUser' {$pool.ProcessModel.UserName};'NetworkService' {'NT AUTHORITY\NETWORK SERVICE'};'LocalService' {'NT AUTHORITY\LOCAL SERVICE'};'LocalSystem' {'NT AUTHORITY\SYSTEM'};default {''}}
    $identityMatches=$identity -and $identity -ieq $env:FARM_PREFLIGHT_EXPECTED_PROCESS_IDENTITY -and $identityType -ne 'LocalSystem'
    $result.Checks+=@{Id='configuredPoolIdentity';Status=$(if($identityMatches){'PASS'}else{'FAIL'});Summary='Configured pool identity matches the attested process identity and is not LocalSystem. This does not prove a live worker token or SQL identity.';IdentityType=$identityType;IdentitySha256=(Hash-Value $identity);LoadUserProfile=$pool.ProcessModel.LoadUserProfile}
    $runtimeOk=[string]::IsNullOrEmpty($pool.ManagedRuntimeVersion) -and -not $pool.Enable32BitAppOnWin64
    $result.Checks+=@{Id='poolRuntimeSettings';Status=$(if($runtimeOk){'PASS'}else{'FAIL'});Summary='Expected no managed CLR and 64-bit process for the published ASP.NET Core application.'}
    $https=@($site.Bindings | Where-Object {$_.Protocol -eq 'https'})
    $certBound=$https.Count -gt 0 -and @($https | Where-Object {$null -eq $_.CertificateHash -or $_.CertificateHash.Length -eq 0}).Count -eq 0
    $result.Checks+=@{Id='httpsBindingMetadata';Status=$(if($certBound){'PASS'}else{'FAIL'});Summary='HTTPS binding(s) have configured certificate hashes. Certificate trust, hostname, expiry and external TLS negotiation are NOT_VERIFIED.';HttpsBindingCount=$https.Count}
    $result.Checks+=@{Id='observedState';Status=$(if($site.State.ToString() -eq 'Started' -and $pool.State.ToString() -eq 'Started'){'PASS'}else{'FAIL'});Summary='Observed site and pool start state only; no start/stop/recycle was performed.'}
    $result.CollectorStatus=if(@($result.Checks | Where-Object {$_.Status -eq 'FAIL'}).Count -gt 0){'FAIL'}else{'PASS'}
} catch {
    $result.Checks+=@{Id='iisInspection';Status='NOT_VERIFIED';Summary='Local IIS configuration could not be inspected with current access. Do not elevate the application account for this tool; request an independent DBA/operations review. Error details are suppressed.'}
    $result.CollectorStatus='NOT_VERIFIED'
} finally {
    if ($null -ne $manager) { $manager.Dispose() }
    $result.Checks+=@{Id='tlsAclKeysAndRestart';Status='NOT_VERIFIED';Summary='External certificate/hostname/TLS, ACLs, secrets, key-ring persistence, worker identity and cookie survival after a separately approved restart require operator evidence.'}
    try {
        $bytes=[Text.Encoding]::UTF8.GetBytes(($result | ConvertTo-Json -Depth 8))
        $stream.Write($bytes,0,$bytes.Length); $stream.Flush()
    } finally { $stream.Dispose() }
}
Write-Host ('Read-only IIS sidecar saved. Collector='+$result.CollectorStatus+'; productionApproved=false.')
