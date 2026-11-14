# UrsaUITools

Blazor component library (C#, .NET 10) that **extends MudBlazor**. It does not rebuild what MudBlazor already provides.

## Structure
- `src/Ursa.UITools`: the library (Razor Class Library, depends on `MudBlazor`).
- `samples/Ursa.UITools.Demo`: Blazor Server demo app (one page per component).
- `tests/Ursa.UITools.Tests`: xUnit tests for the pure logic (run with `dotnet test`).
- `UrsaUITools.slnx`: solution. Build with `dotnet build`.

## Language
**English first**: all UI text, component default labels, tool/CLI output, exceptions, XML docs, code comments, docs and commit messages are in English. Localization (e.g. French) comes later through resource files; never hard-code non-English strings.

## Design rules
1. **Compose before creating**: an Ursa component wraps or composes MudBlazor components (`MudPaper`, `MudStack`, `MudText`, `MudButton`, etc.) or inherits `MudComponentBase` / `MudBaseInput<T>` when relevant.
2. **Consume MudBlazor styles**: no hard-coded color, font, radius, shadow or spacing.
   - Colors: `Color` enum, or CSS variables `var(--mud-palette-primary)`, `--mud-palette-surface`, `--mud-palette-text-primary`, etc.
   - Typography: `Typo` enum / `var(--mud-typography-*)`.
   - Radius, elevation, z-index: `var(--mud-default-borderradius)`, `Elevation`, `--mud-zindex-*`.
   - Spacing: utility classes `pa-*`, `ma-*`, `gap-*`.
   - Must work in light and dark mode with no specific code.
3. **Parameters aligned with MudBlazor**: reuse usual names and types (`Color`, `Variant`, `Size`, `Class`, `Style`, `UserAttributes`, `Disabled`, `Elevation`...). Expose `Class`/`Style` for customization.
4. **CSS**: isolated `.razor.css` only when a MudBlazor utility class is not enough, and then using `--mud-*` variables. Class prefix: `ursa-`.
5. Nullable enabled, warnings as errors, XML docs on the public API.
6. Every component has a demo page in `samples`.

## Naming
Components prefixed `Ursa` (e.g. `UrsaStatCard`), namespace `Ursa.UITools`.