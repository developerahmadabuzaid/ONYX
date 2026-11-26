namespace Onyx.Domain.Delivery;

/// <summary>Kind of work a delivery project covers. Stored as text.</summary>
public enum DeliveryProjectType
{
    Software = 0,
    SaaS = 1,
    AiAgent = 2,
    Automation = 3,
    Maintenance = 4,
    Internal = 5,
}
