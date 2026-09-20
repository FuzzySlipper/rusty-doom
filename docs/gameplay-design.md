# Loading Bay gameplay design

Status: established by task #8377 for campaign #8376; updated as owners land.
Read with `AGENTS.md`. Exact Engine SDK/runtime pair belongs in machine
config/Den, not in normative prose — this document names capabilities, not
revisions.

Loading Bay is one concrete single-player game: Doom E1M1 only. There is no
RPG framework, no reusable kit, and no traversal-certification campaign. This
document records the one canonical entity/state graph, the authored-vs-runtime
identity rule, the game-domain owners, direct trusted mutation, explicit
current save capture, separate Engine realizations, and preserved source
provenance. Later tasks (#8378–#8381) migrate callers toward it; the survey
notes below name what is still true on disk until they land.

## Canonical entity/state graph

One graph. `LoadingBaySession` (`csharp/LoadingBay.Game/LoadingBaySession.cs`)
is the admitted-step product root. It owns:

- the `EntityStore` (`_entities`), the `InventoryStore` (`_inventory`), the
  `SimulationScheduler` (`_scheduler`), and `LoadingBayWorldState` (`_world`);
- live player vitality as `Mechanics.Track` health/armor plus
  `LoadingBayArmorProtection` (`_health`, `_armor`, `_armorProtection`);
- pickup lifecycle (`_pickupStates`), enemy vitality/posture (`_actors`),
  weapon cooldowns (`_weaponReadyAt`), secrets (`_secrets`), completion
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

Current survey: `LoadingBaySession.BootstrapCanonicalEntities`
(`LoadingBaySession.cs:814-821`) creates every number sequentially and throws
when `entity.Value != expected`; the constructor pins `_player = new
EntityId(1)` (`LoadingBaySession.cs:60`). That assertion couples allocation
order to authored numbering.

Target: an explicit authored-E1M1-identity to runtime-entity mapping with
creation-time kind metadata (`EntityTypeId`). Indices may locate canonical
objects but must not mirror mutable authority. All adapters, save, and debug
lookups distinguish authored IDs used by Engine spatial facts from actual
`EntityStore` IDs, and demonstrate that authored IDs resolve correctly even
when runtime allocation order differs. No generic ECS framework.

## Game-domain owners

Current owners (migrated in place by #8378–#8379):

| Concern | Current owner | Target |
| --- | --- | --- |
| Player/enemy/pickup/world-object state | Separate fields/dictionaries in `LoadingBaySession` (`_health`, `_armor`, `_pickupStates`, `_actors`, `_weaponReadyAt`, `_secrets`) plus snapshot-record dictionaries in `LoadingBayWorldState` | Concrete facades/components over canonical entities; live health/armor/enemy vitality in Engine `StatsComponent`/`Track`; existing Engine inventory/equipment attached to the same canonical player entity; small class components for enemy posture/readiness, pickup lifecycle, and door/lift/floor/barrel state |
| Combat and world interactions | `LoadingBaySession` policy mixed with prepare/settle receipts; `LoadingBayEngineServices.Update` positional delegate list (`LoadingBayEngineServices.cs:87-105`); `LoadingBayWorldServices` coordinators | Small named game-domain owners for combat and world interactions using safe Engine queries and canonical state; explicit cohesive dependencies replacing the ~20-delegate list (never a universal interface with the same twenty methods, a generic bus, or task-specific Engine API) |
| Doors | Canonical `LoadingBayWorldState` door map **and** a second freeform session ledger `_doors`/`SetDoor` (`LoadingBaySession.cs:32,406-410`) | Canonical door operations own supported behavior; the named-door dictionary/generic switch path is removed where canonical operations cover it; real callers migrate rather than leave two truths |
| Pickups/doors in fixtures | Fixture-only manual keys (`_manualPickupKeys`) deliberately separate from canonical `_pickupStates` | Fixture shortcuts never become production authority |
| Reads | `LoadingBayWorldState.Capture()` allocations serving live reads (`LoadingBaySession.cs:442,452,462,525`); `LoadingBayEngineServices.CapturePlayer()` per update | Ordinary reads use live typed state; `Capture` stays at save/diagnostic boundaries |

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

Current ceremony to remove:

- `LoadingBayEngineServices.Update` receiving ~20 `Action`/`Func` delegates
  for pickups, hazards, doors, combat, and world reads
  (`LoadingBayEngineServices.cs:87-105`), with prepare/settle receipts split
  across session and coordinators;
- `LoadingBayWorldState.DamageBarrel` cloning the whole barrel map before the
  chain (`LoadingBayWorldState.cs:126-152`) instead of a bounded work queue
  with one explosion per barrel;
- barrel/chain inputs kept as whole-world defensive copies rather than local
  resolved query inputs; failure semantics of native calls preserved with no
  rollback promise for terminal callbacks.

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
