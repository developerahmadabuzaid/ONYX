using System.Net.Mail;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Leads;

/// <summary>Who submitted an inquiry. Email is required (the reply channel); phone, company and country are optional.</summary>
public sealed record ContactDetails(string Name, string Email, string? Phone, string? Company, string? Country)
{
    public const int NameMaxLength = 150;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 40;
    public const int CompanyMaxLength = 200;

    /// <summary>Lower-cased, trimmed email used to match an inquiry to an open lead.</summary>
    public string NormalizedEmail => Email.Trim().ToLowerInvariant();

    public static Result<ContactDetails> Create(string? name, string? email, string? phone, string? company, string? country)
    {
        var cleanName = name?.Trim() ?? string.Empty;
        var cleanEmail = email?.Trim() ?? string.Empty;
        if (cleanName.Length is 0 or > NameMaxLength || ContentBodyRules.HasControlCharacters(cleanName, allowNewLines: false))
        {
            return Error.Validation("Inquiry.InvalidName", $"Enter a name of up to {NameMaxLength} characters.");
        }

        if (cleanEmail.Length is 0 or > EmailMaxLength || !IsEmail(cleanEmail))
        {
            return Error.Validation("Inquiry.InvalidEmail", "Enter a valid email address.");
        }

        var cleanPhone = Clean(phone);
        if (cleanPhone is { Length: > PhoneMaxLength } || (cleanPhone is not null && !cleanPhone.All(character => char.IsDigit(character) || character is '+' or ' ' or '-' or '(' or ')')))
        {
            return Error.Validation("Inquiry.InvalidPhone", "Enter a phone number using digits, spaces, +, - and brackets only.");
        }

        var cleanCompany = Clean(company);
        if (cleanCompany is { Length: > CompanyMaxLength } || (cleanCompany is not null && ContentBodyRules.HasControlCharacters(cleanCompany, allowNewLines: false)))
        {
            return Error.Validation("Inquiry.InvalidCompany", $"Company names are limited to {CompanyMaxLength} characters.");
        }

        var cleanCountry = Clean(country)?.ToUpperInvariant();
        if (cleanCountry is not null && (cleanCountry.Length != 2 || !cleanCountry.All(char.IsAsciiLetterUpper)))
        {
            return Error.Validation("Inquiry.InvalidCountry", "Country is a two-letter ISO code.");
        }

        return new ContactDetails(cleanName, cleanEmail, cleanPhone, cleanCompany, cleanCountry);
    }

    private static bool IsEmail(string value) =>
        !value.Contains(' ', StringComparison.Ordinal) && MailAddress.TryCreate(value, out var parsed) && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
