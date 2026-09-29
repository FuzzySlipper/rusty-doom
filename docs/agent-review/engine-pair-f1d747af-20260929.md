# Engine pair f1d747afc01c review

Den task rusty-doom #8830 (with Engine #8821). Doom moves from pair
`e14db30ba217` to `f1d747afc01c`.

- **Engine reuse: no findings.** Every hunk deletes code or renames a type or
  call to what the Engine now provides. Nothing re-creates an Engine mechanism
  locally.
- **Existing product reuse: two low findings, both filed as #8845.**
  - The pickup `TriggerRevision` mirror is no longer read by any decision, and
    it already disagrees with the Engine. Removing it changes the save shape.
  - `MaximumPickupBindings` repeats the catalog's pickup count.
- **Runtime trust: one low finding, fixed.**
  - Pickup settlement retired the trigger, collected, and reactivated it on
    failure. That existed only because the revision-fenced call could fail.
    It now collects first and retires only on success; `Reactivate` and its
    `AggregateException` are gone.
  - The same reviewer verified the fix in a second round: the fact order and
    content are unchanged, and a throw leaves the pickup active and
    uncollected.
  - The same lane confirmed that dropping `InventoryEdit.Validate()` loses
    nothing, because `GrantCore` and `MaterializeUniqueCore` check every case
    the pickup preflight needs.
  - The hazard overlap readback's revision check predates this change and is
    in #8845.

**Checks.** `scripts/verify-csharp-spine.sh` passes: build with 0 warnings,
recipe and lifecycle exercises, NativeAOT. `node scripts/audit-boundary.mjs`
passes.

**Live check.** A CoreCLR run on the device audio path fired the pistol,
cleared the corridor troopers and opened the north-wing door with ordinary E.
The null-sink recording matches `DSPISTOL` and `DSDOROPN` at 0.999. Receipts
are in the Engine repo under `docs/evidence/doom-device-audio-8821/`. That run
came before the settlement reorder; the reorder touches only pickup
settlement, and the lifecycle exercise covers it.
