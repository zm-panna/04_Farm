using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EnterpriseERP.Web;

public sealed record StagingSchemaItem(string Kind, string Schema, string Table, string Name, string Definition);
public sealed record StagingObservedSchemaItem(string Kind, string Schema, string Table, string Name, string Definition,
    bool Disabled = false, bool Untrusted = false, bool NotForReplication = false);

/// <summary>Pure model/catalog comparison. No database access or mutation.</summary>
public static class StagingPreflightSchema
{
    public static IReadOnlyList<StagingSchemaItem> Expected(AppDbContext db)
    {
        var model = db.GetService<IDesignTimeModel>().Model;
        var items = new List<StagingSchemaItem>();
        foreach (var entity in model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null) continue;
            var schema = entity.GetSchema() ?? "dbo";
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            items.Add(new("TABLE", schema, table, table, ""));
            foreach (var property in entity.GetProperties())
                items.Add(new("COLUMN", schema, table, property.GetColumnName(store)!,
                    ColumnDefinition(property.GetColumnType() ?? property.GetRelationalTypeMapping().StoreType, property.IsNullable,
                        SqlServerPropertyExtensions.GetValueGenerationStrategy(property, store) == SqlServerValueGenerationStrategy.IdentityColumn, property.GetComputedColumnSql(store) != null)));
            foreach (var key in entity.GetKeys())
                items.Add(new(key.IsPrimaryKey() ? "PRIMARY_KEY" : "UNIQUE_KEY", schema, table, key.GetName()!,
                    string.Join("|", key.Properties.Select(p => p.GetColumnName(store)))));
            foreach (var index in entity.GetIndexes().Where(i => i.IsUnique))
                items.Add(new("UNIQUE_INDEX", schema, table, index.GetDatabaseName()!,
                    string.Join("|", index.Properties.Select(p => p.GetColumnName(store))) + ";FILTER=" + NormalizeFilter(index.GetFilter())));
            foreach (var fk in entity.GetForeignKeys())
            {
                var targetTable = fk.PrincipalEntityType.GetTableName()!;
                var targetSchema = fk.PrincipalEntityType.GetSchema() ?? "dbo";
                var target = StoreObjectIdentifier.Table(targetTable, fk.PrincipalEntityType.GetSchema());
                var delete = fk.DeleteBehavior == DeleteBehavior.Cascade ? "CASCADE" : fk.DeleteBehavior == DeleteBehavior.SetNull ? "SET_NULL" : "NO_ACTION";
                items.Add(new("FOREIGN_KEY", schema, table, fk.GetConstraintName()!,
                    string.Join("|", fk.Properties.Select(p => p.GetColumnName(store))) + "->" + targetSchema + "." + targetTable + ":"
                    + string.Join("|", fk.PrincipalKey.Properties.Select(p => p.GetColumnName(target))) + ";DELETE=" + delete + ";UPDATE=NO_ACTION"));
            }
            foreach (var check in entity.GetCheckConstraints())
                // Presence/state is checked. SQL Server rewrites expressions; do not pretend text equality proves semantic equivalence.
                items.Add(new("CHECK", schema, table, check.Name!, ""));
        }
        return items.Distinct().OrderBy(x => x.Kind).ThenBy(x => x.Schema).ThenBy(x => x.Table).ThenBy(x => x.Name).ToArray();
    }
    public static IReadOnlyList<string> Compare(IReadOnlyList<StagingSchemaItem> expected, IReadOnlyList<StagingObservedSchemaItem> observed)
    {
        var failures = new List<string>();
        var actual = observed.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        foreach (var item in expected)
        {
            if (!actual.TryGetValue(Key(item), out var matches) || matches.Length != 1)
            { failures.Add("Missing or ambiguous " + Label(item)); continue; }
            var match = matches[0];
            if (match.Disabled || match.Untrusted || match.NotForReplication) failures.Add("Disabled, untrusted or replication-exempt " + Label(item));
            if (item.Kind != "CHECK" && item.Definition != match.Definition) failures.Add("Definition mismatch " + Label(item));
        }
        return failures;
    }
    public static string ColumnDefinition(string type, bool nullable, bool identity, bool computed) =>
        NormalizeType(type) + ":" + nullable + ";IDENTITY=" + identity + ";COMPUTED=" + computed;
    public static string NormalizeType(string value) => value.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant().Replace("timestamp", "rowversion", StringComparison.Ordinal);
    // SQL Server adds parentheses to simple filtered-index predicates. Normalize only a grammar
    // of AND-combined identifier IS NOT NULL / integer-equality atoms; all other expressions stay exact.
    public static string NormalizeFilter(string? value)
    {
        var original = (value ?? "").Trim();
        var flat = original.Replace("(", " ", StringComparison.Ordinal).Replace(")", " ", StringComparison.Ordinal);
        const string atom = @"\[[A-Za-z_][A-Za-z0-9_]*\]\s*(?:IS\s+NOT\s+NULL|=\s*-?\d+)";
        if (!Regex.IsMatch(flat, @"^\s*" + atom + @"(?:\s+AND\s+" + atom + @")*\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return original;
        return string.Join(" AND ", Regex.Matches(flat, atom, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Select(m =>
            Regex.Replace(Regex.Replace(m.Value.Trim(), @"\s+", " "), @"\s*=\s*", "=")));
    }
    private static string Key(StagingSchemaItem i) => i.Kind + "\0" + i.Schema + "\0" + i.Table + "\0" + i.Name;
    private static string Key(StagingObservedSchemaItem i) => i.Kind + "\0" + i.Schema + "\0" + i.Table + "\0" + i.Name;
    private static string Label(StagingSchemaItem i) => i.Kind + " " + i.Schema + "." + i.Table + "." + i.Name;
}
