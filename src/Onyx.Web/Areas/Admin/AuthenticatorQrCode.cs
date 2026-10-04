using System.Text;
using Net.Codecrete.QrCodeGenerator;

namespace Onyx.Web.Areas.Admin;

/// <summary>
/// Renders the API-issued <c>otpauth://</c> URI of the current enrolment attempt as a QR code, in memory, for that one response.
/// It encodes the same secret as the textual key (no second secret). Nothing is stored, cached or logged; the page itself is
/// sent with <c>no-store</c> and <c>no-referrer</c>.
/// </summary>
public static class AuthenticatorQrCode
{
    private const string TotpUriPrefix = "otpauth://totp/";
    private const int MaxUriLength = 512;

    /// <summary>An SVG <c>data:</c> URI (allowed by the CSP's <c>img-src data:</c>), or null when the value is not a TOTP URI.</summary>
    public static string? ToDataUri(string? authenticatorUri)
    {
        if (string.IsNullOrEmpty(authenticatorUri) || authenticatorUri.Length > MaxUriLength
            || !authenticatorUri.StartsWith(TotpUriPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var svg = QrCode.EncodeText(authenticatorUri, QrCode.Ecc.Medium).ToSvgString(4);
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }
}
