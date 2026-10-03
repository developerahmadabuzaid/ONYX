namespace Onyx.Application.Identity;

public sealed record UserContact(Guid UserId, string Email, string DisplayName, string PreferredLanguage);
