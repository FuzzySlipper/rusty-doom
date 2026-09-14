#!/usr/bin/env node
import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultBounds = [480, -3700, 1400, -2860];
const boundsOf = (points) => [
  Math.min(...points.map((p) => p.x)),
  Math.min(...points.map((p) => p.y)),
  Math.max(...points.map((p) => p.x)),
  Math.max(...points.map((p) => p.y)),
];
const inside = (p, b) =>
  p.x >= b[0] && p.y >= b[1] && p.x <= b[2] && p.y <= b[3];
const area = (points) =>
  points.reduce((a, p, i) => {
    const q = points[(i + 1) % points.length];
    return a + p.x * q.y - q.x * p.y;
  }, 0) / 2;
const escape = (s) =>
  String(s).replace(
    /[&<>"']/g,
    (c) =>
      ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        '"': "&quot;",
        "'": "&apos;",
      })[c],
  );

// Recover separate boundary loops, including holes. Never bridge a branch or open chain.
export function boundaryLoops(edges, vertices) {
  const incidence = new Map();
  edges.forEach((e, i) =>
    [e.startVertex, e.endVertex].forEach((v) =>
      incidence.set(v, [...(incidence.get(v) ?? []), i]),
    ),
  );
  const remaining = new Set(edges.map((_, i) => i));
  const loops = [],
    warnings = [];
  while (remaining.size) {
    const first = remaining.values().next().value;
    const component = new Set(),
      pending = [first];
    while (pending.length) {
      const i = pending.pop();
      if (component.has(i)) continue;
      component.add(i);
      remaining.delete(i);
      for (const v of [edges[i].startVertex, edges[i].endVertex])
        pending.push(...incidence.get(v).filter((j) => !component.has(j)));
    }
    if (
      [...component].some((i) =>
        [edges[i].startVertex, edges[i].endVertex].some(
          (v) => incidence.get(v).length !== 2,
        ),
      )
    ) {
      warnings.push(
        `Open or branched boundary: lines ${[...component].map((i) => edges[i].id).join(",")}; retained as lines only.`,
      );
      continue;
    }
    const ids = [],
      points = [];
    let edgeId = first,
      vertex = edges[first].startVertex;
    do {
      const e = edges[edgeId];
      ids.push(e.id);
      points.push(vertices[vertex]);
      vertex = e.startVertex === vertex ? e.endVertex : e.startVertex;
      edgeId = incidence.get(vertex).find((i) => i !== edgeId);
    } while (edgeId !== first);
    loops.push({
      lineIds: ids,
      points,
      bounds: boundsOf(points),
      signedArea: area(points),
    });
  }
  return { loops, warnings };
}

// Each edge is oriented with the owning sector on its right. At a touching
// vertex, follow the first outgoing ray counterclockwise from the incoming
// edge's reverse ray. This follows the same face instead of joining two lobes.
export function orientedBoundaryLoops(edges, vertices) {
  const outgoing = new Map(),
    incoming = new Map();
  const fail = (reason) => ({
    loops: [],
    warnings: [`${reason}; retained as lines only.`],
  });
  edges.forEach((e, i) => {
    outgoing.set(e.startVertex, [...(outgoing.get(e.startVertex) ?? []), i]);
    incoming.set(e.endVertex, [...(incoming.get(e.endVertex) ?? []), i]);
  });
  for (const vertex of new Set([...outgoing.keys(), ...incoming.keys()]))
    if (
      (outgoing.get(vertex)?.length ?? 0) !==
      (incoming.get(vertex)?.length ?? 0)
    )
      return fail("Open or inconsistently oriented boundary");
  const next = [];
  for (const e of edges) {
    const a = vertices[e.startVertex],
      b = vertices[e.endVertex];
    if (a.x === b.x && a.y === b.y) return fail("Zero-length boundary");
    const reverse = Math.atan2(a.y - b.y, a.x - b.x);
    const candidates = outgoing
      .get(e.endVertex)
      .map((i) => {
        const c = vertices[edges[i].endVertex];
        const turn =
          (Math.atan2(c.y - b.y, c.x - b.x) - reverse + Math.PI * 2) %
          (Math.PI * 2);
        return { i, turn };
      })
      .sort((a, b) => a.turn - b.turn);
    if (
      candidates.length > 1 &&
      Math.abs(candidates[0].turn - candidates[1].turn) < 1e-10
    )
      return fail("Coincident outgoing boundary rays");
    next.push(candidates[0].i);
  }
  if (new Set(next).size !== edges.length)
    return fail("Ambiguous face continuation");
  const remaining = new Set(edges.map((_, i) => i)),
    loops = [];
  while (remaining.size) {
    const first = remaining.values().next().value,
      lineIds = [],
      points = [];
    let i = first;
    do {
      remaining.delete(i);
      lineIds.push(edges[i].id);
      points.push(vertices[edges[i].startVertex]);
      i = next[i];
    } while (i !== first);
    if (points.length < 3 || Math.abs(area(points)) < 1e-8)
      return fail("Degenerate boundary loop");
    loops.push({
      lineIds,
      points,
      bounds: boundsOf(points),
      signedArea: area(points),
    });
  }
  return { loops, warnings: [] };
}

