namespace Onyx.Application.Search;

/// <summary>One ranked result; <see cref="GroupTotal"/> is how many matches the group has in all.</summary>
public sealed record SearchHit(string Group, string ContentType, string Title, string Summary, string Path, double Score, int GroupTotal);
