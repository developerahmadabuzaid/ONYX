namespace Onyx.Application.Common;

/// <summary>Parses API enum names strictly: defined names only (case-insensitive), never numeric strings.</summary>
public static class EnumParsing
{
    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) && char.IsLetter(value.Trim()[0])
            && Enum.TryParse(value.Trim(), ignoreCase: true, out result) && Enum.IsDefined(result);
    }
}
