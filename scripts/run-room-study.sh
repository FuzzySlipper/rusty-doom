#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
PATH="$HOME/.local/bin:$PATH" exec rusty dev \
  --project "$repo_root/csharp/LoadingBay.RoomStudy/LoadingBay.RoomStudy.csproj" \
  --bind-host 127.0.0.1 --port 4395 --live-debug "$@"
