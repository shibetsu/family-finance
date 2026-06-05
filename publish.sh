#!/usr/bin/env bash
set -euo pipefail

# Anchor all paths to the script's directory so the script works regardless of CWD.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$SCRIPT_DIR/version.txt"
OUT_DIR="$SCRIPT_DIR/release"
ARCHIVES_DIR="$SCRIPT_DIR/archives"

# Read previous version before overwriting (used for archiving).
prev_version=$(cat "$VERSION_FILE" 2>/dev/null || echo "")

# Auto-increment patch if no version supplied; explicit version updates the file.
if [ -z "${1:-}" ]; then
    current="${prev_version:-1.0.0}"
    major=$(echo "$current" | cut -d. -f1)
    minor=$(echo "$current" | cut -d. -f2)
    patch=$(echo "$current" | cut -d. -f3)
    VERSION="$major.$minor.$((patch + 1))"
else
    VERSION="$1"
fi
printf '%s' "$VERSION" > "$VERSION_FILE"

echo "Building release packages v$VERSION..."

# Move existing release packages into archives/<prev-version>/ before wiping the folder.
if [ -n "$prev_version" ] && [ -d "$OUT_DIR" ]; then
    pkgs=$(find "$OUT_DIR" -maxdepth 1 \( -name "*.zip" -o -name "*.tar.gz" \) 2>/dev/null)
    if [ -n "$pkgs" ]; then
        archive_dest="$ARCHIVES_DIR/$prev_version"
        mkdir -p "$archive_dest"
        echo "$pkgs" | xargs -I{} mv {} "$archive_dest/"
        echo "Archived v$prev_version packages → archives/$prev_version"
    fi
fi

rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

publish_rid() {
    local rid="$1"
    local linux="$2"
    local dest="$OUT_DIR/$rid"
    local base="$OUT_DIR/family-finance-$VERSION-$rid"

    echo ""
    echo "Publishing $rid..."
    dotnet publish "$SCRIPT_DIR/FinTool.Server" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        -o "$dest"
    cp "$VERSION_FILE" "$dest/version.txt"

    if [ "$linux" = "true" ]; then
        echo "Creating $base.tar.gz..."
        # chmod so the binary gets 755 in the archive; everything else stays at its current mode
        chmod +x "$dest/FinTool.Server"
        (cd "$dest" && tar czf "$base.tar.gz" .)
        echo "Done: $base.tar.gz"
    else
        echo "Zipping $base.zip..."
        (cd "$dest" && zip -r "$base.zip" .)
        echo "Done: $base.zip"
    fi

    rm -rf "$dest"
}

publish_rid "win-x64"     "false"
publish_rid "linux-x64"   "true"
publish_rid "linux-arm64" "true"
publish_rid "osx-x64"     "true"
publish_rid "osx-arm64"   "true"

echo ""
echo "All packages built in $OUT_DIR"
