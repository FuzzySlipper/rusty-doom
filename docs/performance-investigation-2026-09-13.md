# Loading Bay performance investigation — 2026-09-13

The measured slow path is browser rendering/submission, with a smaller amount of unnecessary product publication work. This investigation did not change gameplay, renderer policy, or the immutable Engine package.

## Live evidence

Direct parent-operated native Wolf session `fb547653-91b5-4943-90bb-f0398be9230e`, CoreCLR pair `0.1.0-dev.2e4255bd3ad5`, 1280×720. The existing live game state was retained. Native look and W movement visibly changed the scene. Original captures live under `/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/fb547653-91b5-4943-90bb-f0398be9230e/`; initial `831c9b88-d264-4011-9527-99fe90d5c905.png`, movement `bb150f0e-8cd2-40ed-8558-794eb5d780db.png`. Cleanup returned `released: true`, no errors.

Raw diagnostics, renderer samples, 15-second managed trace, Speedscope export, and cleanup receipts are retained locally at `.runtime/performance/2026-09-13/`. The debug catalog supplied the commands and the packaged live-debug client supplied the HTTP routes; no invented inspection endpoint or injected game state was used.

| Measurement | Observation |
| --- | --- |
| C# callback | approximately 1.7–1.8 ms median, 2.5 ms p95 |
| Character step | 0.075–0.12 ms in cited samples; 21 narrow-phase candidates |
| Rust post-callback | approximately 0.42 ms |
| Worker output conversion / encode-write | approximately 0.13 / 0.10 ms |
| Browser product update rate | approximately 60 Hz received and applied |
| Browser apply latency | 2 ms median, 4 ms p95 |
| Render submission rate | approximately 39.5–41 Hz in the measured view |
| Synchronous backend submission | 10 ms median, 7–20 ms range over 44 admitted attempts |
| Render admission | 44 admitted, 20 backend-blocked attempts across four recent-attempt windows |
| Draws / triangles | 1,904–1,931 draws, approximately 12,700 triangles in this view |
| Resident resources | 196 geometries, 2,722 materials, including 2,721 voxel specializations |

The runtime reported no dropped realtime steps or queued input backlog. Voxel residency and scene-presentation update calls were zero in the sampled callbacks: this is not evidence of per-frame remeshing. The output ring being at its retained capacity is not itself proof of an unconsumed backlog.

GPU timers were unavailable; completion-fence mode was active with one pending submission permitted. Backend-blocked callbacks and 17/33 ms render intervals demonstrate admission/pacing effects, but do not isolate physical GPU time from browser/driver scheduling. Wolf's video stream is 30 fps; that is separate from the renderer's own submission counter. Other host workloads were present, so these are operational measurements, not an isolated hardware benchmark.

## Confirmed source paths and priorities

1. **Engine voxel draw grouping and material reuse.** `rust/crates/svc-mesh/src/lib.rs` partitions greedy geometry by `(material_slot, direction)`. `rust/crates/render-projection/src/voxel.rs` maps those groups to effective materials without merging groups that resolve identically. `render/packages/renderer-three/src/three-renderer.ts` creates a fresh material for each uploaded group and adds a corresponding BufferGeometry group. Loading Bay supplies 49 authored materials, yet the realized groups create 2,721 material instances. The next owning optimization is to coalesce compatible effective-material groups and reuse equivalent material instances while preserving directional overrides, UVs, alpha policy, and chunk culling. This is Engine work, not a downstream renderer replacement. The exact improvement needs an upstream before/after test; it is not measured here.

2. **Product HUD publication.** Every `LoadingBaySession.Update` calls `Publish`, and `LoadingBayHudProjection.Publish` rebuilds a complete structured snapshot including static catalog/program/material data, all pickup/enemy bindings, tuning, and the bounded fact journal. Closing the Debug block stops diagnostic widget polling, but does not stop this C# publication. A 15-second sampled managed trace attributes approximately 42% of callback stacks to HUD construction/publication. Separate immutable content from changing HUD state and avoid retransmitting unchanged data, preserving attachment/restart baselines and the actual consumer contract.

3. **Trigger reconciliation.** Pickup and world-interaction coordinators separately project/reconcile their trigger state each admitted step. Reconciliation accounts for approximately 30% of sampled callback stacks. Inspect their distinct scopes and required movement/activation/restore semantics before attempting shared reconciliation or suppressing work for stationary players; do not bypass Engine overlap authority.

4. **Unchanged exit appearance publication.** `LoadingBayExitButtonAnimation.Publish` republishes the same appearance snapshot every update. Its branch accounts for approximately 11% of sampled callback stacks. Use lifecycle/change-driven publication with correct fresh-browser reattachment and restart realization rather than suppressing required baselines.

The managed trace samples stacks, including generated SDK/native boundary calls; its percentages are not precise pure-C# CPU accounting, do not add across nested frames, and are not percentages of the entire browser frame. The Engine callback timers establish absolute duration. These product costs merit cleanup, but current evidence does not support blaming a C# simulation stall for the roughly 40 Hz renderer.

Native Debug clicking after Escape was not reliable in this direct probe; clicks reacquired pointer control. Performance data came from the same supported debug transport directly. This investigation does not upgrade the prior console/refocus acceptance status.
