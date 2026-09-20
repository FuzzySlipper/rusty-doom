# Lane: Engine reuse

**Always on.** Run this lane on every task.

## One question

Does this change recreate a mechanism that is already safely available upstream
in `Rusty.Engine`?

## Why

A downstream agent that cannot find a capability will build one. That produces a
second implementation of something the Engine already guarantees, which then
drifts, and it hides the real signal: either the Engine API should be used, or it
is genuinely missing and deserves one narrow upstream request. Product policy
belongs downstream; guarantees do not.

Campaign #8376 exists in large part to move Loading Bay onto canonical upstream
mechanics (`EntityStore`, `EntityTypeId` creation-time kind metadata, `Actor`
facade where identity fits, ordinary class components, `StatsComponent` / `Track`
sharing its maximum `Stat`, inventory/equipment, component capture/rebuild
helpers, `ProductStateStore`, `JsonProductStateCodec` with source-generated
`JsonTypeInfo`, content services, persistence primitives, safe spatial /
character / projectile / rendering queries). A change that hand-rolls any of
those instead of adopting them works against the campaign.

## Basis required for an actionable finding

Name all four:

1. the current safe Engine API that already covers the behavior;
2. the local duplicate the change introduces, at file and line;
3. the concrete adoption or replacement path;
4. whether the difference is an Engine guarantee or product policy.

A finding that names only "this looks like something the Engine might do" is not
actionable. Verify the contract in the generated safe C# surface (plus the
packaged SDK in use) before claiming it exists, and prefer `obj/`-generated
bindings plus the packaged SDK over recollection. Never propose a sibling Engine
checkout dependency, downstream Rust, handwritten P/Invoke, `unsafe` product
code, a composition project, an `EngineProduct` assembly attribute, an exported
entrypoint, or generated bindings as the adoption path — see `AGENTS.md`.

## Not a finding

- Engine machinery that is not exposed safely. That is an upstream gap, not a
  duplicate — report it as a gap and say so.
- Product policy implemented downstream on top of an Engine mechanism. That is
  the intended split (`csharp/LoadingBay.Game` owns E1M1 policy, validation,
  game state, save meaning, facts, snapshots, and HUD projection; the Engine
  owns spatial, character, projectile, and rendering authority).
- A local helper that is genuinely narrower than the Engine API and does not
  duplicate its guarantees.
