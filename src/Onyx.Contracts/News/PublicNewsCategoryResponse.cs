namespace Onyx.Contracts.News;

/// <param name="Path">Root-relative, unencoded path, e.g. <c>/en/news/cybersecurity</c>.</param>
public sealed record PublicNewsCategoryResponse(string Key, string Name, string Slug, string Path);
