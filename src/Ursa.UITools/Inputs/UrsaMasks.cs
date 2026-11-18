using System.Globalization;

namespace Ursa.UITools;

/// <summary>Ready-made masks for <see cref="UrsaMaskedField"/>.</summary>
public static class UrsaMasks
{
    private static readonly UrsaLoc L = new();

    // An IPv4 octet, 0-255, no leading zeros beyond a single "0".
    private const string Octet = @"(?:25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)";

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

    /// <summary>
    /// The mask, validation pattern and error message of a <see cref="UrsaMaskType"/>, or null for
    /// <see cref="UrsaMaskType.None"/>. <paramref name="culture"/> only matters for the date types and defaults to the current culture.
    /// </summary>
    public static UrsaMaskPreset? ForType(UrsaMaskType type, CultureInfo? culture = null) => type switch
    {
        UrsaMaskType.PhoneNorthAmerica => new("(000) 000-0000", @"^\([2-9]\d{2}\) [2-9]\d{2}-\d{4}$", L["Not a valid phone number"]),
        UrsaMaskType.PhoneEurope => new("+00 0 00 00 00 00", @"^\+[34]\d \d \d{2} \d{2} \d{2} \d{2}$", L["Not a valid European phone number"]),
        UrsaMaskType.PostalCodeCanada => new("a0a 0a0", @"^[ABCEGHJ-NPRSTVXY]\d[ABCEGHJ-NPRSTV-Z] \d[ABCEGHJ-NPRSTV-Z]\d$", L["Not a valid postal code"], UpperCase: true),
        UrsaMaskType.ZipCodeUS => new("00000", @"^\d{5}$", L["Not a valid ZIP code"]),
        UrsaMaskType.Ipv4 => new("", @"^" + Octet + @"(?:\." + Octet + @"){3}$", L["Not a valid IPv4 address"],
            RegexPattern: @"^(?:25[0-5]|2[0-4][0-9]|[0-1]?[0-9]{0,2})(?:\.(?:25[0-5]|2[0-4][0-9]|[0-1]?[0-9]{0,2})){0,3}(?!\s)$",
            Delimiters: "."),
        UrsaMaskType.Email => new("", @"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", L["Not a valid email address"]),
        UrsaMaskType.Date =>DatePreset(culture, withTime: false),
        UrsaMaskType.DateTime => DatePreset(culture, withTime: true),
        _ => null,
    };

    private static UrsaMaskPreset DatePreset(CultureInfo? culture, bool withTime)
    {
        var current = culture ?? CultureInfo.CurrentCulture;
        var mask = Date(current, withTime);
        var format = DateFormat(current, withTime);
        if (mask is null || format is null)
        {
            // The culture's pattern cannot be a fixed-width mask: fall back to ISO 8601.
            current = CultureInfo.InvariantCulture;
            mask = withTime ? "0000-00-00 00:00" : "0000-00-00";
            format = withTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd";
        }

        var error = withTime ? L["Not a valid date and time"] : L["Not a valid date"];
        return new(mask, null, error, UpperCase: true,
            Validator: text => DateTime.TryParseExact(text, format, current, DateTimeStyles.None, out _) ? null : error);
    }

    private static UrsaDatePattern? Analyse(CultureInfo? culture, bool withTime)
    {
        var info = (culture ?? CultureInfo.CurrentCulture).DateTimeFormat;
        var pattern = withTime ? $"{info.ShortDatePattern} {info.ShortTimePattern}" : info.ShortDatePattern;
        return UrsaDatePattern.TryCreate(pattern, info, out var result) ? result : null;
    }
}
