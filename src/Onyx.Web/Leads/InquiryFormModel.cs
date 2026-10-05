using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Leads;

/// <summary>
/// Contact and consent fields shared by every public form. Server-side validation here is for the visitor; the API validates
/// everything again. <see cref="Website"/> is the honeypot (hidden from people, left empty).
/// </summary>
public class InquiryFormModel
{
    [Required(ErrorMessage = "Form.Required")]
    [StringLength(150, ErrorMessage = "Form.TooLong")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Form.Required")]
    [EmailAddress(ErrorMessage = "Form.InvalidEmail")]
    [StringLength(254, ErrorMessage = "Form.TooLong")]
    public string? Email { get; set; }

    [StringLength(40, ErrorMessage = "Form.TooLong")]
    [RegularExpression(@"^[0-9+()\- ]*$", ErrorMessage = "Form.InvalidPhone")]
    public string? Phone { get; set; }

    [StringLength(200, ErrorMessage = "Form.TooLong")]
    public string? Company { get; set; }

    [RegularExpression("^[A-Z]{2}$", ErrorMessage = "Form.Invalid")]
    public string? Country { get; set; }

    [StringLength(4000, ErrorMessage = "Form.TooLong")]
    public string? Message { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Form.ConsentRequired")]
    public bool ContactConsent { get; set; }

    public bool MarketingConsent { get; set; }

    /// <summary>Honeypot.</summary>
    public string? Website { get; set; }

    public Guid? ServiceId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? CtaId { get; set; }

    /// <summary>The landing context of the form page (campaign tags, referring site), captured when the form was shown.</summary>
    [StringLength(2000)]
    public string? Context { get; set; }
}
