using EnterpriseERP.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
using TrustedInfrastructure;

// Generate all SQL Server model tables without opening a database connection.
if (args.Length == 2 && args[0] == "--export-schema")
{
    var path = DemoSqlExporter.ValidateOutputPath(args[1]);
    await using var schemaDb = new AppDbContextFactory().CreateDbContext([]);
    await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await using var writer = new StreamWriter(output);
    await writer.WriteAsync(schemaDb.Database.GenerateCreateScript());
    return;
}

// Read-only staging route must precede host/service/Data Protection initialization.
if (StagingPreflight.Requested(args))
{ Environment.ExitCode = await StagingPreflight.RunAsync(args); return; }

var builder = WebApplication.CreateBuilder(args);
await builder.AddTrustedInfrastructureAsync("farm", new TrustedSecretBindings
{
    SqlConnectionName = "DefaultConnection",
    RedisConfigurationKeys = ["Scaling:Cache:Connection"],
    RedisBackplaneConfigurationKeys = ["Scaling:SignalR:Connection"],
    DataProtectionCertificatePasswordConfigurationKeys = ["Security:DataProtection:CertificatePassword"]
});
if (builder.Configuration.GetValue<bool>("FORWARDEDHEADERS_ENABLED") || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Automatic forwarded-header trust is prohibited. Use explicit TrustedProxy configuration for the reviewed gateway.");
if (!builder.Environment.IsDevelopment() && builder.Configuration["TrustedVault:Mode"] == "OpenBao"
    && (!string.IsNullOrEmpty(builder.Configuration["Kestrel:Certificates:Default:Path"]) || !string.IsNullOrEmpty(builder.Configuration["Kestrel:Certificates:Default:Password"])))
    throw new InvalidOperationException("The vault-protected hosting profile uses certificate-store or IIS-managed TLS; Kestrel PFX bootstrap secrets are outside this profile.");
var trustedProxy = TrustedProxyOptions.Read(builder.Configuration, builder.Environment.IsDevelopment());
var sqlite = builder.Configuration["Database:Provider"]?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
if (sqlite && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("SQLite is supported only for isolated Development demos/tests. Use SQL Server for staging/production.");
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connection))
    connection = sqlite ? "Data Source=farm-demo.db" : throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection securely.");
