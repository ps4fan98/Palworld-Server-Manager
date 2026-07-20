using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PalworldServerManager.Core.Options;
using PalworldServerManager.Core.Services;
using PalworldServerManager.Infrastructure.Persistence;
using PalworldServerManager.Infrastructure.Services;

namespace PalworldServerManager.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPalworldManagerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<PalworldServerOptions>()
            .Bind(configuration.GetSection(PalworldServerOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.InstallDirectory),
                "Palworld:InstallDirectory is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ExecutableRelativePath),
                "Palworld:ExecutableRelativePath is required.")
            .ValidateOnStart();

        var dataRoot = GetDataRoot(configuration, environment);
        Directory.CreateDirectory(dataRoot);

        var paths = new ManagerPaths(dataRoot);
        Directory.CreateDirectory(paths.LogDirectory);
        Directory.CreateDirectory(paths.ExportDirectory);

        services.AddSingleton(paths);

        services.AddDbContextFactory<ManagerDbContext>(
            options => options.UseSqlite(
                $"Data Source={paths.DatabasePath};Cache=Shared"));

        services.AddSingleton<IPalworldProcessService, WindowsPalworldProcessService>();
        services.AddScoped<IAuditWriter, EfAuditWriter>();

        return services;
    }

    public static async Task InitializePalworldManagerInfrastructureAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var dbContextFactory =
            scope.ServiceProvider.GetRequiredService<
                IDbContextFactory<ManagerDbContext>>();

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await ManagerSqliteSchemaUpgrader.UpgradeAsync(dbContext, cancellationToken);
    }

    private static string GetDataRoot(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredDataRoot = configuration["Manager:DataRoot"];
        if (!string.IsNullOrWhiteSpace(configuredDataRoot))
        {
            return Path.GetFullPath(configuredDataRoot);
        }

        if (OperatingSystem.IsWindows())
        {
            var commonApplicationData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData);

            return Path.Combine(
                commonApplicationData,
                "Palworld Server Manager");
        }

        return Path.Combine(environment.ContentRootPath, "data");
    }
}
