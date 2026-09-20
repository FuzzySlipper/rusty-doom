# Lane: Ownership and values

**Optional.** Use when the change crosses an ownership seam, adds tuning, or
touches E1M1 definitions or provenance.

## One question

Does this change leak policy across its owner seam, or put authored and tunable
values into incidental code?

## Basis required for an actionable finding

Name all three:

1. the actual assumption or value, quoted, with file and line;
2. its current owner and its correct owner, in `AGENTS.md`'s terms
   (`csharp/LoadingBay.Game` owns E1M1 policy, validation, game state, save
   meaning, facts, snapshots, and HUD projection with values typed and named in
   `LoadingBayTuning`, definitions, and product records; `Rusty.Engine` is an
   immutable package generating composition below `obj`; the matching runtime
   pack owns host integration, renderer/canvas, input, lifecycle, browser shell,
   and renderer preload; TypeScript exports `mountProductUi` and renders the
   read-only HUD only; authored E1M1 content, live C# runtime state, and
   transient presentation stay separate with exact provenance in
   `docs/source-provenance.md`);
3. the affected uses — what breaks or becomes wrong when that value changes.

## Not a finding

- A demand for a universal abstraction, a new interface, or a generic factory.
- A constant for every literal. Compact structural constants stay beside the
  algorithm that owns them; only genuinely adjustable or authored values are
  promoted to `LoadingBayTuning`, definitions, or product records.
- A rename that moves vocabulary somewhere without changing who owns
  the decision.
- A C# runtime parse of source-shaped authoring data, or an edit of generated
  output (`E1M1SemanticCatalog.g.cs`, `LoadingBayRecipeSprites.g.cs`) instead
  of its generator. Those are defects, but they belong to this lane only when
  framed as the ownership violation above with the owning generator or admission
  boundary named; otherwise they are behavior or ceremony concerns in their own
  lanes.
- A demand for validation, verification, or audit machinery as the "correct
  owner" for a value. That is a runtime-trust question, not an ownership one.
