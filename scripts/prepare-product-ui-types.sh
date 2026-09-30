#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
ui_types=${1:-}
if [[ -z "$ui_types" ]]; then
  ui_types=$(dotnet msbuild "$repo_root/csharp/LoadingBay.Game/LoadingBay.Game.csproj" -getProperty:RustyEngineProductUiTypes -nologo)
fi
ui_target="$repo_root/apps/loading-bay/src/rusty-engine-product-ui.generated.d.ts"
if ! cmp -s "$ui_types" "$ui_target"; then
  cp "$ui_types" "$ui_target"
fi
