using Microsoft.Extensions.DependencyInjection;

namespace TourFlow.Application;

/// <summary>Registers application-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds application services to the container.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services;
}
