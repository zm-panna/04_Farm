using EnterpriseERP.Domain;
using TrustedInfrastructure;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

// Deterministic in-app notifications only. Never executes model text, external messages or accounting actions.
public sealed class JobWorker(IServiceScopeFactory scopeFactory, ILogger<JobWorker> log, ITrustedRuntimeGate gate) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                try { await ReviewDueJobsAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { log.LogError(ex, "In-app notification worker failed"); }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    private static async Task<int> LowStockCount(AppDbContext db, int company, int branch, CancellationToken ct)
    {
        var stocks = await db.StockItems.AsNoTracking().Where(x => x.CompanyId == company && x.BranchId == branch).Select(x => new { x.Id, x.QuantityOnHand }).ToListAsync(ct);
        var policies = await db.StockReorderPolicies.AsNoTracking().Where(x => x.CompanyId == company && x.BranchId == branch).ToDictionaryAsync(x => x.StockItemId, ct);
        return stocks.Count(x => x.QuantityOnHand <= (policies.TryGetValue(x.Id, out var policy) ? policy.ReorderPoint : 5m));
    }
    public async Task<int> ReviewDueJobsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); gate.ThrowIfUnavailable();
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var due = await db.ScheduledJobs.Where(x => x.Status == "Pending" && x.DueUtc <= now && (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
            .OrderBy(x => x.DueUtc).Take(15).ToListAsync(ct);
        foreach (var job in due)
        {
            var rule = await db.AutomationRules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == job.RuleId && x.CompanyId == job.CompanyId && x.Active, ct);
            job.Attempt++; job.LockedUntilUtc = null;
            if (rule is null || !await db.Companies.AnyAsync(x => x.Id == job.CompanyId, ct))
            { job.Status = "Rejected"; job.LastError = "Active rule unavailable in this company"; continue; }
            if (rule.Action != "InAppAlert")
            { job.Status = "ReviewRequired"; job.LastError = "Human review required; no domain write action is enabled"; continue; }
            if (rule.Trigger is not ("LowStock" or "HealthDue" or "Manual"))
            { job.Status = "Rejected"; job.LastError = "Unsupported notification trigger"; continue; }
            if (await db.AutomationExecutions.AnyAsync(x => x.CompanyId == job.CompanyId && x.ExecutionKey == job.ExecutionKey, ct))
            { job.Status = "Completed"; job.LastError = null; continue; }
            var count = 0; var today = DateOnly.FromDateTime(now.UtcDateTime);
            var branches = await db.Branches.AsNoTracking().Where(x => x.CompanyId == job.CompanyId).ToListAsync(ct);
            foreach (var branch in branches)
            {
                int matches = rule.Trigger switch
                {
                    "LowStock" => await LowStockCount(db, job.CompanyId, branch.Id, ct),
                    "HealthDue" => await db.AnimalHealthRecords.CountAsync(x => x.CompanyId == job.CompanyId && x.BranchId == branch.Id && x.NextDueDate != null && x.FollowUpCompletedDate == null && x.NextDueDate <= today && db.Animals.Any(a => a.Id == x.AnimalId && a.CompanyId == x.CompanyId && a.BranchId == x.BranchId && !a.Sold), ct),
                    _ => 1
                };
                if (matches == 0) continue;
                var message = rule.Trigger == "LowStock" ? $"{matches} stock items are at or below their reorder point. Review purchasing needs." :
                    rule.Trigger == "HealthDue" ? $"{matches} recorded health follow-ups are due. Staff review is required." : $"Scheduled review: {rule.Name}";
                db.Notifications.Add(new InAppNotification { CompanyId = job.CompanyId, BranchId = branch.Id, ExecutionKey = job.ExecutionKey, Message = message, Category = rule.Trigger }); count++;
            }
            db.AutomationExecutions.Add(new AutomationExecution { CompanyId = job.CompanyId, RuleId = rule.Id, ExecutionKey = job.ExecutionKey, Result = $"Created {count} branch notifications" });
            job.Status = "Completed"; job.LastError = null;
        }
        gate.ThrowIfUnavailable(); await db.SaveChangesAsync(ct); gate.ThrowIfUnavailable(); await tx.CommitAsync(ct); return due.Count;
    }
}
