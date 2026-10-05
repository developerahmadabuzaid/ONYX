using Onyx.Contracts.Common;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>The lead aggregate: the lead, its notes and its activity timeline.</summary>
public interface ILeadRepository
{
    Task<Lead?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The most recent open lead for a normalized email, to attach a new inquiry to.</summary>
    Task<Lead?> FindOpenByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<PagedResult<Lead>> ListAsync(LeadFilter filter, CancellationToken cancellationToken);

    /// <summary>Every lead matching the filter (no paging), for export. Capped by <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<Lead>> ListForExportAsync(LeadFilter filter, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeadNote>> ListNotesAsync(Guid leadId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeadActivity>> ListActivitiesAsync(Guid leadId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken);

    /// <summary>Adds a new lead together with its pending activities.</summary>
    void Add(Lead lead);

    /// <summary>Adds the pending activities of a tracked lead to the unit of work.</summary>
    void AddPendingActivities(Lead lead);

    void AddNote(LeadNote note);

    void SetExpectedVersion(Lead lead, uint version);
}
