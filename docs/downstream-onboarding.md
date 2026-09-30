# Loading Bay onboarding

Loading Bay is an ordinary C# consumer of the packaged Rusty Engine SDK. Its supported authored content is E1M1. Copy its ownership boundaries rather than its Doom-specific policy or assets.

Install .NET 10, Node 24 and the pinned pnpm. Bootstrap the Engine CLI with `curl -fsSL https://raw.githubusercontent.com/FuzzySlipper/rusty-engine/main/scripts/install-rusty.sh | bash`, then run:

```bash
rusty install --project csharp/LoadingBay.Game/LoadingBay.Game.csproj
pnpm install --frozen-lockfile
./scripts/run-csharp-product.sh
```

`Directory.Build.props` selects the immutable SDK/runtime pair. `rusty dev` restores/builds the C# product, builds the Angular UI through the declared SDK command, stages content and runs CoreCLR. No source WAD or prebuilt UI is required. Host, port, output and live debugging use CLI flags. An Engine contributor may explicitly append `--engine-source /absolute/rusty-engine`.

The C# product owns gameplay, state, save meaning and HUD projection. Engine generates composition below `obj` and owns runtime, renderer, canvas, input and lifecycle. Angular only mounts the DOM HUD from the packaged typed UI contract. Committed E1M1 content is an immutable create-time snapshot; content edits replace the runtime rather than opening independent bundles. Angular is retained as the declared product UI framework, with unused workspace topology removed. The optional room study is a separate ordinary product; see [room study](room-study.md).

`pnpm run verify` runs typechecking, retained content checks, TypeScript tests, focused C# tests, managed build, CoreCLR staging and the lifecycle exercise. The absent offline WAD skips only WAD-backed tests; malformed/synthetic tests and retained-content checks remain active. Set `DOOM1_WAD` to the recorded archive for full offline source tests. Deliberate regeneration needs the source and must preserve [provenance](source-provenance.md).

Use `./scripts/verify-csharp-spine.sh --aot` only for a real NativeAOT fidelity question. Browser evidence must answer the affected visible interaction. The retired certifier stopped at `[127,121]`; no complete E1M1 traversal certificate is claimed.