export function scan(
  input,
  { bounds = defaultBounds, unitsPerEngineUnit = 16 } = {},
) {
  if (
    bounds.length !== 4 ||
    !bounds.every(Number.isFinite) ||
    bounds[0] >= bounds[2] ||
    bounds[1] >= bounds[3] ||
    !Number.isFinite(unitsPerEngineUnit) ||
    unitsPerEngineUnit <= 0
  )
    throw new Error("Invalid bounds or scale");
  const l = input.level;
  if (l?.mapName !== "E1M1")
    throw new Error("Expected E1M1 intermediate export");
  for (const p of l.vertices)
    if (![p.x, p.y].every(Number.isFinite))
      throw new Error("Non-finite vertex");
  for (const s of l.sectors)
    if (![s.floorHeight, s.ceilingHeight, s.lightLevel].every(Number.isFinite))
      throw new Error("Non-finite sector measurement");
  const lines = l.linedefs.map((e, id) => {
    for (const key of ["startVertex", "endVertex"])
      if (!Number.isInteger(e[key]) || !l.vertices[e[key]])
        throw new Error(`Invalid vertex on line ${id}`);
    const sector = (i) => {
      if (i === -1) return null;
      if (
        !Number.isInteger(i) ||
        !l.sidedefs[i] ||
        !l.sectors[l.sidedefs[i].sector]
      )
        throw new Error(`Invalid sidedef on line ${id}`);
      return l.sidedefs[i].sector;
    };
    return {
      id,
      ...e,
      frontSector: sector(e.frontSidedef),
      backSector: sector(e.backSidedef),
    };
  });
  const all = l.sectors.map((s, id) => {
    const edges = lines.filter(
      (e) =>
        e.frontSector !== e.backSector &&
        (e.frontSector === id || e.backSector === id),
    );
    const points = edges.flatMap((e) => [
      l.vertices[e.startVertex],
      l.vertices[e.endVertex],
    ]);
    return {
      id,
      ...s,
      bounds: points.length ? boundsOf(points) : null,
      lineIds: edges.map((e) => e.id),
      ...orientedBoundaryLoops(
        edges.map((e) =>
          e.frontSector === id
            ? e
            : { ...e, startVertex: e.endVertex, endVertex: e.startVertex },
        ),
        l.vertices,
      ),
      selected: points.length > 0 && points.every((p) => inside(p, bounds)),
    };
  });
  const selected = new Set(all.filter((s) => s.selected).map((s) => s.id));
  if (!selected.size)
    throw new Error(
      "No complete sectors inside bounds; enlarge the scan region",
    );
  const relevant = lines.filter(
    (e) => selected.has(e.frontSector) || selected.has(e.backSector),
  );
  const neighbors = new Set(
    relevant
      .flatMap((e) => [e.frontSector, e.backSector])
      .filter((id) => id !== null && !selected.has(id)),
  );
  const transitions = relevant
    .filter(
      (e) =>
        e.frontSector !== null &&
        e.backSector !== null &&
        e.frontSector !== e.backSector,
    )
    .map((e) => {
      const a = l.sectors[e.frontSector],
        b = l.sectors[e.backSector];
      const p = l.vertices[e.startVertex],
        q = l.vertices[e.endVertex];
      const floorDelta = b.floorHeight - a.floorHeight;
      const openHeight =
        Math.min(a.ceilingHeight, b.ceilingHeight) -
        Math.max(a.floorHeight, b.floorHeight);
      return {
        lineId: e.id,
        sectors: [e.frontSector, e.backSector],
        width: Math.hypot(q.x - p.x, q.y - p.y),
        floorDelta,
        openHeight,
        movementBlocking: !!(e.flags & 1),
        lineType: e.lineType,
        interpretation:
          openHeight <= 0
            ? "closed boundary; possible moving sector"
            : e.flags & 1
              ? "height opening with blocking flag"
              : floorDelta
                ? "height transition; possible step or ledge"
                : "open connection; may only divide lighting/materials",
      };
    });
  const suggestions = all
    .filter((s) => s.selected)
    .map((s) => ({
      id: `region-${s.id}`,
      kind: "floor-and-ceiling-region",
      confidence: "measured footprint; architectural role unresolved",
      sourceSectorIds: [s.id],
      sourceLineIds: s.lineIds,
      parameters: {
        footprintLoops: s.loops.map((p) => p.points),
        floor: s.floorHeight,
        ceiling: s.ceilingHeight,
        floorTexture: s.floorTexture,
        ceilingTexture: s.ceilingTexture,
      },
      refinement:
        "Name/group this region. Preserve polygon and holes until choosing boxes, slabs or subtractive shapes. Bounds are not a fitted solid.",
    }));
  for (const t of transitions.filter(
    (t) => t.floorDelta || t.openHeight <= 0 || t.width <= 128,
  ))
    suggestions.push({
      id: `boundary-${t.lineId}`,
      kind: t.floorDelta ? "step-or-ledge-candidate" : "opening-candidate",
      confidence: "tentative",
      sourceSectorIds: t.sectors,
      sourceLineIds: [t.lineId],
      parameters: t,
      refinement:
        "Check neighboring boundaries and first-person view; dimensions alone do not establish stairs, windows or doors.",
    });
  const smallLoops = all
    .filter((s) => s.selected)
    .flatMap((s) =>
      s.loops
        .filter(
          (p) =>
            p.bounds[2] - p.bounds[0] <= 64 && p.bounds[3] - p.bounds[1] <= 64,
        )
        .map((p) => ({ sectorId: s.id, ...p })),
    );
  const groups = Map.groupBy(
    smallLoops,
    (p) => `${p.bounds[2] - p.bounds[0]}x${p.bounds[3] - p.bounds[1]}`,
  );
  for (const [size, group] of groups)
    if (group.length > 1)
      suggestions.push({
        id: `repeat-${size}`,
        kind: "repeated-small-boundary-candidate",
        confidence: "tentative; may be holes or decoration",
        sourceSectorIds: [...new Set(group.map((p) => p.sectorId))],
        sourceLineIds: group.flatMap((p) => p.lineIds),
        parameters: { size, instances: group.map((p) => p.bounds) },
        refinement:
          "Inspect whether these are supports. Do not fill holes automatically.",
      });
  return {
    schemaVersion: 1,
    source: input.source,
    selection: {
      bounds,
      rule: "Complete sector boundaries inside bounds; adjacent sectors retained as context only",
    },
    coordinates: {
      units: "Doom map units",
      unitsPerEngineUnit,
      conversion:
        "Engine X = map x / scale; Engine Z = -map y / scale; Engine Y = height / scale. No origin shift.",
    },
    sectors: all.filter((s) => selected.has(s.id) || neighbors.has(s.id)),
    lines: relevant.map((e) => ({
      ...e,
      start: l.vertices[e.startVertex],
      end: l.vertices[e.endVertex],
      front: e.frontSidedef < 0 ? null : l.sidedefs[e.frontSidedef],
      back: e.backSidedef < 0 ? null : l.sidedefs[e.backSidedef],
    })),
    things: l.things.filter((p) => inside(p, bounds)),
    transitions,
    suggestions,
    warnings: all
      .filter((s) => s.selected)
      .flatMap((s) => s.warnings.map((w) => `Sector ${s.id}: ${w}`)),
  };
}

