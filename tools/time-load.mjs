/**
 * How many heavy runs are under way as a test timing is taken, so the time ratchet judges only timings taken with the machine
 * to themselves (a timing taken under load is the load's, not the test's).
 *
 * The count is the machine's heavy-run admission (`tools/machine-admission.mjs`): the places held, the run's own included. A
 * record file is marked in `_fixtures/test-timing/load-marks.json` as `{ "<file>": { holds, at } }`, `holds` the most places held
 * while its run lasted and `at` the record's write time at the mark (a record written again after is unmarked). A record is
 * under load when more than `ALONE_HOLDS` places were held; one with no mark is judged as alone, as every record written before
 * this mark is. With no Daoris home nothing is shared and nothing counts.
 */
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { writeAtomic } from './fsx.mjs';
import { lookAt } from './machine-admission.mjs';

/** The most places a run may itself hold and still be alone: its own. */
export const ALONE_HOLDS = 1;

const MARKS = 'load-marks.json';

/** The heavy runs under way on the machine now: the live places held (0 with no Daoris home or no admission file). */
export function heldNow(home = process.env.DAORIS_HOME, seam = {}) {
  if (!home) return 0;
  try {
    return lookAt(home, seam).holds.length;
  } catch {
    return 0;
  }
}

/** The marks of `dir`, file -> { holds, at } (none when the file is absent or unreadable). */
export function readMarks(dir) {
  try {
    const marks = JSON.parse(readFileSync(join(dir, MARKS), 'utf8'));
    return marks !== null && typeof marks === 'object' ? marks : {};
  } catch {
    return {};
  }
}

/** Whether the record `file` written at `at` is marked as taken under load, by `marks`. */
export function isLoaded(marks, file, at) {
  const mark = marks[file];
  return mark !== undefined && mark.at >= at && mark.holds > ALONE_HOLDS;
}

/** Mark every record of `dir` written at or after `since` with `holds`, the most heavy runs under way during the run that wrote it. */
export function stampLoad(dir, holds, since) {
  if (!existsSync(dir)) return [];
  const marks = readMarks(dir);
  const stamped = [];
  for (const file of readdirSync(dir)) {
    if (file === MARKS) continue;
    const at = statSync(join(dir, file)).mtimeMs;
    if (at < since) continue;
    marks[file] = { holds, at };
    stamped.push(file);
  }
  if (stamped.length) writeAtomic(join(dir, MARKS), JSON.stringify(marks, null, 2) + '\n');
  return stamped;
}

/** A sampler of the load while a run lasts: `peak()` is the most places held at any sample, and `stop()` ends the sampling. */
export function loadSampler({ every = 5_000, count = () => heldNow() } = {}) {
  let peak = count();
  const timer = setInterval(() => { peak = Math.max(peak, count()); }, every);
  timer.unref();
  return { peak: () => { peak = Math.max(peak, count()); return peak; }, stop: () => clearInterval(timer) };
}
