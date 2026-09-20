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
