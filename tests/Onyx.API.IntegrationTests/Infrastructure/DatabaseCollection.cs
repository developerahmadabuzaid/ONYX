namespace Onyx.API.IntegrationTests.Infrastructure;

/// <summary>Tests that write to the shared test databases run sequentially so resets cannot interleave.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection
{
    public const string Name = "Database";
}
