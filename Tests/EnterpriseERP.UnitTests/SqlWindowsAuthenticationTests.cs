using EnterpriseERP.Web;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class SqlWindowsAuthenticationTests
{
    private const string Valid = "Server=sql.example.invalid;Database=AppSynthetic;Integrated Security=true;Encrypt=true;TrustServerCertificate=false";
    [Fact] public void AcceptsIntegratedAndTrustedConnectionAlias()
    {
        SqlWindowsAuthentication.Validate("Windows", "SqlServer", Valid);
        SqlWindowsAuthentication.Validate("windows", "sqlserver", Valid.Replace("Integrated Security=true", "Trusted_Connection=SSPI"));
    }
    [Fact] public void PreservesExistingProviderConfiguration()
    {
        SqlWindowsAuthentication.Validate(null, "Sqlite", "Data Source=:memory:");
        SqlWindowsAuthentication.Validate("ConnectionString", "SqlServer", "Server=unused;User Id=unused;Password=unused");
    }
    [Theory]
    [InlineData("Server=sql.example.invalid;Database=AppSynthetic;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;User ID=unused")]
    [InlineData("Server=sql.example.invalid;Database=AppSynthetic;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;Password=NEVER-PRINT")]
    [InlineData("Server=sql.example.invalid;Database=AppSynthetic;Integrated Security=false;Encrypt=true;TrustServerCertificate=false")]
    [InlineData("Server=sql.example.invalid;Database=AppSynthetic;Integrated Security=true;Encrypt=false;TrustServerCertificate=false")]
    [InlineData("Server=sql.example.invalid;Database=AppSynthetic;Integrated Security=true;Encrypt=true;TrustServerCertificate=true")]
    [InlineData("Server=sql.example.invalid;Database=AppSynthetic;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;Persist Security Info=true")]
    [InlineData("Password=NEVER-PRINT;Unrecognized=hidden")]
    public void RejectsUnsafeOrConflictingSettingsWithoutLeakingSecrets(string connection)
    {
        var error = Assert.Throws<InvalidOperationException>(() => SqlWindowsAuthentication.Validate("Windows", "SqlServer", connection));
        Assert.DoesNotContain("NEVER-PRINT", error.ToString());
        Assert.Null(error.InnerException);
    }
    [Fact] public void RejectsWrongProviderAndUnknownMode()
    {
        Assert.Throws<InvalidOperationException>(() => SqlWindowsAuthentication.Validate("Windows", "Sqlite", Valid));
        Assert.Throws<InvalidOperationException>(() => SqlWindowsAuthentication.Validate("Window", "SqlServer", Valid));
    }
}
