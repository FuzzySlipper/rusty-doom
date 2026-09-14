#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
export LOADING_BAY_SCENE=room-study
export LOADING_BAY_PORT=${LOADING_BAY_PORT:-4395}
export LOADING_BAY_LIVE_DEBUG=${LOADING_BAY_LIVE_DEBUG:-1}
exec "$repo_root/scripts/run-csharp-product.sh" "$@"
