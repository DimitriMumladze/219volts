using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TwoNineteenVolts.Venue.Application.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Data;

public static class DataModule
{
    private const string DatabaseFileName = "venue.db";

    public static IServiceCollection AddData(this IServiceCollection services)
    {
        services.AddOptions<VenueOptions>()
            .BindConfiguration(VenueOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<VenueDbContext>((provider, options) =>
        {
            var dataFolder = provider.GetRequiredService<IOptions<VenueOptions>>().Value.DataFolder;
            options.UseSqlite($"Data Source={Path.Combine(dataFolder, DatabaseFileName)}");
        });
        services.AddScoped<IVenueDbContext>(provider => provider.GetRequiredService<VenueDbContext>());

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var dataFolder = scope.ServiceProvider.GetRequiredService<IOptions<VenueOptions>>().Value.DataFolder;
        Directory.CreateDirectory(dataFolder);

        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        await db.Database.MigrateAsync();
    }
}
