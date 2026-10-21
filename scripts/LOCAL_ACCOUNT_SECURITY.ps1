param(
 [Parameter(Mandatory=$true)][ValidateSet('RecoverAccount','ProvisionRecoveryAdministrator')][string]$Operation,
 [Parameter(Mandatory=$true)][string]$ApplicationDll,
 [Parameter(Mandatory=$true)][string]$ActorEmail,
 [Parameter(Mandatory=$true)][string]$TargetEmail,
 [Parameter(Mandatory=$true)][string]$ApprovalReference,
 [ValidateSet('YES','NO')][string]$ResetMfa='NO'
)
$ErrorActionPreference='Stop'
if (-not (Test-Path -LiteralPath $ApplicationDll -PathType Leaf)) { throw 'Published application DLL not found' }
$ApplicationDll=(Resolve-Path -LiteralPath $ApplicationDll).Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 10 runtime is required' }
# Run locally as the approved application operator with its protected database/key-ring configuration.
# Do not enable PowerShell transcript, HTTP request-body logging, command tracing, or environment dumps.
function Read-PrivateValue([string]$Prompt) {
 $secure=Read-Host $Prompt -AsSecureString
 $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
 try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
 finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secure.Dispose() }
}
$variables=@('FARM_ALLOW_ACCOUNT_RECOVERY','FARM_RECOVERY_ACTOR_EMAIL','FARM_RECOVERY_ACTOR_PASSWORD','FARM_RECOVERY_ACTOR_TOTP','FARM_RECOVERY_TARGET_EMAIL','FARM_RECOVERY_NEW_PASSWORD','FARM_RECOVERY_RESET_MFA','FARM_RECOVERY_REFERENCE','FARM_ALLOW_PLATFORM_ADMIN_PROVISION','FARM_PLATFORM_ADMIN_ACTOR_EMAIL','FARM_PLATFORM_ADMIN_ACTOR_PASSWORD','FARM_PLATFORM_ADMIN_ACTOR_TOTP','FARM_PLATFORM_ADMIN_NEW_EMAIL','FARM_PLATFORM_ADMIN_NEW_PASSWORD','FARM_PLATFORM_ADMIN_REFERENCE')
$locationChanged=$false
try {
 Push-Location (Split-Path -Parent $ApplicationDll); $locationChanged=$true
 Write-Host "Operation: $Operation; actor: $ActorEmail; exact target: $TargetEmail; approval: $ApprovalReference"
 if($Operation -eq 'RecoverAccount'){ Write-Host "Reset MFA: $ResetMfa. All previous target sessions will be invalidated." }
 else { Write-Host 'This creates a NEW security-critical platform administrator with no tenant memberships or business grants.' }
 if((Read-Host 'After trusted identity verification and approval, type EXECUTE to continue') -cne 'EXECUTE'){throw 'Cancelled'}
 $actorPassword=Read-PrivateValue 'Current platform administrator password'
 $newPassword=Read-PrivateValue 'New target password (minimum 12 characters)'
 $confirmation=Read-PrivateValue 'Confirm new target password'
 if($newPassword -cne $confirmation){throw 'New passwords do not match'}
 $confirmation=$null
 # Ask last so a current TOTP is used immediately.
 $totp=Read-PrivateValue 'Current six-digit platform administrator authenticator code'
 if($Operation -eq 'RecoverAccount'){
  $env:FARM_ALLOW_ACCOUNT_RECOVERY='YES'; $env:FARM_RECOVERY_ACTOR_EMAIL=$ActorEmail; $env:FARM_RECOVERY_ACTOR_PASSWORD=$actorPassword
  $env:FARM_RECOVERY_ACTOR_TOTP=$totp; $env:FARM_RECOVERY_TARGET_EMAIL=$TargetEmail; $env:FARM_RECOVERY_NEW_PASSWORD=$newPassword
  $env:FARM_RECOVERY_RESET_MFA=$ResetMfa; $env:FARM_RECOVERY_REFERENCE=$ApprovalReference
  & dotnet $ApplicationDll --recover-account
 }else{
  $env:FARM_ALLOW_PLATFORM_ADMIN_PROVISION='YES'; $env:FARM_PLATFORM_ADMIN_ACTOR_EMAIL=$ActorEmail; $env:FARM_PLATFORM_ADMIN_ACTOR_PASSWORD=$actorPassword
  $env:FARM_PLATFORM_ADMIN_ACTOR_TOTP=$totp; $env:FARM_PLATFORM_ADMIN_NEW_EMAIL=$TargetEmail; $env:FARM_PLATFORM_ADMIN_NEW_PASSWORD=$newPassword; $env:FARM_PLATFORM_ADMIN_REFERENCE=$ApprovalReference
  & dotnet $ApplicationDll --provision-platform-admin
 }
 if($LASTEXITCODE -ne 0){throw 'Local account operation failed; review sanitized application error and audit records before retrying'}
}finally{
 if($locationChanged){Pop-Location}
 foreach($name in $variables){Remove-Item "Env:$name" -ErrorAction SilentlyContinue}
 $actorPassword=$null; $newPassword=$null; $confirmation=$null; $totp=$null
}
