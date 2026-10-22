using System.Data;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EnterpriseERP.Web;

/// <summary>Explicit local export of an unchanged, synthetic V3 fixture. Never executes SQL on a target.</summary>
public static class DemoSqlExporter
{
    public const string FingerprintAction = "DemoSeedFingerprintV3";
    private const string FingerprintPrefix = "SHA256:";
    private static readonly MethodInfo LoadMethod = typeof(DemoSqlExporter).GetMethod(nameof(LoadAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly IReadOnlyDictionary<Type, int> ExpectedCounts = new Dictionary<Type, int>
    {
        [typeof(ScanLabel)] = 0, [typeof(ScanIntent)] = 0,
        [typeof(ScaleCacheEpoch)] = 15,
        [typeof(InternalMessage)] = 0, [typeof(UserInboxNotification)] = 0, [typeof(MessagingOutbox)] = 0, [typeof(MessagingRateBucket)] = 0, [typeof(MessagingAutomationRead)] = 0,
        [typeof(CompanyCurrencyLock)] = 0, [typeof(CompanyBusinessPolicy)] = 0, [typeof(TaxPolicy)] = 0,
        [typeof(HRPayrollStaff)] = 0, [typeof(HRPayrollCompensation)] = 0, [typeof(HRPayrollAttendance)] = 0, [typeof(HRPayrollLeave)] = 0, [typeof(HRPayrollPolicy)] = 0, [typeof(HRPayrollRun)] = 0, [typeof(HRPayrollLine)] = 0,
        [typeof(CRMLead)] = 0, [typeof(CRMConsentEvent)] = 0, [typeof(CRMOpportunity)] = 0, [typeof(CRMActivity)] = 0, [typeof(CRMQuote)] = 0, [typeof(CRMQuoteLine)] = 0, [typeof(CRMSalesReady)] = 0,
        [typeof(FixedAsset)] = 0, [typeof(FixedAssetCustody)] = 0, [typeof(FixedAssetDepreciationRun)] = 0, [typeof(FixedAssetDepreciationLine)] = 0, [typeof(FixedAssetDisposal)] = 0,
        [typeof(EcommerceCustomer)] = 0, [typeof(EcommerceProduct)] = 0, [typeof(EcommerceCart)] = 0, [typeof(EcommerceCartLine)] = 0, [typeof(EcommerceOrder)] = 0, [typeof(EcommerceOrderLine)] = 0, [typeof(EcommerceStockReservation)] = 0, [typeof(EcommerceOrderEvent)] = 0, [typeof(EcommerceFulfillment)] = 0, [typeof(EcommerceReturn)] = 0,
        [typeof(Company)] = 5, [typeof(Farm)] = 5, [typeof(Branch)] = 10, [typeof(Animal)] = 15,
        [typeof(StockItem)] = 10, [typeof(InventoryCostBalance)] = 10, [typeof(StockMovement)] = 45,
        [typeof(OpeningBalance)] = 5, [typeof(OpeningStockValue)] = 5, [typeof(OpeningAnimalValue)] = 5,
        [typeof(StockTransfer)] = 10, [typeof(MilkYield)] = 5, [typeof(AnimalSale)] = 5,
        [typeof(PurchaseReceipt)] = 5, [typeof(ProductSale)] = 5, [typeof(StockDocumentReturn)] = 5,
        [typeof(TradingParty)] = 10, [typeof(PurchaseOrder)] = 5, [typeof(PurchaseOrderReceipt)] = 5,
        [typeof(StockReorderPolicy)] = 5, [typeof(Voucher)] = 70, [typeof(VoucherLine)] = 170,
        [typeof(FinanceDocument)] = 25, [typeof(FinancePayment)] = 5, [typeof(FinancePaymentAllocation)] = 5,
        [typeof(BankStatementLine)] = 5, [typeof(AnimalHealthRecord)] = 5, [typeof(AnimalFeedRecord)] = 5,
        [typeof(FatteningPlan)] = 5, [typeof(AnimalWeightRecord)] = 5, [typeof(FarmExpense)] = 5,
        [typeof(BreedingCycle)] = 5, [typeof(BreedingCycleEvent)] = 5, [typeof(SupportTicket)] = 5,
        [typeof(SupportTicketActivity)] = 5, [typeof(SubscriptionInvoice)] = 5, [typeof(SubscriptionPayment)] = 5,
        [typeof(CompanySubscriptionPolicy)] = 5, [typeof(AutomationRule)] = 5, [typeof(AuditEntry)] = 6,
        [typeof(AppUser)] = 5, [typeof(IdentityRole)] = 5, [typeof(IdentityUserRole<string>)] = 5,
        [typeof(UserCompanyAccess)] = 5, [typeof(UserBranchAccess)] = 10, [typeof(UserPermission)] = 53,
        [typeof(AccountingPeriodClose)] = 0, [typeof(ScheduledJob)] = 0, [typeof(AutomationApproval)] = 0,
        [typeof(AutomationExecution)] = 0, [typeof(InAppNotification)] = 0,
        [typeof(IdentityUserToken<string>)] = 0, [typeof(IdentityUserLogin<string>)] = 0,
        [typeof(IdentityUserClaim<string>)] = 0, [typeof(IdentityRoleClaim<string>)] = 0
    };

    public static async Task<int> RunAsync(IServiceProvider services, string outputPath, CancellationToken ct = default)
    {
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Demo SQL export is Development-only.");
        if (Environment.GetEnvironmentVariable("FARM_ALLOW_DEMO_EXPORT") != "YES")
            throw new InvalidOperationException("Set FARM_ALLOW_DEMO_EXPORT=YES explicitly. Exported SQL contains sensitive Identity password hashes; keep it private.");
        var webRoot = !string.IsNullOrWhiteSpace(environment.WebRootPath) ? environment.WebRootPath
            : !string.IsNullOrWhiteSpace(environment.ContentRootPath) ? Path.Combine(environment.ContentRootPath, "wwwroot") : null;
        var path = ValidateOutputPath(outputPath, webRoot);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();
        var provider = db.Database.ProviderName ?? "";
        DemoBusinessSeeder.ValidateTarget("Development", provider,
            provider.Contains("Sqlite", StringComparison.Ordinal) ? connection.DataSource : connection.Database, "YES");
        string script = "";
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var snapshot = await ReadSnapshotAsync(db, ct);
            ValidatePristineFixture(snapshot);
            script = RenderSql(snapshot);
            await tx.CommitAsync(ct);
        });
        // CreateNew is essential: never replace an existing export, database, or arbitrary user file.
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using (var file = new FileStream(path, options))
        await using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
            await writer.WriteAsync(script.AsMemory(), ct);
        Console.WriteLine("Demo SQL export created. Contains sensitive Identity hashes: keep the file private. No target SQL Server was connected or modified.");
        return 0;
    }

