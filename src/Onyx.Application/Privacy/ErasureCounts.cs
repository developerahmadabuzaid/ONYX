namespace Onyx.Application.Privacy;

/// <summary>What an erasure changed. Counts only; never personal data.</summary>
public sealed record ErasureCounts(int Leads, int Inquiries, int ConsentRecords, int Bookings, int NotesDeleted, int AuditEntriesMinimized)
{
    public int EmailsDeleted { get; init; }

    public bool AccountErased { get; init; }

    public int RequestsForgotten { get; init; }
}
