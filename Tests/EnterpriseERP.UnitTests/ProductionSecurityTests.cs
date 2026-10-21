using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class ProductionSecurityTests
{
 private const string SafeSql = "Server=db.example.invalid;Database=Farm;Integrated Security=true;Encrypt=Mandatory;TrustServerCertificate=false";
 private static IConfiguration Config(Dictionary<string, string?>? overrides = null)
 {
  var values = new Dictionary<string, string?>
  {
   ["AllowedHosts"] = "farm.example.invalid;erp.example.invalid",
   ["Security:DataProtection:KeyDirectory"] = Path.Combine(Path.GetTempPath(), "farm-production-keys"),
   ["Security:DataProtection:ApplicationName"] = "FarmERP-Production",
   ["Security:DataProtection:ProtectionMode"] = "Certificate",
   ["Security:DataProtection:CertificatePath"] = Path.Combine(Path.GetTempPath(), "farm-protection.pfx")
  };
  if (overrides != null) foreach (var item in overrides) values[item.Key] = item.Value;
  return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
 }
 [Theory]
 [InlineData("Server=db;Database=Farm;Encrypt=false;TrustServerCertificate=false")]
 [InlineData("Server=db;Database=Farm;Encrypt=Optional;TrustServerCertificate=false")]
 [InlineData("Server=db;Database=Farm;Encrypt=true;TrustServerCertificate=true")]
 [InlineData("Server=db;Encrypt=true;TrustServerCertificate=false")]
 public void InsecureSqlConfigurationFailsClosed(string connection)
 { Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Validate(Config(), connection)); }
 [Fact] public void InvalidConnectionErrorNeverEchoesCredentials()
 {
  var error = Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Validate(Config(), "Server=db;Password=DO-NOT-EXPOSE;UnknownOption=secret-value"));
  Assert.DoesNotContain("DO-NOT-EXPOSE", error.ToString()); Assert.DoesNotContain("secret-value", error.ToString());
 }
 [Theory] [InlineData("Mandatory")] [InlineData("Strict")] [InlineData("true")]
 public void VerifiedSqlEncryptionIsAccepted(string encryption)
 { Assert.Equal("FarmERP-Production", ProductionSecurityOptions.Validate(Config(), $"Server=db;Database=Farm;Encrypt={encryption};TrustServerCertificate=false").ApplicationName); }
 [Theory] [InlineData("")] [InlineData("*")] [InlineData("*.example.invalid")] [InlineData("farm.example.invalid;*")] [InlineData("https://farm.example.invalid")] [InlineData("farm.example.invalid:443")]
 public void MissingOrWildcardHostsAreRejected(string hosts)
 { Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Validate(Config(new() { ["AllowedHosts"] = hosts }), SafeSql)); }
 [Theory] [InlineData("relative/keys")] [InlineData("")]
 public void KeyDirectoryMustBeExplicitAndAbsolute(string directory)
 { Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Validate(Config(new() { ["Security:DataProtection:KeyDirectory"] = directory }), SafeSql)); }
 [Fact] public void WebRootCannotHoldKeyRingOrPrivateCertificate()
 {
  var web = Path.Combine(Path.GetTempPath(), "webroot");
  Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Validate(Config(new() { ["Security:DataProtection:KeyDirectory"] = Path.Combine(web, "keys") }), SafeSql, web));
  Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Validate(Config(new() { ["Security:DataProtection:CertificatePath"] = Path.Combine(web, "private.pfx") }), SafeSql, web));
 }
 [Fact] public void DpapiIsExplicitlyWindowsOnly()
 {
  var config = Config(new() { ["Security:DataProtection:ProtectionMode"] = "Dpapi" });
  Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Validate(config, SafeSql, isWindows: false));
  Assert.Equal("Dpapi", ProductionSecurityOptions.Validate(config, SafeSql, isWindows: true).ProtectionMode);
 }
 [Fact] public void LinuxCannotFallbackToUnencryptedKeys()
 { Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Validate(Config(new() { ["Security:DataProtection:ProtectionMode"] = null }), SafeSql, isWindows: false)); }
 [Fact] public void PrivateDirectoryMustBeProvisionedBeforeProductionStartup()
 {
  var path = Path.Combine(Path.GetTempPath(), "missing-farm-keys-" + Guid.NewGuid().ToString("N"));
  Assert.Throws<InvalidOperationException>(() => ProductionSecurityOptions.Configure(new ServiceCollection(), Config(new() { ["Security:DataProtection:KeyDirectory"] = path }), SafeSql));
  Assert.False(Directory.Exists(path));
 }
 [Fact] public void CertificateProtectedKeyRingSurvivesProviderRestart()
 {
  var root = Path.Combine(Path.GetTempPath(), "farm-key-test-" + Guid.NewGuid().ToString("N"));
  if (OperatingSystem.IsWindows()) Directory.CreateDirectory(root);
  else Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
  try
  {
   using var rsa = RSA.Create(2048);
   var request = new CertificateRequest("CN=Farm ephemeral test only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
   using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
   var pfx = Path.Combine(root, "test-only.pfx"); var password = Guid.NewGuid().ToString("N");
   File.WriteAllBytes(pfx, certificate.Export(X509ContentType.Pfx, password));
   if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(pfx, UnixFileMode.UserRead | UnixFileMode.UserWrite);
   var config = Config(new() { ["Security:DataProtection:KeyDirectory"] = root, ["Security:DataProtection:CertificatePath"] = pfx, ["Security:DataProtection:CertificatePassword"] = password });
   string protectedValue;
   var first = new ServiceCollection(); first.AddLogging(); ProductionSecurityOptions.Configure(first, config, SafeSql);
   using (var provider = first.BuildServiceProvider()) { ProductionSecurityOptions.VerifyDataProtection(provider); protectedValue = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie-session-test").Protect("synthetic-session-data"); }
   var second = new ServiceCollection(); second.AddLogging(); ProductionSecurityOptions.Configure(second, config, SafeSql);
   using (var provider = second.BuildServiceProvider()) Assert.Equal("synthetic-session-data", provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie-session-test").Unprotect(protectedValue));
   var files = Directory.GetFiles(root, "key-*.xml"); Assert.NotEmpty(files);
   Assert.All(files, file => { var xml = File.ReadAllText(file); Assert.Contains("encryptedSecret", xml); Assert.DoesNotContain("synthetic-session-data", xml); });
  }
  finally { Directory.Delete(root, recursive: true); }
 }
 [Fact] public async Task SensitiveResponsesHaveBrowserDefensesAndNoStore()
 {
  var context = new DefaultHttpContext(); context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") }, "test"));
  var middleware = new SecurityHeadersMiddleware(http => { http.Response.Headers.CacheControl = "public, max-age=3600"; return Task.CompletedTask; });
  await middleware.Invoke(context);
  Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString()); Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
  Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString()); Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());
  Assert.Contains("frame-ancestors 'none'", context.Response.Headers["Content-Security-Policy"].ToString());
  var scriptPolicy = context.Response.Headers["Content-Security-Policy"].ToString().Split(';').Select(x => x.Trim()).Single(x => x.StartsWith("script-src ", StringComparison.Ordinal));
  Assert.Equal("script-src 'self'", scriptPolicy); Assert.DoesNotContain("unsafe-inline", scriptPolicy); Assert.DoesNotContain("unsafe-eval", scriptPolicy);
  Assert.Contains("style-src 'self' 'unsafe-inline'", context.Response.Headers["Content-Security-Policy"].ToString()); // Existing inline styles still require a style-only allowance.
 }
 [Fact] public async Task AnonymousLoginTokensAreNotCacheable()
 {
  var context = new DefaultHttpContext(); context.Request.Path = "/Account/Login";
  await new SecurityHeadersMiddleware(_ => Task.CompletedTask).Invoke(context);
  Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());
 }
}
