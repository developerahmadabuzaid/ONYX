namespace Onyx.Domain.Content;

/// <summary>Stored as text inside the body JSON. Append only.</summary>
public enum ContentBlockType
{
    /// <summary>H2–H4 (the H1 is always the translation title).</summary>
    Heading = 0,
    Paragraph = 1,
    List = 2,

    /// <summary>A media library image; <see cref="ContentBlock.Alt"/> overrides the asset's default alt text in this context.</summary>
    Image = 3,

    /// <summary>A reference to a managed call to action.</summary>
    CallToAction = 4,
}
