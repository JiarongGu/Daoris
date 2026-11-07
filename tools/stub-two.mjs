import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { stubTwoManifest } from './rehearsal-kit.mjs';

// The Stub Two phase of the family rehearsal: the kit's second maker declared by a file, which
// also names the two postures a manifest may carry beside `posture`, its words for asking and for full access.

/** The kit's second-maker manifest with its asking and full postures named. */
export function stubTwoDeclared(script) {
  const manifest = stubTwoManifest(script);
  return { ...manifest, harnesses: manifest.harnesses.map((harness) => ({ ...harness, askingPosture: 'ask', fullPosture: 'allow-all' })) };
}

/** Writes it as `plugin.json` into `folder`, the plugin's own folder under the scratch home's `plugins/`. */
export function declareStubTwo(folder, script) {
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, 'plugin.json'), `${JSON.stringify(stubTwoDeclared(script), null, 2)}\n`);
}

/** Whether `daoris agent list`'s words show the second maker's stub, declared by file. */
export const stubTwoListed = (text) => /stub-b/.test(text) && /declared by plugin `rehearsal\.stub-two`/.test(text);
