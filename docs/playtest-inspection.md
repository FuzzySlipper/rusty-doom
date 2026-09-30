# Adaptive Doom playtesting

Doom consumes Engine time/drawing/observer controls and publishes product facts
through `PlaytestDebugModule`. Use crew-services `playtest assist SESSION` to
discover actions and telemetry; no Jev configuration or gameplay script is needed.

Actions: forward/back/left/right, use, attack, jump, fist/pistol/shotgun. Attack timing
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

## Local geometry and traversal

`assist grid` samples retained static collision plus current door collider bounds;
live enemies and pickups are not included in this occupancy view. It returns
XYZ cell coordinates as Y slices of packed X rows along Z. The default 9×9×9
volume uses 0.25-world-unit cells centered on player feet. Choose a larger radius
or finer cell size when inspecting a bump. This is an explicit read-only query,
so agents can inspect it while simulation is held.

`assist probe` adds ankle/step/head/floor rays and the latest character-controller
receipt: grounded state, blocked axes, contact normal/source and step attempt.
`assist interaction` supplies each door's signed yaw/pitch adjustment and current
refusal. Door focus points clamp the eye position to the door surface.

`assist jump-plan` takes world XYZ **feet** coordinates and estimates a bounded
jump from live tuning. `assist jump` executes ordinary jump and forward controls,
then reports the observed endpoint and grounded state. It does not teleport or
guarantee a landing. The compact observation also includes `player.lastDamage`
(source, health/armor lost and simulation time), distinguishing hazards, enemy
hitscan and enemy projectiles.

## Development artifacts

The Engine pair is pinned in `Directory.Build.props` and installed with the
Engine `rusty` CLI (`rusty install`; `rusty status` shows the pin and paths).
`scripts/run-csharp-product.sh` runs it with `rusty dev`. Build the UI normally
and use `LOADING_BAY_LIVE_DEBUG=1` when running the product for these tools.

A browser reconnect retains the shared native game. Restart this owned product
host for a fresh world; do not infer a reset from a new playtest session. Other
sessions on the same host share mode, game state and player controls. The
runtime renders the world, so its observer camera, drawing mode and held time
are runtime state too: every page attached to the host sees them.

`assist clearance` takes a nearby world XYZ target feet position. It inspects the
actual standing capsule against retained level geometry and current doors,
returning current/target overlaps, straight sweep contact, and support below the
target. Enemies/pickups are outside these collider inputs. The query does not
advance time or predict the controller's full step/jump maneuver. Use contact
source/normal and the actual post-action movement receipt together when diagnosing
an edge or selecting a recovery direction.
