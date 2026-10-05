using System.Text;
using Onyx.Application.Booking;
using Onyx.Application.Email;
using Onyx.Application.Leads;
using Onyx.Domain.Leads;

namespace Onyx.Application.Tests.Booking;

public sealed class IcsAndCsvTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calendar_files_use_utc_crlf_escaping_and_fold_long_lines_on_character_boundaries()
    {
        var uid = Guid.Parse("01a10d03-0000-7000-8000-000000000001");
        var summary = "استشارة مع Onyx; فريق, المبيعات " + new string('ا', 60);

        var ics = IcsCalendar.Build(uid, summary, Now.AddDays(1), Now.AddDays(1).AddMinutes(30), Now, cancelled: false);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTSTART:20261006T090000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND:20261006T093000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains($"UID:{uid:N}@consultation.onyx", ics, StringComparison.Ordinal);
        Assert.Contains("METHOD:PUBLISH", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.All(ics.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75));

        var unfolded = ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:استشارة مع Onyx\\; فريق\\, المبيعات", unfolded, StringComparison.Ordinal);
        Assert.Contains("STATUS:CANCELLED", IcsCalendar.Build(uid, "x", Now, Now.AddMinutes(30), Now, cancelled: true), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("=SUM(A1)", "\"'=SUM(A1)\"")]
    [InlineData("+1", "\"'+1\"")]
    [InlineData("-cmd", "\"'-cmd\"")]
    [InlineData("@x", "\"'@x\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("شركة", "\"شركة\"")]
    public void Csv_fields_are_quoted_and_formulas_neutralized(string value, string expected) =>
        Assert.Equal(expected, LeadCsv.Escape(value));

    [Fact]
    public void Csv_export_starts_with_a_bom_and_a_header()
    {
        var lead = Lead.Open(ContactDetails.Create("Sample", "a@example.com", null, "=evil()", null).Value, InquiryType.Contact, null, "ar", Now);

        var csv = LeadCsv.Write([lead], new Dictionary<Guid, string>(), new HashSet<Guid> { lead.Id });

        Assert.StartsWith((char)0xFEFF + "\"lead_id\",\"name\",\"email\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"'=evil()\"", csv, StringComparison.Ordinal);
        Assert.EndsWith("\"yes\"\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void The_manage_link_is_percent_encoded_on_the_public_origin_and_absent_without_one()
    {
        var withOrigin = new BookingMessages(LeadSettings.Default with { PublicBaseUrl = new Uri("https://onyx.test") });
        var link = withOrigin.ManageLink("ar", "token_ABC-123")!;

        Assert.Equal("https://onyx.test/ar/%D8%A5%D8%AF%D8%A7%D8%B1%D8%A9-%D8%A7%D9%84%D8%AD%D8%AC%D8%B2?token=token_ABC-123", link.AbsoluteUri);
        Assert.Null(new BookingMessages(LeadSettings.Default).ManageLink("ar", "token"));
    }

    [Fact]
    public void Email_attachments_are_limited_to_small_calendar_files()
    {
        var validator = new EmailRequestValidator();
        EmailRequest Request(params EmailAttachment[] attachments) =>
            new("bookings.confirmed", "en", "a@example.com", null, new Dictionary<string, string>(), attachments);

        Assert.True(validator.Validate(Request(new EmailAttachment("onyx-consultation.ics", "text/calendar", "BEGIN:VCALENDAR"))).IsValid);
        Assert.False(validator.Validate(Request(new EmailAttachment("payload.exe", "application/octet-stream", "MZ"))).IsValid);
        Assert.False(validator.Validate(Request(new EmailAttachment("../x.ics", "text/calendar", "x"))).IsValid);
        Assert.False(validator.Validate(Request(new EmailAttachment("big.ics", "text/calendar", new string('x', EmailRequestValidator.AttachmentMaxLength + 1)))).IsValid);
    }
}