    public static string ValidateOutputPath(string outputPath, string? webRootPath = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathFullyQualified(outputPath)
            || !string.Equals(Path.GetExtension(outputPath), ".sql", StringComparison.OrdinalIgnoreCase)
            || outputPath.StartsWith(@"\\", StringComparison.Ordinal) || outputPath.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidOperationException("Choose an absolute local .sql output path; network/relative destinations are not accepted.");
        var path = Path.GetFullPath(outputPath);
        if (!string.IsNullOrWhiteSpace(webRootPath))
        {
            var root = Path.GetFullPath(webRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidOperationException("Sensitive demo exports cannot be written into the application's public web root. Choose a private local directory.");
        }
        if (File.Exists(path) || Directory.Exists(path)) throw new InvalidOperationException("The export destination already exists. Choose a new file; overwrite is disabled.");
        var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
        if (!directory.Exists) throw new InvalidOperationException("Create the private local output directory first.");
        for (var parent = directory; parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Export destinations through symbolic links/reparse points are not accepted.");
        return path;
    }

    /// <summary>Called once, inside the seed transaction, after all completion markers have been saved.</summary>
    public static async Task<string> ComputeFixtureFingerprintAsync(AppDbContext db, CancellationToken ct = default) =>
        Fingerprint(await ReadSnapshotAsync(db, ct));

    private static async Task<List<object>> LoadAsync<TEntity>(AppDbContext db, CancellationToken ct) where TEntity : class =>
        (await db.Set<TEntity>().ToListAsync(ct)).Cast<object>().ToList();

    private static async Task<List<TableData>> ReadSnapshotAsync(AppDbContext db, CancellationToken ct)
    {
        var tables = new List<TableData>();
        foreach (var entity in db.Model.GetEntityTypes().OrderBy(x => x.GetSchema() ?? "dbo", StringComparer.Ordinal).ThenBy(x => x.GetTableName(), StringComparer.Ordinal))
        {
            var name = entity.GetTableName() ?? throw new InvalidOperationException("Unmapped entity cannot be exported.");
            var store = StoreObjectIdentifier.Table(name, entity.GetSchema());
            var columns = entity.GetProperties().Where(p => !(p.ClrType == typeof(byte[]) && p.IsConcurrencyToken)
                && p.ValueGenerated != ValueGenerated.OnAddOrUpdate).OrderBy(p => p.GetColumnName(store), StringComparer.Ordinal).ToArray();
            if (columns.Any(p => p.GetColumnName(store) is null) || tables.Any(x => x.Name == name && x.Schema == (entity.GetSchema() ?? "dbo")))
                throw new InvalidOperationException("Unsupported split-table/model mapping; no export was written.");
            var entities = await (Task<List<object>>)LoadMethod.MakeGenericMethod(entity.ClrType).Invoke(null, [db, ct])!;
            var rows = entities.Select(value => new RowData(value, columns.Select(p => SqlLiteral(db.Entry(value).Property(p.Name).CurrentValue)).ToArray()))
                .OrderBy(row => string.Join(",", row.Values), StringComparer.Ordinal).ToList();
            var key = entity.FindPrimaryKey();
            var identity = key?.Properties.Count == 1 && key.Properties[0].ValueGenerated == ValueGenerated.OnAdd
                && IsInteger(key.Properties[0].ClrType);
            tables.Add(new TableData(entity, entity.GetSchema() ?? "dbo", name,
                columns.Select(p => p.GetColumnName(store)!).ToArray(), rows, identity));
        }
        return tables;
    }

    private static bool IsInteger(Type type) => type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte);
    private static string Fingerprint(IEnumerable<TableData> tables)
    {
        var text = new StringBuilder("Farm demo canonical data v1\n");
        // Cache generations are derived invalidation metadata, not part of the synthetic business fingerprint.
        foreach (var table in tables.Where(t => t.Entity.ClrType != typeof(ScaleCacheEpoch)))
        {
            text.Append(table.QualifiedName).Append('(').AppendJoin(',', table.Columns.Select(Quote)).Append(")\n");
            foreach (var row in table.Rows.Where(r => r.Entity is not AuditEntry { Action: FingerprintAction }))
                text.Append('(').AppendJoin(',', row.Values).Append(");\n");
        }
        return FingerprintPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static void ValidatePristineFixture(List<TableData> tables)
    {
        var tenantCount = tables.Single(t => t.Entity.ClrType == typeof(Company)).Rows.Count;
        if (tenantCount is 10 or 20)
        {
            if (tables.Any(t => t.Rows.Count < tenantCount))
                throw new InvalidOperationException("Minimum-row export requires at least the tenant count in every mapped table.");
            var demoAudits = tables.Single(t => t.Entity.ClrType == typeof(AuditEntry)).Rows.Select(r => (AuditEntry)r.Entity).ToList();
            var saved = demoAudits.SingleOrDefault(a => a.Action == FingerprintAction && a.ActorId == "DEMO-CLI");
            if (saved is null || saved.Reference != Fingerprint(tables)
                || demoAudits.Count(a => a.Action == DemoBusinessSeeder.CompletionAction) != tenantCount
                || demoAudits.Count(a => a.Action == EnterpriseDemoSeeder.CompletionAction) != tenantCount)
                throw new InvalidOperationException("Minimum-row export requires the unchanged complete synthetic enterprise fixture.");
            return;
        }
        var enterprise = tables.Single(t => t.Entity.ClrType == typeof(AuditEntry)).Rows.Any(r => r.Entity is AuditEntry a && a.Action == EnterpriseDemoSeeder.CompletionAction);
        var expected = ExpectedCounts.ToDictionary(x => x.Key, x => x.Value);
        if (enterprise)
        {
            expected[typeof(ScanLabel)] = 25; expected[typeof(ScanIntent)] = 5; expected[typeof(AuditEntry)] += 35; expected[typeof(UserPermission)] += 7;
            expected[typeof(InternalMessage)] = 5; expected[typeof(UserInboxNotification)] = 5; expected[typeof(MessagingOutbox)] = 5;
            expected[typeof(MessagingRateBucket)] = 10; expected[typeof(MessagingAutomationRead)] = 5;
            expected[typeof(InAppNotification)] += 5; expected[typeof(AuditEntry)] += 12; expected[typeof(UserPermission)] += 7;
            foreach (var type in new[] { typeof(CompanyCurrencyLock), typeof(CompanyBusinessPolicy), typeof(TaxPolicy),
                typeof(HRPayrollStaff), typeof(HRPayrollCompensation), typeof(HRPayrollAttendance), typeof(HRPayrollLeave), typeof(HRPayrollPolicy), typeof(HRPayrollRun), typeof(HRPayrollLine),
                typeof(CRMLead), typeof(CRMConsentEvent), typeof(CRMOpportunity), typeof(CRMActivity), typeof(CRMQuote), typeof(CRMQuoteLine), typeof(CRMSalesReady),
                typeof(FixedAsset), typeof(FixedAssetCustody), typeof(FixedAssetDepreciationRun), typeof(FixedAssetDepreciationLine), typeof(FixedAssetDisposal),
                typeof(EcommerceCustomer), typeof(EcommerceProduct), typeof(EcommerceCart), typeof(EcommerceCartLine), typeof(EcommerceOrder), typeof(EcommerceOrderLine),
                typeof(EcommerceStockReservation), typeof(EcommerceFulfillment), typeof(EcommerceReturn) }) expected[type] = 5;
            expected[typeof(EcommerceOrderEvent)] = 20;
            expected[typeof(AppUser)] += 5; expected[typeof(UserCompanyAccess)] += 5; expected[typeof(UserBranchAccess)] += 5;
            expected[typeof(UserPermission)] += 5 + 5 * PermissionService.GrantablePermissions.Count(x => x.StartsWith("HR.") || x.StartsWith("Payroll.") || x.StartsWith("Employee.") || x.StartsWith("CRM.") || x.StartsWith("FixedAssets.") || x.StartsWith("Ecommerce.") || x.StartsWith("BusinessPolicy.") || x.StartsWith("TaxPolicy.") || x == "AI.Read");
            expected[typeof(Voucher)] += 30; expected[typeof(VoucherLine)] += 115; expected[typeof(StockMovement)] += 10;
            expected[typeof(ProductSale)] += 5; expected[typeof(FinanceDocument)] += 5; expected[typeof(StockDocumentReturn)] += 5; expected[typeof(AuditEntry)] += 65;
        }
        if (tables.Count != expected.Count || tables.Any(t => !expected.TryGetValue(t.Entity.ClrType, out var count) || t.Rows.Count != count))
            throw new InvalidOperationException("Export requires an exact untouched supported synthetic fixture. Extra, missing, or unsupported rows were found; no file was written.");
        List<T> Rows<T>() => tables.Single(t => t.Entity.ClrType == typeof(T)).Rows.Select(r => (T)r.Entity).ToList();
        var companies = Rows<Company>(); var users = Rows<AppUser>(); var roles = Rows<IdentityRole>(); var audits = Rows<AuditEntry>();
        for (var number = 1; number <= 5; number++)
        {
            var email = $"farmdemo{number}@example.invalid";
            if (!companies.Any(c => c.Slug == $"demo-farm-{number}" && c.Name == $"DEMO Farm {number}")
                || !users.Any(u => u.Email == email && u.UserName == email && u.NormalizedEmail == email.ToUpperInvariant()
                    && u.NormalizedUserName == email.ToUpperInvariant() && !u.TwoFactorEnabled && u.PhoneNumber == null
                    && !u.PhoneNumberConfirmed && u.EmailConfirmed && u.LockoutEnabled && u.LockoutEnd == null
                    && u.AccessFailedCount == 0 && !string.IsNullOrWhiteSpace(u.PasswordHash) && !string.IsNullOrWhiteSpace(u.SecurityStamp)))
                throw new InvalidOperationException("Only the five unchanged synthetic demo companies/accounts may be exported. No file was written.");
        }
        if (enterprise)
        {
            for (var number = 1; number <= 5; number++)
            {
                var email = $"farmreviewer{number}@example.invalid";
                if (!users.Any(u => u.Email == email && u.UserName == email && u.EmailConfirmed && !u.TwoFactorEnabled && u.PhoneNumber == null
                    && !u.PhoneNumberConfirmed && u.LockoutEnabled && u.LockoutEnd == null && u.AccessFailedCount == 0
                    && !string.IsNullOrWhiteSpace(u.PasswordHash) && !string.IsNullOrWhiteSpace(u.SecurityStamp)))
                    throw new InvalidOperationException("Only unchanged fictional enterprise reviewer identities may be exported.");
            }
            if (audits.Count(x => x.Action == EnterpriseDemoSeeder.CompletionAction && x.Reference == EnterpriseDemoSeeder.SeedVersion && x.ActorId == "DEMO-CLI") != 5)
                throw new InvalidOperationException("Fictional enterprise fixture completion markers are missing.");
        }
        if (roles.Any(r => !DemoIdentitySeeder.RoleNames.Contains(r.Name, StringComparer.Ordinal))
            || roles.Select(r => r.Name).Distinct().Count() != 5)
            throw new InvalidOperationException("Demo export refuses platform or unexpected roles.");
        var markers = audits.Where(a => a.Action == DemoBusinessSeeder.CompletionAction && a.Reference == DemoBusinessSeeder.SeedVersion && a.ActorId == "DEMO-CLI").ToList();
        var fingerprint = audits.SingleOrDefault(a => a.Action == FingerprintAction && a.ActorId == "DEMO-CLI");
        if (markers.Count != 5 || markers.Select(x => x.CompanyId).Distinct().Count() != 5
            || companies.Any(c => !markers.Any(m => m.CompanyId == c.Id)) || fingerprint is null
            || fingerprint.Reference != Fingerprint(tables))
            throw new InvalidOperationException("Demo source fingerprint is missing or changed. Export a new isolated fixture before login, MFA enrollment, editing, or other use. No file was written.");
    }

    private static List<TableData> DependencyOrder(List<TableData> tables)
    {
        var remaining = tables.ToDictionary(t => t.Entity); var ordered = new List<TableData>();
        while (remaining.Count > 0)
        {
            var ready = remaining.Values.Where(t => t.Entity.GetForeignKeys().All(f => f.PrincipalEntityType == t.Entity || !remaining.ContainsKey(f.PrincipalEntityType)))
                .OrderBy(t => t.QualifiedName, StringComparer.Ordinal).ToList();
            if (ready.Count == 0) throw new InvalidOperationException("A foreign-key cycle prevents safe export ordering.");
            foreach (var table in ready) { ordered.Add(table); remaining.Remove(table.Entity); }
        }
        return ordered;
    }

    private static string RenderSql(List<TableData> tables)
    {
        var sql = new StringBuilder("-- FARM-DEMO: synthetic core/optional enterprise data only. Fictional policies and approvals are not statutory guidance. Apply the separately reviewed SQL Server schema first.\n" +
            "-- SECURITY SENSITIVE: contains real Identity password hashes/security stamps. Keep private; never publish.\n" +
            "-- No plaintext password, authenticator secret, recovery code, external token, or real customer data is exported.\n" +
            "-- The password is the one chosen locally when seeding. Export does not reset or reveal it.\n" +
            "-- Run only against an EMPTY isolated DEMO database, after verifying the connection.\n" +
            "SET NOCOUNT ON;\nSET XACT_ABORT ON;\n" +
            "IF DB_NAME() IS NULL OR CHARINDEX(N'demo', LOWER(DB_NAME())) = 0 OR CHARINDEX(N'prod', LOWER(DB_NAME())) > 0\n" +
            "    THROW 51000, 'Import requires an isolated database name containing demo and excluding prod.', 1;\n" +
            "SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;\nDECLARE @IdentityTable nvarchar(776) = NULL;\nBEGIN TRY\n    BEGIN TRANSACTION;\n");
        foreach (var table in tables)
        {
            sql.Append("    IF OBJECT_ID(").Append(SqlLiteral(table.QualifiedName)).Append(", N'U') IS NULL THROW 51001, 'Apply the reviewed matching schema before importing demo data.', 1;\n");
            sql.Append("    IF EXISTS (SELECT 1 FROM ").Append(table.QualifiedName).Append(" WITH (UPDLOCK, HOLDLOCK)) THROW 51002, 'Import requires all application and Identity tables to be empty.', 1;\n");
        }
        foreach (var table in DependencyOrder(tables))
        {
            sql.Append("\n    -- ").Append(table.QualifiedName).Append(": ").Append(table.Rows.Count).Append(" rows\n");
            if (table.Rows.Count == 0) continue;
            if (table.Identity) sql.Append("    SET @IdentityTable = ").Append(SqlLiteral(table.QualifiedName)).Append(";\n    SET IDENTITY_INSERT ").Append(table.QualifiedName).Append(" ON;\n");
            foreach (var row in table.Rows)
                sql.Append("    INSERT INTO ").Append(table.QualifiedName).Append(" (").AppendJoin(", ", table.Columns.Select(Quote))
                    .Append(") VALUES (").AppendJoin(", ", row.Values).Append(");\n");
            if (table.Identity) sql.Append("    SET IDENTITY_INSERT ").Append(table.QualifiedName).Append(" OFF;\n    SET @IdentityTable = NULL;\n");
        }
        sql.Append("    COMMIT TRANSACTION;\nEND TRY\nBEGIN CATCH\n    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;\n" +
            "    IF @IdentityTable IS NOT NULL\n    BEGIN\n        BEGIN TRY\n            EXEC (N'SET IDENTITY_INSERT ' + @IdentityTable + N' OFF;');\n" +
            "        END TRY\n        BEGIN CATCH\n            PRINT N'Identity cleanup failed. Discard this SQL session before retrying.';\n        END CATCH\n    END;\n    THROW;\nEND CATCH;\n");
        return sql.ToString();
    }

    public static string SqlLiteral(object? value) => value switch
    {
        null => "NULL", string text => "N'" + text.Replace("'", "''", StringComparison.Ordinal) + "'",
        bool flag => flag ? "1" : "0", byte[] bytes => "0x" + Convert.ToHexString(bytes),
        DateOnly day => "CONVERT(date, N'" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "', 23)",
        DateTimeOffset at => "CONVERT(datetimeoffset, N'" + at.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture) + "', 127)",
        DateTime at => "CONVERT(datetime2, N'" + at.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "', 126)",
        TimeOnly at => "CONVERT(time, N'" + at.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "')",
        Guid id => "CONVERT(uniqueidentifier, N'" + id.ToString("D") + "')",
        decimal amount => amount.ToString("G29", CultureInfo.InvariantCulture),
        double number when double.IsFinite(number) => number.ToString("R", CultureInfo.InvariantCulture),
        float number when float.IsFinite(number) => number.ToString("R", CultureInfo.InvariantCulture),
        byte or short or int or long or sbyte or ushort or uint or ulong => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        _ => throw new InvalidOperationException("Unsupported or non-finite SQL value type; no export was written.")
    };
    private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    private sealed record RowData(object Entity, string[] Values);
    private sealed record TableData(IEntityType Entity, string Schema, string Name, string[] Columns, List<RowData> Rows, bool Identity)
    { public string QualifiedName => Quote(Schema) + "." + Quote(Name); }
}
