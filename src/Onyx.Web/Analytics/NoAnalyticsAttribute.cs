namespace Onyx.Web.Analytics;

/// <summary>Pages that must never load analytics (signed-token pages, previews), whatever the visitor's consent.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class NoAnalyticsAttribute : Attribute;
