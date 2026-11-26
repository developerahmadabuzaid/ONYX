using Onyx.Domain.Common;

namespace Onyx.Domain.Products;

public sealed class ProductPlanTranslation : Entity
{
    private ProductPlanTranslation()
    {
        Language = string.Empty;
        Name = string.Empty;
    }

    public Guid PlanId { get; private set; }

    public string Language { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    internal static ProductPlanTranslation Create(Guid planId, string language) => new() { PlanId = planId, Language = language };

    internal void Set(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}
