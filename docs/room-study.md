# Doom-inspired spawn room construction study

Run `pnpm run build:shell`, then `bash scripts/run-room-study.sh`. The default test origin is `http://127.0.0.1:4395/`; set `LOADING_BAY_BIND_HOST=0.0.0.0` for LAN access. The ordinary launcher and Den URL continue to select the authored E1M1 port. The study is explicitly selected with `LOADING_BAY_SCENE=room-study` and uses the same packaged Engine pair.

`LoadingBayRoomRecipe` now follows the measured starting-room footprint: a low perimeter walkway, recessed blue center with quarter-unit rim, stepped ceiling, two attached supports, eastern windows and a direct western alcove. The original room plan, heights and connections guide authoring; practical recipes take precedence over exact decoration. See [the spawn comparison](experiments/spawn-layout/README.md). Other rooms remain simplified interpretations, not an exact WAD reconstruction or a full Doom campaign.

All 125 pieces are generated with Engine `ImplicitRecipe`/`RecipeWriter` and dual contouring, including the subtractive window and doorway. Engine supplies ordinary textured mesh resources, material realization, and collision from those exact meshes. The study uses nine existing WAD-derived textures from the preserved manifest: STARTAN3, FLOOR4_8, FLAT14, COMPSPAN, CEIL3_5, DOOR3, BROWN1, FLOOR5_2, and NUKAGE3. Planar UVs repeat in world units; this test does not claim original linedef texture offsets. No new asset source, baked substitute mesh, source-shaped runtime parser, downstream field evaluator, or separate renderer is introduced.

`LoadingBayRoomStudy` owns material/mesh/appearance lifetime and composes the existing FPS controller. A standing character is 1.8 units tall with a 1.62-unit eye height; the spawn floor is 0, blue rim -0.25 and blue center -0.5. Ceiling heights are 2.25 above the walkway, 3.75 over the rim and 6.25 over the center. Character step height is 0.3 for the quarter-unit rises. Collision is generated geometry, not the old E1M1 voxel volume. WASD, mouse look, Space, and the configured controller axes use the same input path as the port. There are no enemies, pickups, saves, or level-completion mechanics in this construction test.

The static study HUD identifies the scene and controls. It publishes on attachment/lifecycle publication rather than every admitted simulation step. `loading-bay.readout` remains an explicit one-shot current player/step query; Engine renderer commands provide live performance counters. The study's generic debug readout has no authored voxel catalog or gameplay tracks.

## Initial validation

On 2026-09-13, the Angular build/lint and boundary audit passed, along with the repository C# spine including lifecycle exercise and NativeAOT compilation. Direct native Wolf session `6b0dbe24-0dba-4d5b-b939-9fa9b72dd744` visibly showed the room and descended the spawn stairs: character center moved from approximately `(0,1.501,3)` to `(0.068,0.912,12.126)`. Submitted camera/readout facts support the interaction but are not GPU-frame correlation.

One native sample showed 24 draws, 2676 submitted triangles, 27 resident geometries/materials, five textures, about 59 Hz submission and 1 ms synchronous submission time at 1280×720. This is a smaller room than the full E1M1 port, not a matched whole-level performance comparison or proof that DC always improves performance.

