using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Persistence;

/// <summary>Bound from <c>ConnectionStrings:Onyx</c>. The value comes from user secrets or environment variables, never committed config.</summary>
public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Onyx";

    [Required(AllowEmptyStrings = false, ErrorMessage = "ConnectionStrings:Onyx must be configured (user secrets locally, environment variables in production).")]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(5, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;
}
