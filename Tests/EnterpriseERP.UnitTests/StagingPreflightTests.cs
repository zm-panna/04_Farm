using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using EnterpriseERP.Web;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class StagingPreflightTests
{
    private const string SafeSql = "Server=sql.example.invalid;Database=FarmStaging;Integrated Security=true;Encrypt=Mandatory;TrustServerCertificate=false";
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "farm-preflight-tests-" + Guid.NewGuid().ToString("N"));
        public string Output => Path.Combine(Root,"evidence.json");
        public string Web => Path.Combine(Root,"web");
        public string App => Path.Combine(Root,"app");
        public Dictionary<string,string?> Values { get; } = new()
        {
            ["Database__Provider"]="SqlServer", ["ConnectionStrings__DefaultConnection"]=SafeSql,
            ["FARM_PREFLIGHT_RUNTIME_IDENTITY_CONFIRMED"]="YES", ["FARM_PREFLIGHT_PRIVATE_OUTPUT_CONFIRMED"]="YES",
            ["FARM_PREFLIGHT_EXPECTED_PROCESS_IDENTITY"]="runtime-test", ["FARM_PREFLIGHT_EXPECTED_SERVER"]="sql.example.invalid",
            ["FARM_PREFLIGHT_EXPECTED_DATABASE"]="FarmStaging", ["FARM_PREFLIGHT_EXPECTED_SQL_LOGIN"]="runtime-login",
            ["FARM_PREFLIGHT_EXPECTED_SQL_USER"]="runtime-user", ["FARM_PREFLIGHT_COMPANY_ID"]="1",
            ["FARM_PREFLIGHT_EXPECTED_TIMEZONE"]="Asia/Dhaka"
        };
        public Fixture()
        {
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(Root);
            else Directory.CreateDirectory(Root, UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            Directory.CreateDirectory(Web); Directory.CreateDirectory(App);
            Values["FARM_PREFLIGHT_WEBROOT"] = Web;
        }
        public StagingPreflightOptions Read(string? output = null, string identity = "runtime-test") => StagingPreflightOptions.FromEnvironment(output ?? Output, key => Values.GetValueOrDefault(key), identity, App);
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Fact] public void SafeConfigurationIsBoundedAndDoesNotCreateOutput()
    {
        using var f = new Fixture(); var options = f.Read();
        var sql = new SqlConnectionStringBuilder(options.ConnectionString);
        Assert.Equal(5,sql.ConnectTimeout); Assert.Equal(0,sql.ConnectRetryCount);
        Assert.False(sql.Pooling); Assert.False(sql.Enlist); Assert.False(sql.PersistSecurityInfo); Assert.False(sql.MultipleActiveResultSets);
        Assert.Equal(1,options.CompanyId); Assert.False(File.Exists(f.Output));
    }
    [Theory]
    [InlineData("Database__Provider", "Sqlite")]
    [InlineData("Database__Provider", null)]
    [InlineData("FARM_PREFLIGHT_RUNTIME_IDENTITY_CONFIRMED", "NO")]
    [InlineData("FARM_PREFLIGHT_PRIVATE_OUTPUT_CONFIRMED", null)]
    [InlineData("FARM_PREFLIGHT_EXPECTED_SERVER", null)]
    [InlineData("FARM_PREFLIGHT_EXPECTED_DATABASE", null)]
    [InlineData("FARM_PREFLIGHT_EXPECTED_SQL_LOGIN", null)]
    [InlineData("FARM_PREFLIGHT_EXPECTED_SQL_USER", null)]
    [InlineData("FARM_PREFLIGHT_EXPECTED_PROCESS_IDENTITY", null)]
    [InlineData("FARM_PREFLIGHT_WEBROOT", null)]
    [InlineData("FARM_PREFLIGHT_COMPANY_ID", "0")]
    [InlineData("FARM_PREFLIGHT_EXPECTED_TIMEZONE", "UTC")]
    public void MissingOrIncompatibleInputsFailClosed(string key,string? value)
    { using var f=new Fixture(); f.Values[key]=value; Assert.Throws<InvalidOperationException>(()=>f.Read()); Assert.False(File.Exists(f.Output)); }
    [Theory]
    [InlineData("Server=db;Database=FarmStaging;Encrypt=false;TrustServerCertificate=false")]
    [InlineData("Server=db;Database=FarmStaging;Encrypt=true;TrustServerCertificate=true")]
    [InlineData("Server=db;Database=Other;Encrypt=true;TrustServerCertificate=false")]
    [InlineData("Server=db;Encrypt=true;TrustServerCertificate=false")]
    [InlineData("Server=db;Database=FarmStaging;Encrypt=true;TrustServerCertificate=false;ApplicationIntent=ReadOnly")]
    [InlineData("Server=db;Database=FarmStaging;Encrypt=true;TrustServerCertificate=false;AttachDBFilename=C:\\fake.mdf")]
    [InlineData("Server=db;Database=FarmStaging;Encrypt=true;TrustServerCertificate=false;User Instance=true")]
    public void UnsafeSqlNeverReachesConnection(string connection)
    { using var f=new Fixture(); f.Values["ConnectionStrings__DefaultConnection"]=connection; Assert.Throws<InvalidOperationException>(()=>f.Read()); }
    [Fact] public void MalformedConnectionExceptionDoesNotEchoSecrets()
    {
        using var f=new Fixture(); f.Values["ConnectionStrings__DefaultConnection"]="Password=NEVER-PRINT;Unrecognized=SECRET-VALUE";
        var e=Assert.Throws<InvalidOperationException>(()=>f.Read());
        Assert.DoesNotContain("NEVER-PRINT",e.ToString()); Assert.DoesNotContain("SECRET-VALUE",e.ToString());
    }
    [Fact] public void DifferentProcessIdentityFailsClosed()
    { using var f=new Fixture(); Assert.Throws<InvalidOperationException>(()=>f.Read(identity:"administrator")); }
    [Fact] public void EvidenceCannotOverwriteOrUseWebrootOrApplicationDirectory()
    {
        using var f=new Fixture(); File.WriteAllText(f.Output,"existing-evidence");
        Assert.Throws<InvalidOperationException>(()=>f.Read()); Assert.Equal("existing-evidence",File.ReadAllText(f.Output));
        Assert.Throws<InvalidOperationException>(()=>f.Read(Path.Combine(f.Web,"new.json")));
        Assert.Throws<InvalidOperationException>(()=>f.Read(Path.Combine(f.App,"new.json")));
        Assert.Throws<InvalidOperationException>(()=>f.Read("relative.json"));
        Assert.Throws<InvalidOperationException>(()=>f.Read(Path.Combine(f.Root,"missing","new.json")));
    }
    [Fact] public void SymlinkDirectoryCannotRedirectEvidence()
    {
        if (OperatingSystem.IsWindows()) return;
        using var f=new Fixture(); var alias=Path.Combine(f.Root,"alias"); Directory.CreateSymbolicLink(alias,f.Web);
        Assert.Throws<InvalidOperationException>(()=>f.Read(Path.Combine(alias,"new.json")));
    }
    [Fact] public void DeclaredRootsMustExistAndCannotAliasThePrivateEvidenceDirectory()
    {
        using var f=new Fixture();
        Assert.Throws<InvalidOperationException>(()=>StagingPreflightOptions.ValidateOutput(f.Output,Path.Combine(f.Root,"missing-web"),f.App));
        if (OperatingSystem.IsWindows()) return;
        var alias=Path.Combine(f.Root,"public-alias"); Directory.CreateSymbolicLink(alias,f.Root);
        Assert.Throws<InvalidOperationException>(()=>StagingPreflightOptions.ValidateOutput(f.Output,alias,f.App));
        Assert.Throws<InvalidOperationException>(()=>StagingPreflightOptions.ValidateOutput(f.Output,f.Web,alias));
    }
    [Fact] public void UnixEvidenceDirectoryMustAlreadyBePrivate()
    {
        if (OperatingSystem.IsWindows()) return;
        using var f=new Fixture(); File.SetUnixFileMode(f.Root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute|UnixFileMode.OtherRead);
        Assert.Throws<InvalidOperationException>(()=>f.Read());
    }
    [Fact] public async Task MixedOrSecretCliArgumentsAreRejectedBeforeHostStartup()
    {
        Assert.True(StagingPreflight.Requested(["--init-demo",StagingPreflight.Switch]));
        Assert.True(StagingPreflight.Requested([StagingPreflight.Switch + "=/private/evidence.json"]));
        Assert.Equal(4,await StagingPreflight.RunAsync([StagingPreflight.Switch + "=/private/evidence.json"]));
        Assert.Equal(4,await StagingPreflight.RunAsync(["--init-demo",StagingPreflight.Switch]));
        Assert.Equal(4,await StagingPreflight.RunAsync([StagingPreflight.Switch,"output.json","Password=DO-NOT-PRINT"]));
    }
    [Fact] public void ModelManifestIncludesTenantConstraintsAndUniquenessWithoutConnecting()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SafeSql).Options);
        var expected=StagingPreflightSchema.Expected(db);
        Assert.Contains(expected,x=>x.Kind=="TABLE"&&x.Table=="Companies");
        Assert.Contains(expected,x=>x.Kind=="UNIQUE_INDEX"&&x.Table=="Animals"&&x.Definition.StartsWith("CompanyId|Tag;"));
        Assert.Contains(expected,x=>x.Kind=="FOREIGN_KEY"&&x.Table=="Animals"&&x.Definition.Contains("CompanyId|BranchId->dbo.Branches:CompanyId|Id"));
        Assert.Contains(expected,x=>x.Kind=="CHECK"&&x.Name=="CK_CompanyBusinessPolicy_Status");
        Assert.Contains(expected,x=>x.Kind=="COLUMN"&&x.Table=="Animals"&&x.Name=="RowVersion"&&x.Definition.StartsWith("rowversion:"));
        Assert.Contains(expected,x=>x.Kind=="COLUMN"&&x.Table=="Companies"&&x.Name=="Id"&&x.Definition=="int:False;IDENTITY=True;COMPUTED=False");
        var observed=expected.Select(x=>new StagingObservedSchemaItem(x.Kind,x.Schema,x.Table,x.Name,x.Definition)).ToArray();
        Assert.Empty(StagingPreflightSchema.Compare(expected,observed));
        Assert.NotEmpty(StagingPreflightSchema.Compare(expected,[]));
    }
    [Fact] public void MissingIdentityOrUnexpectedComputedColumnFailsTheSchemaGate()
    {
        var expected=new[]{new StagingSchemaItem("COLUMN","dbo","Companies","Id",StagingPreflightSchema.ColumnDefinition("int",false,true,false))};
        Assert.Single(StagingPreflightSchema.Compare(expected,[new("COLUMN","dbo","Companies","Id",StagingPreflightSchema.ColumnDefinition("int",false,false,false))]));
        Assert.Single(StagingPreflightSchema.Compare(expected,[new("COLUMN","dbo","Companies","Id",StagingPreflightSchema.ColumnDefinition("int",false,true,true))]));
    }
    [Fact] public void PolicyEffectiveDatesUseEachVersionsOwnTimezone()
    {
        var utc=new DateTimeOffset(2026,10,19,18,30,0,TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026,10,20),StagingPreflight.LocalBusinessDate("Asia/Dhaka",utc));
        Assert.Equal(new DateOnly(2026,10,19),StagingPreflight.LocalBusinessDate("America/New_York",utc));
    }
    [Theory] [InlineData(true,false,false)] [InlineData(false,true,false)] [InlineData(false,false,true)]
    public void DisabledUntrustedOrReplicationExemptConstraintsNeverPass(bool disabled,bool untrusted,bool replication)
    {
        var expected=new[]{new StagingSchemaItem("FOREIGN_KEY","dbo","Animals","TenantFk","CompanyId|BranchId->dbo.Branches:CompanyId|Id;DELETE=NO_ACTION;UPDATE=NO_ACTION")};
        var actual=new[]{new StagingObservedSchemaItem("FOREIGN_KEY","dbo","Animals","TenantFk",expected[0].Definition,disabled,untrusted,replication)};
        Assert.Single(StagingPreflightSchema.Compare(expected,actual));
    }
    [Fact] public void DifferentIndexColumnsAndFiltersNeverPass()
    {
        var expected=new[]{new StagingSchemaItem("UNIQUE_INDEX","dbo","Animals","UniqueTag","CompanyId|Tag;FILTER=")};
        Assert.NotEmpty(StagingPreflightSchema.Compare(expected,[new("UNIQUE_INDEX","dbo","Animals","UniqueTag","Tag;FILTER=")]));
        Assert.NotEmpty(StagingPreflightSchema.Compare(expected,[new("UNIQUE_INDEX","dbo","Animals","UniqueTag","CompanyId|Tag;FILTER=[Tag] IS NOT NULL")]));
    }
    [Theory]
    [InlineData("[NormalizedName] IS NOT NULL", "([NormalizedName] IS NOT NULL)")]
    [InlineData("[IsClosed] = 0", "([IsClosed]=(0))")]
    public void SqlServerParenthesesForSimpleIndexFiltersAreAccepted(string model,string catalog)
    { Assert.Equal(StagingPreflightSchema.NormalizeFilter(model),StagingPreflightSchema.NormalizeFilter(catalog)); }
    [Fact] public void ComplexFilterGroupingIsNotDiscarded()
    { Assert.NotEqual(StagingPreflightSchema.NormalizeFilter("([A]=1 OR [B]=1) AND [C]=1"),StagingPreflightSchema.NormalizeFilter("[A]=1 OR ([B]=1 AND [C]=1)")); }
    [Fact] public void FixedSqlUsesOnlySelectAndCompanyReadsAreParameterizedBoundedSeeks()
    {
        foreach(var field in typeof(StagingPreflightSql).GetFields(BindingFlags.Public|BindingFlags.Static))
        {
            var sql=Assert.IsType<string>(field.GetValue(null)); Assert.StartsWith("SELECT",sql.TrimStart());
            // Literal words in permission names are data; reject executable statements at line/semicolon boundaries.
            Assert.DoesNotMatch(@"(?im)(?:^|;)\s*(?:INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|REVOKE|DENY|EXEC|DBCC|SET|BEGIN)\b",sql);
            if(field.Name!="Identity") Assert.Contains("TOP (@limit)",sql);
        }
        foreach(var sql in new[]{StagingPreflightSql.Company,StagingPreflightSql.Policies,StagingPreflightSql.CurrencyLock,StagingPreflightSql.AuditMarkers,StagingPreflightSql.TaxMarkers})
        { Assert.Contains("@company",sql); Assert.Contains("WITH (FORCESEEK)",sql); Assert.Contains("MAXDOP 1",sql); Assert.DoesNotContain("SELECT *",sql); }
    }
    [Fact] public void UnexecutedChecksAndManualGatesNeverBecomeProductionApproval()
    {
        var r=StagingPreflightReport.Create(); Assert.Equal("NOT_VERIFIED",r.CollectorStatus); Assert.Equal(3,r.ExitCode);
        foreach(var item in r.Checks.Where(c=>c.Automated).ToArray()) r.Set(item.Id,"PASS","Synthetic unit-test evidence only.");
        Assert.Equal("PASS",r.CollectorStatus); Assert.Equal(0,r.ExitCode); Assert.False(r.ProductionApproved);
        Assert.Contains(r.Checks,x=>!x.Automated&&x.Status=="NOT_VERIFIED");
        Assert.Contains(r.Checks,x=>x.Id=="productionSafetyConfiguration"&&x.Status=="NOT_VERIFIED"&&!x.Automated);
        r.Set("migrations","FAIL","Synthetic mismatch."); Assert.Equal(2,r.ExitCode);
        var json=JsonSerializer.Serialize(r,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        Assert.Contains("\"productionApproved\":false",json); Assert.DoesNotContain("ConnectionString",json);
    }
    [Fact] public void FingerprintsDoNotIncludeRawIdentity()
    { var value=StagingPreflightOptions.Fingerprint("runtime@example.invalid"); Assert.Equal(64,value.Length); Assert.Matches("^[0-9a-f]+$",value); Assert.DoesNotContain("runtime",value); }
}