Original spawn image: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/6b0dbe24-0dba-4d5b-b939-9fa9b72dd744/5a99d29b-b76e-448a-a1ab-2363ec8e1a7a.png`. Stair descent: same directory, `951c8e1e-4a3f-4264-a13e-812b0c66d326.png`.

Further visual tuning should compare recognizable room composition, texture repeat scale, low-ceiling impression, window treatment, and stair/portal clearance. Do not treat this initial architectural study as full E1M1 traversal certification.

The independent follow-up confirmed wall collision: strafe reached approximately x=7.73 and a further hold against the wall did not move the camera. Its bounded return attempt stopped before the stairs, so ascent and a clear window view remain unverified. Session cleanup returned `released: true`, no errors. Wall-contact original: `148ec7a6-866f-42a9-8af1-d561abc1078e.png` in the same evidence directory.

## Wall and doorway refinement

Wall repeat is now 0.2 per world unit (one STARTAN tile per five units), while floor/carpet retain their previous scale. Long side walls use a Z-facing local construction frame to keep texture V vertical. The same placements apply to both appearances and collision. Side walls also reach the raised hall ceiling. The exit portal owns the entire 0.7-unit reveal depth; passage sides and ceiling start behind it and end at the back wall, eliminating their overlapping exposed surfaces.

The packaged implicit API still uses scalar repeat and axis-dependent planar charts. Column side faces still rotate their texture relative to the front; independent U/V scale, offsets, and coherent face wrapping would benefit from an upstream UV contract. This refinement does not claim exact Doom linedef alignment.

C# spine, lifecycle exercise and NativeAOT passed again. Direct Wolf session `39332722-4d97-47f1-ae8b-01fa4fcbdbb1` used W to approach the portal, then A/D and relative mouse input to inspect both oblique views. No overlapping pattern was visible in those views. Originals under `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/39332722-4d97-47f1-ae8b-01fa4fcbdbb1/`: spawn `9e5490df-8311-4a5a-9f03-b355b72cdd30.png`, front `72874e35-f725-43fc-b4d2-55f97476161b.png`, left `b6ccb73c-be42-4599-8ab2-0e0cd023aec2.png`, right `a7aa5972-4424-4ef3-b25b-f21558713634.png`. One oblique-view renderer sample reported 20 draws and 58.3 Hz submission; view differs from the initial measurement. Mesh count remains 27. Session was stopped after inspection.


## Connected north-wing construction pass

The existing spawn room now connects through its former dead-end portal to a manually refined dogleg corridor, door vestibule and larger north room. `LoadingBayNorthWingRecipe.cs` follows the measured sector-2 bend and sectors 3/4/0 transition into sector 7. The extension maps `(mapX - 1280) / 32` to X and `20 + (mapY + 2880) / 32` to Z, with heights divided by 32. This local alignment deliberately preserves the earlier stylized spawn room; it is not the scanner's default 16-unit scale or a global exact reconstruction.

The corridor uses two authored convex footprints, combined through Engine implicit half-spaces. One shell owns the corridor's exterior walls. Portal and vestibule extents meet without duplicate exposed surfaces. The north room interprets sectors 10/12 as overhead service bays and 8/51/52 as three 0.25-unit steps. Perimeter galleries and adjoining exits are not included in this bounded room pass. Axis-dependent wall UV behavior remains visible on the multi-direction corridor shell.

`LoadingBayStudyDoor` selects simple study behavior: use E (or controller use) within 2.5 units to raise the initially closed door once. It moves 2.5 units at 1.5 units/second on admitted simulation steps and stays open. Engine CharacterObstacle supplies collision using the same translation as the retained mesh. The door is excluded from static collision instances; static surroundings still use their generated mesh resources. This is not full Doom door timing, closing, obstruction response, combat or save support. Restart resets the study and door.

The C# spine, lifecycle exercise (including door state/range/timing checks), NativeAOT, Angular build/typecheck, boundary audit and provenance checks passed. Direct native Wolf session `ef3effff-0fae-4318-a7b4-340bf485fe4e` completed the round trip with ordinary WASD, gamepad look and E:

- Closed-door forward input held the blocking X coordinate at approximately 7.83.
- After E, forward movement reached north-room center `(17.537, 0.920, 31.193)`.
- Walking up the dais without jumping reached `(30.799, 1.670, 31.620)`.
- Return movement passed back through the still-open door to `(6.150, 0.920, 31.619)`.
- Walking back up the spawn steps reached `(0.785, 1.520, 3.216)`.

Readouts support movement/height observations but are not frame-correlated GPU proof. Original captures under `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/ef3effff-0fae-4318-a7b4-340bf485fe4e/`: corridor `1d669f64-1d9b-4fb8-9428-dddeb14d60e0.png`; closed door `10e05f0f-f3fe-4da0-8aa6-e3f84902473a.png`; room entry `6a298758-7a1b-4a03-a9dc-a59daf1b04e9.png`; return view from dais `91ae153d-8622-4cec-9701-bfbf7f0285f4.png`; returned spawn `eca605ce-75e1-4267-b6d0-ba4ca711cb4d.png`. The immediate opening capture was too early to visibly establish door motion; the later open route and successful passage establish its outcome. Cleanup returned released=true with no errors.

One dais-view sample reported 19 draws, 1906 triangles, 43 resident geometries/materials, six textures and 58.55 Hz submission at 1280×720. This is a view-specific observation, not a matched benchmark. The study server was restarted at spawn afterward with its door closed.


## Eastern zigzag and southern room

`LoadingBayEastWingRecipe.cs` continues through the north room's source line-385 opening: three lower landings inspired by sectors 54/53/71, a raised zigzag inspired by 60/56, the initially closed sector-76 doorway, and a simplified southern room inspired by 73/72/74. The extension shares the north wing's 32-unit local mapping. It uses existing BROWN1, FLOOR5_2 and NUKAGE3 textures, bringing the current scene to nine textures and 68 generated meshes.

`LoadingBayRecipeShapes` is the shared product vocabulary for convex extrusions and square-capped raised walkways. The walkway is one unioned solid, with no stacked coplanar segment floors. The green basin is a lower collidable surface with no damage, fluid simulation or animation in this study. Two quarter-unit recovery steps allow walking back onto the main path. The two doors have independent named definitions, bounds, collision IDs and opening state; both use E nearby, rise once and stay open. Static collision excludes both moving meshes.

The initial direct Wolf pass exposed a snag when returning uphill across a diagonal connector join. The final recipe unions all three floor landings into one solid, adds 0.75-unit sloped lead-ins for the two internal 0.25-unit rises, and clips them to the corridor footprint. It preserves landing heights while avoiding reliance on a discrete capsule step probe at that seam. No controller tuning was changed.

### Native evidence

Initial session `b72ae03b-95f6-4448-947d-d9f1650da500` traversed from spawn through the first door, north room, descending connector and every zigzag turn. The second door remained closed (forward motion blocked near z=-15.33) until E, then allowed entry to the southern room near `(54,-0.75,-25)` at floor level and return. The player deliberately left the walkway: character center fell from y≈0.17 to y≈-0.58, then walked up the recovery steps back to y≈0.17 without jumping. This initial pass found the diagonal return snag; its full-route result is not claimed as final-seam acceptance.

Original evidence directory: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/b72ae03b-95f6-4448-947d-d9f1650da500/`:

