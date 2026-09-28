# Adaptive Doom playtesting

Doom consumes Engine time/drawing/observer controls and publishes product facts
through `PlaytestDebugModule`. Use crew-services `playtest assist SESSION` to
discover actions and telemetry; no Jev configuration or gameplay script is needed.

Actions: forward/back/left/right, use, attack, fist/pistol/shotgun. Attack timing
comes from the current weapon animation, including its full recovery window.
Availability follows weapon readiness and ammunition. Input goes through the
ordinary keyboard bindings; look uses the existing Engine Look service and
product camera without moving time.

`playtest.observe` includes pose/axes/floor, health/ammo/cooldown, nearby actors
including defeated actors, door and pickup state, and interaction focus.
`combat.observe` retains detailed aim-assist diagnostics. Navigation targets and
routes retain existing authored target meaning and Engine spatial queries. Route
suggestions are finite actions for the agent to consider, never an auto follower.
Check player movement, focus and door state after each step; a static path can
still be obstructed by a closed door.

## Development artifacts

The current working tree uses SDK `0.1.0-dev.playtest-20260928a` and a matching
local development runtime under `.runtime/playtest-development-20260928g`.
This is an uncommitted development build, not a published release pair. The
runtime and SDK were built from the companion Engine checkout. `NuGet.Config`
and `scripts/run-csharp-product.sh` use that local directory; environment overrides
remain available. Build the UI normally and use `LOADING_BAY_LIVE_DEBUG=1` when
running the product for these tools. Future clean release publication can replace
both artifacts together.

A browser reconnect retains the shared native game. Restart this owned product
host for a fresh world; do not infer a reset from a new playtest session. Other
sessions on the same host share mode, game state and player controls. Inspector
camera overrides and drawing mode belong to each browser renderer.
