<#
.SYNOPSIS
  Packs Ursa.UITools and pushes it to GitHub Packages.

.DESCRIPTION
  Runs the tests, packs the library in Release mode and pushes the .nupkg to the NuGet source
  named "ursa-github" (see the README for the one-time setup). The version comes from <Version>
  in src/Ursa.UITools/Ursa.UITools.csproj: bump it before each release.

.PARAMETER NoPush
  Only create the package in ./artifacts; do not push it.
#>
param(
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet test -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

Remove-Item ./artifacts -Recurse -Force -ErrorAction SilentlyContinue
dotnet pack src/Ursa.UITools/Ursa.UITools.csproj -c Release -o ./artifacts
if ($LASTEXITCODE -ne 0) { throw 'Pack failed.' }

if ($NoPush) { return }

$package = Get-ChildItem ./artifacts -Filter 'Ursa.UITools.*.nupkg' | Select-Object -First 1
dotnet nuget push $package.FullName --source ursa-github
if ($LASTEXITCODE -ne 0) { throw 'Push failed.' }
