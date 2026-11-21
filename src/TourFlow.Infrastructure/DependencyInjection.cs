using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TourFlow.Infrastructure.Persistence;
using TourFlow.Infrastructure.Persistence.Seeding;
using TourFlow.Infrastructure.Authentication;
using TourFlow.Application.Authentication;
using TourFlow.Domain.Entities;

namespace TourFlow.Infrastructure;

/// <summary>Registers infrastructure-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds persistence and infrastructure services to the container.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        var connectionString = configuration.GetConnectionString("TourFlowDatabase")
            ?? throw new InvalidOperationException("Connection string 'TourFlowDatabase' is not configured.");

        services.AddDbContext<TourFlowDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            if (isDevelopment && bool.TryParse(configuration["DevelopmentData:Enabled"], out var enabled) && enabled)
            {
                DevelopmentSeedData.Configure(options);
            }
        });
        services.AddHealthChecks().AddDbContextCheck<TourFlowDbContext>("sqlserver");
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<TourFlowDbContext>().AddDefaultTokenProviders();
        services.AddDataProtection();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));
        services.AddScoped<IAccounts, AccountService>();
        services.AddScoped<ISessions, SessionService>();
        services.AddSingleton<IPasswordResetDelivery, DisabledPasswordResetDelivery>();
        return services;
    }
}
