// Doom DMX digital sound lumps (`DS*`) to PCM WAV. A DMX lump is a 2-byte
// format tag (3), 2-byte sample rate, 4-byte sample count, then that many
// unsigned 8-bit mono samples, the first and last 16 of which are padding.

const DMX_FORMAT = 3;
const DMX_HEADER_BYTES = 8;
const DMX_PADDING_SAMPLES = 16;
const WAV_HEADER_BYTES = 44;

export interface DmxSound {
  readonly sampleRate: number;
  /** Unsigned 8-bit mono PCM without the DMX padding. */
  readonly samples: Uint8Array;
}

export function decodeDmxSound(lump: Uint8Array): DmxSound {
  if (lump.byteLength < DMX_HEADER_BYTES) throw new Error("DMX sound lump is shorter than its header");
  const view = new DataView(lump.buffer, lump.byteOffset, lump.byteLength);
  const format = view.getUint16(0, true);
  if (format !== DMX_FORMAT) throw new Error(`DMX sound format ${format} is not ${DMX_FORMAT}`);
  const sampleRate = view.getUint16(2, true);
  const count = view.getUint32(4, true);
  if (sampleRate === 0) throw new Error("DMX sound has no sample rate");
  if (count <= DMX_PADDING_SAMPLES * 2) throw new Error("DMX sound has no samples inside its padding");
  if (DMX_HEADER_BYTES + count > lump.byteLength) throw new Error("DMX sample count exceeds its lump");
  const start = DMX_HEADER_BYTES + DMX_PADDING_SAMPLES;
  const end = DMX_HEADER_BYTES + count - DMX_PADDING_SAMPLES;
  return { sampleRate, samples: lump.slice(start, end) };
}

/** A RIFF/WAVE file holding unsigned 8-bit mono PCM, byte-for-byte deterministic. */
export function pcm8MonoToWav(sound: DmxSound): Uint8Array {
  const out = new Uint8Array(WAV_HEADER_BYTES + sound.samples.byteLength);
  const view = new DataView(out.buffer);
  const ascii = (offset: number, text: string) => {
    for (let index = 0; index < text.length; index++) out[offset + index] = text.charCodeAt(index);
  };
  ascii(0, "RIFF");
  view.setUint32(4, out.byteLength - 8, true);
  ascii(8, "WAVE");
  ascii(12, "fmt ");
  view.setUint32(16, 16, true); // PCM fmt chunk size
  view.setUint16(20, 1, true); // PCM
  view.setUint16(22, 1, true); // mono
  view.setUint32(24, sound.sampleRate, true);
  view.setUint32(28, sound.sampleRate, true); // byte rate: 1 channel × 1 byte
  view.setUint16(32, 1, true); // block align
  view.setUint16(34, 8, true); // bits per sample
  ascii(36, "data");
  view.setUint32(40, sound.samples.byteLength, true);
  out.set(sound.samples, WAV_HEADER_BYTES);
  return out;
}
