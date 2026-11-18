using System.Globalization;
using System.Text.RegularExpressions;
using Ursa.UITools;

namespace Ursa.UITools.Tests;

public class UrsaMaskPresetTests
{
    private static bool Matches(UrsaMaskType type, string text)
    {
        var preset = UrsaMasks.ForType(type)!;
        return Regex.IsMatch(text, preset.RegExMatch!);
    }

    [Fact]
    public void None_HasNoPreset() => Assert.Null(UrsaMasks.ForType(UrsaMaskType.None));

    [Theory]
    [InlineData("(514) 555-0123", true)]
    [InlineData("(819) 234-5678", true)]
    [InlineData("(014) 555-0123", false)]   // area code cannot start with 0 or 1
    [InlineData("(514) 155-0123", false)]   // exchange cannot start with 0 or 1
    [InlineData("(514) 555-012", false)]
    [InlineData("514-555-0123", false)]
    public void PhoneNorthAmerica(string text, bool expected) => Assert.Equal(expected, Matches(UrsaMaskType.PhoneNorthAmerica, text));

    [Theory]
    [InlineData("+33 1 23 45 67 89", true)]
    [InlineData("+34 9 12 34 56 78", true)]
    [InlineData("+49 1 51 23 45 67", true)]
    [InlineData("+12 1 23 45 67 89", false)]  // not a European country code
    [InlineData("+33 1 23 45 67", false)]
    public void PhoneEurope(string text, bool expected) => Assert.Equal(expected, Matches(UrsaMaskType.PhoneEurope, text));

