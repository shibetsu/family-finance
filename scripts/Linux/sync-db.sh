#!/usr/bin/env bash
# Downloads family-finance.db from a remote machine over SSH/SCP into ./db-backups/.
# Prompts for connection details every run, pre-filling defaults from sync-db.params.json
# (created/updated automatically) so repeat runs just need Enter to confirm.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
PARAMS_FILE="$REPO_ROOT/sync-db.params.json"
BACKUP_DIR="$REPO_ROOT/db-backups"

read_saved() {
    [ -f "$PARAMS_FILE" ] && jq -r ".$1 // empty" "$PARAMS_FILE" 2>/dev/null || true
}

read_with_default() {
    local prompt="$1" default="$2" value
    if [ -n "$default" ]; then
        read -rp "$prompt [$default]: " value
    else
        read -rp "$prompt: " value
    fi
    echo "${value:-$default}"
}

REMOTE_IP="$(read_with_default "Remote host/IP" "$(read_saved RemoteIp)")"
REMOTE_USER="$(read_with_default "Remote user" "$(read_saved RemoteUser)")"
REMOTE_DB_PATH="$(read_with_default "Remote family-finance.db path" "$(read_saved RemoteDbPath)")"

if [ -z "$REMOTE_IP" ] || [ -z "$REMOTE_USER" ] || [ -z "$REMOTE_DB_PATH" ]; then
    echo "Error: Remote host, user, and database path are all required." >&2
    exit 1
fi

jq -n \
    --arg ip "$REMOTE_IP" \
    --arg user "$REMOTE_USER" \
    --arg path "$REMOTE_DB_PATH" \
    '{RemoteIp: $ip, RemoteUser: $user, RemoteDbPath: $path}' \
    > "$PARAMS_FILE"

mkdir -p "$BACKUP_DIR"

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
DEST_FILE="$BACKUP_DIR/family-finance_${TIMESTAMP}.db"

echo "Downloading ${REMOTE_USER}@${REMOTE_IP}:${REMOTE_DB_PATH} -> $DEST_FILE"
scp "${REMOTE_USER}@${REMOTE_IP}:$REMOTE_DB_PATH" "$DEST_FILE"

echo "Saved to $DEST_FILE"
