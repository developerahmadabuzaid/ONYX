namespace Onyx.Application.News;

/// <summary>Counts of one source's ingestion (no titles, URLs or text).</summary>
public sealed record SourceIngestionResult(bool Attempted, bool Failed, int NewItems, int Duplicates, int Skipped, int AutoPublished, int Held)
{
    public static SourceIngestionResult NotDue { get; } = new(false, false, 0, 0, 0, 0, 0);

    public static SourceIngestionResult Failure { get; } = new(true, true, 0, 0, 0, 0, 0);
}
