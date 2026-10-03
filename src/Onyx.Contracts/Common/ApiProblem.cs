namespace Onyx.Contracts.Common;

/// <summary>
/// RFC 9457 problem details as returned by Onyx.API for every error. Onyx.Web deserializes into this type.
/// Never contains stack traces or internal exception messages.
/// </summary>
public sealed record ApiProblem
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    /// <summary>Stable machine-readable error code, e.g. "Service.SlugTaken".</summary>
    public string? Code { get; init; }

    public string? TraceId { get; init; }

    public string? CorrelationId { get; init; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
