#!/usr/bin/env bash
# Loading Bay verification: CoreCLR policy, UI, deterministic content and tests.
# Browser interaction and desktop packaging remain relevance-triggered checks.
set -euo pipefail

DEMO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$DEMO_ROOT"

dotnet restore csharp/LoadingBay.Game/LoadingBay.Game.csproj
pnpm run typecheck
pnpm run check:content
pnpm run test:ts
./scripts/verify-csharp-spine.sh
pnpm run audit:boundary
