using System.Globalization;
using System.Text.RegularExpressions;

namespace Ursa.UITools;

/// <summary>Ready-made masks for <see cref="UrsaMaskedField"/>.</summary>
public static class UrsaMasks
{
    private static readonly UrsaLoc L = new();

    // An IPv4 octet, 0-255, no leading zeros beyond a single "0".
    private const string Octet = @"(?:25[0-5]|2[0-4][0-9]|[0-1]?[0-9][0-9]?)";

    // A standard .NET numeric format spec: a style letter and an optional precision, e.g. "N2", "C", "F0".
    private static readonly Regex FormatSpec = new(@"^([A-Za-z])(\d{0,2})$", RegexOptions.Compiled);

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
    /// <see cref="UrsaMaskType.None"/>. <paramref name="culture"/> matters for the date and decimal types and
    /// defaults to the current culture; <paramref name="format"/> only matters for <see cref="UrsaMaskType.Decimal"/>.
    /// </summary>
    public static UrsaMaskPreset? ForType(UrsaMaskType type, CultureInfo? culture = null, string? format = null) => type switch
    {
        UrsaMaskType.PhoneNorthAmerica => new("(000) 000-0000", @"^\([2-9]\d{2}\) [2-9]\d{2}-\d{4}$", L["Not a valid phone number"]),
        UrsaMaskType.PhoneEurope => new("+00 0 00 00 00 00", @"^\+[34]\d \d \d{2} \d{2} \d{2} \d{2}$", L["Not a valid European phone number"]),
        UrsaMaskType.PostalCodeCanada => new("a0a 0a0", @"^[ABCEGHJ-NPRSTVXY]\d[ABCEGHJ-NPRSTV-Z] \d[ABCEGHJ-NPRSTV-Z]\d$", L["Not a valid postal code"], UpperCase: true),
        UrsaMaskType.ZipCodeUS => new("00000", @"^\d{5}$", L["Not a valid ZIP code"]),
        UrsaMaskType.Ipv4 => new("", @"^" + Octet + @"(?:\." + Octet + @"){3}$", L["Not a valid IPv4 address"],
            RegExMask: @"^(?:25[0-5]|2[0-4][0-9]|[0-1]?[0-9]{0,2})(?:\.(?:25[0-5]|2[0-4][0-9]|[0-1]?[0-9]{0,2})){0,3}(?!\s)$",
            Delimiters: "."),
        UrsaMaskType.Email => new("", @"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", L["Not a valid email address"]),
        UrsaMaskType.Date => DatePreset(culture, withTime: false),
        UrsaMaskType.DateTime => DatePreset(culture, withTime: true),
        UrsaMaskType.Decimal => DecimalPreset(culture, format),
        _ => null,
    };

    /// <summary>
    /// How <paramref name="format"/> (a standard .NET numeric format string, for example <c>N2</c> or <c>C2</c>;
    /// defaults to <c>N2</c>) and <paramref name="culture"/> (the current culture by default) shape a
    /// <see cref="UrsaMaskType.Decimal"/> value: its separators, decimal digits and, for <c>C</c>, currency symbol.
    /// </summary>
    public static UrsaDecimalFormat DecimalFormatOf(string? format, CultureInfo? culture = null)
    {
        var current = culture ?? CultureInfo.CurrentCulture;
        var spec = string.IsNullOrEmpty(format) ? "N2" : format;
        var match = FormatSpec.Match(spec);
        var isCurrency = match.Success && char.ToUpperInvariant(match.Groups[1].Value[0]) == 'C';
        var numberFormat = current.NumberFormat;
        var defaultDigits = isCurrency ? numberFormat.CurrencyDecimalDigits : numberFormat.NumberDecimalDigits;
        var digits = match.Success && match.Groups[2].Value.Length > 0
            ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
            : defaultDigits;

        return new UrsaDecimalFormat(
            spec,
            isCurrency ? numberFormat.CurrencyDecimalSeparator : numberFormat.NumberDecimalSeparator,
            isCurrency ? numberFormat.CurrencyGroupSeparator : numberFormat.NumberGroupSeparator,
            digits,
            isCurrency ? numberFormat.CurrencySymbol : null,
            numberFormat.CurrencyPositivePattern is 0 or 2);
    }

    /// <summary>
    /// Parses <paramref name="text"/> as a decimal with <paramref name="culture"/> (allowing the grouping,
    /// decimal separator and, for a currency <see cref="UrsaDecimalFormat.NumberFormat"/>, the currency symbol
    /// that <paramref name="format"/> describes) and re-formats it with <see cref="UrsaDecimalFormat.NumberFormat"/>,
    /// or returns null when <paramref name="text"/> is not a valid number.
    /// </summary>
    public static string? NormalizeDecimal(string text, UrsaDecimalFormat format, CultureInfo? culture = null)
    {
        var current = culture ?? CultureInfo.CurrentCulture;
        var styles = format.CurrencySymbol is not null
            ? NumberStyles.Currency
            : NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands
              | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        return decimal.TryParse(text, styles, current, out var value) ? value.ToString(format.NumberFormat, current) : null;
    }

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

    private static UrsaMaskPreset DecimalPreset(CultureInfo? culture, string? format) =>
        new("", null, L["Not a valid number"], DecimalFormat: DecimalFormatOf(format, culture));

    private static UrsaDatePattern? Analyse(CultureInfo? culture, bool withTime)
    {
        var info = (culture ?? CultureInfo.CurrentCulture).DateTimeFormat;
        var pattern = withTime ? $"{info.ShortDatePattern} {info.ShortTimePattern}" : info.ShortDatePattern;
        return UrsaDatePattern.TryCreate(pattern, info, out var result) ? result : null;
    }
}
