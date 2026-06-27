#!/usr/bin/env bash
# Deploys a published linux-x64 release to a remote machine running the app under systemd.
# Prompts for connection/install details every run, pre-filling defaults from
# deploy-remote.params.json (created/updated automatically) so repeat runs just need Enter.
#
# Flow: upload the archive via scp, then over a single ssh session: stop the service,
# extract the archive over the install directory, and start the service back up.
# Run ./publish.sh -Version <x.y.z> first to produce the archive this script looks for.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
PARAMS_FILE="$REPO_ROOT/deploy-remote.params.json"
RELEASE_DIR="$REPO_ROOT/release"
VERSION_FILE="$REPO_ROOT/version.txt"

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

read_yes_no() {
    local prompt="$1" default="$2" default_str value
    default_str="$([ "$default" = "true" ] && echo "y" || echo "n")"
    read -rp "$prompt (y/n) [$default_str]: " value
    value="${value:-$default_str}"
    [ "${value,,}" = "y" ] && echo "true" || echo "false"
}

LATEST_VERSION=""
[ -f "$VERSION_FILE" ] && LATEST_VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"
[ -z "$LATEST_VERSION" ] && LATEST_VERSION="$(read_saved Version)"

REMOTE_IP="$(read_with_default "Remote host/IP" "$(read_saved RemoteIp)")"
REMOTE_USER="$(read_with_default "Remote user" "$(read_saved RemoteUser)")"
REMOTE_INSTALL_DIR="$(read_with_default "Remote install directory" "$(read_saved RemoteInstallDir)")"
SERVICE_NAME="$(read_with_default "Systemd service name" "$(read_saved ServiceName)")"
USE_SUDO="$(read_yes_no "Run remote systemctl/extract commands with sudo?" "$(read_saved UseSudo || echo false)")"
VERSION="$(read_with_default "Version to deploy" "$LATEST_VERSION")"

if [ -z "$REMOTE_IP" ] || [ -z "$REMOTE_USER" ] || [ -z "$REMOTE_INSTALL_DIR" ] || [ -z "$SERVICE_NAME" ] || [ -z "$VERSION" ]; then
    echo "Error: Host, user, install directory, service name, and version are all required." >&2
    exit 1
fi

jq -n \
    --arg ip "$REMOTE_IP" \
    --arg user "$REMOTE_USER" \
    --arg dir "$REMOTE_INSTALL_DIR" \
    --arg svc "$SERVICE_NAME" \
    --argjson sudo "$([ "$USE_SUDO" = "true" ] && echo true || echo false)" \
    --arg ver "$VERSION" \
    '{RemoteIp: $ip, RemoteUser: $user, RemoteInstallDir: $dir, ServiceName: $svc, UseSudo: $sudo, Version: $ver}' \
    > "$PARAMS_FILE"

SUDO_PREFIX="$([ "$USE_SUDO" = "true" ] && echo "sudo " || echo "")"

mapfile -t PACKAGES < <(find "$RELEASE_DIR" -maxdepth 1 -name "family-finance-${VERSION}-*.tar.gz" -type f 2>/dev/null | sort)

if [ "${#PACKAGES[@]}" -eq 0 ]; then
    echo "Error: No release packages found for version $VERSION in $RELEASE_DIR" >&2
    echo "Run ./publish.sh -Version $VERSION first." >&2
    exit 1
fi

echo ""
echo "Packages available for $VERSION:"
for i in "${!PACKAGES[@]}"; do
    echo "  [$((i+1))] $(basename "${PACKAGES[$i]}")"
done
read -rp "Which package to send? [1]: " SELECTION
SELECTION="${SELECTION:-1}"
SELECTED_INDEX=$((SELECTION - 1))

if [ "$SELECTED_INDEX" -lt 0 ] || [ "$SELECTED_INDEX" -ge "${#PACKAGES[@]}" ]; then
    echo "Error: Invalid selection: $SELECTION" >&2
    exit 1
fi

ARCHIVE_PATH="${PACKAGES[$SELECTED_INDEX]}"
ARCHIVE_NAME="$(basename "$ARCHIVE_PATH")"
REMOTE_STAGING="/tmp/$ARCHIVE_NAME"

echo ""
echo "About to deploy:"
echo "  $ARCHIVE_PATH"
echo "  -> ${REMOTE_USER}@${REMOTE_IP}:$REMOTE_INSTALL_DIR (service: $SERVICE_NAME, sudo: $USE_SUDO)"
read -rp "Press Enter to continue, or Ctrl+C to abort" _

echo ""
echo "Uploading archive..."
scp "$ARCHIVE_PATH" "${REMOTE_USER}@${REMOTE_IP}:$REMOTE_STAGING"

REMOTE_COMMAND="${SUDO_PREFIX}systemctl stop $SERVICE_NAME && \
${SUDO_PREFIX}tar -xzf $REMOTE_STAGING -C $REMOTE_INSTALL_DIR && \
rm -f $REMOTE_STAGING && \
${SUDO_PREFIX}systemctl start $SERVICE_NAME && \
${SUDO_PREFIX}systemctl status $SERVICE_NAME --no-pager"

echo ""
echo "Running remote update (stop -> extract -> start)..."
# -t forces a pseudo-terminal so sudo has somewhere to prompt for a password
ssh -t "${REMOTE_USER}@${REMOTE_IP}" "$REMOTE_COMMAND"

echo ""
echo "Deployment complete."
