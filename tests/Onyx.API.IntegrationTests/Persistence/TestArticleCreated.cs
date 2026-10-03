using Onyx.Domain.Common;

namespace Onyx.API.IntegrationTests.Persistence;

public sealed record TestArticleCreated(Guid ArticleId, string Title) : IDomainEvent;
