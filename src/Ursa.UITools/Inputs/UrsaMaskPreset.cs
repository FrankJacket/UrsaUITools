namespace Ursa.UITools;

/// <summary>What a <see cref="UrsaMaskType"/> supplies to <see cref="UrsaMaskedField"/>.</summary>
/// <param name="Mask">The input mask.</param>
/// <param name="Pattern">The regular expression the complete value must match, or null when <paramref name="Validator"/> does the check.</param>
/// <param name="PatternError">The error shown when the value is not valid.</param>
/// <param name="UpperCase">Whether the letters typed are upper-cased.</param>
/// <param name="Validator">Extra validation of a complete value (an error message, or null when valid).</param>
/// <param name="RegexPattern">
/// A regular expression that matches partial (prefix) values as well as complete ones, used instead of
/// <see cref="Mask"/> to constrain typing in real time when the value has no fixed width (for example an IPv4
/// address, whose octets are 1 to 3 digits each). See <see cref="UrsaMaskedField.RegexPattern"/>.
/// </param>
/// <param name="Delimiters">The characters <see cref="RegexPattern"/> inserts automatically as the value is typed, for example <c>"."</c>.</param>
public sealed record UrsaMaskPreset(
    string Mask,
    string? Pattern,
    string PatternError,
    bool UpperCase = false,
    Func<string, string?>? Validator = null,
    string? RegexPattern = null,
    string? Delimiters = null);
