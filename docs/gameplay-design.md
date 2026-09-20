# Loading Bay gameplay design

Status: established by task #8377 for campaign #8376; landed owners recorded by #8378.
Read with `AGENTS.md`. Exact Engine SDK/runtime pair belongs in machine
config/Den, not in normative prose — this document names capabilities, not
revisions.

Loading Bay is one concrete single-player game: Doom E1M1 only. There is no
RPG framework, no reusable kit, and no traversal-certification campaign. This
document records the one canonical entity/state graph, the authored-vs-runtime
identity rule, the game-domain owners, direct trusted mutation, explicit
current save capture, separate Engine realizations, and preserved source
provenance. Task #8378 landed the entity-ownership half; the notes below
record the landed shape and what remains for #8379–#8381.

## Canonical entity/state graph

One graph. `LoadingBaySession` (`csharp/LoadingBay.Game/LoadingBaySession.cs`)
is the admitted-step product root. It owns:

- the `EntityStore` (`_entities`), the authored-to-runtime map
  (`LoadingBayEntityMap`), the `InventoryStore` (`_inventory`), the
  `SimulationScheduler` (`_scheduler`), and `LoadingBayWorldState` (`_world`);
- live player vitality as an attached Engine `StatsComponent`
  (`LoadingBayStats.ForPlayer`) plus `LoadingBayArmorProtection`
  (`_armorProtection`); enemy vitality/posture in attached per-enemy
  `StatsComponent`/`LoadingBayEnemyStateComponent`; pickup lifecycle in
  attached `LoadingBayPickupStateComponent`; door/lift/floor/barrel/hazard
  progression in attached world-state components;
- weapon cooldowns (`_weaponReadyAt`), secrets (`_secrets`), completion
  (`_complete`), and the bounded fact journal (`_journal`);
- Engine realizations through `LoadingBayEngineServices` (`_engineServices`)
  and persistence through `ProductStateStore<LoadingBaySnapshot>` (`_store`).

Static authored definitions stay in the generated semantic catalog
(`E1M1SemanticCatalog.g.cs`) and `LoadingBayDefinitions` /
`LoadingBayTuning`. Native projection owners stay in
`LoadingBayEngineServices` coordinators and
`LoadingBayVoxelScenePresentation`. Snapshot DTOs
(`LoadingBayContracts.cs`) exist only at actual save/diagnostic boundaries.

## Authored vs runtime IDs

Authored E1M1 identity is the catalog `EntityId` (for example pickup, enemy,
door, floor, lift, barrel, hazard, encounter IDs in
`E1M1SemanticCatalog.g.cs`). Runtime identity is the `EntityStore` `EntityId`.

