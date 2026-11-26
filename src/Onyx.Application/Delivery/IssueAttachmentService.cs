using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Media;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Common;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// Issue attachments. The type is decided from the bytes, never from the name or the claimed type: images (checked and stripped of
/// embedded metadata by the same inspector as the media library), PDF, and UTF-8 plain text or CSV. Files are stored under random
/// keys and served only through this service's access check. Contributors attach; the uploader or a project admin removes.
/// </summary>
public sealed class IssueAttachmentService(
    IssueAccessLoader loader,
    IIssueRepository issues,
    IDeliveryReadModel readModel,
    IMediaStorage storage,
    IImageInspector inspector,
    MediaLimits limits,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxPerIssue = 50;

    private static readonly Error NotFound = Error.NotFound("Delivery.AttachmentNotFound", "Attachment not found.");
    private static readonly Error Unsupported = Error.Validation(
        "Delivery.AttachmentType", "Attach images (PNG, JPEG, WebP, GIF), PDF files or plain-text/CSV files.");

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<Result<IssueAttachmentResponse>> UploadAsync(Guid issueId, Stream content, string? fileName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var loaded = await loader.LoadAsync(issueId, can => can.Comment, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (issue, _, viewer, _) = loaded.Value;
        if (await issues.CountAttachmentsAsync(issueId, cancellationToken).ConfigureAwait(false) >= MaxPerIssue)
        {
            return Error.Validation("Delivery.TooManyAttachments", $"An issue can have at most {MaxPerIssue} attachments.");
        }

        var bytes = await ReadBoundedAsync(content, MaxBytes, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return Error.Validation("Delivery.AttachmentTooLarge", $"Attachments are limited to {MaxBytes / (1024 * 1024)} MB.");
        }

        if (bytes.Length == 0)
        {
            return Error.Validation("Delivery.AttachmentEmpty", "The file is empty.");
        }

        var detected = Detect(bytes, fileName);
        if (detected.IsFailure)
        {
            return detected.Error!;
        }

        var (stored, contentType, extension) = (detected.Value.Content, detected.Value.ContentType, detected.Value.Extension);
        var now = timeProvider.GetUtcNow();
        var key = string.Create(CultureInfo.InvariantCulture, $"delivery/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{extension}");
        var attachment = IssueAttachment.Create(
            issue, viewer.UserId, fileName, contentType, extension, stored.LongLength, key, Convert.ToHexStringLower(SHA256.HashData(stored)), now);

        await storage.SaveAsync(key, stored, cancellationToken).ConfigureAwait(false);
        try
        {
            issues.AddAttachment(attachment);
            issues.AddHistory(new IssueHistoryEntry(issue.ProjectId, issue.Id, viewer.UserId, "attachment", null, attachment.FileName, now));
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var names = await readModel.DisplayNamesAsync([viewer.UserId], cancellationToken).ConfigureAwait(false);
        return new IssueAttachmentResponse(
            attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.IsImage,
            names.GetValueOrDefault(viewer.UserId, string.Empty), attachment.UploadedAtUtc, CanDelete: true);
    }

    public async Task<Result<IssueAttachmentFile>> OpenAsync(Guid issueId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(issueId, _ => true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var attachment = await issues.GetAttachmentAsync(attachmentId, cancellationToken).ConfigureAwait(false);
        if (attachment is null || attachment.IssueId != issueId)
        {
            return NotFound;
        }

        var stream = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken).ConfigureAwait(false);
        return stream is null ? NotFound : new IssueAttachmentFile(stream, attachment.ContentType, attachment.FileName, attachment.SizeBytes);
    }

    public async Task<Result> DeleteAsync(Guid issueId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(issueId, _ => true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (issue, _, viewer, can) = loaded.Value;
        var attachment = await issues.GetAttachmentAsync(attachmentId, cancellationToken).ConfigureAwait(false);
        if (attachment is null || attachment.IssueId != issueId)
        {
            return NotFound;
        }

        var ownsIt = attachment.UploadedById == viewer.UserId && can.Comment;
        if (!ownsIt && !can.ManageProject)
        {
            return Error.Forbidden("Delivery.NotAttachmentOwner", "Only the person who attached the file or a project admin can remove it.");
        }

        issues.RemoveAttachment(attachment);
        issues.AddHistory(new IssueHistoryEntry(issue.ProjectId, issue.Id, viewer.UserId, "attachment", attachment.FileName, null, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await storage.DeleteAsync(attachment.StorageKey, CancellationToken.None).ConfigureAwait(false);
        return Result.Success();
    }

    private Result<DetectedFile> Detect(byte[] bytes, string? fileName)
    {
        if (StartsWith(bytes, "%PDF-"u8))
        {
            return new DetectedFile(bytes, "application/pdf", "pdf");
        }

        if (LooksLikeImage(bytes))
        {
            var image = inspector.InspectAndClean(bytes, limits);
            if (image.IsFailure)
            {
                return image.Error!;
            }

            return new DetectedFile(image.Value.Content, image.Value.ContentType, image.Value.Extension);
        }

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (extension is ".txt" or ".csv" or ".log" or ".md" && IsPlainUtf8(bytes))
        {
            return extension == ".csv" ? new DetectedFile(bytes, "text/csv", "csv") : new DetectedFile(bytes, "text/plain", "txt");
        }

        return Unsupported;
    }

    private sealed record DetectedFile(byte[] Content, string ContentType, string Extension);

    private static bool LooksLikeImage(byte[] bytes) =>
        StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47])
        || StartsWith(bytes, [0xFF, 0xD8, 0xFF])
        || StartsWith(bytes, "GIF8"u8)
        || (bytes.Length >= 12 && StartsWith(bytes, "RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8));

    private static bool IsPlainUtf8(byte[] bytes)
    {
        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            return false;
        }

        try
        {
            _ = StrictUtf8.GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool StartsWith(byte[] bytes, ReadOnlySpan<byte> prefix) => bytes.AsSpan().StartsWith(prefix);

    private static async Task<byte[]?> ReadBoundedAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
