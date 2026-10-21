using System.Data.Common;
using System.Collections.Frozen;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace TrustedInfrastructure;

/// <summary>Fixed API shape and project catalogue. This is never registered in the public gateway.</summary>
public sealed class OpenBaoClient : IDisposable
{
    public static readonly IReadOnlySet<string> ProjectIds = new[] { "farm", "ims", "hotel", "email", "academic", "paru" }.ToFrozenSet(StringComparer.Ordinal);
    private readonly TrustedVaultOptions _options;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private readonly TimeProvider _time;
    private string? _token;
    private DateTimeOffset _tokenExpires;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public OpenBaoClient(TrustedVaultOptions options, bool development, TimeProvider? time = null)
        : this(options, development, MakeHandler(options, development), time) { }

    // Handler injection supports isolated tests; production always uses default certificate validation.
    public OpenBaoClient(TrustedVaultOptions options, bool development, HttpMessageHandler handler, TimeProvider? time = null)
    {
        ValidateOptions(options, development);
        _options = options;
        _time = time ?? TimeProvider.System;
        _http = new HttpClient(handler) { BaseAddress = new Uri(options.Address.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 65536 };
    }

    public static void ValidateOptions(TrustedVaultOptions options, bool development)
    {
        if (!Uri.TryCreate(options.Address, UriKind.Absolute, out var address) || !string.IsNullOrEmpty(address.UserInfo) || address.AbsolutePath != "/" || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
            throw new InvalidOperationException("TrustedVault:Address must be an absolute HTTPS origin without credentials, path, query or fragment.");
        if (address.Scheme != Uri.UriSchemeHttps && !(development && options.AllowDevelopmentLoopbackHttp && address.Scheme == Uri.UriSchemeHttp && address.IsLoopback))
            throw new InvalidOperationException("OpenBao requires HTTPS. Only explicit Development loopback fixtures may use HTTP.");
        if (!Regex.IsMatch(options.Mount, "^[a-zA-Z0-9_-]+$") || !Regex.IsMatch(options.AuthMount, "^[a-zA-Z0-9_-]+$"))
            throw new InvalidOperationException("OpenBao mount names must be a single safe segment.");
        if (options.AuthMethod is not ("AppRole" or "Certificate")) throw new InvalidOperationException("Use AppRole or Certificate authentication.");
        ValidateBounds(options);
        if (options.AuthMethod == "AppRole") { RequireFilePath(options.RoleIdFile); RequireFilePath(options.SecretIdFile); }
        else { RequireFilePath(options.CertificatePemFile); RequireFilePath(options.CertificateKeyFile); if (string.IsNullOrWhiteSpace(options.CertificateRole)) throw new InvalidOperationException("CertificateRole is required."); }
    }

    private static void RequireFilePath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("Bootstrap credential files must use absolute trusted-server-only paths.");
    }

    public static void ValidateBounds(TrustedVaultOptions options)
    {
        if (options.MaximumTokenLifetimeSeconds is < 30 or > 86400 || options.MaximumSecretLifetimeMinutes is < 1 or > 1440 || options.RecheckSeconds is < 5 or > 300)
            throw new InvalidOperationException("Invalid bounded credential lifetime or recheck interval.");
    }

    public static void ValidateCredentialPaths(TrustedVaultOptions options, params string[] additionalForbiddenRoots)
    {
        var paths = options.AuthMethod == "AppRole" ? new[] { options.RoleIdFile, options.SecretIdFile } : new[] { options.CertificatePemFile, options.CertificateKeyFile };
        var roots = additionalForbiddenRoots.Concat(new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }).Where(p => !string.IsNullOrEmpty(p)).Select(ResolvePath).ToArray();
        try
        {
            foreach (var path in paths)
            {
                RequireFilePath(path);
                var resolved = ResolvePath(path);
                if (roots.Any(root => IsWithin(resolved, root))) throw new TrustedVaultUnavailableException();
                if (!File.Exists(resolved) || new FileInfo(resolved).Length > 8192) throw new TrustedVaultUnavailableException();
            }
        }
        catch { throw new TrustedVaultUnavailableException(); }
    }

