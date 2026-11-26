namespace Onyx.Domain.Delivery;

/// <summary>Relationship from the source issue to the target issue. Stored as text.</summary>
public enum IssueLinkType
{
    Blocks = 0,
    RelatesTo = 1,
    Duplicates = 2,
}
