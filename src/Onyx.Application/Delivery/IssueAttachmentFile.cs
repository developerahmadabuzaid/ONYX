namespace Onyx.Application.Delivery;

/// <summary>An attachment's stored bytes with the type and name to serve them under (always as a download).</summary>
public sealed record IssueAttachmentFile(Stream Content, string ContentType, string FileName, long Length);
