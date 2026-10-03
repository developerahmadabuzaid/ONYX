namespace Onyx.API.IntegrationTests.TestDatabase;

public sealed class TestDatabaseSafetyException : Exception
{
    public TestDatabaseSafetyException()
    {
    }

    public TestDatabaseSafetyException(string message)
        : base(message)
    {
    }

    public TestDatabaseSafetyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
