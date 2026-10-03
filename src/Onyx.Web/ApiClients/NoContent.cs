namespace Onyx.Web.ApiClients;

/// <summary>Value of a successful call that returns no body.</summary>
public sealed record NoContent
{
    public static NoContent Instance { get; } = new();
}