function labelPoint(sector, lines) {
  const [a, b, c, d] = sector.bounds;
  let best = null,
    clearance = -1;
  for (let i = 0; i < 32; i++)
    for (let j = 0; j < 32; j++) {
      const p = {
        x: a + ((i + 0.5) * (c - a)) / 32,
        y: b + ((j + 0.5) * (d - b)) / 32,
      };
      let interior = false;
      for (const loop of sector.loops)
        for (let k = 0; k < loop.points.length; k++) {
          const u = loop.points[k],
            v = loop.points[(k + 1) % loop.points.length];
          if (
            u.y > p.y !== v.y > p.y &&
            p.x < ((v.x - u.x) * (p.y - u.y)) / (v.y - u.y) + u.x
          )
            interior = !interior;
        }
      if (!interior) continue;
      const distance = Math.min(
        ...lines
          .filter((e) => sector.lineIds.includes(e.id))
          .map((e) => {
            const dx = e.end.x - e.start.x,
              dy = e.end.y - e.start.y;
            const t = Math.max(
              0,
              Math.min(
                1,
                ((p.x - e.start.x) * dx + (p.y - e.start.y) * dy) /
                  (dx * dx + dy * dy || 1),
              ),
            );
            return Math.hypot(
              p.x - e.start.x - t * dx,
              p.y - e.start.y - t * dy,
            );
          }),
      );
      if (distance > clearance) {
        best = p;
        clearance = distance;
      }
    }
  return best;
}

