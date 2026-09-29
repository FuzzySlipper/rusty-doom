#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
game_project="$repo_root/csharp/LoadingBay.Game/LoadingBay.Game.csproj"
live_debug_args=()

if [[ "${LOADING_BAY_LIVE_DEBUG:-0}" == "1" ]]; then
  live_debug_args=(--live-debug)
fi

if [[ ! -f "$repo_root/dist/apps/loading-bay/browser/main.js" ]]; then
  printf 'Loading Bay browser bundle is missing. Build the Angular product UI first with: pnpm run build:shell\n' >&2
  exit 1
fi

# The pinned Engine pair (Directory.Build.props) runs through the rusty CLI;
# pass --engine-source or --runtime for Engine contributor work.
PATH="$HOME/.local/bin:$PATH" exec rusty dev \
  --project "$game_project" \
  --bind-host "${LOADING_BAY_BIND_HOST:-127.0.0.1}" \
  --port "${LOADING_BAY_PORT:-4394}" \
  "${live_debug_args[@]}" \
  "$@"
