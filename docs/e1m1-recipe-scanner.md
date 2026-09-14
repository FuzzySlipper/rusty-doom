# E1M1 recipe scanner

Run `pnpm run scan:e1m1-recipes`. This offline authoring tool reads the existing intermediate export; it does not change or launch either playable scene. The source remains the retained E1M1 WAD export, not copied doom.ts implementation code.

Default output: [spawn-area report](experiments/e1m1-spawn-scan/report.generated.md), [SVG plan](experiments/e1m1-spawn-scan/plan.generated.svg), and [editable draft](experiments/e1m1-spawn-scan/recipe.refined.json).

- `measurements.generated.json` preserves sectors, connected boundary loops, raw lines, sidedef textures/offsets, flags, heights, nearby things and adjacency. Adjacent sectors outside the selected region are marked `selected: false`.
- `suggestions.generated.json` contains region, opening, height-transition and repeated-small-loop hypotheses with source IDs and explicit uncertainty.
- `plan.generated.svg` and `report.generated.md` make the measurements inspectable. White lines are one-sided boundaries; amber lines are two-sided, not necessarily passable. Polygon holes use even-odd fill. Labels are placed inside recovered regions.
- `recipe.refined.json` is seeded only if absent. Edit its decisions/features freely. Reruns overwrite the four generated files but never this draft. Compare new suggestions manually when the source changes; the recorded intermediate hash identifies the draft's original baseline.

The checked draft includes a first manual grouping: chamber shell, lowered blue inset/transition, paired narrow opening candidates, and side alcove connection. These are editable construction notes, not an executable recipe format or automatic mesh reconstruction.

## Scope and coordinates

The default bounds are `[480,-3700,1400,-2860]` in Doom map units. Only sectors whose entire boundary lies inside are selected. This avoids clipping a sector into a plausible but invented room; a large intersecting region may consequently be context only. The default selects sectors 14, 15, 37, 38, 39, 40 and 41. Expand or shift the bounds to inspect another area:

```sh
pnpm run scan:e1m1-recipes --bounds 480,-3700,1400,-2860 --out /tmp/e1m1-study
pnpm run scan:e1m1-recipes --input /absolute/path/e1m1.intermediate.json --scale 16
```

All measurements and draft coordinates remain in Doom units. Conversion is explicitly recorded: X = map x / scale, Z = -map y / scale, Y = height / scale, without origin shifting. Changing scale records the desired interpretation; it does not silently rescale the source coordinates. The existing authoring convention is 16 map units per Engine unit; calibrate player/architecture proportions when constructing the refined room.

## Current limits and evidence

Sector 37 originally exposed a touching-boundary limitation. The scanner now orients edges using front/back sidedefs and follows the owning face at touching vertices. It recovers three separate strips without joining their interiors. Open, inconsistent, coincident-ray or degenerate boundaries still produce warnings instead of guessed polygons. The original manually edited spawn draft is preserved and may still contain its older unresolved note; compare it with the refreshed generated suggestions. Small-loop repetition detection is deliberately conservative and produces no support hypothesis for this particular scan; the visible square boundaries need manual interpretation. Per-line height changes are candidate steps or ledges, not automatically a staircase. Two-sided geometry, line specials and clearance are evidence, not full dynamic-door or traversal semantics.

The scanner does not fit arbitrary regions into boxes, repair arbitrary invalid or intersecting contours, infer a complete constructive solid tree, emit C# or generate runtime meshes. Keep irregular contours and source IDs until deciding how to simplify them. Sector bounds must not be treated as solid boxes.

Validation: `pnpm run test:e1m1-recipes` checks disconnected loops/holes, ambiguous topology, real export selection and texture retention, bad references/scale, and manual-file preservation across repeat runs. The boundary and existing provenance checks also pass. The SVG was rasterized with `rsvg-convert` and inspected directly for labels, holes, bounds and spawn location. No game playtest is applicable to this offline-only change.

