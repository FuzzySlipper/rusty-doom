import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile, mkdtemp, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  scan,
  boundaryLoops,
  orientedBoundaryLoops,
  writeScan,
  planSvg,
} from "../scan-e1m1-recipes.mjs";
const source = JSON.parse(
  await readFile(
    new URL("../../content/doom-e1m1/e1m1.intermediate.json", import.meta.url),
  ),
);

test("retains disconnected outer/hole loops without bridging; flags branches", () => {
  const vertices = [
    { x: 0, y: 0 },
    { x: 10, y: 0 },
    { x: 10, y: 10 },
    { x: 0, y: 10 },
    { x: 3, y: 3 },
    { x: 7, y: 3 },
    { x: 7, y: 7 },
    { x: 3, y: 7 },
  ];
  const edges = [
    [0, 1],
    [2, 1],
    [2, 3],
    [0, 3],
    [4, 5],
    [5, 6],
    [7, 6],
    [7, 4],
  ].map(([startVertex, endVertex], id) => ({ id, startVertex, endVertex }));
  const r = boundaryLoops(edges, vertices);
  assert.equal(r.loops.length, 2);
  assert.equal(r.warnings.length, 0);
  assert.deepEqual(
    r.loops.map((l) => Math.abs(l.signedArea)),
    [100, 16],
  );
  const branched = boundaryLoops(
    [...edges, { id: 8, startVertex: 0, endVertex: 4 }],
    vertices,
  );
  assert.equal(branched.loops.length, 0);
  assert.equal(branched.warnings.length, 1);
});
test("spawn scan retains exact texture offsets and distinguishes exterior context", () => {
  const r = scan(source);
  assert.deepEqual(
    r.sectors.filter((s) => s.selected).map((s) => s.id),
    [14, 15, 37, 38, 39, 40, 41],
  );
  assert(r.sectors.some((s) => !s.selected));
  for (const e of r.lines)
    assert.deepEqual(e.front, source.level.sidedefs[e.frontSidedef]);
  const stair = r.transitions.find(
    (t) => t.floorDelta === 8 || t.floorDelta === -8,
  );
  assert(stair);
  assert.match(stair.interpretation, /possible step or ledge/);
  assert.deepEqual(r.warnings, []);
  assert.equal(
    r.suggestions.find((s) => s.id === "region-37").parameters.footprintLoops
      .length,
    3,
  );
  assert(
    !r.suggestions.some(
      (s) =>
        s.sourceSectorIds.length === 1 &&
        !r.sectors.find((t) => t.id === s.sourceSectorIds[0]).selected,
    ),
  );
});
test("invalid scale, references and empty selection fail explicitly", () => {
  assert.throws(() => scan(source, { unitsPerEngineUnit: 0 }), /Invalid/);
  assert.throws(() => scan(source, { bounds: [0, 0, 1, 1] }), /No complete/);
  const bad = structuredClone(source);
  bad.level.linedefs[0].frontSidedef = 99999;
  assert.throws(() => scan(bad), /Invalid sidedef/);
  assert.match(
    planSvg(scan(source, { unitsPerEngineUnit: 32 })),
    /32 map units\/Engine unit/,
  );
});
test("rerunning updates generated outputs byte-stably and preserves human edits", async () => {
  const directory = await mkdtemp(join(tmpdir(), "e1m1-scan-"));
  try {
    const result = scan(source);
    assert.equal((await writeScan(directory, result)).refinementCreated, true);
    const before = await readFile(
      join(directory, "measurements.generated.json"),
      "utf8",
    );
    await writeFile(join(directory, "recipe.refined.json"), "manual work\n");
    assert.equal((await writeScan(directory, result)).refinementCreated, false);
    assert.equal(
      await readFile(join(directory, "recipe.refined.json"), "utf8"),
      "manual work\n",
    );
    assert.equal(
      await readFile(join(directory, "measurements.generated.json"), "utf8"),
      before,
    );
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

test("oriented face traversal separates touching lobes and rejects unbalanced input", () => {
  const v = [
    { x: 0, y: 0 },
    { x: 0, y: 1 },
    { x: 1, y: 1 },
    { x: 1, y: 0 },
    { x: 1, y: 2 },
    { x: 2, y: 2 },
    { x: 2, y: 1 },
  ];
  const pairs = [
    [0, 1],
    [1, 2],
    [2, 3],
    [3, 0],
    [2, 4],
    [4, 5],
    [5, 6],
    [6, 2],
  ];
  const e = pairs.map(([startVertex, endVertex], id) => ({
    id,
    startVertex,
    endVertex,
  }));
  const r = orientedBoundaryLoops(e, v);
  assert.deepEqual(r.warnings, []);
  assert.deepEqual(
    r.loops.map((l) => l.signedArea),
    [-1, -1],
  );
  assert.deepEqual(
    r.loops.map((l) => l.lineIds),
    [
      [0, 1, 2, 3],
      [4, 5, 6, 7],
    ],
  );
  assert.equal(orientedBoundaryLoops(e.slice(1), v).warnings.length, 1);
});

test("full map recovers every sector boundary exactly once without inventing edges", () => {
  const r = scan(source, { bounds: [-768, -4864, 3808, -2048] });
  assert.equal(r.sectors.filter((s) => s.selected).length, 85);
  assert.equal(r.lines.length, 475);
  assert.deepEqual(r.warnings, []);
  for (const sector of r.sectors) {
    const covered = sector.loops.flatMap((l) => l.lineIds);
    assert.equal(
      covered.length,
      new Set(covered).size,
      `duplicate edge in sector ${sector.id}`,
    );
    assert.deepEqual(
      covered.sort((a, b) => a - b),
      [...sector.lineIds].sort((a, b) => a - b),
    );
    for (const loop of sector.loops)
      for (let i = 0; i < loop.points.length; i++) {
        const e = r.lines.find((e) => e.id === loop.lineIds[i]);
        const [a, b] =
          e.frontSector === sector.id ? [e.start, e.end] : [e.end, e.start];
        assert.deepEqual(loop.points[i], a);
        assert.deepEqual(loop.points[(i + 1) % loop.points.length], b);
      }
  }
  assert.deepEqual(
    r.sectors.filter((s) => s.floorHeight === s.ceilingHeight).map((s) => s.id),
    [4, 68, 76, 81],
  );
});
