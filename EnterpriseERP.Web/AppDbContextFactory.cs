using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace EnterpriseERP.Web;
/// <summary>Migration generation is SQL Server-only and does not start web hosting, seed or background jobs.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        // This inert connection is used to build the model/script without contacting a database.
        // `database update` needs an explicit configured connection or --connection argument.
        if (string.IsNullOrWhiteSpace(connection)) connection = "Server=localhost;Database=FarmMigrationDesignOnly;Integrated Security=True;TrustServerCertificate=False";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
    }
}
