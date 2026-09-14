# Construction-study surface audit

Captured using Engine `0.1.0-dev.8c20a96d10ef` and its public ImplicitSurfaces audit API. `before.json` contains 106 pieces before trimming; `after.json` contains 116 pieces after trimming and adding the courtyard. `south-passage.json` contains the subsequent 133-piece scene with the southern loop and first-door ceiling transition. These are initial authored placements, with doors closed, not live moving-door poses.

| Report | Exposed classifications | Sum of reported exposed area | Largest reported exposed area |
| --- | ---: | ---: | ---: |
| Before | 35 | 40.2627788228422 | 4.799979587788852 |
| After | 16 | 0.000054783793239137035 | 0.000016021581601677956 |
| Southern passage | 17 | 0.00005616538084996431 | 0.000016021581601677956 |

Areas are approximate world-unit squared patch estimates, may count faces separately, and are not camera-visible pixel areas. Remaining findings are tiny contact seams; no remaining exposed report exceeds 0.001 square units. This observation is not an Engine guarantee or an automatic acceptance threshold. The Engine documents sampled/DC/curved/partial-coverage uncertainty; depth precision and shader/texture artifacts are outside this audit.

Run an explicit authoring session (stop any existing process on the chosen port first):

```sh
LOADING_BAY_PORT=4396 LOADING_BAY_STUDY_AUDIT=1 ./scripts/run-room-study.sh --debugger
```

After startup, invoke `loading-bay.geometry-audit` in the Engine debug console, or use its supported transport:

```sh
curl --fail-with-body -H 'Content-Type: text/plain; charset=utf-8' \
  --data 'loading-bay.geometry-audit' \
  http://127.0.0.1:4396/__rusty/product/runtime/debug/execute > audit.json
```

Capture is opt-in; the first command analyses the captured scene synchronously and caches its JSON report, then releases captured Engine fields/meshes. Subsequent reads return that snapshot. Restart after authoring changes to capture a new one. Normal launch does neither capture nor analysis. Use `--debugger` only for this authoring operation: the full-scene analysis exceeded normal worker callback deadlines; attempting it during construction also exceeded the startup deadline. The default supervised play lane retains both deadlines. This expensive operation temporarily stops simulation progress; it is not a live per-frame metric.

Product code owns piece labels (including repeated-name ordinals), deterministic IDs and JSON projection. Engine owns copying captured geometry/fields, candidate filtering, classifications and approximate areas. No geometry is changed by the audit itself.
