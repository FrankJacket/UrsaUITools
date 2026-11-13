namespace Ursa.UITools;

/// <summary>
/// Position and size, in CSS pixels, of a floating dialog relative to the viewport.
/// </summary>
/// <param name="X">Distance from the left edge of the viewport.</param>
/// <param name="Y">Distance from the top edge of the viewport.</param>
/// <param name="Width">Dialog width.</param>
/// <param name="Height">Dialog height.</param>
public readonly record struct UrsaDialogBounds(double X, double Y, double Width, double Height);