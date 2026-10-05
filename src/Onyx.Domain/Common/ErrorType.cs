namespace Onyx.Domain.Common;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,

    /// <summary>A required dependency (e.g. the anti-abuse verifier) is not configured or not reachable; fail closed.</summary>
    Unavailable = 6,
}
