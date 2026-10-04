using System.Text.RegularExpressions;
using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// A reusable call to action with a label per language (<see cref="LocalizedText.Text"/>) and optional supporting line
/// (<see cref="LocalizedText.Secondary"/>). Resolution order: item override → pillar default → content-type default → global default.
/// </summary>
public sealed partial class CallToAction : AuditableEntity, IAuditable
{
    public const int KeyMaxLength = 100;
    public const int LabelMaxLength = 100;
    public const int SecondaryMaxLength = 300;

    private CallToAction()
    {
        Key = string.Empty;
        Texts = [];
    }

    public string Key { get; private set; }

    public CtaActionType ActionType { get; private set; }

    /// <summary>For <see cref="CtaActionType.Url"/>: a site path (e.g. <c>/ar/...</c>) or an absolute https URL.</summary>
    public string? TargetUrl { get; private set; }

    public IReadOnlyList<LocalizedText> Texts { get; private set; }

    public bool IsActive { get; private set; }

    public uint Version { get; private set; }

    public static Result<CallToAction> Create(string key, CtaActionType actionType, string? targetUrl, IReadOnlyList<LocalizedText> texts)
    {
        var trimmedKey = key?.Trim() ?? string.Empty;
        if (!KeyPattern().IsMatch(trimmedKey))
        {
            return Error.Validation("Cta.InvalidKey", "Keys use lower-case letters, digits, dots and hyphens.");
        }

        var cta = new CallToAction { Key = trimmedKey };
        var updated = cta.Update(actionType, targetUrl, texts, isActive: true);
        return updated.IsSuccess ? cta : updated.Error!;
    }

    public Result Update(CtaActionType actionType, string? targetUrl, IReadOnlyList<LocalizedText> texts, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (!Enum.IsDefined(actionType))
        {
            return Error.Validation("Cta.InvalidAction", "Unknown action type.");
        }

        if (actionType == CtaActionType.WhatsApp)
        {
            return Error.Validation("Cta.WhatsAppDisabled", "WhatsApp calls to action are not available at launch.");
        }

        var url = string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl.Trim();
        if (actionType == CtaActionType.Url
            ? url is null || !(ContentBodyRules.IsSafeRelativePath(url) || ContentBodyRules.IsSafeExternalUrl(url))
            : url is not null)
        {
            return Error.Validation("Cta.InvalidTarget", "Only URL calls to action have a target, which must be a site path or an https URL.");
        }

        if (texts.Count == 0 || texts.GroupBy(text => text.LanguageCode).Any(group => group.Count() > 1)
            || texts.Any(text => string.IsNullOrWhiteSpace(text.Text) || text.Text.Trim().Length > LabelMaxLength
                || text.Secondary is { Length: > SecondaryMaxLength } || ContentBodyRules.HasControlCharacters(text.Text, false)))
        {
            return Error.Validation("Cta.InvalidLabels", $"One label (max {LabelMaxLength} characters) per language is required.");
        }

        ActionType = actionType;
        TargetUrl = url;
        Texts = texts.Select(text => text with { Text = text.Text.Trim(), Secondary = string.IsNullOrWhiteSpace(text.Secondary) ? null : text.Secondary.Trim() }).ToList();
        IsActive = isActive;
        return Result.Success();
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
