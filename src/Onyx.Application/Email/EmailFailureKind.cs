namespace Onyx.Application.Email;

public enum EmailFailureKind
{
    /// <summary>The transport itself is unreachable or rejects our credentials: retrying other messages now is pointless.</summary>
    ProviderUnavailable = 0,

    /// <summary>This particular message was rejected (e.g. invalid mailbox); other messages may still be sent.</summary>
    MessageRejected = 1,
}
