namespace Onyx.Domain.Products;

/// <summary>What a product's demo really is. Stored as text. Only demos that exist may be published.</summary>
public enum DemoKind
{
    None = 0,

    /// <summary>A recorded walkthrough at an external https link.</summary>
    Video = 1,

    /// <summary>A guided tour at an external https link.</summary>
    GuidedTour = 2,

    /// <summary>A live sandbox at an external https link.</summary>
    Sandbox = 3,

    /// <summary>The on-site AI demo (signed-in, verified customers only, rate- and budget-limited).</summary>
    AiAssistant = 4,
}
