namespace EnterpriseERP.Web;

/// <summary>Pure set/shape assessment. A matching history is not evidence of physical schema or production readiness.</summary>
public sealed record StagingMigrationHistoryAssessment(string[] Committed, string[] AppliedKnown, string[] Pending,
    string[] UnknownIdSha256, int AppliedCount, bool InvalidOrDuplicateIds)
{
    public bool IsCurrent => Committed.Length > 0 && AppliedCount > 0 && Pending.Length == 0 && UnknownIdSha256.Length == 0 && !InvalidOrDuplicateIds;
}
public static class StagingPreflightMigrationHistory
{
    public static StagingMigrationHistoryAssessment Assess(IEnumerable<string> committedIds, IEnumerable<string> appliedIds)
    {
        var committed = committedIds.ToArray(); var applied = appliedIds.ToArray();
        var invalid = committed.Any(string.IsNullOrWhiteSpace) || applied.Any(string.IsNullOrWhiteSpace)
            || committed.Distinct(StringComparer.Ordinal).Count() != committed.Length || applied.Distinct(StringComparer.Ordinal).Count() != applied.Length;
        var known = applied.Intersect(committed, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var pending = committed.Except(applied, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var unknown = applied.Except(committed, StringComparer.Ordinal).Select(x => StagingPreflightOptions.Fingerprint(x ?? "")).ToArray();
        return new(committed.OrderBy(x => x, StringComparer.Ordinal).ToArray(), known, pending, unknown, applied.Length, invalid);
    }
}
