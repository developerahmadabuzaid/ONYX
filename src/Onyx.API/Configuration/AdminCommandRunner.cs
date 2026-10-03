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
/// </list>
/// </summary>
public static class AdminCommandRunner
{
    public const string PasswordVariable = "ONYX_BOOTSTRAP_PASSWORD";

    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || args[0] is not ("seed-identity" or "bootstrap-superadmin"))
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

    private static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
