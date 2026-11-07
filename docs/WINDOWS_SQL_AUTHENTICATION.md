# Windows-integrated SQL Server authentication

This is SQL Server authentication using the application process/service identity, not browser Windows SSO. Existing Identity login, MFA where configured, institute/company/branch authorization and CSRF protection remain unchanged.

## Explicit profile

Set Database__Provider=SqlServer and Database__Authentication=Windows.
Set ConnectionStrings__DefaultConnection to:

    Server=sql.example.invalid;Database=AppSynthetic;Integrated Security=true;Encrypt=true;TrustServerCertificate=false

Replace only the synthetic server/database with the intended approved target. Do not add a SQL user ID/password or another Authentication method. TLS and certificate validation remain required. The server certificate must match the configured hostname and be trusted by the host.

Omitting Database__Authentication, or setting ConnectionString, preserves existing SQL/provider connection-string behavior. Windows mode rejects non-SQL Server providers, invalid modes, SQL credentials, disabled encryption and certificate trust bypass. No connection-string values are logged by this validation.

## OpenBao off for this profile

Set TrustedVault__Mode=LegacyLocal and TrustedVault__RequireInProduction=false together. This explicitly selects the existing no-vault mode: no OpenBao authentication, secret fetch or lease/rotation watchdog is registered. Do not use Disabled in production because it is Development-only.

This removes vault lease-expiry and rotation protection. Windows authentication replaces only the SQL credential. Provision any required Redis, messaging/API and certificate secrets through protected deployment configuration, never committed JSON, scripts or logs. Optional external integrations should remain disabled unless separately configured and approved. Existing application TLS, key protection, tenant checks and provider restrictions remain applicable.

The example profile in docs/windows-sql-no-vault.example.json is an overlay, not an automatically loaded configuration file. Apply the listed environment settings or merge only these keys into your approved private configuration. Do not change the environment name or overwrite existing security settings to enable it.

## Host acceptance

Under IIS, SQL authenticates the app-pool/service identity, not the signed-in browser user. An operator must review a least-privilege dedicated service identity and existing SQL grants; do not use sa, db_owner or the developer's administrator identity. This source change creates no accounts, permissions, credentials, SPNs or delegation settings.

Windows/IIS SQL connectivity must be tested on the intended host with valid TLS and a non-production database first. Linux requires a separately configured Kerberos identity and is not automatically made Windows-capable. No fallback to SQL passwords or TrustServerCertificate=true is provided.

Run dotnet test Tests/EnterpriseERP.UnitTests/EnterpriseERP.UnitTests.csproj and existing release checks with the SDK in global.json. New offline tests exercise valid/invalid configuration without contacting SQL Server. The authoring cloud environment had no dotnet executable, so these C# tests and full builds were not run here. Real SQL/Windows/IIS and non-vault restart acceptance remain pending. No live settings, databases or deployments were changed.
