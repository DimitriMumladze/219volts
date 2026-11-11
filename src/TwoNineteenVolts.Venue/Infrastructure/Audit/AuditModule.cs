using TwoNineteenVolts.Venue.Application.Audit;

namespace TwoNineteenVolts.Venue.Infrastructure.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<AuditReader>();
        services.AddHostedService<AuditCleanupService>();

        return services;
    }
}
