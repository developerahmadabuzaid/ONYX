using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="PublishAt"/> carries its UTC offset; <see cref="TimeZone"/> is the IANA zone the editor used (display only).</summary>
public sealed record ScheduleContentRequest(DateTimeOffset PublishAt, string? TimeZone, uint Version);
