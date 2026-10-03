using Onyx.Domain.Common;

namespace Onyx.Application.Email;

/// <summary>
/// Renders and queues an email in the current unit of work, so it is persisted atomically with the business change that
/// caused it. Onyx.Worker sends it later with retries; a send failure never loses the originating data.
/// </summary>
public interface IEmailOutbox
{
    Task<Result<Guid>> QueueAsync(EmailRequest request, CancellationToken cancellationToken);
}
