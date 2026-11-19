using System.Globalization;

namespace Ursa.UITools;

/// <summary>Reusable date validation rules (<see cref="UrsaDatePicker"/>, <c>MudTextField&lt;DateTime?&gt;</c>, and so on).</summary>
public static class UrsaDateRules
{
    private static readonly UrsaLoc L = new();

    /// <summary>
    /// Returns an error message when <paramref name="date"/> is outside [<paramref name="min"/>, <paramref name="max"/>],
    /// otherwise null (an empty date is valid). The message is in the UI language and shows the limit in the short date
    /// pattern of <paramref name="culture"/> (the current culture by default).
    /// </summary>
    public static string? Validate(DateTime? date, DateTime? min = null, DateTime? max = null, CultureInfo? culture = null)
    {
        if (date is not { } value)
        {
            return null;
        }

        culture ??= CultureInfo.CurrentCulture;
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        if (min is { } lo && value.Date < lo.Date)
        {
            return L["The date must be on or after {0}", lo.ToString(pattern, culture)];
        }

        if (max is { } hi && value.Date > hi.Date)
        {
            return L["The date must be on or before {0}", hi.ToString(pattern, culture)];
        }

        return null;
    }
}
