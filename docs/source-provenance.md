# E1M1 source provenance

Loading Bay ships one authored content closure: `content/projects/doom-e1m1.project.json` and its direct `content/doom-e1m1/` inputs. The WAD is an offline authoring source only. No WAD bytes, Doom runtime code, sound, music, story text, or trade dress are read or shipped at runtime.

## Offline E1M1 source

The forge reads the id Software shareware IWAD at `/home/research/doom.ts/public/doom1.wad`: 4,196,020 bytes, SHA-256 `1d7d43be501e67d927e415e0b8f3e29c3bf33075e859721816f652a526cac771`, `IWAD`, 1,264 lumps. E1M1 is lump 6. The checked intermediate record retains 467 vertices, 475 linedefs, and 138 Things. The WAD's license/status is not changed by this repository; do not infer distribution permission from this record.

`ts/packages/doom-e1m1-authoring` is the project-local deterministic decoder/forge. `doom.ts` is an offline reading reference only; no source file is copied from it. The mapping uses 16 Doom map units per Engine unit. The authoritative product runtime admits the result through Engine services; neither the forge nor the browser evaluates gameplay.

## Derived Doom assets

- `content/doom-e1m1/doom-e1m1.voxel.json` is the authored voxel volume (SHA-256 `59041f8a5e291b844ccc6c17ea404d5842bf2832633014b93f223a9eeea8497b`).
- `content/doom-e1m1/doom-e1m1.asset-catalog.json` is the committed Engine asset-catalog closure for the voxel scene (SHA-256 `3a5e5347b12e522538950ca085e544f00be63b50bff424860cde01b088f25643`). `scripts/generate-e1m1-asset-catalog.mjs` derives its 49 used material/texture pairs offline from the canonical project, texture manifest, and voxel sparse runs; it normalizes stored catalog hashes and preserves exact source paths. Loading Bay admits and resolves this catalog through Engine `AuthoredContent`; it never parses the source-shaped project JSON at runtime.
- `content/doom-e1m1/textures/manifest.json` closes the 54 derived wall/flat PNGs, their source WAD hash, palette hash `fd895921b5d0a394612bb29852ed003d44d69f76dec31c0dc6b5d5fc7d63f7bb`, and each output hash. `SKY1` is an authored equirectangular presentation asset; its source closure and output hashes are in that manifest.
- `content/doom-e1m1/sprites/manifest.json` closes three generated atlases, 198 source lumps, frame identities, and the WAD/palette hashes. Sprite presentation is selected from authoritative product gameplay state; atlas frames do not own combat, collision, or timing authority.
- `content/projects/doom-e1m1.project.json` is the sole canonical project (current SHA-256 `08d069726cdeaf1fddf1181eb3e75d63bad11e5262a4a5b46b4cf9a3bf5ae31b`). `pnpm run check:content` verifies canonical admission and byte-stable regeneration.

## Semantic catalog provenance

The canonical project carries the closed E1M1 item, weapon, player-setup,
pickup, enemy, encounter, hazard, explosive-prop, door, floor, lift, secret,
switch, and level-exit catalogs. Its WAD-derived entities retain placement and
calibrated gameplay values; the product owns the runtime validation, state
transitions, scheduling, facts, and readouts for those entries.

`scripts/generate-e1m1-semantic-catalog.mjs` checks the canonical project hash,
reference closure, and authored/semantic cardinality, then emits the typed
`csharp/LoadingBay.Game/E1M1SemanticCatalog.g.cs` source used by the product.
This is an offline deterministic projection of committed content, not a live
source parser or a second gameplay evaluator. Generic collision, character
motion, spatial queries, presentation, and rendering remain Engine mechanisms;
product policy and E1M1 state remain in the C# product domains.

## E1M1 prop closure

The only non-Doom static-prop sources are colocated under `content/doom-e1m1/props/`. `assets.json` retains their admitted catalogs and material dependencies (current SHA-256 `b49354d8b1442311457c8c2aa89b4647d00c7721a5bac7cb82a1d78992a15704`). `source-manifest.json` is the exact source record (current SHA-256 `6144dc360f65de8c5c1edccfc994862a54a311852d270c62b8f140988e7800b4`): it records each raw source path, hash, source dependency, bounds, material slots, and visual-only collision intent.

Three meshes derive from the retained Kenney Factory Kit 3.0 GLBs under `content/doom-e1m1/props/sources/kenney-factory-kit/`: `security-door`, `hazard-marker`, and `level-exit`. The copied Factory Kit CC0 notice is `content/doom-e1m1/props/KENNEY-FACTORY-KIT-LICENSE.txt` (SHA-256 `61e86565dd297e143ad631594980eda0a17fc81a4cd7c6d71acf2f5e0cad30b6`).

