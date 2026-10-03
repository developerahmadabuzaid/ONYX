namespace Onyx.Application.Email;

public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);
