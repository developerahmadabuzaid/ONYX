using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Onyx.Infrastructure.Persistence;

/// <summary>Maps a value object (or list of them) to a <c>jsonb</c> column with value comparison by serialized form.</summary>
internal static class JsonColumnExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static PropertyBuilder<T> HasJsonColumn<T>(this PropertyBuilder<T> builder)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        var converter = new ValueConverter<T, string>(value => Serialize(value), json => Deserialize<T>(json));
        var comparer = new ValueComparer<T>(
            (left, right) => Serialize(left) == Serialize(right),
            value => Serialize(value).GetHashCode(StringComparison.Ordinal),
            value => Deserialize<T>(Serialize(value)));
        return builder.HasConversion(converter, comparer).HasColumnType("jsonb");
    }

    private static string Serialize<T>(T? value) => JsonSerializer.Serialize(value, Options);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Null JSON for {typeof(T).Name}.");
}
