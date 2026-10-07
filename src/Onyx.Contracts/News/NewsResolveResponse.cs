namespace Onyx.Contracts.News;

/// <summary>What a slug under the hub is: a category page (<see cref="List"/>), a published item (<see cref="Item"/>), or neither (both null).</summary>
public sealed record NewsResolveResponse(PublicNewsListResponse? List, PublicNewsItemResponse? Item);
