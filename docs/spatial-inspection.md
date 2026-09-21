# Spatial inspection

Run the product with live debug enabled, then invoke one of these commands through
`rusty-live-debug`:

```text
spatial.map <ascii|json> <radius> <cellSize>
spatial.map-at <ascii|json> <centerX> <centerZ> <supportY> <radius> <cellSize>
```

For the normal player-centered view, use a 25 by 25 meter map at one-meter cells:

```text
spatial.map ascii 12 1
spatial.map json 12 1
```

`radius` is measured in cells and must be 0 through 15; `cellSize` must be finite
and greater than zero. `spatial.map-at` is useful while examining a door: supply its
world X/Z center and the floor support Y for the relevant room. It does not move the
player or alter gameplay state.

Both formats describe the same omniscient Engine spatial read. The map origin is the
lower X/Z corner; columns advance +X and rows advance +Z. Collision uses the active
player-body interval above the floor and below the ceiling, so floor and ceiling
surfaces do not obscure a door check. The separate navigation interval may report
unknown because the Room Study does not retain a navigation projection.

Door collision uses the current raised slab; doorway annotations stay at the passage
and report its open/closed state and raised height. Actor annotations report
health and awake state; uncollected pickups and the terminal exit are also included.
ASCII emits a legend, while JSON carries the same geometry, revisions, cells, and
annotations in structured form. Live-debug responses are bounded to 64 KiB.

## Room Study combat observation

`combat.observe` takes no arguments and returns a compact read-only JSON snapshot
for the active Room Study encounter. It includes a simulation `stamp`, keyboard
control metadata, player pose/vitals/ammunition/current weapon/readiness/kills and
the current weapon aim ray, then nearby living enemies and door state. Each enemy
includes its identity, position, health/awake state, distance, relative bearing,
aim pitch error, and line of sight. Positive bearing is right; a positive pitch
error means aim up.

The observation also reports gamepad-assist activation, selected enemy, applied
look scale and correction, plus fresh raw and assisted ray previews. These previews
remain observations: the delayed weapon discharge refreshes candidates and sends
only its returned direction through the ordinary `Spatial.CastRay`, so it cannot
hit through geometry. `interaction.help` explains the shared object-use path;
`interaction.inspect` lists current door/exit candidates and their rejection
facts, and `interaction.use <id> <revision>` invokes the same product action as
E/controller X after a fresh reach, visibility, availability, and revision check.

The command reports J/L yaw and I/K pitch at 120 degrees per second; left Shift
multiplies that rate by 0.2 (24 degrees per second); left Control fires. These are
ordinary gameplay inputs, not debug movement or firing commands.

Enemy line of sight uses the Engine's `Spatial.CastSegment` with the current door
colliders. That is the Engine's full collision query, so the result accounts for
voxels, retained static meshes, and doors just as a blocked weapon ray does.
Shared Engine Perception also includes retained static-mesh occlusion following
task #8385. Doom's direct weapon/LOS ray composition remains valid; the product
does not reinterpret geometry or synthesize visibility locally.
