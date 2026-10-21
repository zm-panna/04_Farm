using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;

// Authenticated operational diagnostic. Never expose connection strings or secrets.
[Authorize]
[ApiController]
[Route("ops/readiness")]
public sealed class DeploymentReadinessController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        // Only an authorized bootstrap/administrative principal may inspect schema health.
        // Do not trust user-supplied tenant headers.
        if (!User.HasClaim(c => c.Type == "permission" && c.Value == "ops.readiness")
            && !User.IsInRole("PlatformAdmin")) return Forbid();
        try
        {
            if (!await db.Database.CanConnectAsync(ct))
                return StatusCode(503, new { status = "blocked", database = "unreachable" });
            if (db.Database.IsSqlite())
                return Ok(new { status = "demo-database-ready", productionReady = false, provider = "Sqlite", note = "Development demo schema; SQL Server migration verification is separate." });
            if (!db.Database.IsSqlServer())
                return StatusCode(503, new { status = "blocked", productionReady = false, database = "unsupported-provider" });
            if (db.Database.HasPendingModelChanges())
                return StatusCode(503, new { status = "blocked", database = "model-migration-mismatch" });
            var committed = db.Database.GetMigrations().ToArray();
            var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            var history = StagingPreflightMigrationHistory.Assess(committed, applied);
            if (!history.IsCurrent)
                return StatusCode(503, new { status = "blocked", productionReady = false, provider = "SqlServer", database = "migration-history-mismatch",
                    appliedMigrations = history.AppliedCount, pendingMigrations = history.Pending.Length,
                    unknownMigrations = history.UnknownIdSha256.Length, invalidOrDuplicateHistory = history.InvalidOrDuplicateIds });
            return Ok(new { status = "database-ready", productionReady = false, provider = "SqlServer", appliedMigrations = history.AppliedCount,
                pendingMigrations = 0, unknownMigrations = 0, note = "Migration-history check only; physical schema, deployment, backup/restore and customer UAT require separate evidence." });
        }
        catch (Exception)
        {
            return StatusCode(503, new { status = "blocked", database = "verification-failed" });
        }
    }
}
