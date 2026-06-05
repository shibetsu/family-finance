#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-1.0.0}"
OUT_DIR="./release"

echo "Building release packages v$VERSION..."

rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

publish_rid() {
    local rid="$1"
    local linux="$2"
    local dest="$OUT_DIR/$rid"
    local base="$OUT_DIR/family-finance-$VERSION-$rid"

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

    echo "Zipping $base.zip..."
    (cd "$dest" && zip -r "$base.zip" .)
    echo "Done: $base.zip"

    if [ "$linux" = "true" ]; then
        echo "Creating $base.tar.gz..."
        # chmod so the binary gets 755 in the archive; everything else stays at its current mode
        chmod +x "$dest/FinTool.Server"
        (cd "$dest" && tar czf "$base.tar.gz" .)
        echo "Done: $base.tar.gz"
    fi
}

publish_rid "win-x64"    "false"
publish_rid "linux-x64"  "true"
publish_rid "linux-arm64" "true"

echo ""
echo "All packages built in $OUT_DIR"
