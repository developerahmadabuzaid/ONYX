namespace Onyx.Contracts.Products;

public sealed record ProductTranslationDto(string Language, string? Name, string? Slug, string? Summary, string? Description);
