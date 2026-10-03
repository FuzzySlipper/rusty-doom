# Loading Bay

Loading Bay is a free experimental Rusty Engine test repository. The supported product is the Doom E1M1 voxel port, implemented in C# with the packaged Engine SDK. CoreCLR through `rusty dev` is the normal development lane; NativeAOT is an optional fidelity/release check.

## Run

```bash
rusty install
pnpm install --frozen-lockfile
rusty dev --port 4394 --live-debug
```

`rusty install` installs the Engine pair pinned in `Directory.Build.props` into the shared cache. Only `rusty update` changes that pin. The SDK builds the Angular HUD from source, stages the product and starts the matching runtime. The default URL is `http://127.0.0.1:4394`. `.runtime/` contains development persistence, not the Engine installation. `Directory.Build.props` names the E1M1 project as the default, so these commands work from anywhere in the repository on Linux and Windows; one `pnpm install` serves both, since `pnpm-workspace.yaml` fetches the Windows builds of the UI toolchain's native packages too.

```bash
rusty dev --port 4397 --live-debug
rusty dev --output window --live-debug
```

`scripts/run-csharp-product.sh` (Linux) is the same launch bound to `127.0.0.1:4394` for service managers.

Engine contributors can explicitly pass `--engine-source /absolute/rusty-engine`. Normal product work uses the installed package pair.

## Controls

Click the canvas to capture the mouse; Escape releases it. WASD moves, mouse movement looks, Space jumps, E uses, primary click or left Control fires, and R restarts. J/L turn and I/K pitch at 120 degrees per second; hold left Shift for precision look at 24 degrees per second. Standard gamepads use left stick movement, right stick look, A jump, X use and right-trigger fire.

The optional room study also supports 1/2/3 for fist, pistol and shotgun selection. Its gamepad aim assistance and interaction inspection are experimental features, separate from supported E1M1 policy.

## Architecture and checks

`csharp/LoadingBay.Game` owns E1M1 state, combat, pickups, world progression, saves and the read-only HUD projection. Engine owns the host, rendering, input, lifecycle, spatial queries and persistence primitives. `apps/loading-bay` exports `mountProductUi` and renders the Angular HUD. The SDK compiles it when declared source inputs change.

```bash
./scripts/verify-csharp-spine.sh  # catalogs, focused tests, managed build, CoreCLR stage and lifecycle exercise
./scripts/verify-csharp-spine.sh --aot  # explicit NativeAOT fidelity check
pnpm run verify
```

The historical manual traversal stalled at waypoint `[127,121]`; its retired `certify:e1m1` command is unavailable. Focused tests and smoke evidence do not establish complete E1M1 traversal.

## Optional room-study experiment

```bash
rusty dev --project csharp/LoadingBay.RoomStudy/LoadingBay.RoomStudy.csproj --port 4395 --live-debug
```

(`scripts/run-room-study.sh` on Linux.) This selects the separate ordinary SDK product `csharp/LoadingBay.RoomStudy/LoadingBay.RoomStudy.csproj` on port 4395, with live debug enabled. Its construction recipes, alternate gameplay and authoring audits are excluded from the default E1M1 assembly. Set `auditGeometry` in `content/loading-bay/room-study.settings.json` to enable construction captures. No environment variable selects the scene or host behaviour.

## Licence and asset notice

Repository-authored code and documentation are [MIT licensed](LICENSE). Doom level data, textures, sprites and sounds were derived from the original Doom shareware WAD. That original work remains owned by id Software and/or its respective rights holders; those assets are outside the MIT licence. Other third-party assets retain their own notices, including Kenney CC0 material.

This is an unofficial, free test repository, unaffiliated with or endorsed by the original rights holders. Providing it free does not change third-party ownership or grant additional rights to their assets. [Source provenance](docs/source-provenance.md) records the source identities and derived files.

## Documentation

- [Gameplay ownership](docs/gameplay-design.md)
- [Design](docs/design.md)
- [Onboarding](docs/downstream-onboarding.md)
- [Game session protocol](docs/game-session-protocol.md)
- [Optional room study](docs/room-study.md)
- [Spatial inspection](docs/spatial-inspection.md)
- [Offline recipe scanner](docs/e1m1-recipe-scanner.md)
