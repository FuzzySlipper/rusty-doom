# Eastern upper gallery

This fills the last major route gap recorded in the construction study before moving on to gameplay placement. Source sectors 62/70 and their links to 57/58 establish the elevated eastern route: upper floor 3.25, lower floor -1.5, total access rise 4.75. `LoadingBayEastGalleryRecipe` adapts that role to the existing simplified zigzag hall. Nineteen quarter-unit steps replace the source's moving access, then a bent northern gallery opens over the basin. Ceiling 5.5 matches the adjoining roof (source 5.75). This is a practical recipe, not exact source topology or lift/secret behavior.

Recipe coordinates use east/north; world Z is negative north. Lower entrance: x=66, north=-12..-8. Stair run: x=70..74, north=-8..11. Upper overlook: x≈66.5, north=17, floor=3.25. Native navigation must use the negative of those north coordinates for world Z.

The existing eastern wall owns the two subtractive reveals. A single floor solid owns the entrance, all stair treads and the gallery landing. Its edge sits within wall thickness, and the wall shell subtracts that floor volume to prevent competing exposed surfaces. The ceiling overlaps the wall thickness through an inset wall-top seat. All three new resources use Engine implicit extraction, matching collision and existing materials. No asset bytes, runtime source parsing or Engine mechanism were added.

## Audit evidence

[Before overlap](overlap-before.json), [after overlap](overlap-after.json), [after continuity](continuity-after.json).

The initial version produced substantial exposed wall/floor faces. Insetting floor edges and assigning the contact volume to the floor removed them. Final scene: 125 pieces, 202765 triangles inspected for integrity. All eight declared expected joins complete without findings. The gallery enclosure completes at requested 0.125 grid spacing with 348922 visited cells, reporting only the two declared openings. Existing dogleg/spawn enclosures still pass at their declared resolution.

The overlap report retains 23 tiny exposed findings totaling approximate area 0.0002984267, largest 0.0001014691; none exceeds 0.001 square units. This is a reported scale, not a universal acceptance threshold or zero-conflict guarantee. The previous four integrity findings remain: two open boundaries (spawn floor/east connector shell) and two non-manifold stair undersides. No new integrity findings are reported for the gallery. These audits describe initial door poses and sampled regional continuity, not complete level certification.

Use `LOADING_BAY_STUDY_AUDIT=1 ./scripts/run-room-study.sh --debugger`, then `loading-bay.geometry-audit` and `loading-bay.continuity-audit`. The latter includes `EastGalleryEnclosure`. Analysis remains opt-in; normal play disables capture.

Required source checks passed: C# semantic/build/lifecycle/CoreCLR/NativeAOT (`/tmp/gallery-spine.log`), Angular (`/tmp/gallery-shell.log`), boundary (`/tmp/gallery-boundary.log`) and provenance (`/tmp/gallery-provenance.log`).


## Native traversal

Root directly drove Wolf session `becf6100-b292-46cc-9efa-d940c27602e6`, slot-1, on the normal audit-disabled launcher. Navigation used read-only debug pose assistance and native W/E/controller-look, not teleportation. The existing route from spawn through the first door and eastern descent remained traversable.

The new lower entrance was crossed from basin `(64.092,-0.580,10.073)` to `(72.103,-0.580,10.074)`. A continuous W ascent reached `(72.098,4.170,-11.831)` without jumping. The bent gallery led to overlook `(66.375,4.170,-16.925)`. The reverse route descended all steps and returned through the entrance to `(63.942,-0.580,9.804)` without a snag or jump. The upper route was then revisited for the owner's preview. These demonstrate assisted traversal, not unaided discovery or full E1M1 certification.

Original captures inspected directly:
- entry: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/becf6100-b292-46cc-9efa-d940c27602e6/5b3c63f5-c4bc-4c59-a854-21bb8444ae73.png`
- stairs: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/becf6100-b292-46cc-9efa-d940c27602e6/e0adc90e-7b8d-4e24-ba66-7669cf45a746.png`
- overlook: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/becf6100-b292-46cc-9efa-d940c27602e6/3ade4022-58da-4059-b046-c52da910fe54.png`
- return: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/becf6100-b292-46cc-9efa-d940c27602e6/525a17d6-87be-46d6-95ae-7c248d3081d1.png`

Remaining work is primarily gameplay integration/placement and local visual refinements. The study still has no enemies, pickups, damaging floors, saves or exit-completion semantics. The four retained mesh-integrity findings remain documented; this pass does not certify exact source topology.

Session cleanup released=true/errors=[]. Native receipts are retained in `.runtime/evidence/east-gallery/`. The demo remains on the new overlook at approximately `(66.417,4.170,-17.104)`, with audit capture disabled, for the owner's inspection.
