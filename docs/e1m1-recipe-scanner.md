# E1M1 recipe scanner

Run `pnpm run scan:e1m1-recipes`. This offline authoring tool reads the existing intermediate export; it does not change or launch either playable scene. The source remains the retained E1M1 WAD export, not copied doom.ts implementation code.

Default output directory: `tmp/e1m1-spawn-scan`, or `tmp/e1m1-full-scan` with `--full`. Use `--out /absolute/path` to choose another directory. Outputs are local offline authoring aids and are not committed runtime dependencies.

- `measurements.generated.json` preserves sectors, connected boundary loops, raw lines, sidedef textures/offsets, flags, heights, nearby things and adjacency. Adjacent sectors outside the selected region are marked `selected: false`.
- `suggestions.generated.json` contains region, opening, height-transition and repeated-small-loop hypotheses with source IDs and explicit uncertainty.
- `plan.generated.svg` and `report.generated.md` make the measurements inspectable. White lines are one-sided boundaries; amber lines are two-sided, not necessarily passable. Polygon holes use even-odd fill. Labels are placed inside recovered regions.
- `recipe.refined.json` is seeded only if absent. Edit its decisions/features freely. Reruns overwrite the four generated files but never this draft. Compare new suggestions manually when the source changes; the recorded intermediate hash identifies the draft's original baseline.

## Scope and coordinates

The default bounds are `[480,-3700,1400,-2860]` in Doom map units. Only sectors whose entire boundary lies inside are selected. This avoids clipping a sector into a plausible but invented room; a large intersecting region may consequently be context only. The default selects sectors 14, 15, 37, 38, 39, 40 and 41. Expand or shift the bounds to inspect another area:

```sh
pnpm run scan:e1m1-recipes --bounds 480,-3700,1400,-2860 --out /tmp/e1m1-study
pnpm run scan:e1m1-recipes --input /absolute/path/e1m1.intermediate.json --scale 16
```

All measurements and draft coordinates remain in Doom units. Conversion is explicitly recorded: X = map x / scale, Z = -map y / scale, Y = height / scale, without origin shifting. Changing scale records the desired interpretation; it does not silently rescale the source coordinates. The existing authoring convention is 16 map units per Engine unit; calibrate player/architecture proportions when constructing the refined room.

## Current limits

The scanner orients edges using front/back sidedefs and follows the owning face at touching vertices. Sector 37 provides a touching-boundary case with three separate strips whose interiors must not be joined. Open, inconsistent, coincident-ray or degenerate boundaries still produce warnings instead of guessed polygons. Small-loop repetition detection is deliberately conservative; repeated square boundaries still need manual interpretation. Per-line height changes are candidate steps or ledges, not automatically a staircase. Two-sided geometry, line specials and clearance are evidence, not full dynamic-door or traversal semantics.

The scanner does not fit arbitrary regions into boxes, repair arbitrary invalid or intersecting contours, infer a complete constructive solid tree, emit C# or generate runtime meshes. Keep irregular contours and source IDs until deciding how to simplify them. Sector bounds must not be treated as solid boxes.

Validation: `pnpm run test:e1m1-recipes` checks disconnected loops/holes, ambiguous topology, real export selection and texture retention, bad references/scale, and manual-file preservation across repeat runs. No game playtest is applicable to this offline-only tool.

## Full-map scan

Run `pnpm run scan:e1m1-recipes --full`. Bounds come from the input vertices; `--full` and `--bounds` are mutually exclusive. The output uses the same generated files and editable-draft preservation rules as a bounded scan. `pnpm run test:e1m1-recipes` checks full-map boundary coverage with source endpoints and no invented bridging edges.

Manual spatial grouping and construction decisions belong in the operator's draft. Initially closed sectors still need line-special/moving-sector interpretation before a playable reconstruction; the tool neither opens those sectors nor converts them into permanent static walls automatically. Complete contours support measurement, but a reusable recipe still needs decisions about wall runs, holes, shared boundaries, materials and moving openings.

Historical scan outputs, manually grouped drafts, visual checks and construction observations are preserved in Den `[doc: rusty-doom/campaign-8976-evidence-archive]`. The optional study recipes live under `csharp/LoadingBay.RoomStudy`.
