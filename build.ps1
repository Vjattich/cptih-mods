<#
.SYNOPSIS
    Builds both plugins into .\dist. With -Install, also copies them into the game's BepInEx\plugins.
    With -Clean, deletes all build output (dist, bin, obj) and exits.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Install
    .\build.ps1 -Clean
#>
param(
    [switch]$Install,
    [switch]$Clean,
    [string]$Configuration = "Release"
)

# Game folder: edit this if the game is installed somewhere else
$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Chicken Police Into the Hive!"

$ErrorActionPreference = "Stop"
$Projects = @("esc-skip\CP2FastMode.csproj", "mouse-controls\CP2MouseControls.csproj")

if ($Clean) {
    $targets = @(Join-Path $PSScriptRoot "dist")
    foreach ($proj in $Projects) {
        $dir = Split-Path (Join-Path $PSScriptRoot $proj)
        $targets += Join-Path $dir "bin"
        $targets += Join-Path $dir "obj"
    }
    foreach ($t in $targets) {
        if (Test-Path $t) {
            Remove-Item -Recurse -Force $t
            Write-Host "Removed $t"
        }
    }
    return
}

if (-not (Test-Path $GameDir)) {
    throw "Game folder not found: $GameDir. Edit `$GameDir at the top of build.ps1."
}
# No trailing backslash: it would escape the closing quote when passed to dotnet (the .csproj adds it back)
$GameDir = $GameDir.TrimEnd('\')

# The plugins compile against DLLs from the game's BepInEx folder, so they are needed even without -Install
if (-not (Test-Path (Join-Path $GameDir "BepInEx\interop"))) {
    throw "No BepInEx\interop in $GameDir. Install BepInEx 6 (IL2CPP) and launch the game once to generate it."
}
Write-Host "Game: $GameDir"

$dist = Join-Path $PSScriptRoot "dist"
New-Item -ItemType Directory -Force $dist | Out-Null

foreach ($proj in $Projects) {
    $path = Join-Path $PSScriptRoot $proj
    $buildArgs = @("build", $path, "-c", $Configuration, "-nologo", "-v", "minimal", "-p:GameDir=$GameDir", "-p:Install=$($Install.IsPresent.ToString().ToLower())")
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $proj" }

    $name = [IO.Path]::GetFileNameWithoutExtension($proj)
    Copy-Item (Join-Path (Split-Path $path) "bin\$Configuration\net6.0\$name.dll") $dist -Force
}

Write-Host ""
Write-Host "Built plugins: $dist"
if ($Install) { Write-Host "Installed to:  $(Join-Path $GameDir 'BepInEx\plugins')" }
