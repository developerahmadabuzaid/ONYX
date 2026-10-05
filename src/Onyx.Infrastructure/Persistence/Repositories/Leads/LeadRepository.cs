using Microsoft.EntityFrameworkCore;
using Onyx.Application.Leads;
using Onyx.Contracts.Common;
using Onyx.Domain.Leads;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class LeadRepository(ApplicationDbContext dbContext) : ILeadRepository
{
    public Task<Lead?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Leads.SingleOrDefaultAsync(lead => lead.Id == id, cancellationToken);

    public Task<Lead?> FindOpenByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Leads
            .Where(lead => lead.NormalizedEmail == normalizedEmail && lead.Stage != LeadStage.Won && lead.Stage != LeadStage.Lost)
            .OrderByDescending(lead => lead.LastInquiryAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<Lead>> ListAsync(LeadFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var query = Filter(filter);
        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(lead => lead.LastInquiryAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<Lead>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<IReadOnlyList<Lead>> ListForExportAsync(LeadFilter filter, int limit, CancellationToken cancellationToken) =>
        await Filter(filter).OrderByDescending(lead => lead.LastInquiryAtUtc).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<LeadNote>> ListNotesAsync(Guid leadId, CancellationToken cancellationToken) =>
        await dbContext.LeadNotes.AsNoTracking().Where(note => note.LeadId == leadId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<LeadActivity>> ListActivitiesAsync(Guid leadId, CancellationToken cancellationToken) =>
        await dbContext.LeadActivities.AsNoTracking().Where(activity => activity.LeadId == leadId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken)
    {
        if (leadIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Leads.AsNoTracking()
            .Where(lead => leadIds.Contains(lead.Id))
            .ToDictionaryAsync(lead => lead.Id, lead => lead.DisplayName, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(Lead lead)
    {
        dbContext.Leads.Add(lead);
        AddPendingActivities(lead);
    }

    public void AddPendingActivities(Lead lead)
    {
        ArgumentNullException.ThrowIfNull(lead);
        dbContext.LeadActivities.AddRange(lead.PendingActivities);
        lead.ClearPendingActivities();
    }

    public void AddNote(LeadNote note) => dbContext.LeadNotes.Add(note);

    public void SetExpectedVersion(Lead lead, uint version) => dbContext.Entry(lead).Property(entry => entry.Version).OriginalValue = version;

    private IQueryable<Lead> Filter(LeadFilter filter)
    {
        var query = dbContext.Leads.AsNoTracking();
        if (filter.Stage is { } stage)
        {
            query = query.Where(lead => lead.Stage == stage);
        }

        if (filter.OwnerId is { } ownerId)
        {
            query = query.Where(lead => lead.OwnerId == ownerId);
        }

        if (filter.UnassignedOnly)
        {
            query = query.Where(lead => lead.OwnerId == null);
        }

        if (filter.Scope is { } scope)
        {
            query = query.Where(lead => lead.OwnerId == null || lead.OwnerId == scope.UserId);
        }

        if (filter.Search is { } search)
        {
            // Bound parameter; LIKE wildcards in the search text are escaped so they match literally.
            var pattern = "%" + search.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            query = query.Where(lead =>
                EF.Functions.ILike(lead.DisplayName, pattern, "\\")
                || (lead.Company != null && EF.Functions.ILike(lead.Company, pattern, "\\"))
                || EF.Functions.ILike(lead.NormalizedEmail, pattern, "\\"));
        }

        return query;
    }
}
