namespace Onyx.Contracts.SiteContent;

public sealed record SiteOffice
{
    public IReadOnlyDictionary<string, string> Name { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> Address { get; init; } = new Dictionary<string, string>();
}
