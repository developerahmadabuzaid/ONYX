namespace Onyx.Application.Content;

public sealed record QualityFinding(QualitySeverity Severity, string Code, string Message, string? LanguageCode = null, string? Field = null);