if (!builder.Environment.IsDevelopment())
    ProductionSecurityOptions.Configure(builder.Services, builder.Configuration, connection,
        builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
builder.Services.AddSingleton<TrustedDatabaseCommandGate>();
builder.Services.AddSingleton<TrustedDatabaseTransactionGate>();
builder.Services.AddDbContext<AppDbContext>((sp, o) =>
{
    if (sqlite) o.UseSqlite(connection); else o.UseSqlServer(connection);
    o.AddInterceptors(sp.GetRequiredService<TrustedDatabaseCommandGate>(), sp.GetRequiredService<TrustedDatabaseTransactionGate>());
});
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.User.RequireUniqueEmail = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { if (c.Request.Path.StartsWithSegments("/api") || c.Request.Path.StartsWithSegments("/ops") || c.Request.Path.StartsWithSegments("/hubs")) c.Response.StatusCode = 401; else c.Response.Redirect(c.RedirectUri); return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
foreach(var scheme in new[]{IdentityConstants.TwoFactorUserIdScheme,IdentityConstants.TwoFactorRememberMeScheme})
    builder.Services.Configure<CookieAuthenticationOptions>(scheme,o=>
    {
        o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
        o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
        o.ExpireTimeSpan=TimeSpan.FromMinutes(5);o.SlidingExpiration=false;
        if(scheme==IdentityConstants.TwoFactorRememberMeScheme)o.Events.OnValidatePrincipal=c=>{c.RejectPrincipal();return Task.CompletedTask;};
    });
builder.Services.AddAuthorization(options => options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .ConfigureApplicationPartManager(m => m.FeatureProviders.Add(new SupportedControllersFeatureProvider()));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRazorPages();
builder.Services.AddScoped<CompanyBusinessPolicyService>();
builder.Services.AddScoped<TaxPolicyService>();
builder.Services.AddScoped<HRPayrollService>();
builder.Services.AddScoped<CRMService>();
builder.Services.AddScoped<FixedAssetService>();
builder.Services.AddScoped<EcommerceService>();
builder.Services.AddScoped<SupportService>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.AddScoped<OpeningBalanceService>();
builder.Services.AddScoped<StockTransferService>();
builder.Services.AddScoped<AccountSecurityService>();
builder.Services.Configure<SecurityStampValidatorOptions>(IdentitySessionOptions.Configure);
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<AccountingPeriodService>();
builder.Services.AddScoped<ReportExportService>();
builder.Services.AddScoped<AutomationApprovalService>();
builder.Services.AddHttpClient<OllamaChatService>(c => c.Timeout = TimeSpan.FromSeconds(45)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
if (builder.Configuration.GetValue<bool>("Automation:WorkerEnabled")) builder.Services.AddHostedService<JobWorker>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<IntegratedOperations>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<AutomationService>();
builder.Services.AddScoped<AiToolService>();
builder.Services.AddScoped<EnterpriseAiService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ScanService>();
builder.Services.AddScoped<FarmService>();
builder.Services.AddScoped<FarmHealthFeedService>();
builder.Services.AddScoped<FarmBusinessService>();
var cacheOptions = GridCacheOptions.Read(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddSingleton(cacheOptions);
builder.Services.AddSingleton<IGridCacheStore, RedisGridCacheStore>();
builder.Services.AddScoped<GridCache>();
builder.Services.AddScoped<GridService>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("scanning", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    o.AddPolicy("data-tables", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
var messaging = builder.Services.AddFarmMessaging();
if (builder.Configuration.GetValue<bool>("Scaling:SignalR:RedisEnabled"))
{
    var redisConnection = builder.Configuration["Scaling:SignalR:Connection"];
    var prefix = builder.Configuration["Scaling:SignalR:ChannelPrefix"] ?? "farm:signalr:v1";
    if (string.IsNullOrWhiteSpace(redisConnection) || prefix.Length is <3 or >64 || prefix.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ':' and not '-' and not '_'))
        throw new InvalidOperationException("Provide a separate Redis backplane connection and bounded channel prefix through secure configuration.");
    var redisOptions = ConfigurationOptions.Parse(redisConnection);
    if (!builder.Environment.IsDevelopment() && !redisOptions.Ssl)
        throw new InvalidOperationException("Production SignalR Redis backplane requires TLS.");
    redisOptions.AbortOnConnectFail = false; redisOptions.ChannelPrefix = RedisChannel.Literal(prefix);
    messaging.AddStackExchangeRedis(o => o.Configuration = redisOptions);
}
var app = builder.Build();
if (trustedProxy is not null)
{
    app.Use(async (context, next) =>
    {
        if (!trustedProxy.Accepts(context)) { context.Response.StatusCode = 400; return; }
        await next(context);
    });
    app.UseForwardedHeaders(trustedProxy.Headers());
}
app.UseTrustedInfrastructure();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception ex) when (ex is TrustedVaultUnavailableException || ex is DbUpdateException { InnerException: TrustedVaultUnavailableException })
    {
        if (context.Response.HasStarted) { context.Abort(); return; }
        context.Response.Clear(); context.Response.StatusCode = 503; context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new { error = "Trusted service temporarily unavailable." });
    }
});
if (!app.Environment.IsDevelopment()) ProductionSecurityOptions.VerifyDataProtection(app.Services);
var exportArgument = Array.FindIndex(args, a => a.Equals("--export-demo-sql", StringComparison.OrdinalIgnoreCase));
if (exportArgument >= 0)
{
    if (args.Length != 2 || exportArgument != 0)
        throw new InvalidOperationException("Use only --export-demo-sql followed by one absolute local .sql output path. Configure the environment separately.");
    await DemoSqlExporter.RunAsync(app.Services, args[1]); return;
}
if (args.Contains("--init-demo", StringComparer.OrdinalIgnoreCase) || args.Contains("--init-enterprise-demo", StringComparer.OrdinalIgnoreCase))
{
    if (!sqlite || !app.Environment.IsDevelopment()) throw new InvalidOperationException("--init-demo requires Development and Database__Provider=Sqlite.");
    using var scope = app.Services.CreateScope();
    var demoDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DemoBusinessSeeder.ValidateTarget(app.Environment.EnvironmentName, demoDb.Database.ProviderName ?? "", demoDb.Database.GetDbConnection().DataSource, Environment.GetEnvironmentVariable("FARM_ALLOW_DEMO_SEED"));
    await demoDb.Database.EnsureCreatedAsync();
    await DemoBusinessSeeder.RunAsync(app.Services, includeEnterprise: args.Contains("--init-enterprise-demo", StringComparer.OrdinalIgnoreCase), tenantCount: args.Contains("--ten-tenants", StringComparer.OrdinalIgnoreCase) ? 10 : 5); return;
}
if (args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase) || args.Contains("--seed-demo-users", StringComparer.OrdinalIgnoreCase))
{ await DemoBusinessSeeder.RunAsync(app.Services); return; }
if (args.Contains("--provision-platform-admin", StringComparer.OrdinalIgnoreCase))
{ await LocalPlatformAdministrator.RunAsync(app.Services); return; }
if (args.Contains("--recover-account", StringComparer.OrdinalIgnoreCase))
{ await LocalAccountRecovery.RunAsync(app.Services); return; }
if (args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase))
{ await BootstrapAdmin.RunAsync(app.Services); return; }
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); app.UseHttpsRedirection(); }
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<PermissionExceptionMiddleware>();
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.UseMiddleware<SubscriptionGuardMiddleware>();
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapFarmMessaging();
app.Run();
public partial class Program { }