Landed (#8378): `LoadingBayEntityMap`
(`csharp/LoadingBay.Game/LoadingBayEntityMap.cs`) is the only path from an
authored catalog identity to the live runtime entity. Bootstrap creates one
entity per canonical authored identity with creation-time kind metadata
(`EntityTypeId`: player, enemy, pickup, encounter, barrel, hazard, door,
floor, lift, secret, exit, world-object) and records the mapping; no code
asserts `EntityId.Value` equals the authored number. The lifecycle exercise
proves resolution under reversed allocation order. Engine spatial,
perception, and trigger facts keep using authored identities by Engine
authority and are never translated by the map. No generic ECS framework.

## Game-domain owners

Owners as landed by #8378–#8379:

| Concern | Landed owner |
| --- | --- |
| Player/enemy/pickup/world-object state | Attached components over canonical entities (`StatsComponent` vitality via `LoadingBayStats`; `LoadingBayEnemyStateComponent`; `LoadingBayPickupStateComponent`; door/lift/floor/barrel/hazard components owned by `LoadingBayWorldState`); Engine inventory/equipment keyed to the same canonical player entity |
| Combat and world interactions | Named game-domain owners with explicit state and typed operations: `LoadingBayCombat` (damage, fire/equipment/loadout, enemy attacks, projectiles, encounters), `LoadingBayWorld` (hazards, movers, secrets, doors, exit, barrels, completion), `LoadingBayPickups` (collection, lifecycle); Engine coordinators call them directly. `Update` carries the three owners plus the world store, one fact-publication callback, and one barrel Engine-composition seam — no delegate list, no universal interface, no bus, no task-specific Engine API |
| Doors | Canonical `LoadingBayWorldState` door operations own supported behavior; the former freeform session ledger and the dead world-action switch path are removed; named-door save shape is a derived read-only projection |
| Pickups/doors in fixtures | Fixture-only manual keys (inside `LoadingBayPickups`) deliberately separate from canonical components |
| Reads | Live typed reads (`WorldState.DoorState/FloorState/LiftState`, component reads, live motion iteration); `Capture` stays at save/diagnostic boundaries |

Armor protection policy and Doom pickup caps are preserved; the game does not
become a generic RPG. Static generated catalog definitions and native
projection owners stay separate.

## Direct trusted mutation

Single-player trusted C# paths need no proposal/acceptance, revision guards,
snapshots, or rollback around ordinary gameplay. Synchronous actions perform
eligibility, query, and application once and publish completed facts for
HUD/audio/debug — not mandatory replay acceptance. A legitimate
query-then-apply split with concrete meaning may remain; names alone are not
defects.

Ceremony removed by #8379 (all three survey items landed):

- the ~20-delegate `Update` list is replaced by three named owners plus the
  world store, one fact-publication callback, and one barrel
  Engine-composition seam; prepare/settle policy lives in the owners,
  called directly by the coordinators;
- `DamageBarrel` runs a bounded work queue with one explosion per barrel
  and per-barrel commit;
- chain inputs are locally resolved (occlusion at discovery); failure
  semantics of native calls preserved with no rollback promise for
  terminal callbacks.

Preserved: real delayed projectile timing, weapon cooldown/ammo accounting,
enemy attack policy, ray/line-of-effect occlusion, pickup atomicity, and
bounded diagnostics. Completed facts stay useful, not mandatory.

## Explicit current save capture

One current save schema. Development saves may break: no old-schema migration,
no runtime integrity or replay gates.

Current survey: `LoadingBaySnapshotCodec`
(`csharp/LoadingBay.Game/LoadingBaySnapshotCodec.cs`) is a hand-maintained
`CurrentSchema = 8` binary codec; `LoadingBayCharacterContinuationSnapshot`
(`LoadingBayContracts.cs:93`) serializes source session/generation/spatial/
content/config fingerprints and raw Engine motion;
`LoadingBaySession.Restore` (`LoadingBaySession.cs:527-594`) temporarily
restores `_world` then rolls it back for validation, snapshots live state for
rollback, and builds a second projection `EntityStore` whose debug identity
differs from the original store.

Target: Engine `ProductStateStore` plus source-generated
`JsonProductStateCodec`/current DTOs (or the minimum current equivalent
justified by actual API). Remove schema numbers, compatibility readers, and
product-owned serialization of Engine session fingerprints. Capture meaningful
canonical game state once — health/armor policy, ammo/weapons/cooldowns,
pose/look, pickup/drop lifecycle, enemies/encounters, world movers/hazards/
barrels, secrets, completion — reusing upstream component capture/rebuild for
stat relationships where useful. Transient controller input, support state, and
optional native continuation are decided and documented separately; supported
Engine reconstruction supplies any movement value necessary to resume
correctly. Validate current DTO shape and relationships without mutating the
live session; build/apply one coherent replacement game state and resource
binding; keep correct native retirement and usable active state on expected
failed load. Semantic map identity is a simple content-selection check
(`ContentIdentity: "doom-e1m1"`), not a historical byte-compatibility gate.

## Separate Engine realizations

The product decides; the Engine guarantees. Engine owns admitted lifecycle and
timing, input delivery, spatial collision and character steps, camera and
perception, voxel realization, presentation/animation, UI streams, persistence
primitives, renderer/canvas, and the browser shell. The immutable SDK exposes
the supported C# services and generates product binding below `obj`.

Adopted for this campaign (pinned pair verified in #8377; see below):
`EntityStore`/`Actor`/`EntityTypeId`/class components,
`StatsComponent`/`Track` (double-backed `Stat` with integer/float accessors,
`Track` sharing its maximum `Stat`), inventory/equipment, and current-state
codec helpers — from installed external artifacts only. No sibling source
dependency, compatibility shim, downstream Rust, P/Invoke, `unsafe`, or
composition project. If a required behavior is absent from the safe API, file
one narrow purpose-neutral `rusty-engine` request and stop at that boundary.

## Preserved source provenance

Doom E1M1 is the sole supported authored content. Exact source and asset
provenance stays in `docs/source-provenance.md`; the canonical closure is
`content/projects/doom-e1m1.project.json` with its `content/doom-e1m1/`
inputs. Content kind `doom-e1m1` is semantic identity, not a byte-integrity
hash to purge. The C# runtime admits committed artifacts through Engine
content services and consumes the generated typed semantic catalog; it never
parses source-shaped authoring data at runtime. Generated catalog provenance
is owned by generators: update `scripts/generate-e1m1-semantic-catalog.mjs`
(and the sprite generator) rather than editing `E1M1SemanticCatalog.g.cs` or
`LoadingBayRecipeSprites.g.cs`.

## Proof posture

Focused semantic tests, build, and CoreCLR staging appropriate to the change
are the standard. Browser evidence answers only a changed interaction; AOT
answers only a real fidelity question. `pnpm run certify:e1m1` remains
release/manual with its known stall at waypoint `[127,121]`; source checks are
not proof of complete traversal. Do not silently weaken
correctness/provenance checks or imply unsupported traversal succeeded.

## Campaign map

- #8377 (this task): matched pair, mechanical build adaptations, this design
  doc, corrected repo/Den guidance, reconciled proof posture.
- #8378: canonical entity components and authored/runtime mapping.
- #8379: concrete gameplay operations replacing callback/proposal plumbing.
- #8380: one current game-state codec replacing versioned native continuation.
- #8381: reconciliation against landed code, not child statuses.
