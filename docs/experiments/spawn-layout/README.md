# Measured spawn recipe and east connector seam

The owner selected recognizable E1M1 footprints, heights and connections as the guide, with practical construction recipes taking precedence over exact decoration. This replaces the previous stylized starting hall, not the separate canonical voxel port.

[Source versus previous study plan](source-vs-study.svg) shows why the old room was difficult to recognize. The manually authored replacement follows sectors 14/15 and 37–41: original start niche and spawn position, perimeter walkway at height 0, blue rim at -0.25, recessed blue center at -0.5, ceilings at 2.25/3.75/6.25, attached eastern supports, two courtyard windows and the tapered western alcove. The invented west detour is removed. The courtyard boundary meets the windows. Small jamb notches, original texture offsets, lighting and decoration remain simplified; onward rooms retain their earlier adaptations.

Recipes use map east/north at 32 source units per world unit. `LoadingBayStudyCoordinates` maps north to world -Z consistently for fields, placements, doors, player and audits. The earlier study was mirrored. Field transformation happens through Engine before extraction; there is no negative renderer scale or downstream mesh rewrite. Historical coordinates in earlier reports predate this correction.

## Geometry evidence

The user left the player at old-world `(48.331757,0.1699984,20.651628)`, yaw 2.8169558, pitch 0.45448452. This is the east connector beside the zigzag. [The baseline](../east-connector-seam/before.json) reproduced missing ceiling/wall contact there. The recipe now extends the ceiling slab over the wall thickness, seats the wall inside it and requests 0.125 sampling for those two pieces. The new spawn ceiling is one connected stepped solid. Its walls have inset ceiling seats; the enclosure audit also caught and led to closing a 0.1-unit slit at the north portal.

Current raw reports: [overlap](overlap.json), [continuity](continuity.json). Engine pair `0.1.0-dev.e4b95f4207dc` performs all analysis. Product code supplies labels, declared contact patches and intended openings.

- 122 pieces; integrity inspected 138,141 triangles.
- Six declared join checks complete with no findings: three dogleg/door contacts, east connector ceiling/wall, spawn west ceiling/wall and floor/wall.
- Dogleg and spawn enclosures complete with no leak witness; only declared openings reported. Grid spacing 0.125; budgets 1.5 million and 5 million cells. Spawn explored 861,714 cells.
- Overlap: 21 tiny exposed classifications; summed approximate area 0.0001968611, maximum 0.0000320432. None exceeds 0.001 square units. This is not an automatic acceptance threshold or zero-conflict certificate.
- Integrity retains two non-manifold stair-underside findings. It also reports open boundary segments on the spawn perimeter floor's north diagonal and east connector shell at y=2.25 after regenerated extraction. These are retained in the raw report, not dismissed as numerical noise or assigned an extractor cause. The regional/contact checks and focused visible observations do not establish every individual piece is watertight.

Reproduce with `LOADING_BAY_STUDY_AUDIT=1 ./scripts/run-room-study.sh --debugger`, after checking port ownership. POST `loading-bay.geometry-audit` and `loading-bay.continuity-audit` as `text/plain; charset=utf-8` to `/__rusty/product/runtime/debug/execute`. They project the same cached initial closed-door snapshot separately to stay within the packaged 64 KiB debug response limit. Restart to recapture. Normal play disables capture and retains ordinary supervision deadlines.

## Native observation

Root directly drove Wolf session `2ae50dca-e6aa-46eb-94f6-89f4cab3ef57`, slot-1, using read-only pose-assisted native W/E and controller look, without teleportation or state injection. The room visibly loaded. The player descended into the blue center at `(-6.940,0.420,-9.110)`, climbed onto the perimeter at `(-15.131,0.920,-9.110)`, entered the western chamber at `(-27.179,0.670,-9.091)` and returned. The north portal led into the dogleg. The first closed door blocked at x=7.830; E opened it and movement reached `(15.138,0.920,-31.877)`.

The route continued to the owner's seam location, corrected for orientation: `(48.386,0.170,-20.625)`, yaw 0.345923, pitch 0.439807. The bright openings were absent in the directly inspected upward view. This is a close pose comparison, not an exact replay or GPU-frame correlation.

Original capture directory: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/2ae50dca-e6aa-46eb-94f6-89f4cab3ef57/`.

| View | Original PNG |
| --- | --- |
| New spawn, windows/supports on the right | `4f8fecf0-fb37-46f8-80b2-f20060ce9b69.png` |
| Inside recessed blue center | `76939989-3085-4fb4-81f3-783ec063ba0a.png` |
| Western chamber reached directly | `a3a48464-8407-4107-84fb-1232d4bff77a.png` |
| North room beyond first door | `ab283f45-29c3-488b-9460-c170972aa3f2.png` |
| Repaired east connector ceiling contact | `6b03accc-02ba-45ef-a014-b6e1ad2675e3.png` |

These originals were inspected directly. Source/build checks and native observation are separate evidence; no full E1M1 layout, combat or traversal certification is claimed.

Return traversal climbed back through the eastern connector and returned through the open north door to spawn `(-7.142,0.920,-15.953)`, without jumping. An attempted straight shortcut across the north room met its existing raised dais/walls; correcting the route through `(16,-32)` reached the doorway. The returned room overview is `ff07ea20-fdbf-4554-b5ea-ecf0b4645ae6.png` in the same original directory. Cleanup reported released=true, errors=[]. Receipts are retained in `.runtime/evidence/spawn-layout/`.

C# semantic/build/lifecycle/CoreCLR/NativeAOT checks, Angular build, boundary audit and retained-content/provenance check passed. Logs: `/tmp/spawn-spine.log`, `/tmp/spawn-shell.log`, `/tmp/spawn-boundary.log`, `/tmp/spawn-provenance-final.log`. No new texture bytes or runtime source parser were introduced.
