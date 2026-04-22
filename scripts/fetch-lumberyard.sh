#!/usr/bin/env bash
set -euo pipefail

# Fetches CryPhysics sources from aws/lumberyard into third_party/CryPhysicsNative/lumberyard-src/.
# Source: https://github.com/aws/lumberyard (Apache 2.0, pinned commit).

LY_COMMIT="${LY_COMMIT:-master}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DEST="$ROOT/lumberyard-src"
TMP="$ROOT/.lumberyard-tmp"

if [ -d "$DEST" ]; then
    echo "lumberyard-src/ already exists — delete it to re-fetch."
    exit 0
fi

echo "Cloning aws/lumberyard (sparse) at $LY_COMMIT..."
rm -rf "$TMP"
git clone --filter=blob:none --no-checkout --depth 1 --branch "$LY_COMMIT" \
    https://github.com/aws/lumberyard.git "$TMP"

cd "$TMP"
git sparse-checkout init --cone
git sparse-checkout set dev/Code/CryEngine/CryPhysics dev/Code/CryEngine/CryCommon dev/Code/Framework/AzCore
git checkout "$LY_COMMIT"

mkdir -p "$DEST"
cp -r "$TMP/dev/Code/CryEngine/CryPhysics/." "$DEST/"

mkdir -p "$ROOT/crycommon-min"
cp -r "$TMP/dev/Code/CryEngine/CryCommon/." "$ROOT/crycommon-min/"

# AzCore: preserve the stock Framework/AzCore + Platform layout. Lumberyard's
# Platform/<os>/AzCore/... headers use relative <../Common/.../...> includes,
# so we keep the shape and point CMake at both roots.
mkdir -p "$ROOT/azcore-src"
cp -r "$TMP/dev/Code/Framework/AzCore/AzCore" "$ROOT/azcore-src/"
cp -r "$TMP/dev/Code/Framework/AzCore/Platform" "$ROOT/azcore-src/"

rm -rf "$TMP"

echo "Done. Sources in $DEST, CryCommon headers in $ROOT/crycommon-min."
echo "Configure + build: cmake -B build && cmake --build build --config Release"
