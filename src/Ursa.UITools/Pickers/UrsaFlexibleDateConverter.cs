using System.Globalization;
using System.Text.RegularExpressions;
using MudBlazor;

namespace Ursa.UITools;

/// <summary>
/// Converts a date (and the time, when asked) to text using the short patterns of a culture, and reads free-form input:
/// digits stuck together (<c>20260102</c>, <c>260102</c>, <c>202601021430</c>) or separated by any non-digit character,
/// in the year-month-day order of the culture. Invalid input throws a <see cref="FormatException"/> whose message is
/// shown under the field.
/// </summary>
public sealed class UrsaFlexibleDateConverter : IReversibleConverter<DateTime?, string>
{
    private static readonly UrsaLoc L = new();

    private readonly Func<bool> _withTime;
    private readonly Func<CultureInfo> _culture;

    /// <param name="withTime">Tells, at each conversion, whether the time is part of the text.</param>
    /// <param name="culture">The culture of each conversion. Defaults to the current culture.</param>
    public UrsaFlexibleDateConverter(Func<bool>? withTime = null, Func<CultureInfo>? culture = null)
    {
        _withTime = withTime ?? (() => false);
        _culture = culture ?? (() => CultureInfo.CurrentCulture);
    }

    /// <summary>The display pattern: the culture's short date, followed by its short time when asked.</summary>
    public static string Pattern(CultureInfo culture, bool withTime) =>
        withTime
            ? $"{culture.DateTimeFormat.ShortDatePattern} {culture.DateTimeFormat.ShortTimePattern}"
            : culture.DateTimeFormat.ShortDatePattern;

    /// <inheritdoc />
    public string Convert(DateTime? input)
    {
        var culture = _culture();
        return input is { } d ? d.ToString(Pattern(culture, _withTime()), culture) : "";
    }

    /// <inheritdoc />
    public DateTime? ConvertBack(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var culture = _culture();
        var withTime = _withTime();
        if (TryParse(text, culture, withTime, out var date))
        {
            return date;
        }

        var example = new DateTime(2026, 1, 2, 14, 30, 0).ToString(Pattern(culture, withTime), culture);
        throw new FormatException(L["Not a valid date. Enter for example {0} or {1}.", example, Regex.Replace(example, @"\D", "")]);
    }

    /// <summary>The order of the components (y, M, d) in the culture's short date pattern.</summary>
    public static char[] OrderOf(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var order = new List<char>();
        foreach (var c in pattern)
        {
            if (c is 'y' or 'M' or 'd' && !order.Contains(c))
            {
                order.Add(c);
            }
        }

        foreach (var c in "yMd")
        {
            if (!order.Contains(c))
            {
                order.Add(c);
            }
        }

        return order.ToArray();
    }

    /// <summary>Reads a date alone (midnight).</summary>
    public static bool TryParse(string text, CultureInfo culture, out DateTime date) => TryParse(text, culture, false, out date);

    /// <summary>Reads a date, followed by the time (<c>14:30</c>, <c>14 h 30</c>, <c>1430</c>, <c>2:30 pm</c>) when <paramref name="withTime"/> is set.</summary>
    public static bool TryParse(string text, CultureInfo culture, bool withTime, out DateTime date)
    {
        var s = text.Trim();
        if (TryParseDate(s, culture, out date))
        {
            return true; // date alone: midnight
        }

        if (!withTime)
        {
            return false;
        }

        // am / pm
        bool? pm = null;
        var ap = Regex.Match(s, @"\s*([ap])\.?\s?m\.?$", RegexOptions.IgnoreCase);
        if (ap.Success)
        {
            pm = char.ToLowerInvariant(ap.Groups[1].Value[0]) == 'p';
            s = s[..ap.Index].TrimEnd();
        }

        // "14 h 30" becomes "14:30"
        s = Regex.Replace(s, @"(?<=\d)\s*h\s*(?=\d)", ":", RegexOptions.IgnoreCase);

        var m = Regex.Match(s, @"^(?<d>.+?)(?:\s+|T)(?<h>\d{1,2})\s*:\s*(?<m>\d{2})(?::\d{2})?$");
        if (!m.Success)
        {
            m = Regex.Match(s, @"^(?<d>\d{6}|\d{8})\s*(?<h>\d{2})(?<m>\d{2})$"); // 202601021430
        }

        if (!m.Success)
        {
            m = Regex.Match(s, @"^(?<d>.+?)\s+(?<h>\d{2})(?<m>\d{2})$"); // 2026-01-02 1430
        }

        if (!m.Success || !TryParseDate(m.Groups["d"].Value.Trim(), culture, out var day))
        {
            return false;
        }

        var hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
        if (minute > 59)
        {
            return false;
        }

        if (pm is { } afternoon)
        {
            if (hour is < 1 or > 12)
            {
                return false;
            }

            hour = hour % 12 + (afternoon ? 12 : 0);
        }
        else if (hour > 23)
        {
            return false;
        }

        date = day.AddHours(hour).AddMinutes(minute);
        return true;
    }

    private static bool TryParseDate(string text, CultureInfo culture, out DateTime date)
    {
        date = default;
        var order = OrderOf(culture);
        var trimmed = text.Trim();
        string[] parts;

        if (Regex.IsMatch(trimmed, @"^\d+$"))
        {
            // Digits stuck together: 8 (4-digit year) or 6 (2-digit year)
            var yearWidth = trimmed.Length switch { 8 => 4, 6 => 2, _ => 0 };
            if (yearWidth == 0)
            {
                return false;
            }

            var list = new List<string>();
            var pos = 0;
            foreach (var c in order)
            {
                var width = c == 'y' ? yearWidth : 2;
                list.Add(trimmed.Substring(pos, width));
                pos += width;
            }

            parts = list.ToArray();
        }
        else
        {
            parts = Regex.Split(trimmed, @"\D+").Where(p => p.Length > 0).ToArray();
            if (parts.Length != 3 || Regex.IsMatch(trimmed, @"[^\d\s./\-,]"))
            {
                return false;
            }
        }

        int year = 0, month = 0, day = 0;
        for (var i = 0; i < 3; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                return false;
            }

            switch (order[i])
            {
                case 'y':
                    if (parts[i].Length is not (2 or 4))
                    {
                        return false;
                    }

                    year = parts[i].Length == 2 ? 2000 + n : n;
                    break;
                case 'M':
                    month = n;
                    break;
                default:
                    day = n;
                    break;
            }
        }

        // Only the limits of DateTime apply here; a business range is set with MinDate/MaxDate.
        if (year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateTime(year, month, day);
        return true;
    }
}
