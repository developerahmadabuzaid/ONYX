using Microsoft.Extensions.Caching.Memory;

namespace Onyx.Infrastructure.Identity;

/// <summary>Per-instance cache of session checks. Local revocations remove entries immediately; other instances catch up within the TTL.</summary>
public sealed class SessionValidationCache(IMemoryCache cache)
{
    public bool TryGet(Guid sessionId, out SessionValidationEntry entry) =>
        cache.TryGetValue(Key(sessionId), out entry!) && entry is not null;

    public void Set(Guid sessionId, SessionValidationEntry entry, TimeSpan timeToLive) => cache.Set(Key(sessionId), entry, timeToLive);

    public void Remove(IEnumerable<Guid> sessionIds)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        foreach (var sessionId in sessionIds)
        {
            cache.Remove(Key(sessionId));
        }
    }

    private static string Key(Guid sessionId) => $"onyx:session:{sessionId:N}";
}
