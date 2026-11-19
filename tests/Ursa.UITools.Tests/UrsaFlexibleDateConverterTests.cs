using System.Globalization;
using Ursa.UITools;

namespace Ursa.UITools.Tests;

public class UrsaFlexibleDateConverterTests
{
    private static readonly CultureInfo FrCa = CultureInfo.GetCultureInfo("fr-CA");   // yyyy-MM-dd
    private static readonly CultureInfo FrFr = CultureInfo.GetCultureInfo("fr-FR");   // dd/MM/yyyy
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");   // M/d/yyyy

    [Theory]
    [InlineData("20260102")]
    [InlineData("260102")]
    [InlineData("2026/1/2")]
    [InlineData("2026.01.02")]
    [InlineData("2026-01-02")]
    [InlineData(" 2026 1 2 ")]
    public void Year_first_cultures_read_free_form_dates(string text)
    {
        Assert.True(UrsaFlexibleDateConverter.TryParse(text, FrCa, out var date));
        Assert.Equal(new DateTime(2026, 1, 2), date);
    }

    [Theory]
    [InlineData("02012026", "fr-FR")]
    [InlineData("020126", "fr-FR")]
    [InlineData("2/1/2026", "fr-FR")]
    [InlineData("01022026", "en-US")]
    [InlineData("1/2/2026", "en-US")]
    public void The_order_of_the_culture_decides_day_and_month(string text, string culture)
    {
        Assert.True(UrsaFlexibleDateConverter.TryParse(text, CultureInfo.GetCultureInfo(culture), out var date));
        Assert.Equal(new DateTime(2026, 1, 2), date);
    }

    [Theory]
    [InlineData("20260231")]    // February 31st
    [InlineData("20261301")]    // month 13
    [InlineData("2026")]
    [InlineData("hello")]
    [InlineData("2026-01-02-03")]
    public void Dates_that_do_not_exist_or_are_not_dates_are_refused(string text) =>
        Assert.False(UrsaFlexibleDateConverter.TryParse(text, FrCa, out _));

    [Theory]
    [InlineData("202601021430", 14, 30)]
    [InlineData("2026-01-02 14:30", 14, 30)]
    [InlineData("2026-01-02 14 h 30", 14, 30)]
    [InlineData("2026-01-02 1430", 14, 30)]
    [InlineData("2026-01-02 2:30 pm", 14, 30)]
    [InlineData("2026-01-02 12:05 am", 0, 5)]
    public void The_time_is_read_after_the_date(string text, int hour, int minute)
    {
        Assert.True(UrsaFlexibleDateConverter.TryParse(text, FrCa, withTime: true, out var date));
        Assert.Equal(new DateTime(2026, 1, 2, hour, minute, 0), date);
    }

    [Theory]
    [InlineData("2026-01-02 25:00")]
    [InlineData("2026-01-02 14:61")]
    [InlineData("2026-01-02 13:00 pm")]
    public void An_impossible_time_is_refused(string text) =>
        Assert.False(UrsaFlexibleDateConverter.TryParse(text, FrCa, withTime: true, out _));

    [Fact]
    public void Convert_uses_the_culture_and_shows_the_time_only_when_asked()
    {
        var withTime = false;
        var converter = new UrsaFlexibleDateConverter(() => withTime, () => FrCa);
        var value = new DateTime(2026, 1, 2, 14, 30, 0);

        Assert.Equal("2026-01-02", converter.Convert(value));
        withTime = true;
        Assert.Equal("2026-01-02 14 h 30", converter.Convert(value));   // the fr-CA short time pattern
        Assert.Equal("", converter.Convert(null));
    }

    [Fact]
    public void ConvertBack_returns_null_for_empty_text_and_throws_a_helpful_message_for_bad_text()
    {
        var converter = new UrsaFlexibleDateConverter(culture: () => FrCa);

        Assert.Null(converter.ConvertBack("  "));
        Assert.Equal(new DateTime(2026, 1, 2), converter.ConvertBack("20260102"));

        var error = Assert.Throws<FormatException>(() => converter.ConvertBack("20260231"));
        Assert.Contains("2026-01-02", error.Message);
        Assert.Contains("20260102", error.Message);
    }

    [Fact]
    public void The_error_message_follows_the_UI_culture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
            var converter = new UrsaFlexibleDateConverter(culture: () => FrCa);
            var error = Assert.Throws<FormatException>(() => converter.ConvertBack("nope"));
            Assert.StartsWith("Date invalide", error.Message);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void DateRules_check_the_limits_and_ignore_empty_dates()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = EnUs;   // the messages follow the UI language, not the machine's
            var min = new DateTime(2026, 1, 1);
            var max = new DateTime(2026, 12, 31);

            Assert.Null(UrsaDateRules.Validate(null, min, max, EnUs));
            Assert.Null(UrsaDateRules.Validate(new DateTime(2026, 6, 1), min, max, EnUs));
            Assert.Null(UrsaDateRules.Validate(new DateTime(2026, 12, 31, 23, 59, 0), min, max, EnUs));   // the time does not matter
            Assert.Equal("The date must be on or after 1/1/2026", UrsaDateRules.Validate(new DateTime(2025, 12, 31), min, max, EnUs));
            Assert.Equal("The date must be on or before 12/31/2026", UrsaDateRules.Validate(new DateTime(2027, 1, 1), min, max, EnUs));

            CultureInfo.CurrentUICulture = FrCa;
            Assert.Equal("La date doit être le 2026-01-01 ou après", UrsaDateRules.Validate(new DateTime(2025, 12, 31), min, max, FrCa));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
