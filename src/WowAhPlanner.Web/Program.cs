using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WowAhPlanner.Core.Ports;
using WowAhPlanner.Core.Services;
using WowAhPlanner.Infrastructure.DependencyInjection;
using WowAhPlanner.Infrastructure.Persistence;
using WowAhPlanner.Web.Api;
using WowAhPlanner.Web.Auth;
using WowAhPlanner.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<RealmCatalog>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<SelectionState>();
builder.Services.AddSingleton<WowAddonInstaller>();
builder.Services.AddScoped<PlannerService>();

var dbDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dbDir);

var appDbPath = Path.Combine(dbDir, "wowahplanner.db");
var authDbPath = Path.Combine(dbDir, "wowahplanner.auth.db");
var appDbConnectionString = $"Data Source={appDbPath}";
var authDbConnectionString = $"Data Source={authDbPath}";

builder.Services.AddWowAhPlannerSqlite(appDbConnectionString);
builder.Services.AddDbContext<AuthDbContext>(o => o.UseSqlite(authDbConnectionString));

builder.Services
    .AddIdentity<AppUser, IdentityRole>(o =>
    {
        o.SignIn.RequireConfirmedAccount = false;
        o.User.RequireUniqueEmail = false;
        o.Password.RequiredLength = 6;
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization();

builder.Services.AddWowAhPlannerInfrastructure(
    configureDataPacks: o =>
    {
        var configured = builder.Configuration["DataPacks:RootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            o.RootPath = configured;
        }
        else
        {
            var baseDir = Path.Combine(AppContext.BaseDirectory, "data");
            o.RootPath = Directory.Exists(baseDir)
                ? baseDir
                : Path.Combine(builder.Environment.ContentRootPath, "data");
        }
    },
    configurePricing: o =>
    {
        o.PrimaryProviderName = builder.Configuration["Pricing:PrimaryProviderName"] ?? o.PrimaryProviderName;
        o.FallbackProviderName = builder.Configuration["Pricing:FallbackProviderName"] ?? o.FallbackProviderName;
    },
    configureWorker: o =>
    {
        builder.Configuration.GetSection("PriceRefreshWorker").Bind(o);
    },
    configureCommunityUploads: o =>
    {
        builder.Configuration.GetSection("CommunityUploads").Bind(o);
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    await SqliteSchemaBootstrapper.EnsureAppSchemaAsync(db);

    await using var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await authDb.Database.EnsureCreatedAsync();

    _ = scope.ServiceProvider.GetRequiredService<IRecipeRepository>();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapWowAhPlannerApi();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
