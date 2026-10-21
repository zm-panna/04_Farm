using System.Data;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

/// <summary>Tenant-checked operational ledgers. Posted expense and cash voucher are committed together.</summary>
public sealed class FarmBusinessService(AppDbContext db, TenantContext tenant, PermissionService permissions, TimeProvider? clock = null)
{
    private async Task<T> Atomic<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await work();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
    private async Task<Animal> ActiveAnimal(int id, int company, int branch, CancellationToken ct) =>
        await db.Animals.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company && x.BranchId == branch && !x.Sold, ct)
        ?? throw new InvalidOperationException("Animal unavailable in this company and branch, or already sold.");
    private void Audit(int company, string user, string action, string reference) =>
        db.Audits.Add(new AuditEntry { CompanyId = company, ActorId = user, Action = action, Reference = reference });
    private IQueryable<Animal> BranchAnimals(int company, int branch) => db.Animals.AsNoTracking().Where(x => x.CompanyId == company && x.BranchId == branch);

    public async Task<FatteningPage> Fattening(CancellationToken ct = default)
    {
        await permissions.Demand("Fattening.View", ct);
        var t = await tenant.GetAsync(ct);
        var animals = await BranchAnimals(t.company, t.branch).OrderBy(x => x.Tag).ToListAsync(ct);
        var plans = await db.FatteningPlans.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderByDescending(x => x.StartDate).ToListAsync(ct);
        var weights = await db.AnimalWeightRecords.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderByDescending(x => x.WeighedOn).ThenByDescending(x => x.Id).ToListAsync(ct);
        var feed = await db.AnimalFeedRecords.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).ToListAsync(ct);
        var expenses = await db.FarmExpenses.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.AnimalId != null).ToListAsync(ct);
        var sales = await db.AnimalSales.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).ToListAsync(ct);
        var today = (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date;
        var results = new List<FatteningResult>();
        foreach (var p in plans)
        {
            var a = animals.SingleOrDefault(x => x.Id == p.AnimalId);
            if (a == null) continue;
            var w = weights.FirstOrDefault(x => x.AnimalId == a.Id && x.WeighedOn >= p.StartDate);
            // Whole-animal costs include recorded pre-plan expenses. Shared branch overhead is not allocated.
            results.Add(new(p, a, w?.WeightKg ?? p.InitialWeightKg, w?.WeighedOn ?? p.StartDate,
                feed.Where(x => x.AnimalId == a.Id).Sum(x => x.CostAmount),
                expenses.Where(x => x.AnimalId == a.Id).Sum(x => x.Amount), sales.SingleOrDefault(x => x.AnimalId == a.Id)?.Amount));
        }
        return new FatteningPage { Plan = new FatteningPlanInput { StartDate = today, TargetDate = today.AddDays(90) }, Weight = new WeightEntryInput { WeighedOn = today }, Animals = animals.Where(x => !x.Sold).ToList(), Results = results, RecentWeights = weights.Take(100).ToList() };
    }

    public async Task<FatteningPlan> StartPlan(FatteningPlanInput input, CancellationToken ct = default)
    {
        await permissions.Demand("Fattening.Create", ct);
        var t = await tenant.GetAsync(ct);
        var reference = FarmBusinessRules.Reference(input.Reference);
        FarmBusinessRules.RecordedDate(input.StartDate, (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date);
        FarmBusinessRules.DecimalValue(input.InitialWeightKg, "Initial weight", 5000);
        FarmBusinessRules.DecimalValue(input.TargetWeightKg, "Target weight", 5000);
        FarmBusinessRules.DecimalValue(input.ExpectedSaleAmount, "Expected sale amount", 1000000000000m, true);
        if (input.TargetWeightKg <= input.InitialWeightKg || input.TargetDate <= input.StartDate || input.TargetDate > input.StartDate.AddYears(5))
            throw new ArgumentException("Target weight must exceed starting weight; target date must be after start and within five years.");
        return await Atomic(async () =>
        {
            var animal = await ActiveAnimal(input.AnimalId, t.company, t.branch, ct);
            if (await db.FatteningPlans.AnyAsync(x => x.CompanyId == t.company && (x.Reference == reference || x.AnimalId == animal.Id), ct))
                throw new InvalidOperationException("A plan already exists for this animal or reference.");
            if (await db.AnimalWeightRecords.AnyAsync(x => x.CompanyId == t.company && x.Reference == "PLAN:" + reference, ct))
                throw new InvalidOperationException("Starting-weight reference already exists.");
            var sameDay = await db.AnimalWeightRecords.SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.AnimalId == animal.Id && x.WeighedOn == input.StartDate, ct);
            if (sameDay != null && sameDay.WeightKg != input.InitialWeightKg)
                throw new InvalidOperationException("Starting weight differs from the recorded weight on that date.");
            if (sameDay == null) db.AnimalWeightRecords.Add(new AnimalWeightRecord
            {
                CompanyId = t.company, BranchId = t.branch, AnimalId = animal.Id, Reference = "PLAN:" + reference,
                WeighedOn = input.StartDate, WeightKg = input.InitialWeightKg, Notes = "Fattening plan starting weight", RecordedBy = t.user
            });
            if (!await db.AnimalWeightRecords.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.AnimalId == animal.Id && x.WeighedOn > input.StartDate, ct))
                animal.CurrentWeightKg = input.InitialWeightKg;
            var plan = new FatteningPlan
            {
                CompanyId = t.company, BranchId = t.branch, AnimalId = animal.Id, Reference = reference, StartDate = input.StartDate,
                TargetDate = input.TargetDate, InitialWeightKg = input.InitialWeightKg, TargetWeightKg = input.TargetWeightKg,
                ExpectedSaleAmount = input.ExpectedSaleAmount, RecordedBy = t.user
            };
            db.FatteningPlans.Add(plan);
            Audit(t.company, t.user, "Fattening.Plan.Start", reference);
            return plan;
        }, ct);
    }

    public async Task<AnimalWeightRecord> RecordWeight(WeightEntryInput input, CancellationToken ct = default)
    {
        await permissions.Demand("Fattening.Create", ct);
        var t = await tenant.GetAsync(ct);
        var reference = FarmBusinessRules.Reference(input.Reference);
        FarmBusinessRules.RecordedDate(input.WeighedOn, (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date);
        FarmBusinessRules.DecimalValue(input.WeightKg, "Weight", 5000);
        var notes = FarmBusinessRules.Text(input.Notes, "Notes", 1000, false);
        return await Atomic(async () =>
        {
            await db.ValidatePendingScan("Weight", ct);
            var animal = await ActiveAnimal(input.AnimalId, t.company, t.branch, ct);
            if (await db.AnimalWeightRecords.AnyAsync(x => x.CompanyId == t.company && (x.Reference == reference || x.AnimalId == animal.Id && x.WeighedOn == input.WeighedOn), ct))
                throw new InvalidOperationException("A record already exists for this reference or animal and date.");
            var row = new AnimalWeightRecord { CompanyId = t.company, BranchId = t.branch, AnimalId = animal.Id,
                Reference = reference, WeighedOn = input.WeighedOn, WeightKg = input.WeightKg, Notes = notes, RecordedBy = t.user };
            db.AnimalWeightRecords.Add(row);
            if (!await db.AnimalWeightRecords.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.AnimalId == animal.Id && x.WeighedOn > input.WeighedOn, ct))
                animal.CurrentWeightKg = input.WeightKg;
            Audit(t.company, t.user, "Fattening.Weight.Record", reference);
            return row;
        }, ct);
    }

    public async Task<ExpensesPage> Expenses(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        await permissions.Demand("Expenses.View", ct);
        var t = await tenant.GetAsync(ct);
        var today = (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date;
        var end = to ?? today;
        var start = from ?? end.AddDays(-30);
        if (start > end || end.DayNumber - start.DayNumber > 3660) throw new ArgumentException("Select an ordered date range of at most ten years.");
        return new ExpensesPage
        {
            From = start, To = end, Expense = new ExpenseInput { ExpenseDate = today },
            Animals = await BranchAnimals(t.company, t.branch).OrderBy(x => x.Tag).ToListAsync(ct),
            Records = await db.FarmExpenses.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.ExpenseDate >= start && x.ExpenseDate <= end)
                .OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.Id).ToListAsync(ct)
        };
    }

    public async Task<FarmExpense> RecordExpense(ExpenseInput input, CancellationToken ct = default)
    {
        await permissions.Demand("Expenses.Create", ct);
        await permissions.Demand("Accounts.Post", ct);
        var t = await tenant.GetAsync(ct);
        var reference = FarmBusinessRules.Reference(input.Reference);
        FarmBusinessRules.RecordedDate(input.ExpenseDate, (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date);
        FarmBusinessRules.DecimalValue(input.Amount, "Amount", 1000000000000m);
        var description = FarmBusinessRules.Text(input.Description, "Description", 1000);
        if (input.Category == null || !FarmBusinessRules.ExpenseAccounts.TryGetValue(input.Category, out var account))
            throw new ArgumentException("Select a supported expense category.");
        return await Atomic(async () =>
        {
            await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, input.Amount, ct, date: input.ExpenseDate);
            if (input.AnimalId.HasValue && !await BranchAnimals(t.company, t.branch).AnyAsync(x => x.Id == input.AnimalId, ct))
                throw new InvalidOperationException("Expense animal is unavailable in this company and branch.");
            if (await db.FarmExpenses.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct) ||
                await db.Vouchers.AnyAsync(x => x.CompanyId == t.company && x.Reference == "EXP:" + reference, ct))
                throw new InvalidOperationException("Expense or accounting reference already exists.");
            var voucher = BusinessInput.Voucher(t.company, t.branch, "EXP:" + reference,
                (account, input.Amount, 0m), ("1000", 0m, input.Amount));
            voucher.Date = CompanyBusinessDate.AccountingDate(input.ExpenseDate);
            var expense = new FarmExpense
            {
                CompanyId = t.company, BranchId = t.branch, AnimalId = input.AnimalId, Reference = reference, ExpenseDate = input.ExpenseDate,
                Category = input.Category, Amount = input.Amount, Description = description, RecordedBy = t.user,
                Voucher = voucher
            };
            db.FarmExpenses.Add(expense);
            Audit(t.company, t.user, "Expenses.Post", reference);
            return expense;
        }, ct);
    }

    public async Task<BreedingPage> Breeding(CancellationToken ct = default)
    {
        await permissions.Demand("Breeding.View", ct);
        var t = await tenant.GetAsync(ct);
        var animals = await BranchAnimals(t.company, t.branch).OrderBy(x => x.Tag).ToListAsync(ct);
        var today = (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date;
        return new BreedingPage
        {
            Today = today, Breeding = new BreedingInput { ServiceDate = today, ExpectedCalvingDate = today.AddDays(283) }, OutcomeEntry = new BreedingOutcomeInput { EventDate = today },
            Animals = animals.Where(x => !x.Sold).ToList(), AnimalTags = animals.ToDictionary(x => x.Id, x => x.Tag),
            Cycles = await db.BreedingCycles.AsNoTracking().Include(x => x.Events).Where(x => x.CompanyId == t.company && x.BranchId == t.branch)
                .OrderBy(x => x.IsClosed).ThenBy(x => x.ExpectedCalvingDate).ToListAsync(ct)
        };
    }

    public async Task<BreedingCycle> StartBreeding(BreedingInput input, CancellationToken ct = default)
    {
        await permissions.Demand("Breeding.Create", ct);
        var t = await tenant.GetAsync(ct);
        var reference = FarmBusinessRules.Reference(input.Reference);
        FarmBusinessRules.RecordedDate(input.ServiceDate, (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date);
        var sire = FarmBusinessRules.Text(input.SireOrSemen, "Sire or semen identifier", 120);
        if (input.Method is not ("Natural" or "ArtificialInsemination")) throw new ArgumentException("Choose Natural or Artificial Insemination.");
        if (input.ExpectedCalvingDate <= input.ServiceDate || input.ExpectedCalvingDate > input.ServiceDate.AddYears(2))
            throw new ArgumentException("Expected calving date must follow service date and be within two years. Confirm species-specific dates with your vet.");
        return await Atomic(async () =>
        {
            var animal = await ActiveAnimal(input.AnimalId, t.company, t.branch, ct);
            if (await db.BreedingCycles.AnyAsync(x => x.CompanyId == t.company && (x.Reference == reference || x.AnimalId == animal.Id && !x.IsClosed), ct))
                throw new InvalidOperationException("Reference already exists, or this animal already has an open breeding cycle.");
            if (await db.BreedingCycles.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.AnimalId == animal.Id && x.IsClosed && x.LastEventDate > input.ServiceDate, ct))
                throw new InvalidOperationException("Service date cannot precede the last completed cycle outcome.");
            var cycle = new BreedingCycle { CompanyId = t.company, BranchId = t.branch, AnimalId = animal.Id, Reference = reference,
                ServiceDate = input.ServiceDate, ExpectedCalvingDate = input.ExpectedCalvingDate, Method = input.Method,
                SireOrSemen = sire, Status = "Bred", LastEventDate = input.ServiceDate, RecordedBy = t.user };
            db.BreedingCycles.Add(cycle);
            Audit(t.company, t.user, "Breeding.Start", reference);
            return cycle;
        }, ct);
    }

    public async Task<BreedingCycleEvent> RecordOutcome(BreedingOutcomeInput input, CancellationToken ct = default)
    {
        await permissions.Demand("Breeding.Update", ct);
        var t = await tenant.GetAsync(ct);
        var reference = FarmBusinessRules.Reference(input.Reference);
        FarmBusinessRules.RecordedDate(input.EventDate, (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date);
        var notes = FarmBusinessRules.Text(input.Notes, "Outcome notes", 2000);
        return await Atomic(async () =>
        {
            var cycle = await db.BreedingCycles.SingleOrDefaultAsync(x => x.Id == input.CycleId && x.CompanyId == t.company && x.BranchId == t.branch, ct)
                ?? throw new InvalidOperationException("Breeding cycle unavailable in this company and branch.");
            await ActiveAnimal(cycle.AnimalId, t.company, t.branch, ct);
            if (cycle.IsClosed) throw new InvalidOperationException("This breeding cycle is closed.");
            if (input.EventDate < (cycle.LastEventDate ?? cycle.ServiceDate)) throw new ArgumentException("Outcome date cannot precede the last cycle event.");
            var closed = FarmBusinessRules.ClosesCycle(cycle.Status, input.Outcome);
            if (await db.BreedingCycleEvents.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct))
                throw new InvalidOperationException("Outcome reference already exists.");
            var result = new BreedingCycleEvent { CompanyId = t.company, BranchId = t.branch, BreedingCycleId = cycle.Id,
                Reference = reference, EventDate = input.EventDate, Outcome = input.Outcome, Notes = notes, RecordedBy = t.user };
            db.BreedingCycleEvents.Add(result);
            cycle.Status = input.Outcome; cycle.IsClosed = closed; cycle.LastEventDate = input.EventDate;
            Audit(t.company, t.user, "Breeding.Outcome." + input.Outcome, reference);
            return result;
        }, ct);
    }
}
