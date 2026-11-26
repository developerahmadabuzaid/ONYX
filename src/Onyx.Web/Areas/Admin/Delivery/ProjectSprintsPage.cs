using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>A project's sprints with the velocity of the closed ones.</summary>
public sealed record ProjectSprintsPage(DeliveryProjectResponse Project, IReadOnlyList<SprintResponse> Sprints, IReadOnlyList<SprintVelocityResponse> Velocity);