`button-floor-square.glb` is retained from the same Kenney Factory Kit source pack under that license and contains the authored `toggle-on`, `toggle-off`, and `toggle` node clips. The GLB remains byte-for-byte unchanged at SHA-256 `f32def1dd9a57939b096d64361fc5058a8ba240a0394951e8681fb7326ebdeb6`. Its exact Factory Kit 3.0 external image dependency is retained at `content/doom-e1m1/props/sources/kenney-factory-kit/Textures/colormap.png` (512×512 RGBA PNG, SHA-256 `35d7bd6900dde0208429eeaec87fa17fbf024ed59f3f4eab54bc92802eba9dd7`) under the same copied CC0 notice; `source-manifest.json` checks both hashes. At Engine `913a9e665035e6bfdf6ac613cedb62396be4f31d`, landed #7589/#7591/#7595 support the GLB's texture transform, bounded external-image closure, and degenerate visual faces. Loading Bay therefore admits this source directly through `Animation.OpenAnimatedMesh`, retains the named E1M1 `doom-exit` appearance/instance, samples `toggle-off`, and plays `toggle-on` once on the authoritative completion transition. It does not use clip-pack association, a rig, or a local evaluator. The E1M1 closure currently contains no audio clip assets and its gameplay ledger excludes sound/music, so the product retains only a typed Engine SFX bus volume/mute policy and emits no synthetic audio.

The five original low-poly meshes — `energy-cell`, `scatter-shells`, `med-patch`, `impact-vest`, and `breach-scattergun` — retain their own E1M1-local mesh JSON as canonical raw source. Their manifest records the retired historical generator identity only for traceability; no legacy prop kit or generator is required to reimport them.

All eight meshes are visual-only. Collision, navigation, triggers, pickups, doors, hazards, and exit meaning remain explicit admitted game entities. Adding or changing a shipped asset requires updating its direct source closure and this document, then running the content check.

## Optional construction study

The opt-in `room-study` scene is original product-owned C# construction code in `LoadingBayRoomRecipe.cs`, inspired by E1M1's initial-room composition. It reuses the existing manifest-closed STARTAN3, FLOOR4_8, FLAT14, COMPSPAN, CEIL3_5, DOOR3, BROWN1, FLOOR5_2, and NUKAGE3 textures without changing their bytes or source closure. Its geometry is generated by Engine implicit surfaces from the recipe; it is not a byte-faithful WAD geometry import. See `docs/room-study.md` for the boundary and intentional differences.

## Offline recipe-scanner experiment

`scripts/scan-e1m1-recipes.mjs` reads the retained `e1m1.intermediate.json` offline and writes measured plans and tentative construction suggestions under `docs/experiments/e1m1-spawn-scan/` and `docs/experiments/e1m1-full-scan/`. The outputs retain the WAD identity and intermediate SHA-256; the manually editable draft records interpretations separately. They are authoring references, not new shipped runtime content. No doom.ts source implementation or new asset bytes are imported. See `docs/e1m1-recipe-scanner.md`.


The connected north-wing study adds manually authored `LoadingBayNorthWingRecipe.cs`, guided by the full-map scanner's sectors 2/3/4/0/7 and overhead/step detail measurements. It retains no runtime dependency on scanner JSON. DOOR3 uses the already closed texture manifest. Local extension coordinates use 32 Doom units per Engine unit and an explicit origin alignment to the stylized spawn room; this does not change the authored E1M1 port's source scale. See `docs/room-study.md` for the simplifications and native traversal evidence.


`LoadingBayEastWingRecipe.cs` manually refines the measured eastern connector, zigzag and southern-room groups, using the same local 32-unit mapping as the north wing. BROWN1, FLOOR5_2 and NUKAGE3 are existing manifest-closed textures; no bytes or asset closure changed. The basin, walkway, recovery steps and independent second door are original construction-policy interpretations. The scanner remains offline; runtime construction reads only typed C# recipe values.

The construction study's `LoadingBayTerminalRecipe.cs` manually interprets scanner sectors 78–84 after the simplified southern room: a narrowing approach, sector-81-inspired opening door, and terminal chamber. Coordinates retain the extension's 32-source-unit scale and local alignment. Existing BROWN1, FLOOR5_2, COMPSPAN, CEIL3_5 and DOOR3 assets are reused without new source bytes. Suspended strips interpret sectors 79/83 with the existing trim palette rather than claiming exact source lighting. This adds architecture, not Doom exit-switch or level-completion semantics.

`LoadingBayWestWingRecipe.cs` manually interprets sectors 24–45: the angled western chamber, paired raised/suspended blocks, stair sequence, gallery and larger outer hall. The source draft guides dimensions and heights at the existing 32-unit mapping; the spawn-side approach is deliberately rerouted around the study's retained window bay. Half-unit source stair rises are split into quarter-unit treads, and a new return staircase makes the lower hall recoverable. The outer chamber outline and gallery centerpiece are simplified. All textures reuse the existing palette; this is construction refinement, not a claim of exact WAD topology or gameplay.

