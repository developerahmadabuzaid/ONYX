using System.Text;

namespace Onyx.Infrastructure.Persistence.Conventions;

/// <summary>Converts .NET identifiers to PostgreSQL-friendly snake_case ("CreatedAtUtc" → "created_at_utc", "HTTPStatus" → "http_status").</summary>
public static class SnakeCaseNaming
{
    public static string ToSnakeCase(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current))
            {
                var previous = i > 0 ? name[i - 1] : '\0';
                var next = i + 1 < name.Length ? name[i + 1] : '\0';
                var startsNewWord = i > 0
                    && previous != '_'
                    && (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && char.IsLower(next)));

                if (startsNewWord)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
