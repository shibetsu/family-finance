#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"

echo "Starting FinTool..."
echo "App will be available at http://localhost:5111"

cd "$REPO_ROOT"
dotnet run --project FinTool.Server --launch-profile http
