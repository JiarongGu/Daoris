/**
 * The verdicts of quick steps, kept under `local/` (git ignores it): the last run's, and the base's (main at the merge base with
 * main, `tools/quick/base.mjs`). A run reads which of its reds the base stood in.
 */
import { mkdirSync, readFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { writeAtomic } from '../fsx.mjs';

export const LAST_FILE = 'local/quick-verdicts.json';
export const BASE_FILE = 'local/quick-base.json';

/** The record a file holds, or null where it is absent or does not read: no base is said as no base, never as a green one. */
export function readVerdicts(file) {
  try {
    const record = JSON.parse(readFileSync(file, 'utf8'));
    return record && typeof record.steps === 'object' ? record : null;
  } catch {
    return null;
  }
}

export function writeVerdicts(file, record) {
  mkdirSync(dirname(file), { recursive: true });
  writeAtomic(file, `${JSON.stringify(record, null, 2)}\n`);
}

/** Each red of `verdicts` placed: `base` where the base is red at it, `new` where it passes or has no verdict, `unknown` with no base. */
export function judge(verdicts, base) {
  const placed = { new: [], base: [], unknown: [] };
  for (const [step, verdict] of Object.entries(verdicts)) {
    if (verdict !== 'fail') continue;
    if (!base) placed.unknown.push(step);
    else if (base.steps[step] === 'fail') placed.base.push(step);
    else placed.new.push(step);
  }
  return placed;
}

/** The lines that say each red and whose it is. */
export function say(placed) {
  return [
    ...placed.new.map((step) => `  NEW ${step}`),
    ...placed.base.map((step) => `  RED ON THE BASE ${step}`),
    ...placed.unknown.map((step) => `  RED (no base recorded) ${step}`),
  ];
}
