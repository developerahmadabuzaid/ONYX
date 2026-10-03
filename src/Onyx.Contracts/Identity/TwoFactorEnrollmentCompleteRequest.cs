namespace Onyx.Contracts.Identity;

public sealed record TwoFactorEnrollmentCompleteRequest(string EnrollmentToken, string Code);
