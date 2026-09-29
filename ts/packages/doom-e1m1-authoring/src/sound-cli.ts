import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";

import { directoryByName, hashBytes, wadLumpBytes } from "./textures.js";
import { decodeDmxSound, pcm8MonoToWav } from "./sounds.js";
import { decodeWad } from "./wad-decode.js";

const DEFAULT_WAD = "/home/research/doom.ts/public/doom1.wad";
const DEFAULT_OUT_DIR = resolve(new URL("../../../../content/doom-e1m1/sounds", import.meta.url).pathname);

/**
 * The E1M1 sound closure: one fire sound per E1M1 weapon and the door sounds
 * the canonical project names (`doom:DSDOROPN`, `doom:DSDORCLS`).
 */
export const E1M1_SOUND_LUMPS = ["DSDORCLS", "DSDOROPN", "DSPISTOL", "DSPUNCH", "DSSHOTGN"] as const;

export interface SoundManifestEntry {
  readonly name: string;
  readonly sourceLump: string;
  readonly sourceByteLength: number;
  readonly sourceSha256: string;
  readonly sampleRate: number;
  readonly sampleCount: number;
  readonly wavSha256: string;
  readonly wavByteLength: number;
}

export interface SoundManifest {
  readonly wadPath: string;
  readonly wadSha256: string;
  readonly wadByteLength: number;
  readonly format: "wav-pcm-u8-mono";
  readonly entries: readonly SoundManifestEntry[];
}

export function buildSoundArtifacts(wadPath: string = DEFAULT_WAD, outDir: string = DEFAULT_OUT_DIR): SoundManifest {
  const raw = readFileSync(wadPath);
  const buffer = raw.buffer.slice(raw.byteOffset, raw.byteOffset + raw.byteLength) as ArrayBuffer;
  const wad = decodeWad(buffer, { computeSha256: true });
  const directory = directoryByName(wad.entries);
  mkdirSync(outDir, { recursive: true });
  const entries = E1M1_SOUND_LUMPS.map((name): SoundManifestEntry => {
    const entry = directory.get(name);
    if (!entry) throw new Error(`WAD missing sound lump ${name}`);
    const lump = wadLumpBytes(buffer, entry);
    const sound = decodeDmxSound(lump);
    const wav = pcm8MonoToWav(sound);
    writeFileSync(join(outDir, `${name}.wav`), wav);
    return {
      name,
      sourceLump: name,
      sourceByteLength: lump.byteLength,
      sourceSha256: hashBytes(lump),
      sampleRate: sound.sampleRate,
      sampleCount: sound.samples.byteLength,
      wavSha256: hashBytes(wav),
      wavByteLength: wav.byteLength,
    };
  });
  const manifest: SoundManifest = {
    wadPath,
    wadSha256: wad.sha256!,
    wadByteLength: buffer.byteLength,
    format: "wav-pcm-u8-mono",
    entries,
  };
  writeFileSync(join(outDir, "manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
  return manifest;
}

const wadArg = process.argv.find((arg) => arg.startsWith("--wad="))?.slice("--wad=".length) ?? DEFAULT_WAD;
const outArg = process.argv.find((arg) => arg.startsWith("--out="))?.slice("--out=".length) ?? DEFAULT_OUT_DIR;

if (process.argv.includes("--check")) {
  // Rebuild beside the retained closure and require byte-identical output.
  const tmpDir = `${outArg}.tmpcheck`;
  try {
    buildSoundArtifacts(wadArg, tmpDir);
    for (const file of ["manifest.json", ...E1M1_SOUND_LUMPS.map((name) => `${name}.wav`)]) {
      if (!readFileSync(resolve(outArg, file)).equals(readFileSync(resolve(tmpDir, file)))) {
        throw new Error(`${resolve(outArg, file)} is stale; run sounds:generate`);
      }
    }
    console.log(`OK ${outArg} (${E1M1_SOUND_LUMPS.length} sounds)`);
  } finally {
    rmSync(tmpDir, { recursive: true, force: true });
  }
} else {
  const manifest = buildSoundArtifacts(wadArg, outArg);
  console.log(`Wrote ${manifest.entries.length} sounds to ${outArg}`);
}
