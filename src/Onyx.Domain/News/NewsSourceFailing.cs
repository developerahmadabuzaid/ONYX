using Onyx.Domain.Common;

namespace Onyx.Domain.News;

/// <summary>A source's fetch failed after succeeding (or on its first attempt); raised once per run of failures.</summary>
public sealed record NewsSourceFailing(Guid NewsSourceId, string SourceName) : IDomainEvent;
