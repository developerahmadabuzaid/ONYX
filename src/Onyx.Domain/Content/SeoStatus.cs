namespace Onyx.Domain.Content;

/// <summary>Editorial progress of a translation's SEO work. Not a score, and implies nothing about rankings.</summary>
public enum SeoStatus
{
    NotStarted = 0,
    InProgress = 1,
    Ready = 2,
    NeedsReview = 3,
}
