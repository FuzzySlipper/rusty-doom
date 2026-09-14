# Recipe gameplay authoring pass

The normal product now selects the authored E1M1 recipe scene. `LOADING_BAY_SCENE=legacy-voxel` explicitly selects the retained original voxel implementation; `run-room-study.sh` remains a compatibility launcher for the recipe scene.

`LoadingBayRecipeGameplay` owns named pickup kinds, refined world-space placements, health/armor/ammunition, pistol and shotgun selection, actor health/awareness/attack timing, toxic-floor damage, and terminal completion. There are 16 authored supplies and 12 trooper/imp placements across the starting wings, north hall, zigzag, southern terminal, lower passage and upper gallery. These are a playable recipe interpretation, not an exact reproduction of every original THING or difficulty flag.

Engine owns sprite realization and viewport placement, static-mesh collision, character movement, trigger overlap and projectile dynamics. Fireballs use an unbound Dynamics world for motion and Spatial capsule sweeps for continuous collision against the room, current doors and player; they do not also bind the whole level into Dynamics. All appearances share the room's complete graphics snapshot. The player's continuation is captured immediately after its own proposal, before enemy proposals; each actor supplies its own motion and command sequence. No per-enemy copy of the level collision scene is needed.

The generated C# sprite catalog comes from the existing retained Doom sprite manifest. Run `python3 scripts/authoring/generate-recipe-sprites.py` to regenerate it. The source WAD, atlas PNGs, source hashes and donor sprite semantics remain recorded in `content/doom-e1m1/sprites/manifest.json` and `docs/source-provenance.md`. No new extracted art is introduced by this pass. The terminal marker reuses EXITSIGN.png.

Controls: WASD/mouse, click/RT fire, E/controller X use doors and exit, 1 fist, 2 pistol, 3 shotgun after collecting one, R restart. F3/backquote opens the collapsed debug block. Its console and renderer metrics use the packaged Engine widgets. `loading-bay.diagnostics true` enables HUD diagnostic samples at 0.25-second intervals; `false` disables them. Opening Debug alone does not enable that stream. `loading-bay.readout` remains a one-shot read and includes HUD publication and trigger reconciliation counts.

Combat is deliberately modest: troopers use line-of-sight hitscan; imps launch visible Engine Dynamics fireballs. Actors approach directly when visible, respecting mesh collision. They do not yet navigate around occluding corners or reproduce Doom's full animation/AI state machine. Recipe sessions reset gameplay while retaining static meshes and do not yet participate in the legacy voxel save codec. Doom certification remains manual and is not claimed by this work.

## Checks and native evidence

Required lifecycle/CoreCLR/NativeAOT spine, Angular typecheck/build, boundary and retained-content provenance checks passed. `python3 scripts/authoring/generate-recipe-sprites.py --check` verifies the compiled atlas rectangles and sizes against the retained manifest.

Root-operated native Wolf session `4625a291-f147-4e98-9cbf-cbb7d2fcad7d` demonstrated ammo/armor collection, enemy attack/death feedback, pistol kill, western shotgun collection and F3 debug expansion. The slower inspection route used explicit `loading-bay.set-track health 100` calls after death: this is assisted interaction evidence, not a normal full-level survival run. Session `dfd6cb8c-1c21-4023-bd4b-db978c5b6a48` then used physical movement/use input through the first door into the north hall; shotgun shots reduced shells and killed a guard. Continued movement reached the zigzag and activated visible imps/fireballs. Receipts and one-shot readouts are in `.runtime/evidence/recipe-gameplay/`.

Diagnostics-off windows: HUD publication count stayed 6 over 300 admitted steps and stayed 28 over 301 steps. The enabled five-second window advanced 7 -> 27 publications, exactly 4 Hz. Idle callback telemetry was about 232–233 us median / 314–320 us p95. Its rolling windows overlap; this is not an isolated before/after benchmark against the old voxel renderer. Native attacks, pickup collection and damage continued to publish real HUD changes with diagnostics off.

The first combat pass exposed an additional cost: duplicate Dynamics world binding plus Spatial casts reached 123 ms callback p95, with admitted catch-up and dropped steps. The recipe was changed to one continuous Spatial capsule sweep per Engine-advanced fireball, removing the redundant full-level Dynamics collision binding. The raw pre-fix sample is `combat-diagnostics.json`; subsequent proof must be read separately from the earlier native captures.

