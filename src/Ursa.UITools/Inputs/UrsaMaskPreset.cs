namespace Ursa.UITools;

/// <summary>What a <see cref="UrsaMaskType"/> supplies to <see cref="UrsaMaskedField"/>.</summary>
/// <param name="Mask">The input mask.</param>
/// <param name="RegExMatch">The regular expression the complete value must match, or null when <paramref name="Validator"/> does the check.</param>
/// <param name="RegExMatchError">The error shown when the value is not valid.</param>
/// <param name="UpperCase">Whether the letters typed are upper-cased.</param>
/// <param name="Validator">Extra validation of a complete value (an error message, or null when valid).</param>
/// <param name="RegExMask">
/// A regular expression that matches partial (prefix) values as well as complete ones, used instead of
/// <see cref="Mask"/> to constrain typing in real time when the value has no fixed width (for example an IPv4
/// address, whose octets are 1 to 3 digits each). See <see cref="UrsaMaskedField.RegExMask"/>.
/// </param>
/// <param name="Delimiters">The characters <see cref="RegExMask"/> inserts automatically as the value is typed, for example <c>"."</c>.</param>
/// <param name="DecimalFormat">How a <see cref="UrsaMaskType.Decimal"/> value is grouped, separated and parsed back. See <see cref="UrsaDecimalFormat"/>.</param>
public sealed record UrsaMaskPreset(
    string Mask,
    string? RegExMatch,
    string RegExMatchError,
    bool UpperCase = false,
    Func<string, string?>? Validator = null,
    string? RegExMask = null,
    string? Delimiters = null,
    UrsaDecimalFormat? DecimalFormat = null);

/// <summary>
/// How <see cref="UrsaMaskType.Decimal"/> groups and separates digits in the browser, and how the committed text
/// is parsed and re-formatted on the server. Computed from <see cref="UrsaMaskedField.Format"/> and
/// <see cref="UrsaMaskedField.Culture"/> by <see cref="UrsaMasks.DecimalFormatOf"/>.
/// </summary>
/// <param name="NumberFormat">The standard .NET numeric format string used to parse and re-format the committed value, for example <c>N2</c>.</param>
/// <param name="DecimalSeparator">The culture's decimal separator, for example <c>","</c>.</param>
/// <param name="GroupSeparator">The culture's thousands separator, for example <c>" "</c>.</param>
/// <param name="MaxFractionDigits">The maximum number of digits accepted after the decimal separator while typing.</param>
/// <param name="CurrencySymbol">The culture's currency symbol for the <c>C</c> format, or null otherwise.</param>
/// <param name="CurrencyPrefix">Whether <paramref name="CurrencySymbol"/> is shown before the number rather than after.</param>
public sealed record UrsaDecimalFormat(
    string NumberFormat,
    string DecimalSeparator,
    string GroupSeparator,
    int MaxFractionDigits,
    string? CurrencySymbol,
    bool CurrencyPrefix);
