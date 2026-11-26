namespace Onyx.Contracts.SiteContent;

public sealed record SitePhone
{
    public IReadOnlyDictionary<string, string> Label { get; init; } = new Dictionary<string, string>();

    public string Number { get; init; } = string.Empty;
}