After that change, the same route reached the zigzag normally with two live fireballs: callback telemetry was 248 us median / 6,564 us p95 / 13,359 us maximum in the retained window, versus the earlier 123,089 us combat p95. The post-change run reported 3 dropped steps over its startup and route, rather than the earlier accumulated 1,978. These are operational samples, not isolated hardware/GPU measurements. Original visible fireball: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/af0cfbd8-63de-4170-ab48-418bc6f057f0/98e5817d-dcd4-4c2a-a161-25cea52eb5e9.png`.

The assisted route traversed the basin recovery stairs via `(59,8) -> (54,8) -> (54,15)`, opened the southern and terminal doors, and reached `(54.089,0.170,40.118)`. E set `complete=True`; the visible completion panel reported 7/16 pickups. Original: `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/af0cfbd8-63de-4170-ab48-418bc6f057f0/856ff823-a066-4960-8ab0-8c846aa12ec1.png`. Earlier direct diagonal attempts cut off the upper recovery step and correctly remained in the lower basin; those are retained as failed route choices, not claimed traversal.

A final UI correction connects debug open/close to `context.ui.setInteractionMode` and `focusGameplay`. This uses Engine's public arbitration port rather than implementing browser pointer-lock or input state downstream. F3 remains available when the console input is focused; backquote is left alone while typing.

The first R-key probe reset C# state but exceeded the realtime callback deadline while reconstructing every DC mesh, leaving the old browser frame. Restart now restores the initial player snapshot, closes all doors, restores the Engine trigger baseline, resets supplies/actors/weapons and disposes active fireballs while retaining static level resources and UI stream identity. This avoids extraction work in a realtime callback. The failed probe is retained separately in `restart-observe.json` and is not successful restart evidence.

Final native Wolf session `0439d149-e8e0-4202-b5fe-d56d8b04f8c5` (slot-2) verified the corrected restart. Walking collected bullets (69 ammo, 1/16 supplies); R visibly returned to spawn with 100 health, 50 ammo and 0/16 supplies. Walking again recollected the restored pickup (70 ammo), also proving input continuation and trigger reactivation. Before/after originals are `3ce4a238-d926-47c3-8c36-429002d95d71.png` and `5df9dcb8-3d59-4bab-a981-0f79fd832361.png` beneath `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/slots/slot-2/0439d149-e8e0-4202-b5fe-d56d8b04f8c5/`.

The same session typed and executed `loading-bay.readout` in the packaged console. Original `c161cf84-270b-466c-bc8e-f71ec096d9d0.png` shows its response and unchanged gameplay state. F3 from the focused input closed Debug and returned movement/pointer capture; `39b40c81-aa29-4d4a-91c4-eacce3a19a4b.png` shows the subsequent pickup. The widget clears its filter after execution, placing responses below the full command catalog; typing the filter again made the response visible. Final R reset left the demo at spawn. All seven owned native sessions were released with no cleanup errors.

A separate managed allocation sample used `dotnet-counters` on the final CoreCLR worker at idle spawn, with no playtest browser attached: nine one-second samples averaged 253,641 bytes/s with diagnostics off and 345,019 bytes/s at 4 Hz (medians 253,328 and 343,280). Raw `allocations-off.csv` and `allocations-on.csv` are in the evidence directory. This measures whole-worker managed allocation, including Engine bridge/service work and collection overhead; it is not a per-HUD allocation attribution or an isolated pre-pivot comparison. Diagnostics were disabled afterward.

Task disposition: #8182 implemented; #8183–8185 cancelled as superseded by the recipe scene. The retained voxel path received safe HUD/unchanged-exit publication cleanup, but no old voxel batching/trigger performance acceptance is claimed. Saves, audio, full Doom AI/animation, exact difficulty placements and full certification remain outside this pass.

## Sprite animation and weapon framing follow-up

The weapon no longer fits each cropped frame into a top-aligned box. The generated `recipe-viewmodels.png` puts retained pistol/shotgun patches on a common 320×168 canvas using original offsets, preserving source pixels and applying the classic vertical pixel aspect in Engine sprite sizing. Engine contains and bottom-aligns this canvas above the HUD; the status bar now occupies the matching bottom 16% of the viewport. This removes aspect-dependent floating and frame-to-frame rescaling.

Source-guided 35 Hz state sequences replace the previous two-image swap: pistol A/B/C/B recoil/recovery, shotgun A/A/B/C/D/C/B/A/A pumping, separate pistol and two-frame shotgun muzzle flashes, trooper/imp idle/walk/attack/pain/death, and fireball flight/impact. Discharge happens at the source attack frame. Gameplay owns timing; Engine owns sprite realization and viewport fitting. Dead enemies advance through their death frames before retaining the final corpse. World patch origins are retained, with below-patch origins translated vertically to satisfy Engine's normalized-pivot contract. The initial invalid-pivot admission was found and fixed during verification.

Donor consultation: direct read of `doom.ts` revision `0d88ba912f7b084a05b776a19801d45f383cef20`, since no Doom code index was configured. State tables, psprite callbacks, patch offsets and rendering placement were consulted; source/provenance details are in `docs/source-provenance.md`. Directional rotations, extreme-death/gib variants, weapon bob/raise/lower and full Doom AI behavior are not added by this pass.

Required C# spine/lifecycle/CoreCLR/NativeAOT passed, including focused animation boundary checks (discharge, recoil, reverse pump, flash expiry and corpse retention). Angular build/typecheck and focused `nx lint loading-bay` passed. Boundary and deterministic provenance/atlas generation checks passed. Full repository lint still reports four errors in unchanged legacy authoring TypeScript (prefer-const/no-empty/no-inferrable-types); this is separate from the passing UI lint.

Root-operated native Wolf session `336dc529-944f-4a0b-9a36-60c87c6eee46`, slot-2, verified normal canvas startup, corrected pistol/shotgun framing, a visible pistol muzzle flash, shotgun pump and return-to-ready frames, and the start of the trooper death sequence after a shotgun kill. The inspection route used read-only pose-assisted physical input and explicit health replenishment. Captures sample animation and do not independently show every short-lived intermediate frame; the timing tests establish those sequences. Original files beneath `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/slots/slot-2/336dc529-944f-4a0b-9a36-60c87c6eee46/`:

- Pistol framing: `f004882c-024a-4637-938f-4a7740568aea.png`.
- Pistol muzzle flash: `14e99be9-eaa9-46f0-9867-6262f0fe71f6.png`.
- Shotgun idle: `5f645d57-92bc-4fe5-bb85-964263c09f66.png`.
- Pump and return: `07c9eefc-d77c-43d2-bb11-fc7810382cbf.png`, `a21b3dcd-98c1-471d-8778-04f22dfeb1da.png`.
- Trooper death starts: `ce7b76ba-7146-4552-912e-3357a26976c5.png` (kill count 1); subsequent corpse lies below this close camera view.

Raw receipts: `.runtime/evidence/recipe-animation/`. Session cleanup released=true/errors=[]; normal demo remains running at :4395, reset at spawn. Refresh existing browser pages for the new HUD stylesheet. No full level certification or new AI/navigation claim.

### Compact HUD correction

Owner rejected the proportional status bar as too tall. It is now a fixed 88 CSS pixels again, with hints positioned above it. The common weapon/flash canvas is bottom-aligned at viewport Y=0 beneath the DOM bar, preserving its frame scale and offsets. The art is deliberately below the crosshair; camera-directed aiming remains unchanged. This supersedes the 16% HUD layout above. C# spine/CoreCLR/NativeAOT, shell build/typecheck, focused UI lint and boundary checks passed. Native Wolf session `455d6983-8ea4-4ae1-b2b8-4bf923d6dab0` showed the compact bar and lower pistol in original `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/455d6983-8ea4-4ae1-b2b8-4bf923d6dab0/03c6acf6-757c-4537-b100-1657a7fa6229.png`. Receipts: `.runtime/evidence/compact-hud/`; cleanup released without errors. Refresh existing pages for the CSS update.

## Fist and atlas isolation (2026-09-14)

Fists use retained PUNG artwork: ready A, swing B4/C4/D5/C4/B5 at 35 Hz, damage at tic 4. Engine Spatial limits the camera-directed punch ray to 2 world units (64 map units), with fixed 20 damage and no ammunition use or flash. Original random damage/spread, berserk, autoaim and held-fire refire are not implemented.

The viewmodel atlas now isolates each canvas with a two-pixel transparent gutter and uses interior texel-center UVs. This addresses float32 frame boundaries sampling the preceding frame's bottom row. The compact HUD and weapon placement are unchanged.

C# spine/lifecycle/CoreCLR/NativeAOT, Angular build, focused UI lint, provenance and boundary checks passed. Native Wolf session `3422e3ee-f8a3-487f-91fb-ccb0115e9feb` verified selection, melee HUD and trooper health 30 → 10 → 0 while bullets remained 49 and shells 16. Pose-assisted movement/aim and explicit health replenishment were used, including revival during inspection. This does not establish unaided difficulty. Original captures under `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/slots/slot-2/3422e3ee-f8a3-487f-91fb-ccb0115e9feb/`:

- Fist HUD: `61f2b415-c78d-4307-8ef1-3e6819f95494.png`.
- Punch kill feedback: `9bbc05c4-7c36-438a-9bc1-3a63f46c1590.png`.
- Pistol flash: `d0054ebd-8d6f-45e5-b3b5-991f1fc7f153.png`.
- Shotgun flash: `59cfa50e-f6c0-43f3-ad37-eab593959973.png`.

No stray atlas rows appear in these sampled frames; every transient frame was not independently captured. Timing assertions cover the complete swing. Receipts and verification logs: `.runtime/evidence/fist-atlas/`.
