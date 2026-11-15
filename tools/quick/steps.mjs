/**
 * The steps of `npm run quick`: the index and the contracts written, each check of the merge's `cli` gate (the `verify:checks` script's
 * `&&` chain, split here so the gate and quick read one list) but its time judgements, then the layers' census's source counts. Quick
 * judges no test time: the merge's gates and the full set judge it, on classes measured again alone, so load never reddens quick.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const BEFORE = ['node tools/orient-index.mjs', 'node tools/contracts.mjs --if-stale'];
const AFTER = ['node tools/layers-census.mjs --counts'];

/** The gate's steps that judge test time, which quick leaves out. */
export const TIME_JUDGED = ['node tools/time-ratchet.mjs'];
export const TIME_LINE = 'quick: judges no test time; the merge\'s gates and the full set judge it on classes measured again alone (node tools/time-ratchet.mjs, node tools/layers-census.mjs)';

/** The commands of the root's `verify:checks` script, in order. */
export function checkSteps(root) {
  const script = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).scripts?.['verify:checks'] ?? '';
  return script.split('&&').map((step) => step.trim()).filter((step) => step.length > 0);
}

/** Every step quick runs, in order, each once. */
export function quickSteps(root) {
  return [...new Set([...BEFORE, ...checkSteps(root).filter((step) => !TIME_JUDGED.includes(step)), ...AFTER])];
}
