using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Worker;
using Onyx.Application.Media;
using Onyx.Contracts.Media;
using Onyx.Domain.Auditing;
using Onyx.Domain.Media;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Media;

namespace Onyx.API.IntegrationTests.Content;

/// <summary>
/// Deleting media commits the record removal together with an outbox event; the Worker removes the file and retries when storage
/// fails, so a file is never left behind untracked.
/// </summary>
public sealed class MediaDeletionApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public async Task A_failed_file_removal_is_retried_by_the_worker_until_the_file_is_gone()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var asset = await UploadAsync(owner);
        var file = Assert.Single(Directory.GetFiles(MediaRoot, "*", SearchOption.AllDirectories));

        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(owner.DeleteAsync($"/api/v1/admin/media/{asset.Id}", Token)));

        // The record is gone and audited; the file is still tracked by the outbox message that will remove it.
        Assert.Empty(await QueryAsync(db => db.MediaAssets.Where(media => media.Id == asset.Id)));
        Assert.Contains(AuditAction.MediaDeleted, await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == asset.Id.ToString()).Select(log => log.Action)));
        Assert.True(File.Exists(file));
        var message = Assert.Single(await PendingCleanupAsync());
        Assert.Equal(asset.Id, JsonDocument.Parse(message.Payload).RootElement.GetProperty("mediaAssetId").GetGuid());

        var storage = new FlakyStorage(failures: 1);
        await using var worker = WorkerTestHost.Create(
            Fixture.ApplicationDatabase!.ConnectionString, "worker-a", Time,
            services => services.AddSingleton<IMediaStorage>(provider => storage.Wrapping(ActivatorUtilities.CreateInstance<LocalFileMediaStorage>(provider))),
            new Dictionary<string, string?> { ["Media:StorageRoot"] = MediaRoot });
        var processor = worker.Get<OutboxProcessor>();

        // First attempt: storage fails, the message stays pending with the error and a retry time, the file remains.
        await processor.ProcessBatchAsync(Token);
        var failed = Assert.Single(await PendingCleanupAsync());
        Assert.Equal(1, failed.AttemptCount);
        Assert.Contains("storage unavailable", failed.LastError, StringComparison.Ordinal);
        Assert.Null(failed.ProcessedAtUtc);
        Assert.True(File.Exists(file));

        // Retry after the backoff: the file is removed and the message completes.
        Time.Advance(TimeSpan.FromMinutes(2));
        await processor.ProcessBatchAsync(Token);
        Assert.False(File.Exists(file));
        Assert.Empty(await PendingCleanupAsync());
        Assert.Equal(2, storage.Attempts);
    }

    [Fact]
    public async Task Media_in_use_is_still_never_deleted_and_no_cleanup_is_scheduled()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var asset = await UploadAsync(owner);
        var page = await CreatePageAsync(owner);
        await ReadAsync<Onyx.Contracts.Content.ContentDetailResponse>(await owner.PutAsync(
            $"{Admin}/{page.Id}/settings",
            System.Net.Http.Json.JsonContent.Create(new Onyx.Contracts.Content.UpdateContentSettingsRequest(null, null, "Page", 0, null, asset.Id, page.Version)),
            Token));

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.DeleteAsync($"/api/v1/admin/media/{asset.Id}", Token)));
        Assert.Single(await QueryAsync(db => db.MediaAssets.Where(media => media.Id == asset.Id)));
        Assert.Empty(await PendingCleanupAsync());
        Assert.Single(Directory.GetFiles(MediaRoot, "*", SearchOption.AllDirectories));
    }

    private Task<List<Onyx.Infrastructure.Persistence.Outbox.OutboxMessage>> PendingCleanupAsync()
    {
        var type = typeof(MediaAssetDeleted).FullName;
        return QueryAsync(db => db.OutboxMessages.Where(message => message.Type == type && message.ProcessedAtUtc == null));
    }

    private static async Task<MediaAssetResponse> UploadAsync(HttpClient owner)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "photo.png");
        return await ReadAsync<MediaAssetResponse>(await owner.PostAsync("/api/v1/admin/media", form, Token));
    }

    /// <summary>Real file storage whose first deletions fail, as if the disk or share were briefly unavailable.</summary>
    private sealed class FlakyStorage(int failures)
    {
        private int _remainingFailures = failures;

        public int Attempts { get; private set; }

        public IMediaStorage Wrapping(IMediaStorage inner) => new Wrapper(this, inner);

        private sealed class Wrapper(FlakyStorage owner, IMediaStorage inner) : IMediaStorage
        {
            public Task SaveAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => inner.SaveAsync(storageKey, content, cancellationToken);

            public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => inner.OpenReadAsync(storageKey, cancellationToken);

            public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
            {
                owner.Attempts++;
                if (owner._remainingFailures-- > 0)
                {
                    throw new IOException("storage unavailable");
                }

                return inner.DeleteAsync(storageKey, cancellationToken);
            }
        }
    }
}
