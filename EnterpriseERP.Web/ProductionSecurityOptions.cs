using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
namespace EnterpriseERP.Web;

public sealed record ProductionSecuritySettings(string KeyDirectory, string ApplicationName, string ProtectionMode, string? CertificatePath);

/// <summary>Fail-closed production preflight. Does not connect to SQL Server or emit secrets.</summary>
public static class ProductionSecurityOptions
{
 public static ProductionSecuritySettings Validate(IConfiguration config, string connectionString, string? webRootPath = null, bool? isWindows = null)
 {
  SqlConnectionStringBuilder sql;
  try { sql = new SqlConnectionStringBuilder(connectionString); }
  catch (Exception ex) when (ex is ArgumentException or FormatException)
  { throw new InvalidOperationException("Production SQL Server connection configuration is invalid."); }
  var encryption = sql.Encrypt.ToString();
  if (string.IsNullOrWhiteSpace(sql.DataSource) || string.IsNullOrWhiteSpace(sql.InitialCatalog)
   || !(encryption.Equals("Mandatory", StringComparison.OrdinalIgnoreCase) || encryption.Equals("Strict", StringComparison.OrdinalIgnoreCase) || encryption.Equals("True", StringComparison.OrdinalIgnoreCase))
   || sql.TrustServerCertificate)
   throw new InvalidOperationException("Production SQL Server requires a database, mandatory/strict encryption, and a verifiable server certificate (TrustServerCertificate=false).");

  var hosts = (config["AllowedHosts"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
  if (hosts.Length == 0 || hosts.Any(host => host.Contains('*') || host.Contains('/') || host.Contains(':') || host.Contains('+') || Uri.CheckHostName(host) == UriHostNameType.Unknown || host == "0.0.0.0"))
   throw new InvalidOperationException("Production AllowedHosts must contain specific DNS names or IPv4 addresses separated by semicolons; wildcards, schemes and ports are prohibited.");
  var directory = config["Security:DataProtection:KeyDirectory"];
  if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
   throw new InvalidOperationException("Configure an absolute persistent Security:DataProtection:KeyDirectory outside the web root.");
  directory = Path.GetFullPath(directory);
  if (directory == Path.GetPathRoot(directory) || (!string.IsNullOrWhiteSpace(webRootPath) && IsWithin(directory, Path.GetFullPath(webRootPath))))
   throw new InvalidOperationException("Data protection keys cannot be stored in the web root or filesystem root.");
  var name = config["Security:DataProtection:ApplicationName"];
  if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
   throw new InvalidOperationException("Configure a stable Security:DataProtection:ApplicationName (max 200 characters).");
  var mode = config["Security:DataProtection:ProtectionMode"];
  if (mode is not ("Dpapi" or "Certificate"))
   throw new InvalidOperationException("Set DataProtection:ProtectionMode to Dpapi on Windows or Certificate with a private X.509 certificate.");
  if (mode == "Dpapi" && !(isWindows ?? OperatingSystem.IsWindows()))
   throw new InvalidOperationException("DPAPI is Windows-only. Non-Windows production requires explicitly configured certificate protection.");
  string? certificatePath = null;
  if (mode == "Certificate")
  {
   certificatePath = config["Security:DataProtection:CertificatePath"];
   if (string.IsNullOrWhiteSpace(certificatePath) || !Path.IsPathFullyQualified(certificatePath))
    throw new InvalidOperationException("Certificate protection requires an absolute CertificatePath. Supply its password through the deployment secret store.");
   certificatePath = Path.GetFullPath(certificatePath);
   if (!string.IsNullOrWhiteSpace(webRootPath) && IsWithin(certificatePath, Path.GetFullPath(webRootPath)))
    throw new InvalidOperationException("The data protection certificate cannot be stored inside the web root.");
  }
  return new(directory, name.Trim(), mode, certificatePath);
 }
 private static bool IsWithin(string path, string parent) => path.Equals(parent.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
  || path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

 public static void VerifyDataProtection(IServiceProvider services)
 {
  try
  {
   var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("FarmERP.StartupKeyRingCheck.v1");
   var probe = Guid.NewGuid().ToString("N");
   if (protector.Unprotect(protector.Protect(probe)) != probe) throw new CryptographicException();
  }
  catch (Exception)
  { throw new InvalidOperationException("Persistent data protection failed its startup verification. Check directory access, application identity, and key encryption configuration."); }
 }

 public static void Configure(IServiceCollection services, IConfiguration config, string connectionString, string? webRootPath = null)
 {
  var settings = Validate(config, connectionString, webRootPath);
  if (!Directory.Exists(settings.KeyDirectory))
   throw new InvalidOperationException("Provision the persistent data protection key directory and restrict it to the application identity before starting production.");
  if (new DirectoryInfo(settings.KeyDirectory).LinkTarget != null)
   throw new InvalidOperationException("The data protection key directory must be a dedicated directory, not a symbolic link.");
  if (!OperatingSystem.IsWindows())
  {
   var permissions = File.GetUnixFileMode(settings.KeyDirectory);
   const UnixFileMode publicAccess = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
   if ((permissions & publicAccess) != 0)
    throw new InvalidOperationException("The data protection key directory must be accessible only to its application owner (Unix mode 0700).");
  }
  var protection = services.AddDataProtection().SetApplicationName(settings.ApplicationName).PersistKeysToFileSystem(new DirectoryInfo(settings.KeyDirectory));
  if (settings.ProtectionMode == "Dpapi")
  {
   if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("DPAPI requires Windows.");
   protection.ProtectKeysWithDpapi(protectToLocalMachine: false);
   return;
  }
  if (!OperatingSystem.IsWindows() && File.Exists(settings.CertificatePath))
  {
   const UnixFileMode publicReadOrWrite = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite;
   if ((File.GetUnixFileMode(settings.CertificatePath!) & publicReadOrWrite) != 0)
    throw new InvalidOperationException("The private data protection certificate must be readable only by its application owner.");
  }
  X509Certificate2 certificate;
  try
  {
   certificate = X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath!, config["Security:DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
   using var rsa = certificate.GetRSAPrivateKey();
   if (!certificate.HasPrivateKey || rsa is null || rsa.KeySize < 2048 || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
   { certificate.Dispose(); throw new CryptographicException(); }
  }
  catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
  { throw new InvalidOperationException("The data protection certificate could not be loaded or is invalid; provide a valid RSA private certificate of at least 2048 bits."); }
  // Data Protection retains this certificate for its lifetime. Never log or expose its private key/password.
  protection.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
 }
}
