using System.Globalization;

namespace Ursa.UITools;

/// <summary>Ready-made masks for <see cref="UrsaMaskedField"/>.</summary>
public static class UrsaMasks
{
    /// <summary>
    /// The mask of a date (and optionally time) in the format of <paramref name="culture"/>, for example
    /// <c>00/00/0000</c> in fr-FR or <c>00/00/0000 00:00 aa</c> in en-US with time. Returns null when the culture's
    /// pattern cannot be expressed as a fixed-width mask.
    /// </summary>
    public static string? Date(CultureInfo? culture = null, bool withTime = false) =>
        Analyse(culture, withTime) is { } pattern
            ? pattern.MaskText.Replace('y', '0').Replace('M', '0').Replace('d', '0')
            : null;

    /// <summary>
    /// The fixed-width .NET format that matches <see cref="Date"/>, to parse what the field returns with
    /// <c>DateTime.TryParseExact</c> and the same culture.
    /// </summary>
    public static string? DateFormat(CultureInfo? culture = null, bool withTime = false) =>
        Analyse(culture, withTime)?.Normalized;

    private static UrsaDatePattern? Analyse(CultureInfo? culture, bool withTime)
    {
        var info = (culture ?? CultureInfo.CurrentCulture).DateTimeFormat;
        var pattern = withTime ? $"{info.ShortDatePattern} {info.ShortTimePattern}" : info.ShortDatePattern;
        return UrsaDatePattern.TryCreate(pattern, info, out var result) ? result : null;
    }
}
