namespace Onyx.Domain.Intelligence;

/// <summary>A before/after proposal for one field of a content translation (e.g. "metaTitle").</summary>
public sealed record ProposedFieldChange(string Field, string? CurrentValue, string? ProposedValue);
