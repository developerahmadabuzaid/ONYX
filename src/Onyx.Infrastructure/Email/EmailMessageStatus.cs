namespace Onyx.Infrastructure.Email;

public enum EmailMessageStatus
{
    Pending = 0,
    Sent = 1,
    DeadLettered = 2,
}
