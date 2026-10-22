using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

/// <summary>Five fictional branch examples only in the guarded, new enterprise demo transaction.
/// Cancelled drafts deliberately post no stock, money, weight, health or custody action.</summary>
public static class ScanningDemoSeeder
{
    public static async Task SeedAsync(AppDbContext db, IReadOnlyList<Company> companies, IReadOnlyList<Branch> branches,
        IReadOnlyList<AppUser> makers, DateTimeOffset now, DateOnly today, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null || companies.Count is not (5 or 10 or 20) || branches.Count != companies.Count || makers.Count != companies.Count
            || companies.Where((c, i) => c.Slug != $"demo-farm-{i + 1}").Any()
            || makers.Where((u, i) => u.Email != $"farmdemo{i + 1}@example.invalid").Any()
            || await db.ScanLabels.AnyAsync(ct) || await db.ScanIntents.AnyAsync(ct))
            throw new InvalidOperationException("Scanner samples require the new guarded synthetic enterprise fixture transaction.");
        var previous = db.ScanWorkflowWrite; db.ScanWorkflowWrite = true;
        try
        {
            for (var i = 0; i < companies.Count; i++)
            {
                var company = companies[i]; var branch = branches[i]; var maker = makers[i];
                var grants = await db.UserPermissions.Where(x => x.CompanyId == company.Id && x.UserId == maker.Id).Select(x => x.Permission).ToListAsync(ct);
                if (!grants.Contains("*"))
                {
                    db.UserPermissions.Add(new UserPermission { CompanyId = company.Id, UserId = maker.Id, Permission = "Scanning.Use" });
                    if (i < 4) db.UserPermissions.Add(new UserPermission { CompanyId = company.Id, UserId = maker.Id, Permission = "Scanning.ManageLabels" });
                }
                var item = await db.StockItems.Where(x => x.CompanyId == company.Id && x.BranchId == branch.Id).OrderBy(x => x.Id).FirstAsync(ct);
                var animal = await db.Animals.Where(x => x.CompanyId == company.Id && x.BranchId == branch.Id && !x.Sold).OrderBy(x => x.Id).FirstAsync(ct);
                var asset = await db.FixedAssets.SingleAsync(x => x.CompanyId == company.Id && x.BranchId == branch.Id, ct);
                var order = await db.PurchaseOrders.SingleAsync(x => x.CompanyId == company.Id && x.BranchId == branch.Id, ct);
                var document = await db.FinanceDocuments.Where(x => x.CompanyId == company.Id && x.BranchId == branch.Id).OrderBy(x => x.Id).FirstAsync(ct);
                ScanLabel? animalLabel = null;
                foreach (var (kind, n) in ScanService.Kinds.Select((kind, n) => (kind, n)))
                {
                    var value = $"FARM1-{kind.ToUpperInvariant()}-D0000000000040008000{i + 1:000000}{n + 1:000000}";
                    var label = new ScanLabel { CompanyId = company.Id, BranchId = branch.Id, Kind = kind, Format = "Internal", Value = value,
                        ValueHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))), CreatedBy = maker.Id, CreatedUtc = now.AddMinutes(-20),
                        StockItemId = kind == "Product" ? item.Id : null, AnimalId = kind == "Animal" ? animal.Id : null, FixedAssetId = kind == "Asset" ? asset.Id : null,
                        PurchaseOrderId = kind == "PurchaseOrder" ? order.Id : null, FinanceDocumentId = kind == "Document" ? document.Id : null };
                    db.ScanLabels.Add(label); if (kind == "Animal") animalLabel = label;
                    db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = maker.Id, Action = "Scanning.LabelCreated", Reference = $"SYNTHETIC DEMO branch:{branch.Id};{kind}", CreatedUtc = now.AddMinutes(-20) });
                }
                await db.SaveChangesAsync(ct);
                var input = new ScanDraftInput { RequestKey = $"D0000000000040008000{i + 1:000000000000}", Action = "Weight", LabelId = animalLabel!.Id,
                    Quantity = 250, Date = today, Notes = "SYNTHETIC DEMO cancelled draft. No animal weight, diagnosis, stock movement or money posting occurred." };
                var preview = new ScanPreview(animalLabel.Id, branch.Id, "Animal", "Internal", animalLabel.Value,
                    new ScanTarget(animal.Id, animal.Tag + " · " + animal.Breed, "Active", "KG", null, "/Livestock"), ["Weight"]);
                var review = new ScanReview(preview, null, input, today, "Confirmation records the entered animal weight. No diagnosis or treatment is generated.", new ScanImpact(null, "", 3, "", "", animal.CurrentWeightKg));
                var payload = JsonSerializer.Serialize(input); var reference = $"SCAN-D0000000000040008000{i + 1:000000000000}";
                db.ScanIntents.Add(new ScanIntent { CompanyId = company.Id, BranchId = branch.Id, OwnerUserId = maker.Id, RequestKey = input.RequestKey, Action = "Weight", ScanLabelId = animalLabel.Id,
                    PayloadJson = payload, PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
                    SourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { animal, label = animalLabel.Value })))),
                    ReviewJson = JsonSerializer.Serialize(review), BusinessReference = reference, Status = "Cancelled", CreatedUtc = now.AddMinutes(-20), ExpiresUtc = now.AddMinutes(-10), CompletedUtc = now.AddMinutes(-15) });
                foreach (var action in new[] { "Scanning.DraftCreated", "Scanning.DraftCancelled" }) db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = maker.Id, Action = action, Reference = "SYNTHETIC DEMO " + reference, CreatedUtc = now.AddMinutes(-15) });
                await db.SaveChangesAsync(ct);
            }
        }
        finally { db.ScanWorkflowWrite = previous; }
    }
}
