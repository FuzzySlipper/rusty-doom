#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
game_project="$repo_root/csharp/LoadingBay.Game/LoadingBay.Game.csproj"
# The pinned Engine pair (Directory.Build.props) runs through the rusty CLI;
# pass --engine-source or --runtime for Engine contributor work.
PATH="$HOME/.local/bin:$PATH" exec rusty dev \
  --project "$game_project" \
  --bind-host 127.0.0.1 \
  --port 4394 \
  "$@"