    private static bool IsWithin(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static string ResolvePath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null) current = info.ResolveLinkTarget(true)?.FullName ?? throw new TrustedVaultUnavailableException();
        }
        return Path.GetFullPath(current);
    }

    private async Task<string> ReadBootstrapAsync(string path, CancellationToken ct)
    {
        ValidateCredentialPaths(_options);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length is < 1 or > 8192) throw new TrustedVaultUnavailableException();
        using var reader = new StreamReader(stream);
        var buffer = new char[8193];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(), ct);
        if (count is < 1 or > 8192) throw new TrustedVaultUnavailableException();
        return new string(buffer, 0, count).Trim();
    }

    private static HttpMessageHandler MakeHandler(TrustedVaultOptions options, bool development)
    {
        ValidateOptions(options, development);
        ValidateCredentialPaths(options);
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        if (options.AuthMethod == "Certificate")
        {
            try
            {
                var certificate = X509Certificate2.CreateFromPemFile(options.CertificatePemFile, options.CertificateKeyFile);
                handler.ClientCertificates.Add(certificate);
            }
            catch { handler.Dispose(); throw new TrustedVaultUnavailableException(); }
        }
        return handler;
    }

    private static string ProjectPath(string project)
    {
        if (!ProjectIds.Contains(project)) throw new ArgumentException("Unknown project.", nameof(project));
        return "apps/" + project + "/runtime";
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        if (_token is not null && _time.GetUtcNow().AddSeconds(15) < _tokenExpires) return _token;
        await _authLock.WaitAsync(ct);
        try
        {
            if (_token is not null && _time.GetUtcNow().AddSeconds(15) < _tokenExpires) return _token;
            _token = null;
            object login;
            if (_options.AuthMethod == "AppRole")
            {
                var roleId = await ReadBootstrapAsync(_options.RoleIdFile, ct);
                var secretId = await ReadBootstrapAsync(_options.SecretIdFile, ct);
                if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(secretId) || roleId.Length > 8192 || secretId.Length > 8192) throw new TrustedVaultUnavailableException();
                login = new { role_id = roleId, secret_id = secretId };
            }
            else login = new { name = _options.CertificateRole };
            using var response = await _http.PostAsJsonAsync($"v1/auth/{_options.AuthMount}/login", login, ct);
            await RequireSuccess(response);
            using var document = await ReadDocument(response, ct);
            var auth = document.RootElement.GetProperty("auth");
            var lease = auth.GetProperty("lease_duration").GetInt32();
            var token = auth.GetProperty("client_token").GetString();
            if (lease < 30 || lease > _options.MaximumTokenLifetimeSeconds || string.IsNullOrWhiteSpace(token)) throw new TrustedVaultUnavailableException();
            if (auth.TryGetProperty("policies", out var policies) && policies.EnumerateArray().Any(p => p.GetString() == "root")) throw new TrustedVaultUnavailableException();
            _tokenExpires = _time.GetUtcNow().AddSeconds(lease);
            _token = token;
            return token;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TrustedVaultUnavailableException) { throw new TrustedVaultUnavailableException(); }
        finally { _authLock.Release(); }
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Vault-Token", await TokenAsync(ct));
            using var response = await _http.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) _token = null;
            await RequireSuccess(response);
            return await ReadDocument(response, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TrustedVaultUnavailableException) { throw new TrustedVaultUnavailableException(); }
    }

    private static Task RequireSuccess(HttpResponseMessage response)
    {
        // Do not surface OpenBao response bodies, request details or bootstrap secrets in exception logs.
        if (!response.IsSuccessStatusCode) throw new TrustedVaultUnavailableException();
        return Task.CompletedTask;
    }

    private static async Task<JsonDocument> ReadDocument(HttpResponseMessage response, CancellationToken ct)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > 65536) throw new TrustedVaultUnavailableException();
        return JsonDocument.Parse(bytes);
    }

    public async Task<(RuntimeSecrets Secrets, int Version)> ReadRuntimeAsync(string project, CancellationToken ct = default)
    {
        using var document = await GetAsync($"v1/{_options.Mount}/data/{ProjectPath(project)}", ct);
        try
        {
            var data = document.RootElement.GetProperty("data");
            var metadata = data.GetProperty("metadata");
            if (metadata.GetProperty("destroyed").GetBoolean() || (metadata.TryGetProperty("deletion_time", out var deleted) && !string.IsNullOrEmpty(deleted.GetString()))) throw new TrustedVaultUnavailableException();
            var secrets = data.GetProperty("data").Deserialize<RuntimeSecrets>(Json) ?? throw new TrustedVaultUnavailableException();
            ValidateSecrets(secrets, _time.GetUtcNow(), _options.MaximumSecretLifetimeMinutes);
            return (secrets, metadata.GetProperty("version").GetInt32());
        }
        catch (Exception ex) when (ex is not TrustedVaultUnavailableException) { throw new TrustedVaultUnavailableException(); }
    }

    public async Task<VaultSecretMetadata> ReadMetadataAsync(string project, CancellationToken ct = default)
    {
        using var document = await GetAsync($"v1/{_options.Mount}/metadata/{ProjectPath(project)}", ct);
        try
        {
            var data = document.RootElement.GetProperty("data");
            var versions = data.GetProperty("versions").EnumerateObject().Select(p => new VaultVersionMetadata(
                int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset.TryParse(p.Value.GetProperty("created_time").GetString(), out var created) ? created : null,
                p.Value.GetProperty("destroyed").GetBoolean(),
                DateTimeOffset.TryParse(p.Value.GetProperty("deletion_time").GetString(), out var deleted) ? deleted : null)).OrderByDescending(v => v.Version).ToArray();
            // Deliberately omit custom_metadata and all data values; operators can accidentally put secrets there.
            return new VaultSecretMetadata(project, data.GetProperty("current_version").GetInt32(), versions);
        }
        catch (Exception ex) when (ex is not TrustedVaultUnavailableException) { throw new TrustedVaultUnavailableException(); }
    }

    public static void ValidateSecrets(RuntimeSecrets secrets, DateTimeOffset now, int maximumLifetimeMinutes)
    {
        if (secrets.ExpiresAtUtc <= now || secrets.ExpiresAtUtc > now.AddMinutes(maximumLifetimeMinutes) || string.IsNullOrWhiteSpace(secrets.SqlConnectionString)) throw new TrustedVaultUnavailableException();
        ValidateSqlConnectionString(secrets.SqlConnectionString);
        if (!string.IsNullOrEmpty(secrets.SecondarySqlConnectionString)) ValidateSqlConnectionString(secrets.SecondarySqlConnectionString);
    }

    private static void ValidateSqlConnectionString(string connectionString)
    {
        try
        {
            var sql = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (!sql.TryGetValue("Encrypt", out var encrypt) || !(encrypt.ToString()!.Equals("True", StringComparison.OrdinalIgnoreCase) || encrypt.ToString()!.Equals("Mandatory", StringComparison.OrdinalIgnoreCase) || encrypt.ToString()!.Equals("Strict", StringComparison.OrdinalIgnoreCase))) throw new TrustedVaultUnavailableException();
            foreach (var alias in new[] { "TrustServerCertificate", "Trust Server Certificate" })
                if (sql.TryGetValue(alias, out var trust) && !trust.ToString()!.Equals("False", StringComparison.OrdinalIgnoreCase)) throw new TrustedVaultUnavailableException();
        }
        catch (Exception ex) when (ex is not TrustedVaultUnavailableException) { throw new TrustedVaultUnavailableException(); }
    }

    public void Dispose() { _token = null; _http.Dispose(); _authLock.Dispose(); }
}
