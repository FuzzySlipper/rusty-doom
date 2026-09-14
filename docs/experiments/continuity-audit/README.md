# Dogleg continuity audit

Engine pair `0.1.0-dev.e4b95f4207dc` (task #8259), verified with the release's `verify-pair.sh`. The public `ReadMeshIntegrity`, `ReadExpectedJoin` and `ReadEnclosure` calls share the existing opt-in capture in `LoadingBayStudyAudit`. Product code declares intended contact patches and intentional corridor openings, and writes the copied report with `Utf8JsonWriter` for CoreCLR/NativeAOT compatibility. Normal play performs no capture or analysis.

| Report | Ceiling/wall contact | Floor/wall contact | Regional enclosure |
| --- | --- | --- | --- |
| before.json | MissingJoinSurface | MissingJoinSurface | EnclosureLeak, floor edge near (5.8125, 0, 33.9375) |
| slab-contact.json | JoinGap, maximum sampled width 0.0307544 | No finding | Only declared intentional opening |
| after.json | No finding | No finding | Only declared intentional opening |

The third declared join, between the dogleg and vestibule ceiling, has no finding throughout. All these queries completed at their declared discrete resolution. The patch sample spacing is 0.025, tolerance is 0.05 extraction cells, search radius 0.4, budget 20,000. Reported resolution also reflects source extraction spacing; it is not just the requested sample spacing. The enclosure uses 0.125 grid spacing, a 1,500,000-cell budget, domain (-3,-1,19)..(13,8,36), seed (7,1,32), and explicitly capped connections to the rest of the level. Initial door poses are closed. A clean report does not establish every door pose, the entire level, or gaps below sampling resolution.

The dogleg floor and ceiling previously ended at the walls' inner edges. Extending slabs across the wall thickness closed the sampled floor leak. The residual ceiling gap was closed with an inset wall-top seat inside the ceiling slab. The seat ends inside the slab's exterior and portal boundaries; extending the entire wall height instead produced coincident exterior faces and was rejected by the overlap audit. The visible ceiling remains at y=4.5.

The final overlap audit retains 15 tiny exposed classifications, total approximate area 0.0000532528, largest 0.0000160216; none exceeds 0.001 square units. No automatic threshold or zero-defect certificate is implied. Full-scene integrity still reports two preexisting NonManifold regions on the southern stair underside and western stair/landing underside. Stable labels in the final report identify them; these remain diagnostic findings, not established visible leaks or an assigned extractor cause.

Reproduce on an available owned port (4397 was used; check listener ownership first):

```sh
LOADING_BAY_BIND_HOST=127.0.0.1 LOADING_BAY_PORT=4397 LOADING_BAY_STUDY_AUDIT=1 \
  ./scripts/run-room-study.sh --debugger
```

After HTTP readiness, POST plain text `loading-bay.geometry-audit` to `/__rusty/product/runtime/debug/execute`, or invoke it from the Engine console. The first call runs both overlap and continuity analysis and caches the initial snapshot. Restart to recapture changed geometry. `--debugger` is the supported long synchronous authoring lane; return to the default supervised launcher for ordinary play.

Current command projection: `loading-bay.geometry-audit` returns overlap output and `loading-bay.continuity-audit` returns integrity, joins and enclosures. Both read the same cached initial snapshot. They are separated to fit the packaged 64 KiB per-debug-response limit; historical combined JSON files above remain unchanged. Current study north is world -Z.
