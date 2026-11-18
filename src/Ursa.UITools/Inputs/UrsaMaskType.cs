namespace Ursa.UITools;

/// <summary>
/// A ready-made format for <see cref="UrsaMaskedField"/>. Setting <see cref="UrsaMaskedField.MaskType"/> supplies the
/// mask, the validation pattern and its error message; each of them can still be overridden explicitly.
/// </summary>
public enum UrsaMaskType
{
    /// <summary>No preset: use <see cref="UrsaMaskedField.Mask"/> and the other parameters as given.</summary>
    None,

    /// <summary>North American phone number of 10 digits: <c>(514) 555-0123</c>. The area code and the exchange cannot start with 0 or 1.</summary>
    PhoneNorthAmerica,

    /// <summary>European phone number in international format: <c>+33 1 23 45 67 89</c> (country code of 2 digits starting with 3 or 4, then 9 digits).</summary>
    PhoneEurope,

    /// <summary>Canadian postal code: <c>H2X 1Y4</c>. Letters are upper-cased, and the letters that Canada Post never uses (D, F, I, O, Q, U) are refused.</summary>
    PostalCodeCanada,

    /// <summary>American ZIP code of 5 digits: <c>90210</c>.</summary>
    ZipCodeUS,

    /// <summary>
    /// IPv4 address: <c>192.168.1.1</c>. Each octet is 1 to 3 digits, so it has no fixed width: typing is
    /// constrained in real time by <see cref="UrsaMaskedField.RegexPattern"/> (which also inserts the <c>.</c>
    /// separators) instead of a fixed <see cref="UrsaMaskedField.Mask"/>.
    /// </summary>
    Ipv4,

    /// <summary>
    /// Email address: <c>name@example.com</c>. An address has no fixed width, so there is no input mask: the field is a plain
    /// email field (email keyboard on mobile) checked when it is committed. Surrounding spaces are trimmed.
    /// </summary>
    Email,

    /// <summary>Date in the short format of <see cref="UrsaMaskedField.Culture"/> (the current culture by default). The date must exist.</summary>
    Date,

    /// <summary>Date and time in the short formats of <see cref="UrsaMaskedField.Culture"/> (the current culture by default). The date and time must exist.</summary>
    DateTime,
}
