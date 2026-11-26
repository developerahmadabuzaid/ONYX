using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Project create/edit form fields.</summary>
public sealed class ProjectForm
{
    public string? Key { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Type { get; set; }

    public string? Classification { get; set; }

    public string? ClientName { get; set; }

    public string? Status { get; set; }

    public string? Priority { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? TargetDate { get; set; }

    public string? RepositoryUrl { get; set; }

    public SaveDeliveryProjectRequest ToRequest(uint? version) =>
        new(Key, Name, Description, Type, Classification, ClientName, Status, Priority, StartDate, TargetDate, RepositoryUrl, version);
}
