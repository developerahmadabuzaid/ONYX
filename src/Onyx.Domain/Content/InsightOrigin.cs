namespace Onyx.Domain.Content;

/// <summary>How an insight was written. Once AI-assisted, always AI-assisted: the provenance cannot be downgraded.</summary>
public enum InsightOrigin
{
    Human = 0,
    AiAssisted = 1,
}
