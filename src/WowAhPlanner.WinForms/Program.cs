namespace WowAhPlanner.WinForms;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WowAhPlanner.Core.Services;
using WowAhPlanner.Infrastructure.DependencyInjection;
using WowAhPlanner.Infrastructure.Pricing;
using WowAhPlanner.Infrastructure.Persistence;
using WowAhPlanner.WinForms.Services;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton<AppPaths>();
        builder.Services.AddSingleton<JsonFileStateStore>();
        builder.Services.AddSingleton<RealmCatalog>();
        builder.Services.AddSingleton<SelectionState>();
        builder.Services.AddSingleton<PlannerPreferences>();
        builder.Services.AddSingleton<WowAddonInstaller>();
        builder.Services.AddSingleton<TargetsService>();
        builder.Services.AddSingleton<OwnedBreakdownService>();

        builder.Services.AddSingleton<PlannerService>();

        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WowAhPlanner");
        Directory.CreateDirectory(appDataDir);
        var dbPath = Path.Combine(appDataDir, "wowahplanner.db");
        var connectionString = $"Data Source={dbPath}";

        builder.Services.AddWowAhPlannerSqlite(connectionString);
        builder.Services.AddWowAhPlannerInfrastructure(
            configureDataPacks: o =>
            {
                var contentDir = Path.Combine(AppContext.BaseDirectory, "data");
                var repoDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"));
                o.RootPath = Directory.Exists(contentDir) ? contentDir : repoDir;
            },
            configurePricing: o =>
            {
                // Default WinForms behavior: prefer local uploads (and fail closed if none exist).
                o.PrimaryProviderName = UploadedSnapshotIngestService.ProviderName;
                o.FallbackProviderName = "";
            });

        using var host = builder.Build();

        using (var scope = host.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = dbFactory.CreateDbContext();
            db.Database.EnsureCreated();
            SqliteSchemaBootstrapper.EnsureAppSchemaAsync(db).GetAwaiter().GetResult();
        }

        host.StartAsync().GetAwaiter().GetResult();

        try
        {
            var mainForm = ActivatorUtilities.CreateInstance<MainForm>(host.Services);
            Application.Run(mainForm);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }    
}
