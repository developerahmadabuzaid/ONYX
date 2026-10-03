namespace Onyx.Web.Localization;

/// <summary>A public page whose first path segment differs per language (e.g. <c>/ar/الخدمات</c> ↔ <c>/en/services</c>).</summary>
public sealed record LocalizedRoute(string Key, string Controller, string Action, IReadOnlyDictionary<string, string> Segments);
