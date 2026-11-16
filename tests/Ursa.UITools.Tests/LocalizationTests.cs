using System.Globalization;
using System.Text.RegularExpressions;
using Ursa.UITools;

namespace Ursa.UITools.Tests;

public partial class LocalizationTests
{
    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("fr-CA", "fr")]
    [InlineData("FR", "fr")]
    [InlineData("en", "en")]
    [InlineData("de", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void An_unknown_language_falls_back_to_English(string? input, string expected) =>
        Assert.Equal(expected, UrsaLoc.Normalize(input));

    [Fact]
    public void English_and_unknown_languages_keep_the_source_text()
    {
        Assert.Equal("Close", UrsaLoc.Format("Close", "en"));
        Assert.Equal("Close", UrsaLoc.Format("Close", "de"));
        Assert.Equal("Never translated", UrsaLoc.Format("Never translated", "fr"));
    }

    [Fact]
    public void Format_inserts_values_in_the_chosen_language()
    {
        Assert.Equal("The image is larger than 500 KB", UrsaLoc.Format("The image is larger than {0} KB", "en", 500));
        Assert.Equal("L'image dépasse 500 Ko", UrsaLoc.Format("The image is larger than {0} KB", "fr", 500));
    }

    [Fact]
    public void Preset_errors_follow_the_UI_culture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Equal("Code postal invalide", UrsaMasks.ForType(UrsaMaskType.PostalCodeCanada)!.PatternError);
            Assert.Equal("Date invalide", UrsaMasks.ForType(UrsaMaskType.Date, CultureInfo.GetCultureInfo("fr-FR"))!.Validator!("31/02/2027"));

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("Not a valid postal code", UrsaMasks.ForType(UrsaMaskType.PostalCodeCanada)!.PatternError);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Every source text of the library (<c>L["..."]</c>) must have its French translation.</summary>
    [Fact]
    public void Every_source_text_has_a_French_translation()
    {
        var root = FindRoot();
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceFiles(root))
        {
            foreach (Match m in UiKey().Matches(File.ReadAllText(file)))
            {
                var key = Unescape(m.Groups[1].Value);
                if (!UrsaFrench.Table.ContainsKey(key))
                {
                    missing.Add($"{Path.GetFileName(file)}: {key}");
                }
            }
        }

        Assert.True(missing.Count == 0, "Texts without a French translation:\n" + string.Join("\n", missing));
    }

    /// <summary>A French entry that no component uses any more is dead weight.</summary>
    [Fact]
    public void Every_French_translation_is_used()
    {
        var used = SourceFiles(FindRoot())
            .SelectMany(f => UiKey().Matches(File.ReadAllText(f)).Select(m => Unescape(m.Groups[1].Value)))
            .ToHashSet(StringComparer.Ordinal);

        var unused = UrsaFrench.Table.Keys.Where(k => !used.Contains(k)).Order().ToList();
        Assert.True(unused.Count == 0, "Unused translations:\n" + string.Join("\n", unused));
    }

    [Fact]
    public void Translations_keep_the_same_placeholders_as_the_source_text()
    {
        var placeholders = new Regex(@"\{\d+\}");
        foreach (var (en, fr) in UrsaFrench.Table)
        {
            Assert.True(
                placeholders.Matches(en).Select(m => m.Value).Order().SequenceEqual(placeholders.Matches(fr).Select(m => m.Value).Order()),
                $"Different placeholders: \"{en}\" / \"{fr}\"");
        }
    }

    private static IEnumerable<string> SourceFiles(string root)
    {
        var sep = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(root, "src", "Ursa.UITools"), "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".razor"))
                        && !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}")
                        && !f.Contains($"{sep}Localization{sep}"));
    }

    private static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

    [GeneratedRegex("""\bL\["((?:[^"\\]|\\.)*)"[\],]""")]
    private static partial Regex UiKey();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UrsaUITools.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("UrsaUITools.slnx not found");
    }
}
