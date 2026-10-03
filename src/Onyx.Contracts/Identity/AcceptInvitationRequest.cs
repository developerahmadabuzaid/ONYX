namespace Onyx.Contracts.Identity;

/// <summary>Sets the first password of an invited account; possession of the emailed token also confirms the email address.</summary>
public sealed record AcceptInvitationRequest(Guid UserId, string Token, string NewPassword);
