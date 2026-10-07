#!/usr/bin/env bash
# Build and install Disco Dictionary from Linux / Steam Deck / macOS (needs the .NET 8 SDK).
#   ./build.sh "/path/to/steamapps/common/Disco Elysium"
# The game folder must already contain MelonLoader, and the game must have been started once with it.
set -euo pipefail
cd "$(dirname "$0")"

GAME="${1:-}"
if [ -z "$GAME" ]; then
  for c in "$HOME/.steam/steam/steamapps/common/Disco Elysium" \
           "$HOME/.local/share/Steam/steamapps/common/Disco Elysium" \
           "$HOME/Library/Application Support/Steam/steamapps/common/Disco Elysium"; do
    if [ -f "$c/disco.exe" ]; then GAME="$c"; break; fi
  done
fi
if [ -z "$GAME" ] || [ ! -d "$GAME" ]; then
  echo "Usage: $0 \"/path/to/Disco Elysium\"   (the folder containing disco.exe)" >&2
  exit 1
fi

dotnet build src/DiscoDictionary.Mod/DiscoDictionary.Mod.csproj -c Release -nologo \
  -p:GamePath="$GAME" -p:MelonLoaderDir="$GAME/MelonLoader"
echo "Installed into: $GAME/Mods"