- Basin entrance: `dc6d7c62-fbf0-4c04-be54-a24a42588643.png`.
- Southern door closed: `17a69796-a570-41db-82a1-06bcff725fb7.png`.
- Southern room entered: `cebf48e0-9292-439e-a5e0-dac794f3e412.png`.
- Basin recovery approach/top: `f857284b-a5da-456c-950f-b152610b957b.png`, `b94c30b6-98e6-43ad-a6a9-d2bfd9d1529e.png`.

Final session `32210f6d-4f50-49a3-968e-b6b00a3154c8` retested the changed connector on the final build. Downhill reached `(49.662,0.170,22.902)`; uphill without jumping passed `(44.122,0.420,27.202)`, `(39.997,0.670,28.133)` and returned to the north room at `(34.905,0.920,28.132)`. Final originals under the matching session directory: `5ad9472c-87b6-44a7-9fd4-34ab8d22ec1d.png` (descent), `a1a43c25-1bf7-42e4-8a0e-6a2dc834e1c7.png` (uphill return).

Both passes used native WASD/E and controller look. A temporary CLI driver used the supported one-shot readout to aim along author-selected waypoints; this is assisted traversal evidence, not unaided route discovery or teleported state. Large native mouse turns did not reliably match requested look in the first navigation attempt, so the driver used controller look with pose verification. Both sessions released cleanly. Renderer samples and cleanup receipts are retained in `.runtime/evidence/east-wing/`.

