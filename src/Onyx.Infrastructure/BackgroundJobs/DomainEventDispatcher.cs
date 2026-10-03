using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Onyx.Application.Abstractions.Messaging;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Invokes every registered <see cref="IDomainEventHandler{TEvent}"/> for an event. No handlers means nothing to do.</summary>
internal static class DomainEventDispatcher
{
    public static async Task DispatchAsync(IServiceProvider services, Type eventType, IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var handle = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

        foreach (var handler in services.GetServices(handlerType))
        {
            Task pending;
            try
            {
                pending = (Task)handle.Invoke(handler, [domainEvent, cancellationToken])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Throw(exception.InnerException);
                throw;
            }

            await pending.ConfigureAwait(false);
        }
    }
}
