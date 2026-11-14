using System.Globalization;

namespace Ursa.UITools.Tests;

public class UrsaDatePatternTests
{
    private static DateTimeFormatInfo Info(string dateSeparator = "/", string timeSeparator = ":")
    {
        var info = (DateTimeFormatInfo)CultureInfo.InvariantCulture.DateTimeFormat.Clone();
        info.DateSeparator = dateSeparator;
        info.TimeSeparator = timeSeparator;
        return info;
    }

    // Literals other than the date and time separators are escaped in the normalized pattern.
    [Theory]
    [InlineData("M/d/yyyy", "MM/dd/yyyy", "MM/dd/yyyy", false)]
    [InlineData("dd/MM/yyyy", "dd/MM/yyyy", "dd/MM/yyyy", true)]
    [InlineData("M/d/yyyy h:mm tt", "MM/dd/yyyy\\ hh:mm\\ tt", "MM/dd/yyyy 00:00 aa", false)]
    [InlineData("dd/MM/yyyy HH:mm", "dd/MM/yyyy\\ HH:mm", "dd/MM/yyyy 00:00", true)]
    [InlineData("d/M/yy H:mm:ss", "dd/MM/yy\\ HH:mm:ss", "dd/MM/yy 00:00:00", false)]
    public void Pads_fields_and_builds_a_mask(string pattern, string normalized, string mask, bool fixedWidth)
    {
        Assert.True(UrsaDatePattern.TryCreate(pattern, Info(), out var result));
        Assert.Equal(normalized, result!.Normalized);
        Assert.Equal(mask, result.MaskText);
        Assert.Equal(fixedWidth, result.IsFixedWidth);
    }

    [Fact]
    public void Uses_the_culture_separators_in_the_mask()
    {
        Assert.True(UrsaDatePattern.TryCreate("dd/MM/yyyy HH:mm", Info(".", "."), out var result));
        Assert.Equal("dd.MM.yyyy 00.00", result!.MaskText);
    }

    [Theory]
    [InlineData("dddd d MMMM yyyy")] // day and month names have no fixed width
    [InlineData("dd/MM/yyyy HH:mm:ss.fff")] // milliseconds
    [InlineData("dd/MM/yyyy HH:mm zzz")] // time zone
    [InlineData("dd 'de' MM yyyy")] // letters used as literals would be read as mask characters
    [InlineData("%d")]
    public void Rejects_patterns_that_cannot_be_masked(string pattern)
    {
        Assert.False(UrsaDatePattern.TryCreate(pattern, Info(), out var result));
        Assert.Null(result);
    }

    [Fact]
    public void Rejects_am_pm_when_designators_are_not_two_letters()
    {
        var info = Info();
        info.AMDesignator = "a. m.";
        info.PMDesignator = "p. m.";
        Assert.False(UrsaDatePattern.TryCreate("h:mm tt", info, out _));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("en-CA")]
    [InlineData("en-GB")]
    [InlineData("fr-CA")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("es-ES")]
    [InlineData("ja-JP")]
    public void Formatted_text_always_has_the_length_of_the_mask(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var info = culture.DateTimeFormat;
        var date = new DateTime(2026, 1, 5, 9, 7, 0);

        foreach (var pattern in new[] { info.ShortDatePattern, $"{info.ShortDatePattern} {info.ShortTimePattern}" })
        {
            if (!UrsaDatePattern.TryCreate(pattern, info, out var result))
            {
                continue; // unsupported culture patterns simply get no mask
            }

            var text = date.ToString(result!.Normalized, culture);
            Assert.Equal(result.MaskText.Length, text.Length);
            Assert.True(
                DateTime.TryParseExact(text, result.Normalized, culture, DateTimeStyles.None, out var parsed),
                $"{cultureName}: '{text}' with '{result.Normalized}'");
            Assert.Equal(pattern.Contains('H') || pattern.Contains('h') ? date : date.Date, parsed);
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void Common_cultures_get_a_mask(string cultureName)
    {
        var info = CultureInfo.GetCultureInfo(cultureName).DateTimeFormat;
        Assert.True(UrsaDatePattern.TryCreate($"{info.ShortDatePattern} {info.ShortTimePattern}", info, out _));
    }
}
