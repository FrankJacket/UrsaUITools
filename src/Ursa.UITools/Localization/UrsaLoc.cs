using System.Globalization;

namespace Ursa.UITools;

/// <summary>
/// The texts of the components in the UI language of the current request or circuit (the browser language, or whatever
/// <c>UseRequestLocalization</c> selected). English is the source language: a text without translation stays in English,
/// which is also the fallback for any other language. Usage: <c>L["Close"]</c>, <c>L["The date must be on or after {0}", date]</c>.
/// </summary>
internal sealed class UrsaLoc
{
    /// <summary>"fr", "fr-CA", "FR" give "fr"; anything else (unknown language, empty) gives "en".</summary>
    public static string Normalize(string? language) =>
        language is not null && language.Trim().StartsWith("fr", StringComparison.OrdinalIgnoreCase) ? "fr" : "en";

    public string Lang => Normalize(CultureInfo.CurrentUICulture.Name);

    public string this[string text] => Format(text, Lang);

    public string this[string template, params object?[] args] => Format(template, Lang, args);

    /// <summary>The text in the given language ("en" or "fr"), with its arguments inserted.</summary>
    public static string Format(string template, string language, params object?[] args)
    {
        var lang = Normalize(language);
        var text = lang == "fr" && UrsaFrench.Table.TryGetValue(template, out var fr) ? fr : template;
        return args.Length == 0 ? text : string.Format(CultureInfo.GetCultureInfo(lang), text, args);
    }
}