export function planSvg(scan) {
  const [x0, y0, x1, y1] = scan.selection.bounds,
    width = x1 - x0,
    height = y1 - y0;
  const colors = [
    "#32566b",
    "#536442",
    "#775633",
    "#584d76",
    "#286c69",
    "#6a4356",
  ];
  const overview = scan.sectors.filter((s) => s.selected).length > 25;
  const fontScale = overview ? width / 1600 : 1;
  let body = "";
  for (const s of scan.sectors.filter((s) => s.selected)) {
    const path = s.loops
      .map(
        (l) =>
          l.points.map((p, i) => `${i ? "L" : "M"}${p.x},${-p.y}`).join(" ") +
          " Z",
      )
      .join(" ");
    body += `<path d="${path}" fill="${colors[s.id % colors.length]}" fill-rule="evenodd" stroke="none"/>`;
  }
  for (const e of scan.lines)
    body += `<line x1="${e.start.x}" y1="${-e.start.y}" x2="${e.end.x}" y2="${-e.end.y}" stroke="${e.backSector === null ? "#eef0e8" : "#f3b85b"}" stroke-width="${e.backSector === null ? 3 : 1.5}" ${e.backSector === null ? "" : 'stroke-dasharray="6 4"'}/>`;
  const occupied = [];
  for (const s of scan.sectors.filter((s) => s.selected)) {
    const p = labelPoint(s, scan.lines);
    if (!p) continue;
    const text = overview
      ? `S${s.id}`
      : `S${s.id} · ${s.floorHeight}/${s.ceilingHeight}`;
    const w = text.length * 8.5 * fontScale,
      h = 19 * fontScale;
    let label = { x: p.x, y: -p.y };
    if (overview) {
      const candidates = [];
      for (let row = -12; row <= 12; row++)
        for (let col = -12; col <= 12; col++)
          candidates.push({
            x: p.x + col * 22 * fontScale,
            y: -p.y + row * 22 * fontScale,
            d: row * row + col * col,
          });
      const chosen = candidates
        .sort((a, b) => a.d - b.d)
        .find(
          (q) =>
            q.x - w / 2 >= x0 &&
            q.x + w / 2 <= x1 &&
            q.y - h >= -y1 &&
            q.y <= -y0 &&
            occupied.every(
              (b) =>
                q.x + w / 2 + 4 * fontScale < b[0] ||
                q.x - w / 2 - 4 * fontScale > b[2] ||
                q.y + 4 * fontScale < b[1] ||
                q.y - h - 4 * fontScale > b[3],
            ),
        );
      if (chosen) label = chosen;
      occupied.push([label.x - w / 2, label.y - h, label.x + w / 2, label.y]);
      if (Math.hypot(label.x - p.x, label.y + p.y) > 1)
        body += `<line x1="${p.x}" y1="${-p.y}" x2="${label.x}" y2="${label.y - h / 3}" stroke="#becbd6" stroke-width="${fontScale}"/>`;
    }
    body += `<text x="${label.x}" y="${label.y}" font-size="${14 * fontScale}" text-anchor="middle" fill="white" paint-order="stroke" stroke="#17202b" stroke-width="${4 * fontScale}">${text}<title>Floor ${s.floorHeight}, ceiling ${s.ceilingHeight}; ${escape(s.floorTexture)} / ${escape(s.ceilingTexture)}</title></text>`;
  }
  for (const t of scan.things.filter((t) => t.type === 1))
    body += `<circle cx="${t.x}" cy="${-t.y}" r="9" fill="#7cfc93"/><text x="${t.x + 14}" y="${-t.y}" font-size="16" fill="#7cfc93">spawn</text>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${x0 - 30 * fontScale} ${-y1 - 90 * fontScale} ${width + 60 * fontScale} ${height + 160 * fontScale}" width="${overview ? 1800 : 1000}" height="${overview ? Math.round((1800 * (height + 160 * fontScale)) / (width + 60 * fontScale)) : 1000}"><rect x="${x0 - 30 * fontScale}" y="${-y1 - 90 * fontScale}" width="${width + 60 * fontScale}" height="${height + 160 * fontScale}" fill="#17202b"/><g font-family="monospace"><text x="${x0}" y="${-y1 - 55 * fontScale}" fill="white" font-size="${24 * fontScale}">E1M1 measured geometry</text><text x="${x0}" y="${-y1 - 25 * fontScale}" fill="#becbd6" font-size="${14 * fontScale}">${overview ? "Sector IDs; heights and materials in report" : "Sector labels: ID · floor/ceiling (Doom units)"}</text>${body}<text x="${x0}" y="${-y0 + 35 * fontScale}" fill="#becbd6" font-size="${13 * fontScale}">White: one-sided boundary · amber: two-sided boundary (not necessarily passable)</text><text x="${x0}" y="${-y0 + 55 * fontScale}" fill="#becbd6" font-size="${13 * fontScale}">${width} × ${height} map units · ${scan.coordinates.unitsPerEngineUnit} map units/Engine unit</text></g></svg>\n`;
}

