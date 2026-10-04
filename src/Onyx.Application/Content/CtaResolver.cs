using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>Call-to-action resolution: item override → pillar default → content-type default → global default.</summary>
public sealed class CtaResolver(ICallToActionRepository ctas)
{
    public async Task<CallToAction?> ResolveAsync(
        Guid? itemCtaId, Guid? pillarCtaId, ContentType type, string languageCode, CancellationToken cancellationToken)
    {
        var defaults = await ctas.ListDefaultsAsync(cancellationToken).ConfigureAwait(false);
        var candidates = new[]
        {
            itemCtaId,
            pillarCtaId,
            defaults.FirstOrDefault(entry => entry.ContentType == type)?.CallToActionId,
            defaults.FirstOrDefault(entry => entry.ContentType is null)?.CallToActionId,
        }.OfType<Guid>().Distinct().ToList();

        var lookup = await ctas.GetLookupAsync(candidates, cancellationToken).ConfigureAwait(false);
        return candidates
            .Select(id => lookup.GetValueOrDefault(id))
            .FirstOrDefault(cta => cta is { IsActive: true } && cta.Texts.Any(text => text.LanguageCode == languageCode));
    }
}
