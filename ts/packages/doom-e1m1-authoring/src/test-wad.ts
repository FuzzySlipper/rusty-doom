import { existsSync } from "node:fs";
import test, { type TestFn } from "node:test";
// This is an offline authoring input, never a product behaviour setting.
export const WAD_PATH = process.env.DOOM1_WAD ?? "/home/agent/research/doom.ts/public/doom1.wad";
export const wadTest: typeof test = ((name: string, body: TestFn) =>
  test(name, { skip: !existsSync(WAD_PATH) && "operator shareware WAD is absent; set DOOM1_WAD" }, body)) as typeof test;
