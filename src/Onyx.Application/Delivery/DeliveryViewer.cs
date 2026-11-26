namespace Onyx.Application.Delivery;

/// <summary>The caller in the delivery workspace. Administrators (Delivery.Administer) see every project; others see their projects.</summary>
public sealed record DeliveryViewer(Guid UserId, bool IsAdministrator);
