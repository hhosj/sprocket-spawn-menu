#!/usr/bin/env bash
# Builds the plugin DLL.
#
# Needs a .NET SDK (6.0 or newer). BepInEx comes from NuGet; the game types come from the
# interop assemblies BepInEx generated the first time the game ran with the loader.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$HERE/src/SprocketSpawnMenu/SprocketSpawnMenu.csproj"

if [[ -n "${DOTNET_ROOT:-}" && -x "${DOTNET_ROOT}/dotnet" ]]; then
  DOTNET="${DOTNET_ROOT}/dotnet"
elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
  DOTNET="$HOME/.dotnet/dotnet"
else
  DOTNET="dotnet"
fi

INTEROP="${INTEROP_DIR:-$HOME/.local/share/Steam/steamapps/common/Sprocket/BepInEx/interop}"
if [[ ! -d "$INTEROP" ]]; then
  echo "interop directory not found: $INTEROP" >&2
  echo "Launch the game once with BepInEx installed, or set INTEROP_DIR." >&2
  exit 1
fi

echo "Using: $("$DOTNET" --version)"
"$DOTNET" build "$PROJ" -c Release -p:InteropDir="$INTEROP" "$@"

DLL="$HERE/src/SprocketSpawnMenu/bin/Release/net6.0/Sprocket.SpawnMenu.dll"
mkdir -p "$HERE/release"
cp -f "$DLL" "$HERE/release/"
echo
echo "Built: $HERE/release/Sprocket.SpawnMenu.dll"
