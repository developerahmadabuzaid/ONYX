using Microsoft.EntityFrameworkCore;
using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Intelligence;
using Onyx.Application.Knowledge;
using Onyx.Application.Media;
using Onyx.Contracts.Common;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Repositories;

/// <summary>Builds a parameterized ILIKE "contains" pattern with wildcard characters escaped.</summary>
internal static class LikePattern
{
    public const string EscapeCharacter = "\\";

    public static string Contains(string value) =>
        "%" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
}
