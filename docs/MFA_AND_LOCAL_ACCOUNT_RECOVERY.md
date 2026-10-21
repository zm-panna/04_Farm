# Authenticator MFA and local account recovery

## Self-service account protection

Open `/Account/Security` after signing in. Starting enrollment requires the current password. The server displays a private manual setup key; add a time-based account in a trusted authenticator (six digits, 30-second period, SHA-1). No QR-code website or other third party receives the key. Confirm the current password and a valid authenticator code to enable MFA. Setup proof is protected, bound to this user, authenticator key and security stamp, and expires after ten minutes.

Save the ten recovery codes offline or in an approved password manager. Each replaces the authenticator for one login **after the password is verified**. Codes are displayed only in the generation response. Replacing them requires the current password and authenticator and immediately invalidates all prior codes and sessions. Disabling MFA also requires both factors, rotates the key, removes recovery codes and signs out. Password changes require the authenticator when MFA is enabled. Do not put any password, setup key, recovery code or reset token in support tickets or logs.

Login requires password first, then TOTP or one unused recovery code. The temporary challenge expires in five minutes and is tied to the current security stamp. Expired challenges, locked accounts, disabled MFA and changed stamps cannot redeem codes. Password/MFA failures count toward account lockout. There is no remembered-browser bypass. Authenticator clocks and the application host clock must be synchronized. TOTP improves password-only security but is not phishing-resistant.

Security changes update the Identity security stamp. Application cookies validate it every request, so other sessions are rejected on their next request. Previously issued MFA login challenges are also rejected. Tenant selection is scoped to the authenticated browser cookie and rechecked against current database access on each business operation.

## Prepare a recovery administrator before an emergency

The initial PlatformAdmin should enroll MFA and keep recovery codes immediately. An already verified PlatformAdmin can create a separate, independently held recovery administrator with the trusted local `--provision-platform-admin` command. It requires the existing administrator's current password and TOTP, a NEW unique email, a strong new password, and a non-secret approval reference. It cannot elevate or overwrite an existing account.

This second administrator receives the actual Identity PlatformAdmin role and **no company memberships, branch memberships or business permission grants**. `PlatformAuditCompanyId` claims carry audit routing only and never grant tenant access. The new owner must visit `/Account/Login`, sign in, and open `/Account/Security` directly to enroll their own authenticator and save recovery codes. There is deliberately no tenant workspace to select until separately authorized. PlatformAdmin is a highly privileged role: provision only a trusted, separately controlled recovery identity with explicit organizational approval.

Keep at least one independently held, usable PlatformAdmin with MFA. If every PlatformAdmin loses its password and all second factors, this application intentionally has no unauthenticated recovery bypass. Follow the organization's approved database/host disaster-recovery process with independent identity verification and audit. Tenant administrators cannot reset an existing user's account or promote themselves.

## Authorized local recovery

For a forgotten password, lost authenticator or exhausted codes, verify the account owner's identity through an established trusted channel. Record a non-secret case/change reference and obtain organizational approval. On the trusted application host, run the published application under the approved operator account with the production database and protected key-ring configuration. Existing account recovery is never exposed as a web endpoint.

Use `scripts/LOCAL_ACCOUNT_SECURITY.ps1` with `-Operation RecoverAccount`, `-ApplicationDll` pointing to the published `EnterpriseERP.Web.dll`, `-ActorEmail`, exact `-TargetEmail`, `-ApprovalReference`, and an explicit `-ResetMfa YES` only if replacing the lost second factor is approved (`NO` preserves it). Passwords and the administrator's current TOTP are prompted privately, never passed as command-line arguments. The script confirms the action, clears transient process environment variables afterward, and prints no credentials. It does not persist secrets or change machine-level environment settings. Do not run in a transcript, shell tracing session or environment-dumping diagnostic wrapper. Process memory/environment remain sensitive to privileged local operators; use only the trusted host.

For a new recovery administrator use the same wrapper with `-Operation ProvisionRecoveryAdministrator`. `TargetEmail` is the new account; an existing email is rejected. Do not use bootstrap on an existing database.

The CLI independently checks the **database** PlatformAdmin role, current password, TOTP and lockout. Cookie role claims and tenant `*` grants are insufficient. Recovery resets the target password, clears target lockout, enables normal lockout enforcement for legacy accounts, rotates the security stamp, and optionally removes MFA/key/recovery codes. It preserves company/branch memberships and permission grants. All changes and non-secret audit entries commit atomically or roll back. Password-reset tokens are generated and consumed only within the local process and are never returned. Deliver the new password only through the approved secure process; the owner should change it after login and immediately re-enroll MFA if reset.

### Environment contract for controlled tooling

These are transient process values, not configuration-file settings. Never put actual values in source control, tickets or documentation.

Recovery: `FARM_ALLOW_ACCOUNT_RECOVERY=YES`, `FARM_RECOVERY_ACTOR_EMAIL`, `FARM_RECOVERY_ACTOR_PASSWORD`, `FARM_RECOVERY_ACTOR_TOTP`, `FARM_RECOVERY_TARGET_EMAIL`, `FARM_RECOVERY_NEW_PASSWORD`, `FARM_RECOVERY_RESET_MFA=YES|NO`, `FARM_RECOVERY_REFERENCE`; command `--recover-account`.

New recovery administrator: `FARM_ALLOW_PLATFORM_ADMIN_PROVISION=YES`, `FARM_PLATFORM_ADMIN_ACTOR_EMAIL`, `FARM_PLATFORM_ADMIN_ACTOR_PASSWORD`, `FARM_PLATFORM_ADMIN_ACTOR_TOTP`, `FARM_PLATFORM_ADMIN_NEW_EMAIL`, `FARM_PLATFORM_ADMIN_NEW_PASSWORD`, `FARM_PLATFORM_ADMIN_REFERENCE`; command `--provision-platform-admin`.

Production TLS, private encrypted persistent Data Protection and host checks apply to these CLI invocations as well. See `PRODUCTION_SECURITY_AND_RESTART.md`. Protect Identity database contents and backups as credential material: database access can expose authenticator/recovery token material. Do not enable EF sensitive-data logging or HTTP body logging. The source uses the built-in ASP.NET Core Identity token providers; no external SMS, email or QR provider is configured.

## Verification evidence

`MfaSecurityTests` uses isolated SQLite Identity stores and synthetic users, keys, passwords and TOTP calculations. It exercises valid enrollment, invalid/expired/cross-user setup, one-time codes, regeneration, disable, lockout, password-first login, challenge expiry/stamp binding, local authority checks, recovery preserving/resetting MFA, no-tenant recovery-admin provisioning, audit rollback and isolated browser tenant selection. These tests do not create production accounts or send credentials externally. Published-host/cookie/browser checks are a separate release gate.
