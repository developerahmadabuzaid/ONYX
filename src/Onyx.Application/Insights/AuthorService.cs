using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Content;
using Onyx.Contracts.Insights;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Insights;

namespace Onyx.Application.Insights;

/// <summary>Authors are real, consenting people. One still credited by content cannot be deleted (deactivate it instead).</summary>
public sealed class AuthorService(IAuthorRepository authors, IAuditTrail auditTrail, IUnitOfWork unitOfWork) : IAuthorService
{
    private static readonly Error NotFound = Error.NotFound("Author.NotFound", "Author not found.");

    public async Task<IReadOnlyList<AuthorResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await authors.ListAsync(cancellationToken).ConfigureAwait(false)).Select(ToResponse).ToList();

    public async Task<Result<AuthorResponse>> CreateAsync(SaveAuthorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var created = Author.Create(ContentMapping.ToDomain(request.Names), request.ConsentNote);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        authors.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(created.Value);
    }

    public async Task<Result<AuthorResponse>> UpdateAsync(Guid id, SaveAuthorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var author = await authors.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (author is null)
        {
            return NotFound;
        }

        if (request.Version is { } version)
        {
            authors.SetExpectedVersion(author, version);
        }

        var updated = author.Update(ContentMapping.ToDomain(request.Names), request.ConsentNote, request.IsActive);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(author);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var author = await authors.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (author is null)
        {
            return NotFound;
        }

        if (await authors.IsCreditedAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Author.Credited", "This author is credited on content. Deactivate the author or change the byline first.");
        }

        authors.Remove(author);
        auditTrail.Record(AuditAction.AuthorDeleted, AuditCategory.Content, nameof(Author), author.Id.ToString(), null);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static AuthorResponse ToResponse(Author author) =>
        new(author.Id, author.Names.Select(ContentMapping.ToDto).ToList(), author.ConsentNote, author.IsActive, author.Version);
}
