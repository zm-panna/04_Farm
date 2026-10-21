using System.Data;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed record StagingPreflightCheck(string Id, string Status, string Summary, bool Automated = true, object? Evidence = null);
public sealed class StagingPreflightReport
{
    public string Format { get; } = "farm-readonly-staging-v1";
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedUtc { get; set; }
    public bool ProductionApproved { get; } = false;
    public string Scope { get; } = "One-company Bangladesh metadata and bounded policy inspection; no business mutations or release approval.";
    public List<StagingPreflightCheck> Checks { get; } = [];
    public string CollectorStatus => Checks.Any(c => c.Automated && c.Status == "FAIL") ? "FAIL"
        : Checks.Any(c => c.Automated && c.Status != "PASS") ? "NOT_VERIFIED" : "PASS";
    public int ExitCode => CollectorStatus == "FAIL" ? 2 : CollectorStatus == "NOT_VERIFIED" ? 3 : 0;
    public void Set(string id, string status, string summary, object? evidence = null)
    {
        var index = Checks.FindIndex(c => c.Id == id);
        var item = new StagingPreflightCheck(id, status, summary, true, evidence);
        if (index < 0) Checks.Add(item); else Checks[index] = item;
    }
    public static StagingPreflightReport Create()
    {
        var report = new StagingPreflightReport();
        foreach (var id in new[] { "configuration", "sqlConnection", "targetIdentity", "metadataVisibility", "migrations", "modelSnapshot", "modelSchema", "constraintsState", "runtimePrivileges", "companyPolicy", "fictionalMarkers" })
            report.Checks.Add(new(id, "NOT_VERIFIED", "Not executed or a prerequisite failed."));
        foreach (var pair in new[] {
            ("checkConstraintSemantics", "CHECK definitions are fingerprinted, not proven equivalent to source; DBA review is required."),
            ("completeSecurityReview", "No exhaustive role/ownership/cross-database/OS/secret/ACL review is performed."),
            ("iisTlsDataProtection", "Separate IIS/TLS, runtime identity, key-ring persistence and restart verification is required."),
            ("productionSafetyConfiguration", "Effective AllowedHosts, persistent protected Data Protection key path/mode and complete host configuration are NOT_VERIFIED. This route does not initialize the host or key ring."),
            ("businessIntegrityAndConcurrency", "No full-ledger scan, stock reconciliation, multi-session concurrency or mutation-based UAT is performed."),
            ("backupRestoreAndCustomerAcceptance", "Dated restore rehearsal, accountant-approved opening balances/policies and signed customer UAT are required.") })
            report.Checks.Add(new(pair.Item1, "NOT_VERIFIED", pair.Item2, false));
        return report;
    }
}

