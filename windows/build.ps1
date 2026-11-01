<#
.SYNOPSIS
    Builds, tests, renders demo images and publishes VK03 Panel for Windows.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$SkipAssets,
    [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
function Assert-CommandSucceeded([string]$step) {
    if ($LASTEXITCODE -ne 0) { throw "$step failed (exit $LASTEXITCODE)." }
}
Push-Location -LiteralPath $root
try {
    Write-Host 'Generating icons'
    dotnet run --project tools\VK03Panel.IconGen -c $Configuration -- (Join-Path $root 'assets')
    Assert-CommandSucceeded 'Icon generation'

    dotnet build VK03Panel.sln -c $Configuration --nologo
    Assert-CommandSucceeded 'Build'

    if (-not $SkipAssets) {
        $cli = Join-Path $root "src\VK03Panel.Cli\bin\$Configuration\net9.0-windows10.0.19041.0\VK03Panel.Cli.exe"
        foreach ($theme in @('win11', 'mac', 'nebula', 'immersive')) {
            foreach ($appearance in @('dark', 'light')) {
                & $cli --demo (Join-Path $root "assets\demo-$theme-$appearance.png") --theme $theme "--$appearance"
                Assert-CommandSucceeded 'Demo rendering'
            }
        }
    }
    if (-not $SkipTests) {
        dotnet test src\VK03Panel.Core.Tests\VK03Panel.Core.Tests.csproj -c $Configuration --no-build --nologo
        Assert-CommandSucceeded 'Tests'
    }
    if (-not $SkipPublish) {
        $dist = Join-Path $root 'dist\VK03Panel'
        # Publish in place; a running application produces an error instead of deleting its files.
        dotnet publish src\VK03Panel.App\VK03Panel.App.csproj `
            -c $Configuration -r win-x64 --self-contained true `
            -p:DebugType=None -p:DebugSymbols=false -o $dist --nologo
        Assert-CommandSucceeded 'Publish'
        foreach ($name in @('README.md', 'README.en.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md')) {
            Copy-Item -LiteralPath (Join-Path $root $name) -Destination $dist -Force
        }
        Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $dist -Recurse -Force
        $assetDir = Join-Path $dist 'assets'
        New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
        Copy-Item -LiteralPath (Join-Path $root 'assets\app-icon.png') -Destination $assetDir -Force
        foreach ($pattern in @('demo-*.png', 'ui-*.png')) {
            Get-ChildItem -LiteralPath (Join-Path $root 'assets') -Filter $pattern -File |
                Copy-Item -Destination $assetDir -Force
        }
        Write-Host "Published: $dist"
    }
}
finally { Pop-Location }
