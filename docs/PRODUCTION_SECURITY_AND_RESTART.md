# Production security and session restart checklist

This source has fail-closed startup validation for non-Development environments. Development SQLite is a local demonstration; it does not establish SQL Server or IIS readiness.

## Required production settings

Provide secrets through the deployment secret store/environment, never in the source ZIP, logs, screenshots, command history or committed appsettings.

- `Database__Provider=SqlServer`
- `ConnectionStrings__DefaultConnection`: use an exact server/database, `Encrypt=Mandatory` (or `Strict`) and `TrustServerCertificate=false`. The server certificate must validate. Do not weaken TLS to work around a certificate error.
- `AllowedHosts`: explicit DNS names or IPv4 addresses separated by `;`. Wildcards, URL schemes and ports are rejected.
- `Security__DataProtection__KeyDirectory`: an existing, absolute, persistent directory outside `wwwroot`. It must survive release-folder changes and app restarts. The directory cannot itself be a symbolic link.
- `Security__DataProtection__ApplicationName`: a stable deployment identifier. Preserve the same value across restarts/upgrade instances of this application. Use distinct values for unrelated environments.

### Windows / IIS

- Set `Security__DataProtection__ProtectionMode=Dpapi`.
- Provision the key directory before application startup. Restrict read/write/create permissions to the IIS application-pool identity and explicitly authorized backup administrators.
- Use a dedicated, stable application-pool identity and enable its user profile. User-scoped DPAPI binds key protection to that account; changing identities or moving the key files alone to another machine is not a valid recovery plan.
- Keep the protected key ring and required Windows identity/machine recovery material under the deployment backup policy. Test recovery in an isolated environment before release.
- For intentional multi-machine deployments, use properly managed shared certificate protection instead of assuming user-scoped DPAPI is portable.

### Linux / explicit certificate protection

- Set `Security__DataProtection__ProtectionMode=Certificate`.
- Set `Security__DataProtection__CertificatePath` to a local PFX file outside the web root, and provide `Security__DataProtection__CertificatePassword` via the secret store/environment.
- The certificate must be currently valid and contain an RSA private key of at least 2048 bits. The key directory must have owner-only access (0700); the private certificate cannot be group/other readable or writable.
- Provision the directory, ownership and certificate through the deployment operator. The application refuses missing or insecure configuration and does not silently create unencrypted fallback storage.
- Protect backups of both the key ring and its decryption certificate. Certificate replacement requires a deliberate rotation/recovery plan retaining the ability to decrypt existing keys; do not overwrite/remove the only usable private certificate.

## Restart acceptance

1. Start the configured environment, sign in and select an assigned company/branch.
2. Confirm that an encrypted key-ring file is present in the configured persistent directory. Do not publish its contents.
3. Restart the application under the same identity and configuration, leaving the directory in place.
4. Verify an existing authorized session still works, a new login works, and anti-forgery-protected POSTs still validate. Recheck access revocation separately.
5. Repeat with the deployment's actual IIS/SQL Server/TLS setup and archive redacted results. The automated test uses a temporary self-signed test certificate and proves local certificate-protected persistence across service-provider restarts only; it does not prove IIS DPAPI or production recovery.

## Response protections

Security middleware adds no-sniff, frame denial, no-referrer and restricted browser permission headers. Authenticated responses and login pages are marked private/no-store. The CSP restricts resources to this application and denies framing/object embedding, and permits only self-hosted scripts (`script-src 'self'`) without inline script or eval allowances. Navigation and company/branch behavior use external versioned JavaScript files. Existing inline styles remain permitted by the separate style directive; this is a source-allowlist CSP, not a nonce/hash policy. SQL Server credentials and certificate passwords are not included in validation error messages.

References: [Microsoft Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0), [SQL client encryption](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnectionstringbuilder.encrypt?view=sqlclient-dotnet-core-6.0).