/// <summary>Dispatched before WebApplication.CreateBuilder. Opens no host, worker, key ring or EF migration path.</summary>
public static class StagingPreflight
{
    public const string Switch = "--verify-staging-readonly";
    public static bool Requested(string[] args) => args.Any(a => a.Equals(Switch, StringComparison.OrdinalIgnoreCase) || a.StartsWith(Switch + "=", StringComparison.OrdinalIgnoreCase));
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 2 || !args[0].Equals(Switch, StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("Use only --verify-staging-readonly followed by one new absolute private .json output path. All settings must be provided through the local process environment."); return 4; }
        StagingPreflightOptions options;
        try
        {
            var identity = OperatingSystem.IsWindows() ? WindowsIdentity.GetCurrent().Name : Environment.UserName;
            options = StagingPreflightOptions.FromEnvironment(args[1], Environment.GetEnvironmentVariable, identity, AppContext.BaseDirectory);
        }
        catch (Exception)
        { Console.Error.WriteLine("Read-only preflight configuration rejected. Check the operator guide, runtime identity, secure SQL settings and new private output path. No connection was attempted; details are suppressed."); return 4; }
        // Reserve a new file before connecting; never overwrite a prior report. Provisioning/ACL changes are outside this tool.
        FileStream output;
        try
        {
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            output = new FileStream(options.OutputPath, fileOptions);
        }
        catch (Exception)
        { Console.Error.WriteLine("A new private evidence file could not be reserved. No SQL connection was attempted."); return 4; }
        await using (output)
        {
            var report = StagingPreflightReport.Create();
            report.Set("configuration", "PASS", "Explicit SQL Server target, verified certificate policy, runtime-identity attestation and private output checks passed.",
                new { processIdentitySha256 = StagingPreflightOptions.Fingerprint(options.ProcessIdentity), companyId = options.CompanyId,
                    connectionTimeoutSeconds = StagingPreflightOptions.ConnectionTimeoutSeconds, commandTimeoutSeconds = StagingPreflightOptions.CommandTimeoutSeconds,
                    overallTimeoutSeconds = StagingPreflightOptions.OverallTimeoutSeconds });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(StagingPreflightOptions.OverallTimeoutSeconds));
            try { await CollectAsync(options, report, timeout.Token); }
            catch (Exception e)
            { report.Checks.Add(new("collectorInterrupted", "NOT_VERIFIED", SafeFailure(e))); }
            report.FinishedUtc = DateTimeOffset.UtcNow;
            try
            {
                await JsonSerializer.SerializeAsync(output, report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await output.FlushAsync();
            }
            catch (Exception)
            { Console.Error.WriteLine("Evidence write did not complete. The reserved file may be partial; do not treat it as a valid report."); return 4; }
            Console.WriteLine("Read-only staging evidence saved. Collector=" + report.CollectorStatus + "; productionApproved=false. Remaining manual gates are NOT_VERIFIED.");
            return report.ExitCode;
        }
    }

    private static async Task CollectAsync(StagingPreflightOptions o, StagingPreflightReport r, CancellationToken ct)
    {
        await using var connection = new SqlConnection(o.ConnectionString);
        try { await connection.OpenAsync(ct); r.Set("sqlConnection", "PASS", "Encrypted SQL Server connection opened with bounded timeout and retries disabled."); }
        catch (Exception e) { r.Set("sqlConnection", "NOT_VERIFIED", SafeFailure(e)); return; }
        var identity = (await Read(connection, StagingPreflightSql.Identity, 1, ct)).Single();
        var server = Str(identity, "ServerName"); var database = Str(identity, "DatabaseName");
        var login = Str(identity, "LoginName"); var user = Str(identity, "UserName");
        bool target = server.Equals(o.ExpectedServer, StringComparison.OrdinalIgnoreCase) && database.Equals(o.ExpectedDatabase, StringComparison.Ordinal)
            && login.Equals(o.ExpectedLogin, StringComparison.OrdinalIgnoreCase) && user.Equals(o.ExpectedUser, StringComparison.Ordinal);
        r.Set("targetIdentity", target ? "PASS" : "FAIL", target ? "Actual SQL server/database/login/database principal match the operator's expected runtime target." : "Actual SQL target or runtime identity differs from the expected target. Further inspection was stopped.",
            new { serverSha256 = StagingPreflightOptions.Fingerprint(server), databaseSha256 = StagingPreflightOptions.Fingerprint(database),
                loginSha256 = StagingPreflightOptions.Fingerprint(login), userSha256 = StagingPreflightOptions.Fingerprint(user),
                productMajorVersion = Num(identity, "MajorVersion") });
        if (!target) return;
        if (Str(identity, "Updateability") != "READ_WRITE")
        { r.Set("targetIdentity", "FAIL", "The selected database is not writable-primary evidence; read-only replicas are not accepted."); return; }
        bool visible = Num(identity, "ViewDefinition") == 1;
        r.Set("metadataVisibility", visible ? "PASS" : "NOT_VERIFIED", visible ? "Existing runtime principal has database VIEW DEFINITION; catalog comparisons may proceed. No permission was granted."
            : "Database VIEW DEFINITION could not be established. Hidden metadata cannot be counted as missing or healthy; obtain a separately reviewed DBA report without elevating this tool.");
        await Privileges(connection, r, ct);
        if (!visible) return;
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(o.ConnectionString).Options);
        var expected = StagingPreflightSchema.Expected(db);
        var committed = db.Database.GetMigrations().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        r.Set("modelSnapshot", db.Database.HasPendingModelChanges() ? "FAIL" : "PASS", "Compared the compiled model with the committed migration snapshot without applying migrations.");
        var observed = await Schema(connection, ct);
        var drift = StagingPreflightSchema.Compare(expected, observed);
        r.Set("modelSchema", drift.Count == 0 ? "PASS" : "FAIL", "Compared required tables, columns, primary/alternate keys, unique indexes, foreign keys and CHECK presence/state with the compiled SQL Server model.",
            new { requiredItems = expected.Count, mismatchCount = drift.Count, mismatches = drift.Take(100).ToArray(), mismatchListTruncated = drift.Count > 100 });
        var damaged = observed.Count(x => x.Disabled || x.Untrusted || x.NotForReplication);
        r.Set("constraintsState", damaged == 0 ? "PASS" : "FAIL", "Inspected visible user-table foreign/check constraints and unique indexes for disabled, untrusted or replication-exempt enforcement.",
            new { unsafeCount = damaged, checkDefinitionFingerprints = observed.Where(x => x.Kind == "CHECK").Select(x => new { objectSha256 = StagingPreflightOptions.Fingerprint(x.Schema + "." + x.Table + "." + x.Name), definitionSha256 = StagingPreflightOptions.Fingerprint(x.Definition) }).ToArray() });
        await Migrations(connection, r, committed, observed, ct);
        if (drift.Count != 0) return;
        var rowSecurity = await Read(connection, StagingPreflightSql.RowSecurity, StagingPreflightOptions.CatalogRowLimit, ct);
        if (rowSecurity.Any(x => expected.Any(e => e.Kind == "TABLE" && e.Schema == Str(x,"SchemaName") && e.Table == Str(x,"TableName"))))
        { r.Set("companyPolicy", "NOT_VERIFIED", "Enabled SQL row-level security can hide rows; scoped data checks require a separately reviewed DBA verification."); return; }
        await Company(connection, r, o, ct);
    }

    private static async Task Migrations(SqlConnection c, StagingPreflightReport r, string[] committed, IReadOnlyList<StagingObservedSchemaItem> schema, CancellationToken ct)
    {
        if (!schema.Any(i => i.Kind == "TABLE" && i.Schema == "dbo" && i.Table == "__EFMigrationsHistory"))
        { r.Set("migrations", "FAIL", "EF migration-history table is missing; no migrations were applied.", new { committed, applied = Array.Empty<string>(), pending = committed }); return; }
        try
        {
            var rows = await Read(c, StagingPreflightSql.Migrations, 200, ct);
            var applied = rows.Select(x => Str(x, "MigrationId")).ToArray();
            var history = StagingPreflightMigrationHistory.Assess(committed, applied);
            r.Set("migrations", history.IsCurrent ? "PASS" : "FAIL", "Migration history was read only; compiled/applied/pending/unknown IDs were compared. History alone does not prove schema integrity.",
                new { committed = history.Committed, applied = history.AppliedKnown, pending = history.Pending, unknownIdSha256 = history.UnknownIdSha256,
                    invalidOrDuplicateIds = history.InvalidOrDuplicateIds });
        }
        catch (Exception e) { r.Set("migrations", "NOT_VERIFIED", SafeFailure(e)); }
    }

    private static async Task Privileges(SqlConnection c, StagingPreflightReport r, CancellationToken ct)
    {
        try
        {
            var rows = await Read(c, StagingPreflightSql.Privileges, StagingPreflightOptions.CatalogRowLimit, ct);
            var knownElevated = rows.Count(x => Num(x, "HasPrivilege") == 1);
            var unresolved = rows.Count(x => x["HasPrivilege"] is DBNull);
            r.Set("runtimePrivileges", knownElevated > 0 ? "FAIL" : unresolved > 0 ? "NOT_VERIFIED" : "PASS",
                "Checked fixed privileged server/database roles and effective elevated server/database/schema/object permissions. This is a bounded risk screen, not a complete SQL security certification.",
                new { elevatedCapabilityCount = knownElevated, unresolvedCapabilityCount = unresolved });
        }
        catch (Exception e) { r.Set("runtimePrivileges", "NOT_VERIFIED", SafeFailure(e)); }
    }

    private static async Task<IReadOnlyList<StagingObservedSchemaItem>> Schema(SqlConnection c, CancellationToken ct)
    {
        var all = new List<StagingObservedSchemaItem>();
        var tables = await Read(c, StagingPreflightSql.Tables, StagingPreflightOptions.CatalogRowLimit, ct);
        all.AddRange(tables.Select(x => new StagingObservedSchemaItem("TABLE", Str(x,"SchemaName"), Str(x,"TableName"), Str(x,"TableName"), "")));
        var columns = await Read(c, StagingPreflightSql.Columns, StagingPreflightOptions.CatalogRowLimit, ct);
        all.AddRange(columns.Select(x => new StagingObservedSchemaItem("COLUMN", Str(x,"SchemaName"), Str(x,"TableName"), Str(x,"ColumnName"),
            StagingPreflightSchema.ColumnDefinition(Str(x,"StoreType"), Num(x,"IsNullable") == 1, Num(x,"IsIdentity") == 1, Num(x,"IsComputed") == 1))));
        var indexes = await Read(c, StagingPreflightSql.Indexes, StagingPreflightOptions.CatalogRowLimit, ct);
        foreach (var group in indexes.GroupBy(x => (Str(x,"SchemaName"), Str(x,"TableName"), Str(x,"IndexName"))))
        {
            var first = group.First();
            var kind = Num(first,"IsPrimary") == 1 ? "PRIMARY_KEY" : Num(first,"IsConstraint") == 1 ? "UNIQUE_KEY" : "UNIQUE_INDEX";
            var definition = string.Join("|", group.OrderBy(x => Num(x,"Ordinal")).Select(x => Str(x,"ColumnName")));
            if (kind == "UNIQUE_INDEX") definition += ";FILTER=" + StagingPreflightSchema.NormalizeFilter(Str(first,"Filter"));
            all.Add(new(kind, group.Key.Item1, group.Key.Item2, group.Key.Item3, definition, Num(first,"IsDisabled") == 1));
        }
        var foreign = await Read(c, StagingPreflightSql.ForeignKeys, StagingPreflightOptions.CatalogRowLimit, ct);
        foreach (var group in foreign.GroupBy(x => (Str(x,"SchemaName"), Str(x,"TableName"), Str(x,"ConstraintName"))))
        {
            var first = group.First(); var ordered = group.OrderBy(x => Num(x,"Ordinal")).ToArray();
            var definition = string.Join("|", ordered.Select(x => Str(x,"ColumnName"))) + "->" + Str(first,"TargetSchema") + "." + Str(first,"TargetTable") + ":"
                + string.Join("|", ordered.Select(x => Str(x,"TargetColumn"))) + ";DELETE=" + Str(first,"DeleteAction") + ";UPDATE=" + Str(first,"UpdateAction");
            all.Add(new("FOREIGN_KEY", group.Key.Item1, group.Key.Item2, group.Key.Item3, definition, Num(first,"IsDisabled") == 1, Num(first,"IsUntrusted") == 1, Num(first,"NotForReplication") == 1));
        }
        var checks = await Read(c, StagingPreflightSql.Checks, StagingPreflightOptions.CatalogRowLimit, ct);
        all.AddRange(checks.Select(x => new StagingObservedSchemaItem("CHECK", Str(x,"SchemaName"), Str(x,"TableName"), Str(x,"ConstraintName"), Str(x,"Definition"),
            Num(x,"IsDisabled") == 1, Num(x,"IsUntrusted") == 1, Num(x,"NotForReplication") == 1)));
        return all;
    }

    private static async Task Company(SqlConnection c, StagingPreflightReport r, StagingPreflightOptions o, CancellationToken ct)
    {
        try
        {
            var company = await Read(c, StagingPreflightSql.Company, 1, ct, o.CompanyId);
            if (company.Count != 1) { r.Set("companyPolicy", "FAIL", "The explicitly selected company does not exist."); return; }
            var rows = await Read(c, StagingPreflightSql.Policies, StagingPreflightOptions.CompanyRowLimit, ct, o.CompanyId);
            var locks = await Read(c, StagingPreflightSql.CurrencyLock, 2, ct, o.CompanyId);
            var atUtc = DateTimeOffset.UtcNow;
            var localDate = LocalBusinessDate(o.ExpectedTimeZone, atUtc);
            // Match the application's effective-date order and evaluate each version in its own timezone.
            var applicable = rows.OrderByDescending(x => (DateTime)x["EffectiveFrom"]).ThenByDescending(x => Num(x,"Version"))
                .FirstOrDefault(x => DateOnly.FromDateTime((DateTime)x["EffectiveFrom"]) <= LocalBusinessDate(Str(x,"TimeZoneId"), atUtc));
            bool valid = applicable != null && Str(applicable,"CountryCode") == "BD" && Str(applicable,"CurrencyCode") == "BDT"
                && Num(applicable,"CurrencyMinorUnits") == 2 && Str(applicable,"TimeZoneId") == o.ExpectedTimeZone
                && (applicable["EffectiveTo"] is DBNull || DateOnly.FromDateTime((DateTime)applicable["EffectiveTo"]) >= localDate)
                && Num(applicable,"IndependentApproval") == 1 && Num(applicable,"HasReviewBasis") == 1
                && locks.Count == 1 && Str(locks[0],"CurrencyCode") == "BDT" && Num(locks[0],"CurrencyMinorUnits") == 2 && Num(locks[0],"HasConfirmation") == 1;
            r.Set("companyPolicy", valid ? "PASS" : "FAIL", "Inspected approved policy for the selected company's Bangladesh-local business date, independent reviewer metadata and BDT/2-decimal currency lock. Authenticity and statutory suitability require human approval.",
                new { companyId = o.CompanyId, localBusinessDate = localDate, expectedCountry = "BD", expectedCurrency = "BDT", expectedTimeZone = o.ExpectedTimeZone });
            bool marker = Num(company[0],"DemoMarker") == 1 || rows.Any(x => Num(x,"DemoMarker") == 1);
            var audits = await Read(c, StagingPreflightSql.AuditMarkers, StagingPreflightOptions.CompanyRowLimit, ct, o.CompanyId, allowOverflow: true);
            var taxes = await Read(c, StagingPreflightSql.TaxMarkers, StagingPreflightOptions.CompanyRowLimit, ct, o.CompanyId, allowOverflow: true);
            marker |= audits.Any(x => Num(x,"DemoMarker") == 1) || taxes.Any(x => Num(x,"DemoMarker") == 1);
            bool complete = audits.Count <= StagingPreflightOptions.CompanyRowLimit && taxes.Count <= StagingPreflightOptions.CompanyRowLimit;
            r.Set("fictionalMarkers", marker ? "FAIL" : complete ? "PASS" : "NOT_VERIFIED",
                marker ? "Known fictional demo markers were found in the selected company, approved policies, tax policies or seed audit rows. Never promote fictional policy approvals into first-live."
                : complete ? "No known fictional markers found in the bounded selected-company metadata/policy/tax/audit set. This does not certify all business rows or accounts are free of demo data."
                : "The selected company's audit or tax rows exceed the bounded sample. No all-clear is possible; a separately approved indexed DBA review is required.");
        }
        catch (Exception e)
        {
            if (r.Checks.Single(x => x.Id == "companyPolicy").Status == "NOT_VERIFIED") r.Set("companyPolicy", "NOT_VERIFIED", SafeFailure(e));
            r.Set("fictionalMarkers", "NOT_VERIFIED", SafeFailure(e));
        }
    }

    private static async Task<List<Dictionary<string, object>>> Read(SqlConnection c, string sql, int limit, CancellationToken ct, int? company = null, bool allowOverflow = false)
    {
        await using var command = c.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = StagingPreflightOptions.CommandTimeoutSeconds;
        command.Parameters.Add("@limit", SqlDbType.Int).Value = limit + 1;
        if (company.HasValue) command.Parameters.Add("@company", SqlDbType.Int).Value = company.Value;
        var result = new List<Dictionary<string, object>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (result.Count > limit) throw new InvalidOperationException("Bounded read exceeded.");
            var row = new Dictionary<string, object>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.GetValue(i);
            result.Add(row);
        }
        if (!allowOverflow && result.Count > limit) throw new InvalidOperationException("Bounded read exceeded.");
        return result;
    }
    public static DateOnly LocalBusinessDate(string timeZone, DateTimeOffset atUtc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(atUtc, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);
    private static string Str(Dictionary<string, object> row, string key) => row[key] is DBNull ? "" : Convert.ToString(row[key], System.Globalization.CultureInfo.InvariantCulture) ?? "";
    private static int Num(Dictionary<string, object> row, string key) => row[key] is DBNull ? -1 : Convert.ToInt32(row[key], System.Globalization.CultureInfo.InvariantCulture);
    private static string SafeFailure(Exception e) => e is OperationCanceledException ? "The overall read-only time budget expired; remaining checks are NOT_VERIFIED."
        : e is SqlException sql && sql.Number == -2 ? "A bounded SQL read timed out; no retry, migration or repair was attempted."
        : "The bounded read could not be verified (connection, permissions, unsupported metadata, row budget or read failure). Details are suppressed; an operator/DBA must diagnose locally without sharing secrets.";
}
