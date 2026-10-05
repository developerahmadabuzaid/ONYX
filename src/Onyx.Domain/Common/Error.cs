namespace Onyx.Domain.Common;

/// <summary>An expected, typed failure. Codes are stable identifiers (e.g. "Service.SlugTaken"); messages are for developers, not end users.</summary>
public sealed record Error
{
    private Error(string code, string message, ErrorType type, IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
        Type = type;
        ValidationErrors = validationErrors;
    }

    public string Code { get; }

    public string Message { get; }

    public ErrorType Type { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure, null);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound, null);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict, null);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized, null);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden, null);

    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable, null);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(code, message, ErrorType.Validation, errors);
}