Final view: 54 draws, 13,506 triangles, 68 resident meshes/materials, nine textures, approximately 58.54 Hz submission and 1 ms synchronous submission at 1280×720. View differs from prior samples; this is not a matched benchmark. Final C# spine/lifecycle/two-door checks/NativeAOT, Angular build/typecheck, boundary and provenance checks passed. The server was restarted at spawn with both doors closed after testing.

### Remaining construction scope

The western rooms, central outdoor court, elevated eastern side routes and final exit sequence remain unbuilt in the study. Existing room shapes are interpretations with simplified perimeter detail; this is not complete E1M1 reconstruction or gameplay certification. Full door closing/obstruction semantics, damage, enemies, pickups and saves remain outside the construction study. Multi-axis shell UV orientation remains an Engine capability limitation described above.


## Terminal approach and chamber — 2026-09-13

`LoadingBayTerminalRecipe.cs` adds the sector-78–84-inspired narrowing approach, third independent study door and terminal chamber beyond the southern room. One unioned floor owns the new connected footprint. The existing southern end wall owns its reveal through z=-32.4; approach walls/ceiling start behind it, and the narrowing frame and chamber entry own their own reveals. Southern vestibule walls and ceiling were also shortened to meet the room entry at z=-19.6 instead of overlapping its thickness. This is a focused ownership correction, not a claim that all earlier overlaps are resolved. Engine task **#8255** tracks the reusable exposed-surface/buried-face audit requested by the owner.

The scene now has 84 generated meshes and nine existing textures. The two suspended trim housings interpret source sectors 79/83; exact source light textures and lighting are not reproduced. All three doors have distinct collision identities and independent state. No exit switch, level-completion transition, combat, damage or save semantics are added.

Direct native Wolf session `92486269-9ab0-45d0-8d59-56de6259caa9` (slot-2) traversed from spawn through the existing two doors and zigzag to the new passage. At the third closed door, W stopped at character-center z=-34.48; a second forward hold retained that Z. E followed by ordinary forward input reached `(54.027,0.170,-39.191)` inside the terminal chamber. Walking around the chamber and back through the narrow passage reached `(54.070,0.170,-32.969)`, then the southern room `(54.067,0.170,-24.858)`, without jumping. Navigation used read-only pose assistance and native W/E/controller-look, not teleportation or state injection.

Original images under `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/slots/slot-2/92486269-9ab0-45d0-8d59-56de6259caa9/`: initial spawn `cfe7b2dd-0371-4c72-9f1c-3c7a991d71f9.png`; close-up closed door `08420ecb-7ede-491e-8a30-3c1babb80261.png`; chamber interior `8d493d7f-a26e-4c86-9a80-ba851266d394.png`; returned approach facing southern room `d445e736-750f-4b63-be9a-1578b5706078.png`; open passage from the southern room `81ea07e9-e212-49af-961b-7fe9af28933e.png`. Originals were directly inspected. Session cleanup released successfully without errors.

The final approach view reported 30 draws, 3158 triangles and 58.76 Hz renderer submission at 1280×720. This is a view-specific sample, not a matched performance benchmark; another playtest slot was occupied. Full C# spine/lifecycle/NativeAOT, Angular build, boundary audit and content/provenance checks passed. Evidence receipts are in `.runtime/evidence/terminal/`. The study is restarted at spawn with all three doors closed after testing.

Remaining construction: western rooms, central outdoor court and elevated eastern side routes. Terminal architecture is now present, but Doom exit interaction/completion remains unimplemented in this construction study. Existing multi-axis UV limitations and owner-reported overlaps elsewhere remain recorded.


## Western chambers and gallery — 2026-09-13

`LoadingBayWestWingRecipe.cs` adds an angled western chamber, paired raised/suspended blocks, a quarter-unit stair sequence, upper gallery and larger lower outer hall. The chamber and heights are guided by scanner sectors 24–45. A dogleg connects the spawn hall through a new opening beside the retained window bay; its routing is adapted to the stylized spawn room. The outer chamber outline and centerpiece are simplified. A product-authored return staircase makes the lower hall recoverable. No new asset bytes, runtime source parser or Engine mechanism were introduced.

