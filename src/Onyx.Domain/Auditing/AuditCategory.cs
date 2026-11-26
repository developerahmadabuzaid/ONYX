namespace Onyx.Domain.Auditing;

/// <summary>Stored as text. Used to filter the Admin activity screen.</summary>
public enum AuditCategory
{
    Content = 0,
    Publishing = 1,
    Leads = 2,
    Security = 3,
    Integrations = 4,
    Privacy = 5,
    System = 6,
    Delivery = 7,
    Customers = 8,
    Commerce = 9,
}