export async function writeScan(directory, result) {
  await mkdir(directory, { recursive: true });
  const { suggestions, ...measurements } = result;
  await writeFile(
    resolve(directory, "measurements.generated.json"),
    JSON.stringify(measurements, null, 2) + "\n",
  );
  await writeFile(
    resolve(directory, "suggestions.generated.json"),
    JSON.stringify(
      { schemaVersion: 1, source: result.source, suggestions },
      null,
      2,
    ) + "\n",
  );
  await writeFile(resolve(directory, "plan.generated.svg"), planSvg(result));
  const report = [
    "# E1M1 geometry scan",
    "",
    "Generated measurements and tentative interpretations. Edit recipe.refined.json; this report is regenerated.",
    "",
    "![Measured plan](plan.generated.svg)",
    "",
    "## Selected regions",
    "",
    "| Sector | Floor / ceiling | Floor material | Boundary loops |",
    "|---|---|---|---|",
    ...result.sectors
      .filter((s) => s.selected)
      .map(
        (s) =>
          `| ${s.id} | ${s.floorHeight} / ${s.ceilingHeight} | ${s.floorTexture} | ${s.loops.length}${s.warnings.length ? " — unresolved boundary" : ""} |`,
      ),
    "",
    "## Suggested features",
    "",
    ...suggestions.map(
      (s) =>
        `- **${s.id}**: ${s.kind}; sectors ${s.sourceSectorIds.join(", ")}; lines ${s.sourceLineIds.join(", ")}. ${s.confidence}.`,
    ),
    "",
    "## Unresolved measurements",
    "",
    ...(result.warnings.length
      ? result.warnings.map((w) => `- ${w}`)
      : ["None."]),
    "",
    "Unresolved regions are not filled or labelled as polygons in the plan. Their measured boundary lines remain visible. Adjacent sectors are context only. Two-sided lines do not guarantee walkability; inspect flags, clearance, height changes and dynamic behavior.",
    "",
    "Box fitting, stair grouping, moving-door behavior, UV layout and Engine mesh generation are intentionally manual in this first scanner. Suggestions are evidence for refinement, not executable or complete geometry.",
    "",
  ].join("\n");
  await writeFile(resolve(directory, "report.generated.md"), report);
  let refinementCreated = true;
  try {
    await writeFile(
      resolve(directory, "recipe.refined.json"),
      JSON.stringify(
        {
          schemaVersion: 1,
          purpose:
            "Human-owned interpretation; scanner never overwrites this file. Coordinates remain Doom units until explicitly converted.",
          source: result.source,
          decisions: [],
          features: suggestions,
        },
        null,
        2,
      ) + "\n",
      { flag: "wx" },
    );
  } catch (e) {
    if (e.code !== "EEXIST") throw e;
    refinementCreated = false;
  }
  return { refinementCreated };
}