One unioned solid owns each stair flight and its landing. Portal cuts extend below the adjoining raised floor to avoid a second exposed horizontal face; gallery walls/ceiling stop at the shell-owned reveals. This is deliberate local shared-surface ownership, not a replacement for the pending Engine #8255 overlap audit. Multi-axis implicit wall shells retain the documented UV projection limitations.

Native Wolf session `32f4504e-6637-49ae-8e91-b5ee66952f8b` (slot-1) completed spawn → western chamber → upper gallery → outer hall floor → return stairs → western chamber → spawn without jumping. Read-only pose assistance guided native W/controller-look waypoints; no teleportation or state injection was used. Key character-center readouts: chamber `(-27.179,0.670,9.155)`, upper gallery `(-38.908,4.170,8.974)`, lower hall `(-55.142,0.920,9.149)`, recovered overlook `(-47.591,4.170,6.856)`, returned spawn `(0.082,1.520,2.945)`. This proves this assisted traversal, not unaided route discovery or full-map certification.

Original captures under `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/32f4504e-6637-49ae-8e91-b5ee66952f8b/`: initial spawn `77440cba-1352-45d9-87a6-26ef9a873f82.png`; chamber/stairs `8dd37ea3-fa14-412f-a62c-fc1df9a984f4.png`; gallery `c47ad387-9e70-415d-9eb1-38c5ebaee6bc.png`; outer hall floor `083eb022-87c5-4cc3-a236-a5580a171fd7.png`; recovered upper platform `1e5b7361-fe91-4392-91da-835865241ac4.png`. Originals were directly inspected. The first session start raced server startup and failed before allocation; after HTTP readiness, the successful session ran and released cleanly.

The scene now has 106 generated meshes and nine textures. One lower-hall sample reported 30 draws, 7430 triangles, 58.77 Hz submission and 2 ms synchronous submission at 1280×720. This is view-specific, not a matched benchmark or GPU-timing result. Final C# spine/lifecycle/NativeAOT, Angular build, boundary and provenance checks passed. Raw receipts and navigation helper are retained in `.runtime/evidence/west-wing/`. The study was reset at spawn after verification with all three doors closed.

Remaining major construction groups are the central outdoor court, southern connecting/secret passages and elevated eastern side routes. Existing rooms remain simplified interpretations, and exit completion/combat/damage/save semantics remain outside the construction study. Earlier owner-reported overlap concerns elsewhere remain active under Engine #8255.


## Central courtyard and Engine surface audit — 2026-09-13

The study now uses matched packaged Engine `0.1.0-dev.8c20a96d10ef` (published revision `8c20a96d10ef226376a273787a9cddb129f3305a`). The release archive SHA-256 was verified before adoption. SDK feed, package reference, normal launcher and boundary expectation agree on that pair.

`LoadingBayCourtyardRecipe.cs` adds the central open-air court, polygonal recessed pool, north-hall descent and pool recovery steps. Scanner sectors 5/13 guide the interpretation; the west boundary and connector accommodate the existing stylized spawn hall. The court is enclosed by perimeter walls with no ceiling. The product reuses its existing exact-provenance `LoadingBaySkyBackground` and admitted SKY1 through Engine CameraView. The sky remains a low-resolution Engine-projected asset; this is not exact original Doom sky mapping. Nukage remains a non-damaging collidable surface in the construction study.

### Audit adoption and corrections

Engine #8255 is landed and consumed. `LoadingBayStudyAudit` owns only deterministic piece IDs/labels and JSON projection; Engine captures copied field/mesh/placement facts and computes the report. Capture is disabled by default. With `LOADING_BAY_STUDY_AUDIT=1`, `loading-bay.geometry-audit` performs the initial-pose analysis once, releases the capture and retains the report. This is a closed-door authoring snapshot, not an audit of moving door positions.

Full-scene analysis exceeded normal supervised worker deadlines (during constructor startup and during a debug callback). The supported `--debugger` mode is therefore required for this explicit authoring operation; normal play retains standard deadlines and no audit work. Exact commands, raw before/after reports and limitations are in [the audit experiment](experiments/geometry-audit/README.md).

