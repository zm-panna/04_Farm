using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace EnterpriseERP.Web;

/// <summary>Environment-only inputs; never serialize this object (it contains the connection secret).</summary>
public sealed class StagingPreflightOptions
{
    public required string ConnectionString { get; init; }
    public required string ExpectedServer { get; init; }
    public required string ExpectedDatabase { get; init; }
    public required string ExpectedLogin { get; init; }
    public required string ExpectedUser { get; init; }
    public required string ProcessIdentity { get; init; }
    public required string OutputPath { get; init; }
    public required int CompanyId { get; init; }
    public required string ExpectedTimeZone { get; init; }
    public const int ConnectionTimeoutSeconds = 5;
    public const int CommandTimeoutSeconds = 5;
    public const int OverallTimeoutSeconds = 120;
    public const int CatalogRowLimit = 12000;
    public const int CompanyRowLimit = 200;

    public static StagingPreflightOptions FromEnvironment(string output, Func<string, string?> read,
        string processIdentity, string applicationDirectory)
    {
        string Required(string key)
        {
            var value = read(key);
            if (string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Any(char.IsControl))
                throw new InvalidOperationException("Required staging preflight settings are missing or invalid. See the operator guide.");
            return value;
        }
        if (!string.Equals(read("Database__Provider"), "SqlServer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Read-only staging verification requires explicit Database__Provider=SqlServer; SQLite is prohibited.");
        if (read("FARM_PREFLIGHT_RUNTIME_IDENTITY_CONFIRMED") != "YES" || read("FARM_PREFLIGHT_PRIVATE_OUTPUT_CONFIRMED") != "YES")
            throw new InvalidOperationException("Confirm the existing runtime identity and private evidence directory before verification; this tool changes neither.");
        var expectedOsIdentity = Required("FARM_PREFLIGHT_EXPECTED_PROCESS_IDENTITY");
        if (!string.Equals(processIdentity, expectedOsIdentity, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The current process identity does not match the explicitly expected runtime identity.");
        var database = Required("FARM_PREFLIGHT_EXPECTED_DATABASE");
        var server = Required("FARM_PREFLIGHT_EXPECTED_SERVER");
        var login = Required("FARM_PREFLIGHT_EXPECTED_SQL_LOGIN");
        var user = Required("FARM_PREFLIGHT_EXPECTED_SQL_USER");
        if (!int.TryParse(read("FARM_PREFLIGHT_COMPANY_ID"), out var companyId) || companyId <= 0)
            throw new InvalidOperationException("An explicit positive company ID is required for the one-company scope.");
        var zone = Required("FARM_PREFLIGHT_EXPECTED_TIMEZONE");
        if (zone is not ("Asia/Dhaka" or "Bangladesh Standard Time"))
            throw new InvalidOperationException("This first-live gate is scoped to an explicitly approved Bangladesh timezone.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(zone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new InvalidOperationException("The approved Bangladesh timezone is unavailable on this host."); }
        SqlConnectionStringBuilder sql;
        try { sql = new SqlConnectionStringBuilder(read("ConnectionStrings__DefaultConnection") ?? ""); }
        catch (Exception e) when (e is ArgumentException or FormatException)
        { throw new InvalidOperationException("The environment SQL Server connection is invalid; details are intentionally suppressed."); }
        var encryption = sql.Encrypt.ToString();
        if (string.IsNullOrWhiteSpace(sql.DataSource) || !string.Equals(sql.InitialCatalog, database, StringComparison.Ordinal)
            || !(encryption.Equals("Mandatory", StringComparison.OrdinalIgnoreCase) || encryption.Equals("Strict", StringComparison.OrdinalIgnoreCase) || encryption.Equals("True", StringComparison.OrdinalIgnoreCase))
            || sql.TrustServerCertificate || sql.UserInstance || !string.IsNullOrEmpty(sql.AttachDBFilename))
            throw new InvalidOperationException("SQL Server must target the expected database with mandatory/strict encryption and verified server certificate; attach-file/user-instance connections are prohibited.");
        // Do not opt into replica routing: evidence must describe the actual primary target, not a lagging replica.
        if (sql.ApplicationIntent != ApplicationIntent.ReadWrite)
            throw new InvalidOperationException("Read-only replica routing is not accepted as primary staging evidence.");
        sql.ConnectTimeout = ConnectionTimeoutSeconds;
        sql.ConnectRetryCount = 0;
        sql.Pooling = false;
        sql.Enlist = false;
        sql.MultipleActiveResultSets = false;
        sql.PersistSecurityInfo = false;
        sql.ApplicationName = "FarmERP-ReadOnlyStagingPreflight";
        ValidateOutput(output, Required("FARM_PREFLIGHT_WEBROOT"), applicationDirectory);
        return new() { ConnectionString = sql.ConnectionString, ExpectedServer = server, ExpectedDatabase = database,
            ExpectedLogin = login, ExpectedUser = user, ProcessIdentity = processIdentity,
            OutputPath = Path.GetFullPath(output), CompanyId = companyId, ExpectedTimeZone = zone };
    }

    public static void ValidateOutput(string output, string webRoot, string applicationDirectory)
    {
        if (output.StartsWith(@"\\", StringComparison.Ordinal) || Path.GetFileName(output).Contains(':')
            || !Path.IsPathFullyQualified(output) || !Path.IsPathFullyQualified(webRoot)
            || !Path.IsPathFullyQualified(applicationDirectory) || !output.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Evidence needs a new absolute .json path and an explicit absolute webroot.");
        output = Path.GetFullPath(output);
        var parent = Path.GetDirectoryName(output)!;
        if (!Directory.Exists(parent) || File.Exists(output) || Directory.Exists(output)
            || Within(output, Path.GetFullPath(webRoot)) || Within(output, Path.GetFullPath(applicationDirectory)))
            throw new InvalidOperationException("Use a new evidence file in an existing private directory outside both the application directory and webroot.");
        // A public-root alias can expose an otherwise private-looking physical output directory.
        // Validate all compared directories rather than resolving untrusted links and guessing visibility.
        foreach (var directory in new[] { parent, Path.GetFullPath(webRoot), Path.GetFullPath(applicationDirectory) })
        {
            if (!Directory.Exists(directory))
                throw new InvalidOperationException("Evidence, application and declared webroot directories must already exist.");
            for (DirectoryInfo? d = new(directory); d != null; d = d.Parent)
                if ((d.Attributes & FileAttributes.ReparsePoint) != 0 || d.LinkTarget != null)
                    throw new InvalidOperationException("Evidence, application and webroot ancestors cannot be symbolic links or reparse points.");
        }
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode others = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((File.GetUnixFileMode(parent) & others) != 0)
                throw new InvalidOperationException("The existing evidence directory must be private to its owner; this tool does not change permissions.");
        }
    }
    private static bool Within(string path, string root) => path.Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
