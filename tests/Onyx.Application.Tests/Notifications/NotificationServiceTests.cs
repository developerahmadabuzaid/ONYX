using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Notifications;
using Onyx.Domain.Common;
using Onyx.Domain.Notifications;

namespace Onyx.Application.Tests.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task Publishing_with_a_permission_outside_the_catalogue_is_rejected_and_nothing_is_saved()
    {
        var repository = new RecordingRepository();
        var unitOfWork = new CountingUnitOfWork();
        var service = new NotificationService(repository, unitOfWork, TimeProvider.System);

        var result = await service.PublishAsync(
            new NotificationRequest(NotificationType.NewLead, NotificationSeverity.Info, "Leads.Veiw"), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(repository.Added);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Publishing_with_a_catalogue_permission_persists_once()
    {
        var repository = new RecordingRepository();
        var unitOfWork = new CountingUnitOfWork();
        var service = new NotificationService(repository, unitOfWork, TimeProvider.System);

        var result = await service.PublishAsync(
            new NotificationRequest(NotificationType.NewLead, NotificationSeverity.Info, Permissions.Leads.View), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value, Assert.Single(repository.Added).Id);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task<Onyx.Domain.Common.Result<T>> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<Onyx.Domain.Common.Result<T>>> work, CancellationToken cancellationToken) => work(cancellationToken);
    }

    private sealed class RecordingRepository : INotificationRepository
    {
        public List<AdminNotification> Added { get; } = [];

        public void Add(AdminNotification notification) => Added.Add(notification);

        public void AddReadState(NotificationReadState readState) => throw new NotSupportedException();

        public Task<int> CountUnreadAsync(NotificationViewer viewer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> IsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> IsVisibleAsync(Guid notificationId, NotificationViewer viewer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PagedResult<AdminNotificationResponse>> ListVisibleAsync(
            NotificationViewer viewer, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListUnreadVisibleIdsAsync(NotificationViewer viewer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
