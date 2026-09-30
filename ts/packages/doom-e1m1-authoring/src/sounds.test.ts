import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

import { directoryByName, wadLumpBytes } from "./textures.js";
import { decodeDmxSound, pcm8MonoToWav } from "./sounds.js";
import { decodeWad } from "./wad-decode.js";

import { WAD_PATH, wadTest } from "./test-wad.js";

test("DMX sounds drop 16 padding samples at each end", () => {
  const lump = new Uint8Array(8 + 40);
  const view = new DataView(lump.buffer);
  view.setUint16(0, 3, true);
  view.setUint16(2, 11025, true);
  view.setUint32(4, 40, true);
  for (let index = 0; index < 40; index++) lump[8 + index] = index;
  const sound = decodeDmxSound(lump);
  assert.equal(sound.sampleRate, 11025);
  assert.deepEqual([...sound.samples], [16, 17, 18, 19, 20, 21, 22, 23]);
  const wav = pcm8MonoToWav(sound);
  assert.equal(new TextDecoder().decode(wav.slice(0, 4)), "RIFF");
  assert.equal(new DataView(wav.buffer).getUint32(40, true), 8);
  assert.deepEqual([...wav.slice(44)], [...sound.samples]);
});

wadTest("DSPISTOL decodes to half a second at 11025 Hz", () => {
  const raw = readFileSync(WAD_PATH);
  const buffer = raw.buffer.slice(raw.byteOffset, raw.byteOffset + raw.byteLength) as ArrayBuffer;
  const entry = directoryByName(decodeWad(buffer).entries).get("DSPISTOL")!;
  const sound = decodeDmxSound(wadLumpBytes(buffer, entry));
  assert.equal(sound.sampleRate, 11025);
  assert.equal(sound.samples.byteLength, 5629);
});