    [Theory]
    [InlineData("H2X 1Y4", true)]
    [InlineData("K1A 0B1", true)]
    [InlineData("V6B 4W2", true)]
    [InlineData("D2X 1Y4", false)]   // D never starts a postal code
    [InlineData("H2X 1Q4", false)]   // Q is never used
    [InlineData("H2X1Y4", false)]
    [InlineData("12345", false)]
    public void PostalCodeCanada(string text, bool expected) => Assert.Equal(expected, Matches(UrsaMaskType.PostalCodeCanada, text));

    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("first.last+tag@sub.example.com", true)]
    [InlineData("a@b", false)]
    [InlineData("a b@c.com", false)]
    [InlineData("@b.com", false)]
    [InlineData("a@b..com", false)]
    [InlineData("a@@b.com", false)]
    [InlineData("name@example.", false)]
    public void Email(string text, bool expected)
    {
        var preset = UrsaMasks.ForType(UrsaMaskType.Email)!;

        Assert.Equal("", preset.Mask);
        Assert.Equal(expected, Regex.IsMatch(text, preset.RegExMatch!));
    }

    [Theory]
    [InlineData("90210", true)]
    [InlineData("00501", true)]
    [InlineData("9021", false)]
    [InlineData("9021A", false)]
    public void ZipCodeUS(string text, bool expected) => Assert.Equal(expected, Matches(UrsaMaskType.ZipCodeUS, text));

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("256.1.1.1", false)]      // out of range
    [InlineData("192.168.1", false)]     // only 3 octets
    [InlineData("192.168.1.1.1", false)] // 5 octets
    [InlineData("192.168.1.", false)]    // trailing dot
    public void Ipv4(string text, bool expected) => Assert.Equal(expected, Matches(UrsaMaskType.Ipv4, text));

    [Fact]
    public void Ipv4_HasNoFixedMaskButHasARegExMask()
    {
        var preset = UrsaMasks.ForType(UrsaMaskType.Ipv4)!;

        Assert.Equal("", preset.Mask);
        Assert.Equal(".", preset.Delimiters);
        Assert.NotNull(preset.RegExMask);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("1", true)]
    [InlineData("192", true)]
    [InlineData("192.", true)]
    [InlineData("192.168.1.1", true)]  // a complete value is still a valid prefix
    [InlineData("256", false)]         // no octet can start this way
    [InlineData("192.256", false)]
    [InlineData("192.168.1.1.", false)] // a 5th octet cannot start
    public void Ipv4_RegExMaskAlsoMatchesPartialValues(string text, bool expected)
    {
        var preset = UrsaMasks.ForType(UrsaMaskType.Ipv4)!;

        Assert.Equal(expected, Regex.IsMatch(text, preset.RegExMask!));
    }

    [Theory]
    [InlineData("fr-CA")]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Date_MaskMatchesFormatOfCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        var preset = UrsaMasks.ForType(UrsaMaskType.Date, culture)!;
        var sample = new DateTime(2027, 3, 9).ToString(UrsaMasks.DateFormat(culture)!, culture);

        Assert.Equal(sample.Length, preset.Mask.Length);
        Assert.Null(preset.Validator!(sample));
        Assert.NotNull(preset.Validator!(sample.Replace("09", "31").Replace("03", "02")));  // not a real date in any order
    }

    [Fact]
    public void Date_RejectsImpossibleDay()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var preset = UrsaMasks.ForType(UrsaMaskType.Date, culture)!;

        Assert.Null(preset.Validator!("29/02/2028"));
        Assert.NotNull(preset.Validator!("29/02/2027"));
        Assert.NotNull(preset.Validator!("31/04/2027"));
    }

    [Fact]
    public void DateTime_ValidatesTimeToo()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var preset = UrsaMasks.ForType(UrsaMaskType.DateTime, culture)!;

        Assert.Null(preset.Validator!("09/03/2027 14:30"));
        Assert.NotNull(preset.Validator!("09/03/2027 25:30"));
        Assert.NotNull(preset.Validator!("09/03/2027 14:61"));
    }

    [Theory]
    [InlineData("fr-CA", null, 2, false)]
    [InlineData("en-US", null, 2, false)]
    [InlineData("fr-CA", "N0", 0, false)]
    [InlineData("en-US", "C2", 2, true)]
    [InlineData("fr-CA", "C2", 2, true)]
    public void Decimal_FormatOfReflectsCultureAndFormat(string cultureName, string? format, int digits, bool isCurrency)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var preset = UrsaMasks.ForType(UrsaMaskType.Decimal, culture, format)!;
        var nf = culture.NumberFormat;

        Assert.Equal("", preset.Mask);
        Assert.NotNull(preset.DecimalFormat);
        Assert.Equal(isCurrency ? nf.CurrencyDecimalSeparator : nf.NumberDecimalSeparator, preset.DecimalFormat!.DecimalSeparator);
        Assert.Equal(isCurrency ? nf.CurrencyGroupSeparator : nf.NumberGroupSeparator, preset.DecimalFormat.GroupSeparator);
        Assert.Equal(digits, preset.DecimalFormat.MaxFractionDigits);
        Assert.Equal(isCurrency ? nf.CurrencySymbol : null, preset.DecimalFormat.CurrencySymbol);
    }

    [Theory]
    [InlineData("fr-CA", "N2", "1234{0}5", "1{1}234{0}50")]
    [InlineData("fr-CA", "N2", "1{1}234{0}5", "1{1}234{0}50")]
    [InlineData("en-US", "N2", "1234.5", "1,234.50")]
    [InlineData("en-US", "N2", "1,234.5", "1,234.50")]
    [InlineData("fr-CA", "N0", "1234", "1{1}234")]
    public void NormalizeDecimal_ParsesAndReformats(string cultureName, string format, string textPattern, string expectedPattern)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var nf = culture.NumberFormat;
        var text = string.Format(textPattern, nf.NumberDecimalSeparator, nf.NumberGroupSeparator);
        var expected = string.Format(expectedPattern, nf.NumberDecimalSeparator, nf.NumberGroupSeparator);
        var decimalFormat = UrsaMasks.DecimalFormatOf(format, culture);

        Assert.Equal(expected, UrsaMasks.NormalizeDecimal(text, decimalFormat, culture));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12.34.56")]
    public void NormalizeDecimal_RejectsInvalidText(string text)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var decimalFormat = UrsaMasks.DecimalFormatOf("N2", culture);

        Assert.Null(UrsaMasks.NormalizeDecimal(text, decimalFormat, culture));
    }
}
