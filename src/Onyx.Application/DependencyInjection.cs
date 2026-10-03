using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Notifications;
using Onyx.Application.Providers;

namespace Onyx.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IProviderConnectionService, ProviderConnectionService>();

        AddDomainEventHandlers(services, assembly);
        return services;
    }

    private static void AddDomainEventHandlers(IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var registrations = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(contract => (Contract: contract, Implementation: type)));

        foreach (var (contract, implementation) in registrations)
        {
            services.AddScoped(contract, implementation);
        }
    }
}