async function main() {
  const args = process.argv.slice(2),
    options = {};
  for (let i = 0; i < args.length; i += 2) {
    if (args[i] === "--full") {
      options.full = true;
      i--;
      continue;
    }
    if (
      !["--input", "--out", "--bounds", "--scale"].includes(args[i]) ||
      !args[i + 1]
    )
      throw new Error(
        "Usage: node scripts/scan-e1m1-recipes.mjs [--input export.json] [--out directory] [--bounds minX,minY,maxX,maxY] [--scale 16] [--full]",
      );
    options[args[i]] = args[i + 1];
  }
  const path = resolve(
    options["--input"] ??
      resolve(root, "content/doom-e1m1/e1m1.intermediate.json"),
  );
  const bytes = await readFile(path);
  const input = JSON.parse(bytes);
  if (options.full && options["--bounds"])
    throw new Error("Choose --full or --bounds");
  const result = scan(input, {
    bounds: options.full
      ? boundsOf(input.level.vertices)
      : options["--bounds"]?.split(",").map(Number),
    unitsPerEngineUnit:
      options["--scale"] === undefined ? 16 : Number(options["--scale"]),
  });
  result.source = {
    ...result.source,
    intermediateSha256: createHash("sha256").update(bytes).digest("hex"),
  };
  const out = resolve(
    options["--out"] ??
      resolve(
        root,
        options.full
          ? "docs/experiments/e1m1-full-scan"
          : "docs/experiments/e1m1-spawn-scan",
      ),
  );
  const status = await writeScan(out, result);
  console.log(
    JSON.stringify(
      {
        out,
        selectedSectors: result.sectors
          .filter((s) => s.selected)
          .map((s) => s.id),
        suggestions: result.suggestions.length,
        warnings: result.warnings,
        ...status,
      },
      null,
      2,
    ),
  );
}
if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
)
  main().catch((e) => {
    console.error(e.message);
    process.exitCode = 1;
  });
