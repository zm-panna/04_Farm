# Shared integration contract v1.1 (2026-10-20)

Copy `src/TrustedInfrastructure/` into each application solution's `Shared/TrustedInfrastructure/` and add a ProjectReference from the trusted MVC backend. This library references only the ASP.NET shared framework. Never reference it from PublicGateway.

Before ANY SQL/Redis/SMTP/AI registration or reading their configuration:

```csharp
using TrustedInfrastructure;
await builder.AddTrustedInfrastructureAsync("farm", new TrustedSecretBindings
{
    SqlConnectionName = "DefaultConnection", // application's actual ConnectionStrings name
    RedisConfigurationKeys = ["Scaling:Cache:Connection"], // actual cache key
    RedisBackplaneConfigurationKeys = ["Scaling:SignalR:Connection"], // actual backplane key
    SmtpPasswordConfigurationKeys = [], // actual SMTP password configuration keys
    OpenAiApiKeyConfigurationKeys = [],
    AiProviderApiKeyConfigurationKeys = []
});
```

After builder.Build(), before authentication/endpoints:

```csharp
app.UseTrustedInfrastructure();
```

Inject `ITrustedRuntimeGate` in background jobs. Call `ThrowIfUnavailable()` at the start of each run/batch and immediately before an irreversible database write, email send or external AI call. Respect host cancellation. HTTP middleware alone does not protect worker jobs. Rotation/expiry/outage marks the gate unavailable and calls only this process's `IHostApplicationLifetime.StopApplication()`; an operator-configured supervisor must restart it. Existing EF connection pools, Redis clients and SMTP sessions do not automatically reload. In-flight operations cannot be retroactively revoked by this library. Keep jobs short/cancellable and enforce revocation at SQL/SMTP/OpenBao too.

Project IDs: `farm`, `ims`, `hotel`, `email`, `academic`, `paru`. Backend reads exactly `secret/data/apps/<project>/runtime`; no arbitrary request-derived path or field exists. JSON keys: `SqlConnectionString` (required, SQL encryption required and certificate trust bypass rejected), `SecondarySqlConnectionString` (independent optional DB), `RedisConnectionString` (cache), `RedisBackplaneConnectionString`, `SmtpPassword`, `OpenAiApiKey`, `AiProviderApiKey`, `GmailClientSecret`, `DataProtectionCertificatePassword`, `LegacyEncryptionKey`, `LegacyAesPassphrase`, `PaymentMerchantPassword`, `ExpiresAtUtc` (required UTC timestamp, future, maximum 24 hours). Optional mapped fields are cleared when absent, not filled from insecure JSON fallbacks.

Modes:
- `OpenBao`: authenticated AppRole or mTLS certificate to OpenBao; secure deploy mode.
- `Synthetic`: explicit Development-only in-memory fixture, never production.
- `Disabled`: Development-only when no vault selected.
- `LegacyLocal`: unprotected migration compatibility mode. Outside Development requires explicit `TrustedVault:RequireInProduction=false`. Do not call it vault-protected.

Production/Staging default fails closed when no OpenBao setup exists. Tests that intentionally exercise pre-vault behavior must explicitly configure LegacyLocal + RequireInProduction=false or use Development fixtures. Do not silently change production defaults.

Deployment JSON contains non-secret Address/Mount/AuthMethod/file locations, never tokens or secret IDs. RoleIdFile/SecretIdFile OR CertificatePemFile/CertificateKeyFile are absolute trusted-server-only paths outside the publish tree. No root tokens, unseal keys, real credentials or persistent infrastructure are provisioned by these artifacts. Bootstrap role/cert creation and secret provisioning are trusted-operator steps requiring separate authorization.

Existing MVC stays on the trusted backend: SQL/business logic/Identity cookies/CSRF/SignalR/user permissions remain server-enforced. PublicGateway is an optional secret-free YARP transport. One backend per public hostname at `/` is the supported profile; no general base-path rewriting. Keep business `/Admin` pages reachable, but reserve and block `/_platform`, `/_vault`, `/v1/sys`, `/v1/auth`, `/v1/secret`, `/v1/kv`, `/v1/identity` at gateway.

To preserve existing cookie login, the backend issues and validates its own cookies; gateway has NO shared Data Protection key or master API key. Gateway forwards cookie and Authorization values unchanged. Gateway is still able to see/steal a user's active session if its operator is malicious; vault isolation does not make an untrusted frontend safe for user data. Use a trusted public TLS termination/frontend for a stronger security boundary.

Additional binding properties: `AdditionalSqlConnectionNames` only for verified same-database aliases; `SecondarySqlConnectionName` for an independent optional DB (missing value clears config; its feature must fail closed); `GmailClientSecretConfigurationKeys`, `DataProtectionCertificatePasswordConfigurationKeys`, `LegacyEncryptionKeyConfigurationKeys`, `LegacyAesPassphraseConfigurationKeys`, `PaymentMerchantPasswordConfigurationKeys` (Paru exact merchant provider setting). `DatabaseProvider=TrustedDatabaseProvider.SqlServer` is the secure v1 profile; PostgreSql/Oracle reject secure startup and retain explicit LegacyLocal only. Do not add arbitrary key dictionaries. The supplied cryptography fields preserve existing application compatibility, not a recommendation to use legacy encryption.

Frozen library source hashes are in TRUSTED-INFRASTRUCTURE-SHA256.txt. Copy only `.cs` and `.csproj`, never bin/obj or credential files.
