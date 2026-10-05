namespace Onyx.Domain.Privacy;

/// <summary>Replacement values for personal fields that must keep a value after erasure (required columns).</summary>
public static class ErasedValues
{
    public const string Name = "[erased]";

    /// <summary>A per-record placeholder that can never match a real (normalized) address, so new inquiries never reattach.</summary>
    public static string Email(Guid recordId) => $"erased-{recordId:N}@erased.invalid";
}
