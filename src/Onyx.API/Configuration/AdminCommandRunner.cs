using Microsoft.Extensions.Options;
using Onyx.Application.Identity;
using Onyx.Application.Localization;

namespace Onyx.API.Configuration;

/// <summary>
/// One-off operator commands (run instead of the web host):
/// <list type="bullet">
/// <item><c>dotnet Onyx.API.dll seed-identity</c> — creates missing seeded roles.</item>
/// <item><c>dotnet Onyx.API.dll bootstrap-superadmin --email you@company --name "Full Name" [--language ar|en]</c> —
/// password read from <c>ONYX_BOOTSTRAP_PASSWORD</c> (never a command-line argument, never printed).</item>
/// <item><c>dotnet Onyx.API.dll reset-two-factor --email owner@company --confirm owner@company --reason "..."</c> —
/// break-glass 2FA recovery for the last enrolled SuperAdmin (docs/development-setup.md §7).</item>
/// </list>
/// </summary>
public static class AdminCommandRunner
{
    public const string PasswordVariable = "ONYX_BOOTSTRAP_PASSWORD";
    public const string ResetTwoFactorCommand = "reset-two-factor";

    private static readonly string[] ResetTwoFactorOptions = ["--email", "--confirm", "--reason"];

    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || args[0] is not ("seed-identity" or "bootstrap-superadmin" or ResetTwoFactorCommand))
        {
            return false;
        }

        try
        {
            if (args[0] == "seed-identity")
            {
                await using var scope = app.Services.CreateAsyncScope();
                var created = await scope.ServiceProvider.GetRequiredService<IIdentityBootstrapService>().SeedRolesAsync(CancellationToken.None);
                Console.WriteLine($"Identity seed complete: {created} role(s) created.");
                return true;
            }

            if (args[0] == ResetTwoFactorCommand)
            {
                await ResetTwoFactorAsync(app, args);
                return true;
            }

            await BootstrapSuperAdminAsync(app, args);
        }
        catch (OptionsValidationException exception)
        {
            Console.Error.WriteLine($"Configuration error: {string.Join(" ", exception.Failures)}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static async Task BootstrapSuperAdminAsync(WebApplication app, string[] args)
    {
        // Validate the command itself before touching any service or database.
        var email = Argument(args, "--email");
        var name = Argument(args, "--name");
        var language = Argument(args, "--language") ?? SupportedLanguages.Default;
        var password = Environment.GetEnvironmentVariable(PasswordVariable);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine($"Usage: bootstrap-superadmin --email <email> --name \"<display name>\" [--language ar|en], with the password in {PasswordVariable}.");
            Environment.ExitCode = 1;
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IIdentityBootstrapService>()
            .CreateInitialSuperAdminAsync(email, name, password, language, CancellationToken.None);

        if (result.IsSuccess)
        {
            Console.WriteLine($"SuperAdmin created ({result.Value}). Sign in to enrol an authenticator, then unset {PasswordVariable}.");
            return;
        }

        Console.Error.WriteLine($"Bootstrap failed: {result.Error!.Message}");
        foreach (var (field, messages) in result.Error.ValidationErrors ?? new Dictionary<string, string[]>())
        {
            Console.Error.WriteLine($"  {field}: {string.Join(" ", messages)}");
        }

        Environment.ExitCode = 1;
    }

    /// <summary>
    /// Validates <c>reset-two-factor</c> arguments without touching any service: exactly <c>--email</c>, a matching
    /// <c>--confirm</c> (typed twice to prevent targeting the wrong account) and a <c>--reason</c> for the audit record.
    /// Returns null when valid, otherwise the problem.
    /// </summary>
    public static string? ResetTwoFactorArgumentError(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = args.Skip(1).ToArray();
        if (options.Length != ResetTwoFactorOptions.Length * 2)
        {
            return "Exactly --email, --confirm and --reason are required, each once.";
        }

        var names = options.Where((_, index) => index % 2 == 0).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length || names.Except(ResetTwoFactorOptions, StringComparer.Ordinal).Any())
        {
            return "Unknown or repeated option. Allowed: --email, --confirm, --reason.";
        }

        var email = Argument(args, "--email")?.Trim();
        var confirm = Argument(args, "--confirm")?.Trim();
        var reason = Argument(args, "--reason")?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(email) || !email.Contains('@', StringComparison.Ordinal) || email.StartsWith("--", StringComparison.Ordinal))
        {
            return "--email must be the target account's email address.";
        }

        if (!string.Equals(email, confirm, StringComparison.OrdinalIgnoreCase))
        {
            return "--confirm must repeat the --email value exactly.";
        }

        return reason.Length is < IdentityRecoveryLimits.ReasonMinLength or > IdentityRecoveryLimits.ReasonMaxLength
            ? $"--reason must be {IdentityRecoveryLimits.ReasonMinLength}-{IdentityRecoveryLimits.ReasonMaxLength} characters (it is stored in the audit log)."
            : null;
    }

    private static async Task ResetTwoFactorAsync(WebApplication app, string[] args)
    {
        // Validate the command itself before touching any service or database.
        if (ResetTwoFactorArgumentError(args) is { } problem)
        {
            Console.Error.WriteLine(problem);
            Console.Error.WriteLine($"Usage: {ResetTwoFactorCommand} --email <superadmin email> --confirm <same email> --reason \"<why, for the audit log>\"");
            Environment.ExitCode = 1;
            return;
        }

        var operatorIdentity = $"{Environment.UserName}@{Environment.MachineName}";
        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IIdentityBootstrapService>().RecoverSuperAdminTwoFactorAsync(
            Argument(args, "--email")!.Trim(), Argument(args, "--reason")!.Trim(), operatorIdentity, CancellationToken.None);

        if (result.IsSuccess)
        {
            // No authentication data is printed: only the account id and what was ended.
            Console.WriteLine(
                $"Two-factor authentication reset for user {result.Value!.UserId} (sessions revoked: {result.Value.SessionsRevoked}, " +
                $"sign-in steps closed: {result.Value.ChallengesClosed}). The user must enrol a new authenticator at the next sign-in. Audit record written.");
            return;
        }

        Console.Error.WriteLine($"Recovery refused: {result.Error!.Message}");
        Environment.ExitCode = 1;
    }

    private static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