The baseline 106-piece audit reported 35 exposed classifications with summed approximate area 40.2627788. Trimming ceilings/walls at portal reveals, ending window-bay pieces at the window wall, shortening columns below capitals, separating the lintel/ceiling extents and removing floor/threshold overlap reduced this to 16 tiny exposed reports totaling 0.0000547838 across the expanded 116-piece scene. The largest remaining report is 0.0000160216. None exceeds 0.001 square units; these are sampled contact-seam findings, not a declaration of zero conflicts or a camera-rendering guarantee. The after report also contains 175 buried-surface classifications, which are not automatically defects.

### Native evidence

Native Wolf session `9f7f8467-77d9-4332-ac26-3e2155dd9559` (slot-2) on the normal audit-disabled launcher traversed the trimmed spawn/dogleg/first-door route and descended to courtyard center `(20.072,-0.830,17.922)`. The player walked into the pool to `(19.543,-1.580,8.823)`, moved around inside, and climbed the three recovery treads to `(19.527,-0.830,15.065)` without jumping. A perimeter circuit passed the east, south and narrow west sides. Return ascent reached north hall `(19.828,0.920,26.934)`. The route then returned through the first door and trimmed western approach to chamber `(-27.086,0.670,9.138)`. No jumps or teleportation were used; read-only pose assistance guided native W/E/controller-look inputs.

