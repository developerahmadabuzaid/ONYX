using Onyx.Domain.Common;

namespace Onyx.API.IntegrationTests.Persistence;

/// <summary>Test-only entity exercising every persistence building block: audit fields, soft delete, audit log, redaction, domain events.</summary>
public sealed class TestArticle : AuditableEntity, ISoftDeletable, IAuditable
{
    private TestArticle()
    {
        Title = string.Empty;
    }

    public string Title { get; private set; }

    [SensitiveData]
    public string? ApiToken { get; private set; }

    [NotAudited]
    public string? InternalNote { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public Guid? DeletedById { get; private set; }

    public static TestArticle Create(string title, string? apiToken, string? internalNote)
    {
        var article = new TestArticle { Title = title, ApiToken = apiToken, InternalNote = internalNote };
        article.RaiseDomainEvent(new TestArticleCreated(article.Id, title));
        return article;
    }

    public void Rename(string title) => Title = title;

    public void RotateToken(string apiToken) => ApiToken = apiToken;

    public void UpdateNote(string note) => InternalNote = note;
}
