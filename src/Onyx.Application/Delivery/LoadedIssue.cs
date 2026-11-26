using Onyx.Contracts.Delivery;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>An issue, its project, the caller and what the caller may do there.</summary>
public sealed record LoadedIssue(Issue Issue, DeliveryProject Project, DeliveryViewer Viewer, DeliveryCapabilities Can);
