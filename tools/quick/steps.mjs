/**
 * The steps of `npm run quick`: the index and the contracts written, each check of the merge's `cli` gate (the steps of the
 * `verify:checks` script, the one list of them), then the layers' census. The script's `&&` chain is split here, so the gate and
 * the quick run read one list and a step added to it is run by both.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const BEFORE = ['node tools/orient-index.mjs', 'node tools/contracts.mjs --if-stale'];
const AFTER = ['node tools/layers-census.mjs'];

/** The commands of the root's `verify:checks` script, in order. */
export function checkSteps(root) {
  const script = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).scripts?.['verify:checks'] ?? '';
  return script.split('&&').map((step) => step.trim()).filter((step) => step.length > 0);
}

/** Every step quick runs, in order, each once. */
export function quickSteps(root) {
  return [...new Set([...BEFORE, ...checkSteps(root), ...AFTER])];
}
