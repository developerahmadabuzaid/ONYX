using Onyx.Domain.Common;

namespace Onyx.Domain.Notifications;

/// <summary>
/// An item in the Admin Notification Center. Visible only to users holding <see cref="RequiredPermission"/>.
/// Text is not stored: the Admin UI localizes <see cref="Type"/> + <see cref="Parameters"/>, which must never carry
/// detail beyond what that permission allows (no secrets, minimal personal data).
/// </summary>
public sealed class AdminNotification : Entity
{
    public const int RequiredPermissionMaxLength = 100;
    public const int ResourceTypeMaxLength = 100;
    public const int ResourceIdMaxLength = 100;
    public const int MaxParameters = 10;
    public const int ParameterKeyMaxLength = 50;
    public const int ParameterValueMaxLength = 200;

    private AdminNotification()
    {
        RequiredPermission = string.Empty;
        Parameters = new Dictionary<string, string>();
    }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public NotificationType Type { get; private set; }

    public NotificationSeverity Severity { get; private set; }

    public string RequiredPermission { get; private set; }

    public IReadOnlyDictionary<string, string> Parameters { get; private set; }

    /// <summary>Admin resource the notification points at (e.g. "Lead"); the link is re-authorized when opened.</summary>
    public string? ResourceType { get; private set; }

    public string? ResourceId { get; private set; }

    public static AdminNotification Create(
        DateTimeOffset createdAtUtc,
        NotificationType type,
        NotificationSeverity severity,
        string requiredPermission,
        IReadOnlyDictionary<string, string>? parameters = null,
        string? resourceType = null,
        string? resourceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermission);
        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Notification timestamps must be UTC.", nameof(createdAtUtc));
        }

        if (requiredPermission.Length > RequiredPermissionMaxLength)
        {
            throw new ArgumentException("Required permission is too long.", nameof(requiredPermission));
        }

        var safeParameters = new Dictionary<string, string>(StringComparer.Ordinal);
        if (parameters is not null)
        {
            if (parameters.Count > MaxParameters)
            {
                throw new ArgumentException($"At most {MaxParameters} parameters are allowed.", nameof(parameters));
            }

            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(key) || key.Length > ParameterKeyMaxLength)
                {
                    throw new ArgumentException("Parameter keys must be non-empty and short.", nameof(parameters));
                }

                safeParameters[key] = value.Length > ParameterValueMaxLength ? value[..ParameterValueMaxLength] : value;
            }
        }

        return new AdminNotification
        {
            CreatedAtUtc = createdAtUtc,
            Type = type,
            Severity = severity,
            RequiredPermission = requiredPermission,
            Parameters = safeParameters,
            ResourceType = Truncate(resourceType, ResourceTypeMaxLength),
            ResourceId = Truncate(resourceId, ResourceIdMaxLength),
        };
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
}
