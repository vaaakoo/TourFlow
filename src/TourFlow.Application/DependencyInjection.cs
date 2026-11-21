using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TourFlow.Application.Operations;

namespace TourFlow.Application;

/// <summary>Registers application-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds application services to the container.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        foreach (var type in typeof(ListOptions).Assembly.GetTypes().Where(type =>
            !type.IsAbstract && !type.ContainsGenericParameters &&
            type.Namespace?.StartsWith(typeof(ListOptions).Namespace!, StringComparison.Ordinal) == true))
        {
            var contract = type.GetInterfaces().SingleOrDefault(candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IValidator<>));
            if (contract is null) continue;
            services.AddTransient(type);
            services.AddTransient(contract, type);
        }

        return services;
    }
}
