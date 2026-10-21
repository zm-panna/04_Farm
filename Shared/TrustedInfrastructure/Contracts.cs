using System.Text.Json.Serialization;

namespace TrustedInfrastructure;

public enum RuntimeSecretKey { SqlConnectionString, SecondarySqlConnectionString, RedisConnectionString, RedisBackplaneConnectionString, SmtpPassword, OpenAiApiKey, AiProviderApiKey, GmailClientSecret, DataProtectionCertificatePassword, LegacyEncryptionKey, LegacyAesPassphrase, PaymentMerchantPassword }

/// <summary>Only trusted startup code supplies bindings; never bind this type from an HTTP request.</summary>
public enum TrustedDatabaseProvider { SqlServer, PostgreSql, Oracle }

public sealed class TrustedSecretBindings
{
    public required string SqlConnectionName { get; init; }
    public string[] AdditionalSqlConnectionNames { get; init; } = [];
    public string? SecondarySqlConnectionName { get; init; }
    public TrustedDatabaseProvider DatabaseProvider { get; init; } = TrustedDatabaseProvider.SqlServer;
    public string[] RedisConfigurationKeys { get; init; } = [];
    public string[] RedisBackplaneConfigurationKeys { get; init; } = [];
    public string[] SmtpPasswordConfigurationKeys { get; init; } = [];
    public string[] OpenAiApiKeyConfigurationKeys { get; init; } = [];
    public string[] AiProviderApiKeyConfigurationKeys { get; init; } = [];
    public string[] GmailClientSecretConfigurationKeys { get; init; } = [];
    public string[] DataProtectionCertificatePasswordConfigurationKeys { get; init; } = [];
    public string[] LegacyEncryptionKeyConfigurationKeys { get; init; } = [];
    public string[] LegacyAesPassphraseConfigurationKeys { get; init; } = [];
    public string[] PaymentMerchantPasswordConfigurationKeys { get; init; } = [];

    internal IReadOnlyDictionary<RuntimeSecretKey, string[]> AsMap() => new Dictionary<RuntimeSecretKey, string[]>
    {
        [RuntimeSecretKey.SqlConnectionString] = new[] { SqlConnectionName }.Concat(AdditionalSqlConnectionNames).Select(name => $"ConnectionStrings:{name}").ToArray(),
        [RuntimeSecretKey.SecondarySqlConnectionString] = SecondarySqlConnectionName is null ? [] : [$"ConnectionStrings:{SecondarySqlConnectionName}"],
        [RuntimeSecretKey.RedisConnectionString] = RedisConfigurationKeys,
        [RuntimeSecretKey.RedisBackplaneConnectionString] = RedisBackplaneConfigurationKeys,
        [RuntimeSecretKey.SmtpPassword] = SmtpPasswordConfigurationKeys,
        [RuntimeSecretKey.OpenAiApiKey] = OpenAiApiKeyConfigurationKeys,
        [RuntimeSecretKey.AiProviderApiKey] = AiProviderApiKeyConfigurationKeys,
        [RuntimeSecretKey.GmailClientSecret] = GmailClientSecretConfigurationKeys,
        [RuntimeSecretKey.DataProtectionCertificatePassword] = DataProtectionCertificatePasswordConfigurationKeys,
        [RuntimeSecretKey.LegacyEncryptionKey] = LegacyEncryptionKeyConfigurationKeys,
        [RuntimeSecretKey.LegacyAesPassphrase] = LegacyAesPassphraseConfigurationKeys,
        [RuntimeSecretKey.PaymentMerchantPassword] = PaymentMerchantPasswordConfigurationKeys
    };
}

public sealed class RuntimeSecrets
{
    public string SqlConnectionString { get; init; } = "";
    public string? SecondarySqlConnectionString { get; init; }
    public string? RedisConnectionString { get; init; }
    public string? RedisBackplaneConnectionString { get; init; }
    public string? SmtpPassword { get; init; }
    public string? OpenAiApiKey { get; init; }
    public string? AiProviderApiKey { get; init; }
    public string? GmailClientSecret { get; init; }
    public string? DataProtectionCertificatePassword { get; init; }
    public string? LegacyEncryptionKey { get; init; }
    public string? LegacyAesPassphrase { get; init; }
    public string? PaymentMerchantPassword { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public override string ToString() => "RuntimeSecrets { values redacted }";
    internal string? Get(RuntimeSecretKey key) => key switch
    {
        RuntimeSecretKey.SqlConnectionString => SqlConnectionString,
        RuntimeSecretKey.SecondarySqlConnectionString => SecondarySqlConnectionString,
        RuntimeSecretKey.RedisConnectionString => RedisConnectionString,
        RuntimeSecretKey.RedisBackplaneConnectionString => RedisBackplaneConnectionString,
        RuntimeSecretKey.SmtpPassword => SmtpPassword,
        RuntimeSecretKey.OpenAiApiKey => OpenAiApiKey,
        RuntimeSecretKey.AiProviderApiKey => AiProviderApiKey,
        RuntimeSecretKey.GmailClientSecret => GmailClientSecret,
        RuntimeSecretKey.DataProtectionCertificatePassword => DataProtectionCertificatePassword,
        RuntimeSecretKey.LegacyEncryptionKey => LegacyEncryptionKey,
        RuntimeSecretKey.LegacyAesPassphrase => LegacyAesPassphrase,
        RuntimeSecretKey.PaymentMerchantPassword => PaymentMerchantPassword,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };
}

public sealed class TrustedVaultOptions
{
    public const string SectionName = "TrustedVault";
    public string Mode { get; set; } = "Disabled";
    public bool RequireInProduction { get; set; } = true;
    public string Address { get; set; } = "";
    public string Mount { get; set; } = "secret";
    public string AuthMethod { get; set; } = "AppRole";
    public string AuthMount { get; set; } = "approle";
    // Bootstrap files are outside source/publish directories and readable only by the trusted service identity.
    public string RoleIdFile { get; set; } = "";
    public string SecretIdFile { get; set; } = "";
    public string CertificatePemFile { get; set; } = "";
    public string CertificateKeyFile { get; set; } = "";
    public string CertificateRole { get; set; } = "";
    public int MaximumTokenLifetimeSeconds { get; set; } = 3600;
    public int MaximumSecretLifetimeMinutes { get; set; } = 1440;
    public int RecheckSeconds { get; set; } = 30;
    public bool AllowDevelopmentLoopbackHttp { get; set; }
    // Synthetic mode is explicit and only accepted in Development, never Production/Staging.
    public RuntimeSecrets? Synthetic { get; set; }
    public override string ToString() => "TrustedVaultOptions { bootstrap details redacted }";
}

public interface ITrustedRuntimeGate
{
    bool IsAvailable { get; }
    void ThrowIfUnavailable();
}

public sealed record VaultVersionMetadata(int Version, DateTimeOffset? CreatedAtUtc, bool Destroyed, DateTimeOffset? DeletedAtUtc);
public sealed record VaultSecretMetadata(string Project, int CurrentVersion, IReadOnlyList<VaultVersionMetadata> Versions);
public sealed class TrustedVaultUnavailableException : Exception
{
    public TrustedVaultUnavailableException() : base("Trusted runtime configuration is unavailable. Contact the trusted server operator.") { }
}
