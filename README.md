# UrsaUITools

Visual extensions for [MudBlazor](https://mudblazor.com), written in C# for Blazor (.NET 10).

## Getting started
```bash
dotnet run --project samples/Ursa.UITools.Demo
```

## Principles
Components build on MudBlazor and reuse its theme (palette, typography, dark mode) instead of defining their own styles. See [CLAUDE.md](CLAUDE.md).

## Publishing (GitHub Packages, no CI)
One-time setup on the publishing machine. Create a GitHub personal access token (classic) with the `write:packages` and `read:packages` scopes, then register it as a NuGet source (the token is stored in your user NuGet config, never in the repository):

```powershell
dotnet nuget add source https://nuget.pkg.github.com/FrankJacket/index.json --name ursa-github --username FrankJacket --password <TOKEN> --store-password-in-clear-text
```

To release: bump `<Version>` in `src/Ursa.UITools/Ursa.UITools.csproj`, then run:

```powershell
./pack.ps1            # tests, pack, push
./pack.ps1 -NoPush    # tests and pack only (output in ./artifacts)
```

A version can only be pushed once; always bump it.

## Using the package in an application
Register the same `ursa-github` source on the consuming machine (token with `read:packages` is enough), then:

```powershell
dotnet add package Ursa.UITools
```

Add `@using Ursa.UITools` to `_Imports.razor`. Scripts and styles are served automatically from `_content/Ursa.UITools/`; the app only needs MudBlazor set up as usual.
