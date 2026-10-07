<#
  Builds Disco Dictionary and installs it into your Disco Elysium "Mods" folder.
  Usually started by double-clicking build.bat. You can also run:
      powershell -ExecutionPolicy Bypass -File build.ps1 -GamePath "D:\Games\Disco Elysium"
#>
param(
    [string]$GamePath = ""
)

$ErrorActionPreference = "Stop"
Set-Location -LiteralPath $PSScriptRoot

function Write-Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host ""; Write-Host $text -ForegroundColor Red; exit 1 }

function Test-GameFolder($path) {
    return $path -and (Test-Path -LiteralPath (Join-Path $path "disco.exe"))
}

function Find-SteamLibraries {
    $roots = @()
    foreach ($key in @("HKCU:\Software\Valve\Steam", "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam", "HKLM:\SOFTWARE\Valve\Steam")) {
        try {
            $item = Get-ItemProperty -Path $key -ErrorAction Stop
            foreach ($name in @("SteamPath", "InstallPath")) {
                if ($item.$name) { $roots += ($item.$name -replace "/", "\") }
            }
        } catch { }
    }
    $roots += "${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam"

    $libraries = @()
    foreach ($root in ($roots | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $libraries += $root
        $vdf = Join-Path $root "steamapps\libraryfolders.vdf"
        if (Test-Path -LiteralPath $vdf) {
            foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libraries += ($m.Groups[1].Value -replace "\\\\", "\")
            }
        }
    }
    return $libraries | Select-Object -Unique
}

function Find-Game {
    $candidates = @()
    foreach ($lib in Find-SteamLibraries) { $candidates += (Join-Path $lib "steamapps\common\Disco Elysium") }
    $candidates += @(
        "${env:ProgramFiles(x86)}\GOG Galaxy\Games\Disco Elysium",
        "${env:ProgramFiles(x86)}\GOG Galaxy\Games\Disco Elysium The Final Cut",
        "C:\GOG Games\Disco Elysium",
        "$env:ProgramFiles\Epic Games\DiscoElysium",
        "$env:ProgramFiles\Epic Games\Disco Elysium"
    )
    foreach ($c in $candidates) { if (Test-GameFolder $c) { return $c } }
    return $null
}

Write-Host "Disco Dictionary: build and install" -ForegroundColor Yellow

# 1. .NET SDK
Write-Step "Checking for the .NET SDK"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$sdks = if ($dotnet) { & dotnet --list-sdks 2>$null } else { @() }
if (-not $sdks) {
    Fail ("The .NET SDK is not installed. Download the .NET 8 SDK (x64 installer) from`n" +
          "  https://dotnet.microsoft.com/download/dotnet/8.0`n" +
          "install it, then double-click build.bat again.")
}
Write-Host "Found: $(($sdks | Select-Object -Last 1))"

# 2. Game folder
Write-Step "Finding Disco Elysium"
if (-not $GamePath -and (Test-Path "GamePath.props")) {
    $m = [regex]::Match((Get-Content "GamePath.props" -Raw), '<GamePath>([^<]+)</GamePath>')
    if ($m.Success) { $GamePath = $m.Groups[1].Value }
}
if (-not (Test-GameFolder $GamePath)) { $GamePath = Find-Game }
while (-not (Test-GameFolder $GamePath)) {
    Write-Host "Couldn't find the game automatically."
    Write-Host "Type (or drag and drop) the folder that contains disco.exe, then press Enter."
    Write-Host "Tip: in Steam, right-click Disco Elysium > Manage > Browse local files."
    $GamePath = (Read-Host "Game folder").Trim().Trim('"')
    if (-not $GamePath) { Fail "No folder given." }
    if (-not (Test-GameFolder $GamePath)) { Write-Host "There's no disco.exe in '$GamePath'." -ForegroundColor Yellow }
}
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
Write-Host "Game: $GamePath"
$escaped = [System.Security.SecurityElement]::Escape($GamePath)
Set-Content -Path "GamePath.props" -Encoding UTF8 -Value @"
<Project>
  <!-- Written by build.ps1. Where your copy of Disco Elysium is installed. -->
  <PropertyGroup>
    <GamePath>$escaped</GamePath>
  </PropertyGroup>
</Project>
"@

# 3. MelonLoader
Write-Step "Checking MelonLoader"
$ml = Join-Path $GamePath "MelonLoader"
if (-not (Test-Path -LiteralPath (Join-Path $ml "net6\MelonLoader.dll"))) {
    Fail ("MelonLoader is not installed in the game folder yet.`n" +
          "  1. Download MelonLoader.Installer.exe from https://github.com/LavaGang/MelonLoader/releases/latest`n" +
          "  2. Run it, choose $GamePath\disco.exe, and click Install.`n" +
          "  3. Start Disco Elysium once and wait for the main menu (the first start takes a few minutes), then quit.`n" +
          "  4. Double-click build.bat again.")
}
if (-not (Test-Path -LiteralPath (Join-Path $ml "Il2CppAssemblies\Assembly-CSharp.dll"))) {
    Fail ("MelonLoader is installed, but it hasn't prepared the game's files yet.`n" +
          "Start Disco Elysium once, wait for the main menu (the first start takes a few minutes), quit, then double-click build.bat again.")
}
Write-Host "MelonLoader is ready."

# 4. Build (the project copies the DLL into <game>\Mods)
Write-Step "Building"
& dotnet build "src\DiscoDictionary.Mod\DiscoDictionary.Mod.csproj" -c Release -nologo -p:GamePath="$GamePath"
if ($LASTEXITCODE -ne 0) {
    Fail ("The build failed (see the messages above). If the game was recently updated, start it once with MelonLoader so it regenerates its files, then try again. " +
          "If it still fails, open an issue on GitHub and paste the error.")
}

Write-Host ""
Write-Host "Done! Disco Dictionary is installed in: $GamePath\Mods" -ForegroundColor Green
Write-Host "Start the game. In game: F1 = dictionary, F2 = show/hide sidebar, F3 (or middle-click) = look up the word under the mouse."
