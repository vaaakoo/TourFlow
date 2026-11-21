using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TourFlow.Infrastructure.Persistence;

namespace TourFlow.Infrastructure;

/// <summary>Registers infrastructure-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds persistence and infrastructure services to the container.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TourFlowDatabase")
            ?? throw new InvalidOperationException("Connection string 'TourFlowDatabase' is not configured.");

        services.AddDbContext<TourFlowDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<TourFlowDbContext>("sqlserver");
        return services;
    }
}