`LoadingBayCourtyardRecipe.cs` manually interprets source sectors 5/13 as an open-air central court and recessed polygonal pool. The western edge is moved clear of the retained stylized spawn hall, and a new quarter-unit descent connects the north room. Pool recovery steps are a deliberate author refinement. The existing admitted SKY1 asset is selected through `LoadingBaySkyBackground`; its source hash/length validation is unchanged. Existing BROWN1, FLOOR5_2, NUKAGE3 and COMPSPAN textures supply the study palette. Source topology, exact texture offsets and damaging-floor semantics are not claimed.

`LoadingBaySouthPassageRecipe.cs` manually interprets scanner sectors 16–23, 48–50, 63–68 and 77 as a courtyard-to-east lower passage with quarter-unit stairs and a fourth usable door. The shared 32-source-unit mapping informs the proportions; bends, openings and stair placement are manually refined to connect the existing simplified court and eastern walkway. It reuses BROWN1, FLOOR5_2, COMPSPAN, CEIL3_5 and DOOR3 from the existing manifest without new source bytes. It does not implement source secret-discovery semantics. The first north doorway ceiling transition is also closed by extending its existing roof/header solids to their common top.

The #8259 continuity-audit refinement changes only authored dogleg solid extents: floor/ceiling slabs cover the wall thickness, and an inset wall-top seat meets the ceiling while preserving its visible height. Existing doorway openings and source assets remain unchanged. Engine-owned diagnostics are consumed from matched package `0.1.0-dev.e4b95f4207dc`; no downstream mesher, welding pass or alternate render path is introduced.


### Measured spawn refinement and study orientation (2026-09-13)

The current `LoadingBayRoomRecipe` replaces the stylized raised spawn deck/window bay with manually authored footprints and height changes measured from sectors 14/15 and 37–41, including the original start niche, blue recess, perimeter walkway, attached supports and western alcove. The original WAD at `/home/research/doom.ts/public/doom1.wad` was rechecked against SHA-256 `1d7d43be501e67d927e415e0b8f3e29c3bf33075e859721816f652a526cac771` (4,196,020 bytes); the offline full-scan measurements and original source were consulted directly because the configured code index had no Doom project. No new source code or asset bytes were copied.

Recipe plan coordinates remain `x=(mapX-1280)/32`, `north=20+(mapY+2880)/32`, with vertical height divided by 32. `LoadingBayStudyCoordinates` converts north to Engine world `-Z` for field extraction, placements, spawn, door bounds and audit intent. This corrects the earlier mirrored study; historical playtest positions above use the old positive-Z north convention. Engine transforms the field before extraction, preserving outward normals without a downstream mesh rewrite.

The invented western approach is removed, and the courtyard west boundary now meets the original eastern windows. Small alcove jamb notches, exact texture offsets, source lighting and decoration are simplified; the retained onward rooms are not newly certified as source-exact. Slab thickness, inset wall seats and slightly inset window jambs are recipe adaptations for robust joins. The east connector uses finer 0.125 requested sampling and a seated ceiling/wall contact. Existing immutable Engine APIs perform all geometry generation and analysis.

### Generated mountain sky replacement

At the owner's request, the selected sky is now `content/loading-bay/sky/mountain-panorama.png`, a built-in image-tool reinterpretation of retained `SKY1.png`, followed by a reference-guided horizontal-wrap edit. It is not a source Doom asset. The original 256×128 texture and WAD manifest remain intact. The generated asset's independent hash/length/dimensions and reference hash are recorded in `content/loading-bay/sky/manifest.json`, validated by the retained-content check and runtime exact-identity admission. Prompts, candidate and wrap details are in `docs/experiments/sky-candidate/README.md`. The existing Engine CameraView path selects the new image in both the study and ordinary product; renderer projection and resource ownership are unchanged.

### Eastern upper gallery recipe

`LoadingBayEastGalleryRecipe` uses the offline E1M1 measurements for sectors 62/70 and their links to 57/58 as source evidence for the raised eastern route. Source upper floor 104/32 = 3.25 and access rise 152/32 = 4.75 are retained. The narrow moving access is adapted to nineteen quarter-unit stairs from the existing basin, followed by a bent northern gallery overlooking the zigzag. The footprint is fitted to the study's simplified eastern hall, and ceiling 5.5 matches that hall rather than the source 5.75. It does not reproduce source lift or secret semantics. Existing generated/retained assets are reused; no new asset bytes or runtime source parser are introduced. Named C# solids own floor/wall/roof contacts; Engine owns field evaluation, extraction, collision and audits.
