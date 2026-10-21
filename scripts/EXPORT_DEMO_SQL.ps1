param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Security.SecureString]$Password,
    [switch]$IncludeEnterprise,
    [switch]$ConfirmFictionalPolicies
)
$ErrorActionPreference = 'Stop'
if ($IncludeEnterprise -and -not $ConfirmFictionalPolicies) { throw 'Confirm fictional policy values and simulated approvals explicitly before creating the optional enterprise demonstration.' }
$root = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 SDK is required.' }
if (-not [IO.Path]::IsPathFullyQualified($OutputDirectory) -or $OutputDirectory.StartsWith('\\') -or $OutputDirectory.StartsWith('//')) {
    throw 'Choose an absolute private local output directory, not a network location.'
}
if (Test-Path -LiteralPath $OutputDirectory) { throw 'The output directory already exists. Choose a new directory; overwrite is disabled.' }
$fullOutput = [IO.Path]::GetFullPath($OutputDirectory)
$publicRoots = @((Join-Path $root 'EnterpriseERP.Web/wwwroot'))
if ($env:ASPNETCORE_WEBROOT) { $publicRoots += $env:ASPNETCORE_WEBROOT }
foreach ($publicRoot in $publicRoots) {
    $publicPath = [IO.Path]::GetFullPath($publicRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ($fullOutput.Equals($publicPath, [StringComparison]::OrdinalIgnoreCase) -or $fullOutput.StartsWith($publicPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Sensitive demo exports and the source database must not be created inside the application public web root.'
    }
}
$ancestor = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputDirectory)))
while ($null -ne $ancestor) {
    if ($ancestor.Exists -and ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Symbolic-link/reparse-point output paths are not accepted.' }
    $ancestor = $ancestor.Parent
}
$schema = Join-Path $root 'evidence/schema-reviewed.sql'
if (-not (Test-Path -LiteralPath $schema)) { throw 'Generate and review evidence/schema-reviewed.sql from the committed migrations first. This export script never connects to SQL Server.' }
if (-not $Password) { $Password = Read-Host 'Choose DEMO login password: 12+ characters, uppercase, lowercase, digit, symbol' -AsSecureString }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
# A new isolated source avoids exporting an existing demo after it has been used or modified.
$source = Join-Path $OutputDirectory 'farm-demo-export-source.db'
$connection = 'Data Source=' + $source
$names = @('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','Database__Provider','ConnectionStrings__DefaultConnection','FARM_ALLOW_DEMO_EXPORT')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    & (Join-Path $PSScriptRoot 'SEED_DEMO.ps1') -Provider Sqlite -ConnectionString $connection -Password $Password -IncludeEnterprise:$IncludeEnterprise -ConfirmFictionalPolicies:$ConfirmFictionalPolicies
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:Database__Provider = 'Sqlite'
    $env:ConnectionStrings__DefaultConnection = $connection
    $env:FARM_ALLOW_DEMO_EXPORT = 'YES'
    $data = Join-Path $OutputDirectory '02-demo-data.sql'
    & dotnet run --no-launch-profile --project (Join-Path $root 'EnterpriseERP.Web/EnterpriseERP.Web.csproj') -- --export-demo-sql $data
    if ($LASTEXITCODE -ne 0) { throw 'Demo SQL export failed. Source files are preserved for review; no existing file was overwritten.' }
    # File.Copy overwrite=false guards concurrent/unexpected destination creation too.
    [IO.File]::Copy($schema, (Join-Path $OutputDirectory '01-schema-reviewed.sql'), $false)
    $readme = @'
PRIVATE DEMO SQL PACKAGE
1. Verify a new, empty SQL Server database name contains demo and excludes prod.
2. Review and apply 01-schema-reviewed.sql to that isolated database.
3. Review and apply 02-demo-data.sql in the same database. It refuses populated application/Identity tables.
4. Login farmdemo1@example.invalid through farmdemo5@example.invalid with the password you chose locally.
The data script and source SQLite database contain sensitive Identity password hashes. Keep this folder private.
No plaintext password or MFA/recovery/token material is included. Do not upload this folder to a public repository.
The content fingerprint detects accidental changes; it is not proof against a database administrator forging data.
No SQL Server connection, schema application, or import was performed by the export command.
SQL Server runtime validation remains a separate release check. There is no production-use claim.
'@
    if ($IncludeEnterprise) { $readme += "`nENTERPRISE DEMO: Additional reviewers farmreviewer1@example.invalid through farmreviewer5@example.invalid use your chosen local password. All configured rates, jurisdictions and maker/checker examples are fictional demonstrations, not real accounting or legal approval. Five fictional internal messages were stored for the demo; no payment or external message was sent.`n" }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'README.txt'), $readme, [Text.UTF8Encoding]::new($false))
    Write-Host 'Created private schema + data SQL package. Your chosen password is not saved or printed. No SQL Server was contacted.'
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    $Password = $null
}
