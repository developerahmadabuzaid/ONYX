using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Builds links into the Onyx.Web admin area (routes implemented in Stage 4). Tokens are base64url-encoded; the receiving
/// page must send <c>Referrer-Policy: no-referrer</c> so they never leak to third parties.
/// </summary>
public sealed class AdminLinkBuilder(IOptions<IdentitySecurityOptions> options)
{
    public const string AcceptInvitationPath = "/admin/account/accept-invitation";
    public const string ResetPasswordPath = "/admin/account/reset-password";

    public Uri AcceptInvitation(Guid userId, string token) => Build(AcceptInvitationPath, userId, token);

    public Uri ResetPassword(Guid userId, string token) => Build(ResetPasswordPath, userId, token);

    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private Uri Build(string path, Guid userId, string token)
    {
        var baseUrl = options.Value.AdminPortalBaseUrl.TrimEnd('/');
        var query = string.Create(CultureInfo.InvariantCulture, $"?userId={userId:D}&token={EncodeToken(token)}");
        return new Uri(baseUrl + path + query, UriKind.Absolute);
    }
}
