# Optional room study

The supported product is the authored E1M1 voxel port. The room study is a separate optional experiment at `csharp/LoadingBay.RoomStudy/LoadingBay.RoomStudy.csproj`; its recipe gameplay, navigation and authoring audits are excluded from the default game's assembly.

Run `./scripts/run-room-study.sh`. The launcher uses ordinary `rusty dev` staging, builds the Angular HUD automatically, and defaults to `http://127.0.0.1:4395`. Append `--bind-host`, `--port`, `--output stream|window` or `--live-debug` as needed. There is no scene environment variable.

The study manually interprets E1M1 with Engine implicit surfaces, matching mesh collision, sprite animation and spatial services. It reuses the retained source-closed textures, sprites and sound effects. Its simplified stairs, doors, routes, combat and construction scale do not establish a faithful Doom port. WASD, mouse, E, Control, Shift and 1/2/3 are ordinary controls; Escape releases the pointer.

Set `auditGeometry` to `true` in `content/loading-bay/room-study.settings.json` for an explicit authoring run. With `--live-debug`, `loading-bay.geometry-audit` and `loading-bay.continuity-audit` inspect the cached initial closed-door construction. Normal play leaves this expensive capture disabled. Engine owns the geometry analysis; the study owns piece names and intended joins/openings.

Historical scans, audit reports, screenshots, gameplay experiments and previous engine-pair checks are preserved in Den `[doc: rusty-doom/campaign-8976-evidence-archive]`. They are historical observations, not current run instructions or a complete traversal certificate. Source manifests and original assets remain in `content/`; see [source provenance](source-provenance.md).
