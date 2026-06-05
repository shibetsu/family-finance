#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-1.0.0}"
OUT_DIR="./release"

echo "Building release packages v$VERSION..."

rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

publish_rid() {
    local rid="$1"
    local dest="$OUT_DIR/$rid"

    echo ""
    echo "Publishing $rid..."
    dotnet publish FinTool.Server \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        -o "$dest"

    local zip="$OUT_DIR/family-finance-$VERSION-$rid.zip"
    echo "Zipping $zip..."
    (cd "$dest" && zip -r "../$(basename "$zip")" .)

    echo "Done: $zip"
}

publish_rid "win-x64"
publish_rid "linux-x64"

echo ""
echo "All packages built in $OUT_DIR"
