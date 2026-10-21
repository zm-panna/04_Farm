using EnterpriseERP.Web;
using Xunit;
namespace EnterpriseERP.UnitTests;

/// <summary>Pure history assessment tests; no SQL Server execution or physical-schema PASS is implied.</summary>
public sealed class StagingMigrationHistoryTests
{
    private static readonly string[] Known = ["20260101000000_Initial", "20260102000000_Integrity"];
    [Fact] public void ExactlyAppliedKnownHistoryMatches()
    {
        var assessment = StagingPreflightMigrationHistory.Assess(Known, Known.Reverse());
        Assert.True(assessment.IsCurrent); Assert.Empty(assessment.Pending); Assert.Empty(assessment.UnknownIdSha256);
    }
    [Fact] public void UnknownFutureMigrationFailsClosedEvenWhenAllKnownMigrationsExist()
    {
        var assessment = StagingPreflightMigrationHistory.Assess(Known, Known.Append("20270101000000_UnknownFuture"));
        Assert.False(assessment.IsCurrent); Assert.Empty(assessment.Pending); Assert.Single(assessment.UnknownIdSha256);
        Assert.Equal(StagingPreflightOptions.Fingerprint("20270101000000_UnknownFuture"),assessment.UnknownIdSha256[0]);
    }
    [Fact] public void MissingAndEmptyHistoriesAreNotCurrent()
    {
        Assert.False(StagingPreflightMigrationHistory.Assess(Known,[]).IsCurrent);
        Assert.False(StagingPreflightMigrationHistory.Assess([],[]).IsCurrent);
        var pending=StagingPreflightMigrationHistory.Assess(Known,[Known[0]]);
        Assert.False(pending.IsCurrent); Assert.Equal(new[]{Known[1]},pending.Pending);
    }
    [Fact] public void DuplicateOrMalformedIdsCannotPass()
    {
        Assert.False(StagingPreflightMigrationHistory.Assess(Known,Known.Append(Known[0])).IsCurrent);
        Assert.False(StagingPreflightMigrationHistory.Assess(Known.Append(Known[0]),Known).IsCurrent);
        Assert.False(StagingPreflightMigrationHistory.Assess(Known,Known.Append(" ")).IsCurrent);
    }
    [Fact] public void CaseChangedHistoryIsUnknownRatherThanSilentlyAccepted()
    {
        var assessment=StagingPreflightMigrationHistory.Assess(Known,Known.Select(x=>x.ToUpperInvariant()));
        Assert.False(assessment.IsCurrent); Assert.Equal(2,assessment.Pending.Length); Assert.Equal(2,assessment.UnknownIdSha256.Length);
    }
}
