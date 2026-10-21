using System.Security.Claims;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseERP.UnitTests;

/// <summary>Real EF relational store + Identity; each test owns an isolated in-memory SQLite database.</summary>
public sealed class SecurityTestScope : IAsyncDisposable
{
 private readonly SqliteConnection connection;
 private readonly ServiceProvider root;
 private readonly IServiceScope scope;
 public AppDbContext Db { get; }
 public UserManager<AppUser> Users { get; }
 public IHttpContextAccessor Accessor { get; }
 public TenantContext Tenant { get; }
 public PermissionService Permissions { get; }
 public IServiceProvider Services => scope.ServiceProvider;
 public HttpContext Http => Accessor.HttpContext!;
 private SecurityTestScope(SqliteConnection connection, ServiceProvider root)
 {
  this.connection = connection; this.root = root; scope = root.CreateScope();
  Db = Services.GetRequiredService<AppDbContext>(); Users = Services.GetRequiredService<UserManager<AppUser>>();
  Accessor = Services.GetRequiredService<IHttpContextAccessor>();
  Accessor.HttpContext = new DefaultHttpContext { RequestServices = Services };
  Http.Response.Body = new MemoryStream();
  Tenant = Services.GetRequiredService<TenantContext>(); Permissions = Services.GetRequiredService<PermissionService>();
 }
 public static Task<SecurityTestScope> CreateSharedAsync() => CreateCoreAsync(null, [], "Data Source=scan-" + Guid.NewGuid().ToString("N") + ";Mode=Memory;Cache=Shared;Default Timeout=2");
 public static Task<SecurityTestScope> CreateAsync(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) => CreateCoreAsync(null, interceptors);
 public static Task<SecurityTestScope> CreateWithAuthenticationAsync(IAuthenticationService authentication) => CreateCoreAsync(authentication, []);
 private static async Task<SecurityTestScope> CreateCoreAsync(IAuthenticationService? authentication, Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors, string? connectionString = null)
 {
  var connection = new SqliteConnection(connectionString ?? "Data Source=:memory:"); await connection.OpenAsync();
  var services = new ServiceCollection();
  services.AddLogging(); services.AddDataProtection(); services.AddControllersWithViews(); services.AddSingleton<IHttpContextAccessor, TestHttpContextAccessor>();
  services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection).AddInterceptors(interceptors));
  services.AddIdentity<AppUser, IdentityRole>(options => { options.Password.RequiredLength = 12; options.User.RequireUniqueEmail = true; })
   .AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
  if (authentication != null) services.AddSingleton<IAuthenticationService>(authentication);
  services.AddScoped<AccountSecurityService>(); services.AddScoped<TenantContext>(); services.AddScoped<PermissionService>(); services.AddScoped<SubscriptionService>(); services.AddScoped<ReportService>(); services.AddScoped<AutomationApprovalService>();
  var fixture = new SecurityTestScope(connection, services.BuildServiceProvider());
  await fixture.Db.Database.EnsureCreatedAsync();
  fixture.Db.Companies.AddRange(new Company { Id = 1, Name = "Company A" }, new Company { Id = 2, Name = "Company B" });
  fixture.Db.Farms.AddRange(new Farm { Id = 1, CompanyId = 1, Name = "Farm A" }, new Farm { Id = 2, CompanyId = 2, Name = "Farm B" });
  fixture.Db.Branches.AddRange(new Branch { Id = 11, CompanyId = 1, FarmId = 1, Name = "A main" }, new Branch { Id = 12, CompanyId = 1, FarmId = 1, Name = "A other" }, new Branch { Id = 21, CompanyId = 2, FarmId = 2, Name = "B main" });
  foreach (var (id, company, branch) in new[] { ("operator-a", 1, 11), ("approver-a", 1, 11), ("operator-b", 2, 21) })
  {
   fixture.Db.Users.Add(new AppUser { Id = id, UserName = id + "@example.invalid", NormalizedUserName = (id + "@example.invalid").ToUpperInvariant(), Email = id + "@example.invalid", NormalizedEmail = (id + "@example.invalid").ToUpperInvariant(), SecurityStamp = Guid.NewGuid().ToString() });
   fixture.Db.UserCompanyAccess.Add(new UserCompanyAccess { UserId = id, CompanyId = company });
   fixture.Db.UserBranchAccess.Add(new UserBranchAccess { UserId = id, CompanyId = company, BranchId = branch });
  }
  await fixture.Db.SaveChangesAsync();
  fixture.ActAs();
  return fixture;
 }
 public void ActAs(string user = "operator-a", int company = 1, int branch = 11, bool authenticated = true)
 {
  Http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user), new Claim("ActiveCompanyId", company.ToString()), new Claim("ActiveBranchId", branch.ToString()) }, authenticated ? "Test" : null));
 }
 public async Task GrantAsync(string permission, string user = "operator-a", int company = 1)
 {
  Db.UserPermissions.Add(new UserPermission { CompanyId = company, UserId = user, Permission = permission });
  await Db.SaveChangesAsync();
 }
 public async ValueTask DisposeAsync()
 { Accessor.HttpContext = null; scope.Dispose(); await root.DisposeAsync(); await connection.DisposeAsync(); }
}

internal sealed class TestHttpContextAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
