using System.Text.Json;
using Onyx.Application.Privacy;
using Onyx.Domain.Privacy;

namespace Onyx.Application.Tests.Privacy;

public sealed class DataSubjectExportBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Manage booking: https://onyx.test/en/manage-booking?token=abcDEF_123-xyz", "Manage booking: https://onyx.test/en/manage-booking?token=[removed]")]
    [InlineData("https://admin.test/reset?userId=1&token=Q2ZE&x=1", "https://admin.test/reset?userId=1&token=[removed]&x=1")]
    [InlineData("لا توجد روابط هنا", "لا توجد روابط هنا")]
    public void One_time_link_tokens_are_removed_from_email_bodies(string body, string expected) =>
        Assert.Equal(expected, DataSubjectExportBuilder.RedactTokens(body));

    [Fact]
    public void The_export_is_valid_utf8_json_with_arabic_as_characters_and_a_stable_shape()
    {
        var request = DataSubjectRequest.Receive(DataSubjectRequestType.Export, "a@example.com", null, Now).Value;
        var emails = new[] { new SubjectEmailRecord("leads.inquiry-received", "ar", "استلمنا طلبك", "مرحباً سارة token=secret123", "Sent", Now, Now) };

        var json = DataSubjectExportBuilder.Build(request, "a@example.com", SubjectRecords.Empty, emails, null, Now);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            ["format", "version", "request", "subject", "leads", "inquiries", "bookings", "emails", "account"],
            document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Contains("استلمنا طلبك", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret123", json, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("account").ValueKind);
        Assert.Equal(json, DataSubjectExportBuilder.Build(request, "a@example.com", SubjectRecords.Empty, emails, null, Now));
    }
}