## Full-map assessment

Run `pnpm run scan:e1m1-recipes --full`. Bounds come from the input vertices; `--full` and `--bounds` are mutually exclusive. The separate [full-map report](experiments/e1m1-full-scan/report.generated.md) and [editable draft](experiments/e1m1-full-scan/recipe.refined.json) retain all 85 sectors and 475 linedefs. There are 232 suggestions and no unresolved boundary warnings. The overview uses compact labels and leader lines where labels would overlap.

The full draft contains seven provisional manual spatial groups covering every sector exactly once. These organize review and construction; they are not inferred gameplay rooms. Four sectors (4, 68, 76, 81) start with zero clearance and need line-special/moving-sector interpretation before a playable reconstruction. The tool neither opens those sectors nor converts them into permanent static walls automatically.

Decision for this pass: defer the full playable reconstruction, while completing full-map measurement and a first grouped authoring draft. Complete contours are sufficient for measurement, but a useful reusable recipe still needs decisions about wall runs, holes, shared boundaries, material treatment and moving openings. The next construction exercise should cover the spawn-to-north corridor connection and its initially closed sector before expanding all groups. This is a product authoring step, not a request for a generic automatic mesh-to-CSG converter.

Six focused tests now include touching lobes, rejection of unbalanced directed edges, and full-map coverage: every measured sector boundary is represented exactly once with source endpoints, and no invented bridging edge. The full SVG was rendered and inspected; the boundary/provenance checks passed. Existing playable scenes remain unchanged.


The proposed next construction exercise has now been implemented and traversed: the existing study includes the dogleg corridor, initially closed usable door and north room. See [room-study evidence](room-study.md#connected-north-wing-construction-pass). This is a manually refined subset; the remaining full-map groups are still authoring references.


The next connected subset now also includes the eastern descending connector, zigzag basin/walkway and southern door/room. See [eastern construction evidence](room-study.md#eastern-zigzag-and-southern-room). Diagonal floor joins were manually refined into a unioned surface with short slopes after native traversal exposed an uphill snag. This is an example of the measured draft supporting construction decisions rather than prescribing exact geometry.

The next manual construction pass uses sectors 78–84 for the terminal approach, a third independent study door and terminal chamber (`LoadingBayTerminalRecipe.cs`). It preserves the scanner's narrowing and floor/ceiling proportions while connecting to the already simplified southern room. Source geometry remains authoring evidence only; the runtime does not parse this draft. Exposed-surface overlap diagnostics are tracked upstream in Engine task #8255.

The western group now has a connected manual interpretation in `LoadingBayWestWingRecipe.cs`: angled chamber, paired blocks, split stair rises, upper gallery and outer hall. The spawn-side dogleg and lower-hall return stair are deliberate author refinements rather than source topology. This is another example of measured scanner suggestions becoming editable construction vocabulary; the full-map draft itself is not consumed at runtime.

The central court now has a manual sector-5/13 interpretation in `LoadingBayCourtyardRecipe.cs`, with a north-hall stair connection and recoverable recessed pool. The west edge and connector accommodate the study's stylized spawn room. Engine task #8255's published audit is now consumed while composing named recipe pieces, with analysis explicitly requested after startup; the scanner remains source-analysis tooling and does not implement geometry auditing.

The southern connecting group now has a manual interpretation in `LoadingBaySouthPassageRecipe.cs`: courtyard descent, bent low passage, eastern ascent and fourth usable door. The old eastern basin and wall are cut to admit the connection, and the landing is unioned into the existing walkway. This is a connected authoring refinement, not automatic scanner output or secret-discovery gameplay.

The first full-map grouping now has a manually authored measured-footprint starting room in `LoadingBayRoomRecipe`; see [spawn refinement](experiments/spawn-layout/README.md). Source dimensions guide practical recipes. Other provisional groups are not thereby certified source-exact. The scanner remains an offline authoring aid.
