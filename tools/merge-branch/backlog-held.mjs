/**
 * A merge in place held to the backlog's rows once git has made it (`rowsHeld`): the rules run only where git stops, and a merge git
 * makes clean can keep a row one side moved while the other archived it, lose a side's words in one, or keep an id twice. A row the
 * rules take out (one side removed it while the other's copy is the base's or only moved) leaves, staged; any other fault is said, for
 * the merge to stop on. A twin of the driver's `BacklogHeld.HoldAsync`, its rules the `held` rows of `backlog-merges.json`.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { writeAtomic } from '../fsx.mjs';
import { rowOf, rowsHeld, rowsRefused } from './backlog-merge.mjs';

/** What git answered, trimmed, or null where it refused. */
function gitOut(root, args) {
  const ran = spawnSync('git', ['--no-optional-locks', ...args], { cwd: root, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, windowsHide: true });
  return ran.status === 0 ? ran.stdout : null;
}

/**
 * The merge in progress at `root` (`HEAD` and `MERGE_HEAD`, the made backlog in the working tree) held to `backlog`'s rows: `said`, the
 * lines for the rows it took out and staged, and `refused`, the lines for a fault it leaves; both empty where it holds, where there is no
 * backlog, or where a side's does not read.
 */
export function holdBacklog(root, backlog) {
  const none = { said: [], refused: [] };
  if (backlog === null || !existsSync(join(root, backlog))) return none;
  const base = gitOut(root, ['merge-base', 'HEAD', 'MERGE_HEAD'])?.trim();
  const sides = base ? [base, 'HEAD', 'MERGE_HEAD'].map((revision) => gitOut(root, ['show', `${revision}:${backlog}`])) : [null];
  if (sides.includes(null)) return none;
  const made = readFileSync(join(root, backlog), 'utf8');
  const held = rowsHeld(sides[0], sides[1], sides[2], made);
  if (held.refused) return { said: [], refused: rowsRefused(backlog, held.refused) };
  const refused = [
    ...held.twice.map((id) => `${backlog}: the merge covers the row ${id} twice; left for you`),
    ...held.lost.map((id) => `${backlog}: the merge lost the row ${id}, which its rows keep; left for you`),
  ];
  if (refused.length || held.kept.length === 0) return { said: [], refused };
  // Only whole rows leave: a row keeping an id the rules take out beside one they keep is the person's.
  const rows = made.split('\n').map((line) => ({ line, ids: rowOf(line)?.ids ?? [] }));
  const out = (row) => row.ids.length > 0 && row.ids.every((id) => held.kept.includes(id));
  if (rows.some((row) => row.ids.some((id) => held.kept.includes(id)) && !out(row))) {
    return { said: [], refused: held.kept.map((id) => `${backlog}: the merge keeps the row ${id} inside a row it keeps otherwise; left for you`) };
  }
  writeAtomic(join(root, backlog), rows.filter((row) => !out(row)).map((row) => row.line).join('\n'));
  if (spawnSync('git', ['add', '--', backlog], { cwd: root, windowsHide: true }).status !== 0) {
    return { said: [], refused: [`${backlog}: the rows it would take out could not be staged; left for you`] };
  }
  return { said: held.kept.map((id) => `${backlog}: row ${id} left by its rows, where git had kept it: one side removed it while the other only moved it`), refused: [] };
}
