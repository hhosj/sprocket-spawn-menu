#!/usr/bin/env bash
# Builds and packages the plugin into release/ (dll + zip + checksums).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION="0.5.6"
NAME="Sprocket.SpawnMenu"
OUT="$HERE/release"
DLL="$OUT/$NAME.dll"

"$HERE/build.sh" >/dev/null
[[ -f "$DLL" ]] || { echo "build produced no DLL" >&2; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
cp -f "$DLL" "$WORK/$NAME.dll"
rm -f "$OUT/$NAME-$VERSION.zip"
( cd "$WORK" && zip -q -X "$OUT/$NAME-$VERSION.zip" "$NAME.dll" )

( cd "$OUT" && sha256sum "$NAME-$VERSION.zip" | awk '{print $1}' > "$NAME-$VERSION.zip.sha256" )
( cd "$OUT" && sha256sum "$NAME.dll"           | awk '{print $1}' > "$NAME.dll.sha256" )
cat > "$OUT/SHA256SUMS.txt" <<SUMS
$(cat "$OUT/$NAME-$VERSION.zip.sha256")  $NAME-$VERSION.zip
$(cat "$OUT/$NAME.dll.sha256")  $NAME.dll
SUMS

echo "artifacts in release/:"
ls -la "$OUT"
echo
cat "$OUT/SHA256SUMS.txt"
