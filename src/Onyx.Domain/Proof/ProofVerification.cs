using Onyx.Domain.Common;

namespace Onyx.Domain.Proof;

/// <summary>
/// A recorded verification of public proof (CLAUDE.md §2.1): who verified it, when, against which source, and the client's
/// permission. Any change to the verified content clears it, so only verified wording can be published.
/// </summary>
public sealed record ProofVerification(Guid VerifiedById, DateTimeOffset VerifiedAtUtc, string SourceNote, ClientPermission ClientPermission, string? ClientPermissionNote)
{
    public const int NoteMaxLength = 1_000;

    public static Result<ProofVerification> Create(
        Guid verifiedById, DateTimeOffset now, string? sourceNote, ClientPermission clientPermission, string? clientPermissionNote, bool clientPermissionRequired)
    {
        var source = sourceNote?.Trim();
        var permissionNote = string.IsNullOrWhiteSpace(clientPermissionNote) ? null : clientPermissionNote.Trim();
        if (string.IsNullOrEmpty(source) || source.Length > NoteMaxLength || permissionNote is { Length: > NoteMaxLength } || !Enum.IsDefined(clientPermission))
        {
            return Error.Validation("Proof.SourceRequired", $"Record where the proof was verified (1-{NoteMaxLength} characters).");
        }

        if (clientPermission == ClientPermission.Granted && permissionNote is null)
        {
            return Error.Validation("Proof.PermissionNoteRequired", "Record how the client granted permission (who, when, how).");
        }

        if (clientPermissionRequired && clientPermission != ClientPermission.Granted)
        {
            return Error.Validation("Proof.ClientPermissionRequired", "This proof identifies the client, so the client's recorded permission is required.");
        }

        return new ProofVerification(verifiedById, now, source, clientPermission, permissionNote);
    }
}
