#!/usr/bin/env bash
set -euo pipefail
# User-installed package managers must also be reachable from the Den service.
export PATH="$HOME/.npm-global/bin:$HOME/.local/share/pnpm:$PATH"
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
bash "$repo_root/scripts/prepare-product-ui-types.sh" "${1:-}"
pnpm --dir "$repo_root" exec nx build loading-bay --skip-nx-cache
