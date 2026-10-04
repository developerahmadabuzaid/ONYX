namespace Onyx.Domain.Intelligence;

/// <summary>Correlation wording only; an SEO change is never claimed to have caused a result.</summary>
public enum ChangeAssessment
{
    InsufficientData = 0,
    AssociatedWithImprovement = 1,
    NoClearChange = 2,
    AssociatedWithDecline = 3,
}
