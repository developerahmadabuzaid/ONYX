using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Onyx.API.IntegrationTests.Persistence;
using Onyx.Application;
using Onyx.Infrastructure;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.Worker;

/// <summary>
/// The real Worker composition (Application + Infrastructure + AddWorkerProcessing) against a disposable test database,
/// with test-controlled time and lease owner. Two hosts with different owners simulate two Worker instances.
/// </summary>
public sealed class WorkerTestHost : IAsyncDisposable
{
    private WorkerTestHost(ServiceProvider services, ManualTimeProvider time)
    {
        Services = services;
        Time = time;
    }

    public ServiceProvider Services { get; }

    public ManualTimeProvider Time { get; }

    public static WorkerTestHost Create(
        string connectionString,
        string instanceId,
        ManualTimeProvider time,
        Action<IServiceCollection>? configure = null,
        IReadOnlyDictionary<string, string?>? settings = null,
        string environmentName = "Testing")
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Onyx"] = connectionString,
            ["Worker:MaxOutboxAttempts"] = "3",
            ["Worker:MaxEmailAttempts"] = "3",
            ["Worker:RetryBaseDelaySeconds"] = "30",
            ["Worker:RetryMaxDelayMinutes"] = "60",
            ["Email:Transport"] = "None",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));

        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddWorkerProcessing(configuration);

        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(time);
        services.RemoveAll<WorkerIdentity>();
        services.AddSingleton(new WorkerIdentity(instanceId));
        services.Configure<DomainEventRegistryOptions>(options => options.Assemblies.Add(typeof(WorkerTestHost).Assembly));

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return new WorkerTestHost(provider, time);
    }

    public T Get<T>()
        where T : notnull => Services.GetRequiredService<T>();

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public ValueTask DisposeAsync() => Services.DisposeAsync();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Onyx.Worker.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
