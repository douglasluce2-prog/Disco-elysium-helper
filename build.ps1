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
    # Work out *why* the generated files are missing, and print what we find so it can be reported.
    Write-Host "MelonLoader's generated game files (MelonLoader\Il2CppAssemblies) are missing. Investigating..." -ForegroundColor Yellow

    $isIl2Cpp = Test-Path -LiteralPath (Join-Path $GamePath "GameAssembly.dll")
    $dataDir = Get-ChildItem -LiteralPath $GamePath -Directory -Filter "*_Data" -ErrorAction SilentlyContinue | Select-Object -First 1
    $isMono = $dataDir -and (Test-Path -LiteralPath (Join-Path $dataDir.FullName "Managed\Assembly-CSharp.dll"))
    Write-Host ""
    Write-Host "Diagnostics (please copy everything below this line if you ask for help):"
    Write-Host "  Game folder:      $GamePath"
    Write-Host "  GameAssembly.dll: $isIl2Cpp   (true = IL2CPP build, which this mod supports)"
    Write-Host "  Managed\Assembly-CSharp.dll: $isMono   (true = older Mono build)"
    Write-Host "  version.dll (MelonLoader bootstrap): $(Test-Path -LiteralPath (Join-Path $GamePath 'version.dll'))"
    Write-Host "  MelonLoader folder contains: $((Get-ChildItem -LiteralPath $ml -Name -ErrorAction SilentlyContinue) -join ', ')"

    $log = @(
        (Join-Path $ml "Latest.log"),
        (Get-ChildItem -LiteralPath (Join-Path $ml "Logs") -Filter *.log -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName)
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Sort-Object { (Get-Item -LiteralPath $_).LastWriteTime } -Descending | Select-Object -First 1
    if ($log) {
        Write-Host "  Newest MelonLoader log: $log  (last written $((Get-Item -LiteralPath $log).LastWriteTime))"
        Write-Host "  ---- last 40 lines of that log ----"
        Get-Content -LiteralPath $log -Tail 40 | ForEach-Object { Write-Host "  $_" }
        Write-Host "  -----------------------------------"
    } else {
        Write-Host "  No MelonLoader log found."
    }
    Write-Host ""

    if ($isMono -and -not $isIl2Cpp) {
        Fail ("Your copy of Disco Elysium is the older 'Mono' build. MelonLoader doesn't generate these files for it,`n" +
              "and this mod currently only supports the IL2CPP build (Steam's The Final Cut). Please send the diagnostics above.")
    }
    if (-not $log) {
        $versionDll = Join-Path $GamePath "version.dll"
        if ((Test-Path -LiteralPath $versionDll) -and -not (Test-Path -LiteralPath (Join-Path $GamePath "winmm.dll"))) {
            Write-Host ("Known cause: Windows has a built-in compatibility fix for Disco Elysium that loads Windows' own version.dll`n" +
                        "first, so MelonLoader's version.dll is silently ignored. Renaming MelonLoader's file to winmm.dll fixes it.") -ForegroundColor Yellow
            $answer = Read-Host "Rename version.dll to winmm.dll in the game folder now? (y/n)"
            if ($answer -match '^[Yy]') {
                Rename-Item -LiteralPath $versionDll -NewName "winmm.dll"
                Fail ("Renamed. Now start Disco Elysium from Steam: a black MelonLoader console should open. Wait at the main menu`n" +
                      "until it stops scrolling, quit, and double-click build.bat again.`n" +
                      "(If you ever reinstall MelonLoader, delete winmm.dll and rename the new version.dll again.)")
            }
        }
        Fail ("MelonLoader never ran: there is no log file. Usually that means the game was started in a way that skipped it.`n" +
              "  - When you start the game, a black MelonLoader console window should open next to it. If it doesn't, reinstall MelonLoader`n" +
              "    with MelonLoader.Installer.exe, choosing disco.exe in: $GamePath`n" +
              "  - Antivirus software sometimes deletes MelonLoader's version.dll from the game folder; check its quarantine.`n" +
              "  - On Steam Deck / Linux, add the launch option: WINEDLLOVERRIDES=`"version=n,b`" %command%")
    }
    Fail ("MelonLoader ran but didn't finish generating the game's files. The log above usually says why`n" +
          "(often a download that was blocked by a firewall or antivirus, or the game being closed too early).`n" +
          "Start the game again and leave it at the main menu until the console window stops scrolling, then try build.bat again.`n" +
          "If it keeps failing, send the diagnostics above.")
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
