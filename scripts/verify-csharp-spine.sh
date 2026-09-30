#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
game_project="$repo_root/csharp/LoadingBay.Game/LoadingBay.Game.csproj"

node "$repo_root/scripts/generate-e1m1-semantic-catalog.mjs" --check
node "$repo_root/scripts/generate-e1m1-asset-catalog.mjs" --check
python3 "$repo_root/scripts/authoring/generate-recipe-sprites.py" --check
dotnet build "$game_project" --nologo
dotnet test "$repo_root/csharp/LoadingBay.Game.Tests/LoadingBay.Game.Tests.csproj" --nologo
rusty build --project "$game_project"
dotnet run --project "$repo_root/csharp/LoadingBay.Game.LifecycleExercise/LoadingBay.Game.LifecycleExercise.csproj" --nologo
if [[ "${1:-}" == "--aot" ]]; then
  rusty build --project "$game_project" --aot
fi
