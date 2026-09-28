# Capsule clearance and movement follow-up review

User-authorized follow-up to task 8721 and the previous steep-slope trial.

- Engine reuse: found that capsule queries included disabled and trigger entity
  colliders. Fixed in Engine before obstacle conversion, with cast/overlap
  regressions. Rereview found no remaining issue. Existing capsule APIs remain
  the authority; Doom does not implement collision or movement prediction.
- Existing product reuse: no findings. Clearance uses current feet/body-center,
  standing height, live controller tuning, and the same door collider records as
  normal player stepping. Support reporting does not promise reachability.
- Runtime trust: no findings after rereview of the native collider filter and
  feasible-crease correction. Reads do not advance time; no replay or teleport.

The controller unit regression reproduces discarded vertical escape with three
side contacts. A separate airborne steep-ramp integration case also passes but
is not an exact recreation of the prior Doom mesh-edge snag. See crew-services
`docs/playtest-evidence/doom-clearance-20260928/` for independent gameplay reports,
original captures, regression logs, and the final outcome.

Final SDK c/runtime k build, lifecycle exercise, semantic catalog check, and
downstream boundary audit pass.
