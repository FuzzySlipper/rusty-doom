# Lane: Runtime trust

**Always on (temporary counterbalance).** Run this lane on every task until
campaign #8376 has landed and ordinary trusted-path code is the established
gravity. When that happens, demote this lane to optional or retire it; do not
keep it as a permanent tax.

## One question

Does this change add validation, verification, or defensive machinery to a
trusted first-party runtime path without a concrete failure it prevents?

## Why

Content-admission and provenance thinking leaks into runtime, where it does not
belong. This is a single-player product built from trusted first-party code;
there is no multiplayer-cheating, MITM, wire-corruption, adversarial-content, or
future-multiplayer threat model that justifies policing every mutation. There
are no pointless SHA checks or repeated hashing gates on already-admitted bytes,
and content kind `doom-e1m1` is semantic identity, not a byte-integrity hash to
purge.

Campaign #8376 deliberately removes that ceremony: the roughly twenty-delegate
positional list per `LoadingBayEngineServices.Update`, serving live reads
through world `Capture` allocations, cloning the whole barrel map before a
`DamageBarrel` chain, `LoadingBaySession.Restore` temporarily restoring live
world state to validate it plus snapshot/rollback around ordinary gameplay plus
a second projection `EntityStore` with divergent debug identity, the
hand-maintained schema-numbered binary codec (`LoadingBaySnapshotCodec`
schema8), product-owned serialization of Engine session fingerprints and raw
motion (`LoadingBayCharacterContinuationSnapshot`), compatibility readers and
historical fallback paths, and the separate named door ledger duplicating
canonical world door state (`_doors` / `SetDoor` beside canonical door
operations). A reviewer that re-asks for the removed machinery undoes the
campaign.

## Basis required for an actionable finding

Name all four:

1. the ceremony, quoted, with file and line — e.g. a propose/validate step, a
   hash/reverify on admitted content or a cache hit, a revision guard or
   snapshot/rollback around ordinary gameplay, a compatibility fingerprint or
   schema-version gate on current development data, a whole-world defensive
   copy before a bounded chain, a mutate-to-validate restore, a second
   numeric-ID projection graph, a named ledger shadowing a canonical owner;
2. why the path is trusted — admitted E1M1 definitions, attached live
   components, Engine-delivered resources, current-schema save state the owning
   boundary already established;
3. the concrete failure it claims to prevent, and why that failure has no
   identified caller — or where the owning check already establishes it;
4. the simpler shape: direct mutation, the existing safeguard, deletion, or
   the boundary the check actually belongs at (usually content admission,
   generation, or offline tooling).

A finding that names only "this could be invalid" is not actionable. Name
the caller, the input that reaches the path, and what goes wrong without
the machinery.

## Where checking belongs

- Content admission (deterministic forge, semantic-catalog generator,
  asset-catalog closure, canonical `content/projects/doom-e1m1.project.json`
  checks, `docs/source-provenance.md` record): reject meaningful
  missing/duplicate authored references once, with actionable errors. Generated
  catalog provenance is owned by generators (`E1M1SemanticCatalog.g.cs`,
  `LoadingBayRecipeSprites.g.cs`); update generators rather than editing output.
  Not this lane's target.
- Offline import/tooling (recipe scanners, room-study measurements, texture and
  prop closures): validation is expected. Not this lane's target.
- Runtime (attached stats/tracks/inventory, admitted E1M1 content, Engine
  services, current-schema saves): default to trust. Keep checks that protect an
  actual requirement: E1M1 gameplay eligibility (identity lifetime, weapon
  cooldown/ammo accounting, enemy attack policy, ray/line-of-effect occlusion,
  pickup atomicity, armor protection policy, Doom pickup caps), delayed
  projectile timing with recheck at impact where the target actually changed,
  track bounds, valid current content references, coherent current-save
  relationships, ABI and native lifetime/disposal order, genuine
  duplicate/dangling identity checks, optional atomic inventory edits for a
  genuinely all-or-nothing multi-item transfer, and understandable failure on
  malformed current data rather than partial success. Bounded diagnostics stay
  useful; they never become mandatory replay/audit work.
- Explicit save capture, previews, tooling, or a stated multiplayer
  requirement may justify stronger machinery at its own boundary. It never
  defines the baseline for ordinary gameplay.

## Not a finding

- Content-admission rejection of missing/duplicate authored references, or
  actionable errors for invalid authored configuration.
- Offline deterministic generation/provenance checks or artifact hashes.
- A concrete safeguard for an actual requirement listed above, kept local
  to the owner that establishes it.
- An Engine decoder or native-safety error the product surfaces and stops on.
- A stronger mechanism the task explicitly requires at a named boundary
  (tooling, explicit save capture).
- A compact structural constant beside the algorithm that owns it. That is
  the ownership lane's call, not this one.
