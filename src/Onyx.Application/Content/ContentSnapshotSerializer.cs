using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Onyx.Application.Content;

/// <summary>Stable JSON for revision snapshots (enum names, camelCase) and its SHA-256 for de-duplication.</summary>
public static class ContentSnapshotSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(ContentSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);

    public static ContentSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<ContentSnapshot>(json, Options) ?? throw new InvalidOperationException("Empty revision snapshot.");

    public static string Hash(string json) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
}