Original capture directory: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/slots/slot-2/9f7f8467-77d9-4332-ac26-3e2155dd9559/`. Entry/sky/pool view `857960c3-f9f4-4b83-ac99-ace66c9ea538.png`; inside pool `878ddd89-3208-43cd-89e1-007f79d72946.png`; recovery and stairs `873678bf-1e9b-486c-b80d-a1cb121d4c68.png`; returned north hall `40635d12-ca7d-48da-af42-957194596eb7.png`; trimmed western approach reached `be4f5a26-8cdb-41db-b010-f0b1717d1bf3.png`. Originals were directly inspected. Session cleanup released=true/errors=[]. Raw receipts are in `.runtime/evidence/courtyard/`.

The scene has 116 generated meshes, nine palette textures plus the existing SKY1 background. Renderer reported 11 resident texture resources (resource count is not a source-asset count). One north-hall view sampled 37 draws, 32,142 triangles, 57.64 Hz submission and 1 ms synchronous submission at 1280×720; another Wolf slot was occupied, and this is not a matched benchmark. Sky resource hash matched the admitted source. Final C# spine/lifecycle/NativeAOT, Angular build, boundary and provenance checks passed. The normal live study is reset at spawn with audit disabled and all three doors closed.

Remaining major authoring scope: southern connecting/secret passages and elevated eastern routes, plus refinement of simplified room outlines/details. Exit completion, damage/combat and save semantics remain outside the construction study. Automated sampled geometry diagnostics now complement visible checks; they do not replace them.


## Southern connecting passage and first-door ceiling transition

`LoadingBaySouthPassageRecipe.cs` adds a connected courtyard-to-east loop informed by sectors 16–23, 48–50, 63–68 and 77: ten quarter-unit steps descend from floor -1.75 to -4.25, a bent low passage crosses the southern group, fourteen quarter-unit rises reach floor -0.75, and a fourth independent door opens onto the eastern walkway. The courtyard wall and eastern inner wall own subtractive openings. The basin is cut around the ascent, the exit landing is unioned into the existing walkway, and adjacent walls/roofs end at existing reveal ownership. All four doors use the same Engine collision/presentation path and independent product state. Secret-discovery semantics are not added.

The first north doorway's approach roof, header and inner roof now extend to their shared top at y=4.9, closing the previously missing authored transition from the dogleg ceiling at y=4.5 to the lower vestibule. A bright ceiling/wall contact seam is still visible in the focused upward view. This pass fixes the absent transition solid; it does not establish the root cause or repair of the smaller seams. Engine #8259 owns the requested mesh-integrity/expected-join/enclosure-leak diagnostics.

The Engine audit of the 133-piece initial scene reports 17 exposed findings, summed approximate area 0.0000561654 and maximum 0.0000160216; none exceeds 0.001 square units. There are 214 buried-surface classifications. The raw report is [south-passage.json](experiments/geometry-audit/south-passage.json). As before, these sampled contact reports are not zero-defect certification or a gap audit. Capture/analysis remain opt-in and were run with `--debugger` on the separate authoring port.

Direct native Wolf session `f99d790e-6b22-4ebd-a79b-e37285410255` (slot-1) traversed from spawn through the first door and courtyard, down the southern stairs, through the bends, and up to the fourth door without jumping. Character-center heights changed from about -0.83 in the courtyard to -3.33 in the low passage and 0.17 on the eastern landing. Two forward attempts at the closed door both stopped at x=50.83. E opened it, and ordinary W reached `(54.041,0.170,-9.936)` on the eastern walkway. The complete reverse route climbed back to the courtyard at `(26.152,-0.830,-2.064)` without jumping or a collision snag. Read-only debug pose assisted navigation; movement/look/use were native input, without teleportation or state injection. This verifies the authored route, not unaided discovery.

Original images under `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/f99d790e-6b22-4ebd-a79b-e37285410255/`: descending entrance `ba5ea8fc-c971-44d5-a35c-2cb37b54c906.png`; fourth door `62d37bac-c910-4547-b3e7-218abc01f0b7.png`; eastern landing `45a65bf2-69d2-426f-b939-947a978fd163.png`; returned courtyard `47c9086a-5667-4035-810a-a485c4b53391.png`; repaired first-door transition and remaining bright seam `6c32a3bc-b270-4f67-a342-8cfb18ae14b1.png`. Originals were directly inspected. Cleanup reported `released: true`, no errors. Local launch/capture/cleanup receipts and the assisted drive helper are under `.runtime/evidence/south-passage/`.

Remaining major authoring scope is the elevated eastern side routes and refinement of simplified source outlines/details. The new lower passage connects the current court and eastern hall; source secret behavior, exit completion, damage/combat and save semantics remain outside this construction study.

Final validation passed: C# semantic catalog, build, lifecycle exercise, staged CoreCLR and NativeAOT verification; Angular build; downstream boundary and retained-content/provenance checks. Logs: `/tmp/south-spine.log`, `/tmp/south-ui.log`, `/tmp/south-boundary.log`, `/tmp/south-provenance.log`. No full E1M1 certification is claimed.

## Engine #8259 continuity diagnostics and dogleg seam repair

Updated the immutable SDK/runtime pair to `0.1.0-dev.e4b95f4207dc`, verified with its release verifier. The opt-in `loading-bay.geometry-audit` report now includes exact mesh integrity, three declared expected joins, stable labels and a regional enclosure query. Engine performs all geometry analysis; product code owns intent, opening declarations and explicit JSON projection. The latter avoids reflection/runtime code generation and passes NativeAOT analysis without new warnings. Render resources now have explicit product owners through the packaged disposable API, including study/voxel textures, sky and exit-button mesh.

The baseline detected missing floor/wall and ceiling/wall contact strips and a sampled escape path through the dogleg floor edge. Extending slabs over the wall thickness removed the floor leak but exposed a residual ceiling gap of about 0.031 units. An inset wall-top seat inside the ceiling closes that contact while preserving the visible y=4.5 ceiling and keeping the exterior/portal boundaries free of substantial exposed overlaps. The whole wall was not simply raised: that intermediate geometry produced coincident exterior faces and was rejected by the overlap audit.

[Reports and invocation](experiments/continuity-audit/README.md) preserve before, slab-contact and final results. Final: 133 pieces / 106,536 triangles inspected for integrity; all three join queries complete with no findings; regional enclosure complete with only the declared intentional opening encountered and no leak witness. Overlap analysis reports 15 tiny exposed findings totaling 0.0000532528 approximate area, maximum 0.0000160216. Two unchanged non-manifold stair-underside findings remain recorded; they are not diagnosed as visible leaks or assigned an extractor cause. These sampled checks do not certify all level joins or moving-door poses.

Direct native Wolf session `d7bdfc1f-421a-44c9-975a-3dc555691ce1` (slot-1) showed the room ready, walked from spawn to the first door, opened it with E, inspected the previously bright ceiling/wall gap, walked through into the north hall, returned and looked up along the opposite corridor direction. The bright openings visible in the prior owner/reference view were absent in these focused originals. The repaired ceiling transition remains visible. Movement passed through to `(15.038,0.920,31.999)` and back to `(2.827,0.920,31.997)` without jumping or a snag. Navigation used read-only pose assistance with ordinary native W/E/controller-look; the viewpoints are approximate comparisons, not exact replay or GPU-frame correlation.

Original images under `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/d7bdfc1f-421a-44c9-975a-3dc555691ce1/`: spawn `48a1c010-e5a6-4702-bb66-ea6422721c71.png`; doorway upward comparison `9097d397-2cee-4513-91b1-295087985f61.png`; traversed north hall `71d79bb1-62c5-4f73-98f0-398dbadbff9d.png`; reverse corridor ceiling `1c179779-519f-43dc-bdc9-4a7005fe1e72.png`. Originals were directly inspected. Session receipts are retained in `.runtime/evidence/continuity/`. An earlier attempt `da560dfb-47d8-4211-8c47-b21cc3e16776` showed only the empty shell after its temporary server supervisor exited; it supplies no gameplay evidence. Its cleanup released without errors. The successful run used the independently retained normal supervised launcher with audit disabled.

Final C# semantic/build/lifecycle/CoreCLR/NativeAOT verification, Angular build, boundary and provenance checks passed. Logs: `/tmp/seams-spine-final.log`, `/tmp/seams-ui.log`, `/tmp/seams-boundary-final.log`, `/tmp/seams-provenance-final.log`. The final explicit JSON report was also invoked through the packaged CoreCLR debug transport and matched the saved final report exactly. No full E1M1 certification is claimed.

## Measured starting-room refinement and east seam repair

The current spawn recipe now follows source sectors 14/15 and 37–41 instead of the previous raised-deck interpretation. It restores the recessed blue center, stepped ceiling, perimeter walkway, attached eastern supports/windows and direct western alcove. All study geometry, doors and audit intent now use map north as world -Z, correcting the earlier mirrored layout. Original room shapes/heights/connections guide this pass; useful recipes take precedence over exact decoration. Other rooms remain adapted interpretations.

The east connector seam identified by the owner is repaired using a slab across the wall thickness, an inset seat and finer extraction. Six expected joins and two regional enclosures complete without missing-contact or leak findings at their sampled resolution. Two open-boundary and two stair-underside non-manifold findings remain explicitly recorded; this is not whole-level watertight certification. Current scene: 122 pieces. Debug overlap and continuity output use separate commands to stay within the packaged response bound.

[Source comparison, current raw audits, limitations and native evidence](experiments/spawn-layout/README.md) document the change. Root directly walked into/out of the blue recess, through the western alcove both ways, opened the north door, visited the repaired east seam and returned to spawn using native controls with read-only pose assistance. The bright ceiling gaps were absent at the owner's corrected viewpoint. Native session cleanup released without errors. C# spine including NativeAOT, Angular, boundary and provenance checks passed.


## Eastern upper route

`LoadingBayEastGalleryRecipe` adds the remaining major recorded route: a lower entrance from the eastern basin, nineteen quarter-height stairs and a bent gallery overlooking the zigzag at source-guided floor height 3.25. The stair access and shared 5.5 ceiling are practical adaptations. Existing wall reveals and new floor/wall/roof ownership are explicit. Eight declared contact checks and all three regional enclosures pass at their stated resolution; the four prior integrity findings remain unchanged.

Native controls demonstrated ascent, overlook access and return descent without jumping. [Source decisions, raw audit reports and original evidence](experiments/east-gallery/README.md) record the 125-piece result. Required C# spine including NativeAOT, Angular, boundary and provenance checks passed. Gameplay integration and placement are now the useful next focus; this remains a construction study, not a complete Doom gameplay loop.
