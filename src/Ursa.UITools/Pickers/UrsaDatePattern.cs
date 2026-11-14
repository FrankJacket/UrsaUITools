using System.Globalization;
using System.Text;

namespace Ursa.UITools;

/// <summary>
/// The result of analysing a .NET date/time format pattern for use with an input mask.
/// </summary>
/// <param name="Normalized">
/// The pattern with every field padded to a fixed width (<c>M/d/yyyy h:mm tt</c> becomes <c>MM/dd/yyyy hh:mm tt</c>).
/// Formatting with it always gives a string of the same length as <see cref="MaskText"/>.
/// </param>
/// <param name="MaskText">
/// A MudBlazor mask: <c>d</c>, <c>M</c> and <c>y</c> for the date parts (to be used with <c>DateMask</c>),
/// <c>0</c> for a digit and <c>a</c> for a letter, with the culture's separators as literals.
/// </param>
/// <param name="IsFixedWidth">Whether the analysed pattern was already fixed width (no padding was needed).</param>
public sealed record UrsaDatePattern(string Normalized, string MaskText, bool IsFixedWidth)
{
    /// <summary>
    /// Analyses <paramref name="pattern"/>. Returns false when it cannot be expressed as a fixed-width mask
    /// (day or month names, milliseconds, time zones, eras, letters or digits used as literals...).
    /// </summary>
    public static bool TryCreate(string pattern, DateTimeFormatInfo info, out UrsaDatePattern? result)
    {
        result = null;
        var normalized = new StringBuilder();
        var mask = new StringBuilder();
        var fixedWidth = true;

        for (var i = 0; i < pattern.Length;)
        {
            var c = pattern[i];

            if (char.IsLetter(c))
            {
                var count = 1;
                while (i + count < pattern.Length && pattern[i + count] == c)
                {
                    count++;
                }

                i += count;

                string field, maskField;
                switch (c)
                {
                    case 'd' when count <= 2:
                        field = maskField = "dd";
                        fixedWidth &= count == 2;
                        break;
                    case 'M' when count <= 2:
                        field = maskField = "MM";
                        fixedWidth &= count == 2;
                        break;
                    case 'y':
                        field = maskField = count == 2 ? "yy" : "yyyy";
                        fixedWidth &= count is 2 or 4;
                        break;
                    case 'h' when count <= 2:
                        field = "hh";
                        maskField = "00";
                        fixedWidth &= count == 2;
                        break;
                    case 'H' when count <= 2:
                        field = "HH";
                        maskField = "00";
                        fixedWidth &= count == 2;
                        break;
                    case 'm' when count <= 2:
                        field = "mm";
                        maskField = "00";
                        fixedWidth &= count == 2;
                        break;
                    case 's' when count <= 2:
                        field = "ss";
                        maskField = "00";
                        fixedWidth &= count == 2;
                        break;
                    case 't' when count <= 2 && HasTwoLetterDesignators(info):
                        field = "tt";
                        maskField = "aa";
                        fixedWidth &= count == 2;
                        break;
                    default:
                        return false;
                }

                normalized.Append(field);
                mask.Append(maskField);
                continue;
            }

            switch (c)
            {
                case '%':
                    return false;

                case '\\' when i + 1 < pattern.Length:
                    if (!AppendLiteral(pattern[i + 1].ToString(), normalized, mask))
                    {
                        return false;
                    }

                    i += 2;
                    break;

                case '\'' or '"':
                    var end = pattern.IndexOf(c, i + 1);
                    if (end < 0 || !AppendLiteral(pattern[(i + 1)..end], normalized, mask))
                    {
                        return false;
                    }

                    i = end + 1;
                    break;

                case '/':
                    if (!IsSeparator(info.DateSeparator))
                    {
                        return false;
                    }

                    normalized.Append('/');
                    mask.Append(info.DateSeparator);
                    i++;
                    break;

                case ':':
                    if (!IsSeparator(info.TimeSeparator))
                    {
                        return false;
                    }

                    normalized.Append(':');
                    mask.Append(info.TimeSeparator);
                    i++;
                    break;

                default:
                    if (!AppendLiteral(c.ToString(), normalized, mask))
                    {
                        return false;
                    }

                    i++;
                    break;
            }
        }

        result = new UrsaDatePattern(normalized.ToString(), mask.ToString(), fixedWidth);
        return true;
    }

    private static bool HasTwoLetterDesignators(DateTimeFormatInfo info) =>
        info.AMDesignator.Length == 2 && info.PMDesignator.Length == 2
        && info.AMDesignator.All(char.IsLetter) && info.PMDesignator.All(char.IsLetter);

    // Letters and digits would be read as mask characters, so they cannot be literals.
    private static bool IsSeparator(string text) => text.Length > 0 && text.All(ch => !char.IsLetterOrDigit(ch));

    private static bool AppendLiteral(string text, StringBuilder normalized, StringBuilder mask)
    {
        if (!IsSeparator(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            normalized.Append('\\').Append(ch);
        }

        mask.Append(text);
        return true;
    }
}
