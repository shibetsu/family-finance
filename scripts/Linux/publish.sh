#!/usr/bin/env bash
set -euo pipefail

VERSION=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        -Version|-version|--version) VERSION="$2"; shift 2 ;;
        *) shift ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
OUT_DIR="$REPO_ROOT/release"
ARCHIVES_DIR="$REPO_ROOT/archives"
VERSION_FILE="$REPO_ROOT/version.txt"

PREV_VERSION=""
[ -f "$VERSION_FILE" ] && PREV_VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"

if [ -z "$VERSION" ]; then
    CURRENT="${PREV_VERSION:-1.0.0}"
    IFS='.' read -r major minor patch <<< "$CURRENT"
    VERSION="$major.$minor.$((patch + 1))"
fi

printf '%s' "$VERSION" > "$VERSION_FILE"
echo "Building release packages v$VERSION..."

# Move existing release packages into archives/<prev-version>/ before wiping the folder.
if [ -n "$PREV_VERSION" ] && [ -d "$OUT_DIR" ]; then
    mapfile -t PKGS < <(find "$OUT_DIR" -maxdepth 1 -type f \( -name "*.zip" -o -name "*.tar.gz" \) 2>/dev/null)
    if [ "${#PKGS[@]}" -gt 0 ]; then
        ARCHIVE_DEST="$ARCHIVES_DIR/$PREV_VERSION"
        mkdir -p "$ARCHIVE_DEST"
        for f in "${PKGS[@]}"; do
            mv "$f" "$ARCHIVE_DEST/"
        done
        echo "Archived v$PREV_VERSION packages -> archives/$PREV_VERSION"
    fi
fi

[ -d "$OUT_DIR" ] && rm -rf "$OUT_DIR"

RIDS=(
    "win-x64:false"
    "linux-x64:true"
    "linux-arm64:true"
    "osx-x64:true"
    "osx-arm64:true"
)

for t in "${RIDS[@]}"; do
    RID="${t%%:*}"
    IS_LINUX="${t##*:}"
    DEST="$OUT_DIR/$RID"

    echo ""
    echo "Publishing $RID..."

    dotnet publish "$REPO_ROOT/FinTool.Server" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        -o "$DEST"

    cp "$VERSION_FILE" "$DEST/version.txt"

    BASE="$OUT_DIR/family-finance-$VERSION-$RID"

    if [ "$IS_LINUX" = "true" ]; then
        TAR_GZ="$BASE.tar.gz"
        echo "Creating $TAR_GZ..."
        # Match PowerShell permissions: server binary 755, everything else 644/755 for dirs
        find "$DEST" -type d -exec chmod 755 {} \;
        find "$DEST" -type f -exec chmod 644 {} \;
        [ -f "$DEST/FinTool.Server" ] && chmod 755 "$DEST/FinTool.Server"
        tar -czf "$TAR_GZ" -C "$DEST" .
        echo "Done: $TAR_GZ"
    else
        ZIP="$BASE.zip"
        echo "Zipping $ZIP..."
        (cd "$DEST" && zip -r "$ZIP" . > /dev/null)
        echo "Done: $ZIP"
    fi

    rm -rf "$DEST"
done

echo ""
echo "All packages built in $OUT_DIR"
