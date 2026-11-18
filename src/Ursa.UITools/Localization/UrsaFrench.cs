namespace Ursa.UITools;

/// <summary>French translations. The key is the English text (the source language); {0}, {1}... are inserted values.</summary>
internal static class UrsaFrench
{
    public static readonly IReadOnlyDictionary<string, string> Table = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Floating dialog, date picker
        ["Close"] = "Fermer",
        ["OK"] = "OK",
        ["The date must be on or after {0}"] = "La date doit être le {0} ou après",
        ["The date must be on or before {0}"] = "La date doit être le {0} ou avant",

        // Masked field
        ["Incomplete value"] = "Valeur incomplète",
        ["Invalid value"] = "Valeur invalide",
        ["Not a valid phone number"] = "Numéro de téléphone invalide",
        ["Not a valid European phone number"] = "Numéro de téléphone européen invalide",
        ["Not a valid postal code"] = "Code postal invalide",
        ["Not a valid ZIP code"] = "Code ZIP invalide",
        ["Not a valid IPv4 address"] = "Adresse IPv4 invalide",
        ["Not a valid email address"] = "Adresse courriel invalide",
        ["Not a valid date"] = "Date invalide",
        ["Not a valid date and time"] = "Date et heure invalides",

        // Editor toolbar
        ["Undo"] = "Annuler",
        ["Redo"] = "Rétablir",
        ["Bold"] = "Gras",
        ["Italic"] = "Italique",
        ["Underline"] = "Souligné",
        ["Strikethrough"] = "Barré",
        ["Heading 1"] = "Titre 1",
        ["Heading 2"] = "Titre 2",
        ["Heading 3"] = "Titre 3",
        ["Bulleted list"] = "Liste à puces",
        ["Numbered list"] = "Liste numérotée",
        ["Quote"] = "Citation",
        ["Code block"] = "Bloc de code",
        ["Link"] = "Lien",
        ["Image"] = "Image",
        ["Clear formatting"] = "Effacer la mise en forme",

        // Editor link and image dialog
        ["Insert image"] = "Insérer une image",
        ["Image properties"] = "Propriétés de l'image",
        ["Remove link"] = "Retirer le lien",
        ["Apply"] = "Appliquer",
        ["Insert"] = "Insérer",
        ["Cancel"] = "Annuler",
        ["Link address"] = "Adresse du lien",
        ["Image address"] = "Adresse de l'image",
        ["Description (alt text)"] = "Description (texte alternatif)",
        ["Width (pixels)"] = "Largeur (pixels)",
        ["Leave empty for the original size; the height follows."] = "Laisser vide pour conserver la taille d'origine; la hauteur suit.",
        ["Upload an image"] = "Téléverser une image",
        ["Enter an address or upload an image"] = "Saisissez une adresse ou téléversez une image",
        ["The image is larger than {0} KB"] = "L'image dépasse {0} Ko",
    };
}
