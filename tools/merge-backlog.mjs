/**
 * Git's merge driver for the backlog (`TASKS.md`): its rows merged by their ids, for every merge git makes.
 *
 *   node tools/merge-backlog.mjs <base> <ours> <theirs> [<marker size>] [<path>]   (git runs it: %O %A %B %L %P)
 *   node tools/merge-backlog.mjs --install                                       (configures the driver, once a clone)
 *
 * Builders add rows, finish rows and edit rows at the same few places, so git's own line merge read neighbouring changes as a
 * conflict at almost every merge. The rule is the merge tool's (`mergeByRows`, `tools/merge-branch/backlog-merge.mjs`): a row a
 * side archived or added merges; a row both sides edited differently is a conflict marked in git's own form, both texts, and
 * the rest of the file merged. A file whose rows the rule cannot read (an id covered twice, a row whose ids do not read, a
 * change to a line that is no row both sides made) is merged as text by `git merge-file`, as git would have.
 *
 * The result is written over ours (git's %A). `--install` sets `merge.backlog.driver` in the repository's own configuration,
 * as `tools/merge-rows.mjs --install` does for the router; the root `npm ci` and `tools/worktree-setup.mjs` run it. Exit 0
 * clean, 1 conflicted (the result written, marked), 2 a tool error.
 */
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { isMain, writeAtomic } from './fsx.mjs';
import { installDriver } from './merge-rows.mjs';
import { mergeByRows, rowOf } from './merge-branch/backlog-merge.mjs';

/** The driver's command as `--install` configures it, run by git from the work tree's top. */
export const DRIVER = 'node tools/merge-backlog.mjs %O %A %B %L %P';
const NAME = 'backlog: TASKS.md rows by their ids';

const marked = (ours, theirs, size) => [`${'<'.repeat(size)} ours`, ours, '='.repeat(size), theirs, `${'>'.repeat(size)} theirs`].join('\n');

/**
 * The backlog's three texts merged by its rows: `{ text, conflicts }`, `text` null where the rows' rule cannot read the file and a text merge
 * is left to git. A row both sides edited stands as ours' and theirs' lines between git's markers, the rest of the file merged.
 */
export function mergeBacklog(base, ours, theirs, markerSize = 7) {
  const merged = mergeByRows(base, ours, theirs);
  if (merged.text !== null) return { text: merged.text, conflicts: 0 };
  const rows = merged.differ ?? [];
  if (merged.twice.length || merged.unread.length || rows.length === 0 || rows.some((row) => row.ours === null || row.theirs === null || row.ours.includes('\n') || row.theirs.includes('\n'))) {
    return { text: null, conflicts: 0 };
  }
  // Each both-edited row takes ours' line on all three sides, the rest merges, and ours' line is then the conflict.
  const sameRow = (line, id) => { const row = rowOf(line); return row !== null && (row.key === id || (row.ids ?? []).includes(id)); };
  const at = (text, id, line) => text.split('\n').map((each) => (sameRow(each, id) ? line : each)).join('\n');
  let [neutralBase, neutralTheirs] = [base, theirs];
  for (const row of rows) {
    neutralBase = at(neutralBase, row.id, row.ours);
    neutralTheirs = at(neutralTheirs, row.id, row.ours);
  }
  const rest = mergeByRows(neutralBase, ours, neutralTheirs);
  if (rest.text === null) return { text: null, conflicts: 0 };
  let text = rest.text;
  for (const row of rows) text = text.split('\n').map((each) => (each === row.ours ? marked(row.ours, row.theirs, markerSize) : each)).join('\n');
  return { text, conflicts: rows.length };
}

export function main(argv, { out = console.log, err = console.error, cwd = process.cwd(), env = process.env } = {}) {
  if (argv[0] === '--install' && argv.length === 1) return installDriver({ key: 'backlog', name: NAME, command: DRIVER, label: 'merge-backlog' }, { out, err, cwd, env });
  const [basePath, oursPath, theirsPath, size] = argv;
  if (!basePath || !oursPath || !theirsPath) {
    err('usage: node tools/merge-backlog.mjs <base> <ours> <theirs> [<marker size>] [<path>] | --install');
    return 2;
  }
  const markerSize = Number.parseInt(size ?? '7', 10);
  let texts;
  try {
    texts = [basePath, oursPath, theirsPath].map((file) => readFileSync(file, 'utf8'));
  } catch (error) {
    err(`merge-backlog: ${error.message}`);
    return 2;
  }
  const merged = mergeBacklog(...texts, Number.isInteger(markerSize) && markerSize > 0 ? markerSize : 7);
  if (merged.text === null) {
    const ran = spawnSync('git', ['merge-file', '-L', 'ours', '-L', 'base', '-L', 'theirs', `--marker-size=${Number.isInteger(markerSize) && markerSize > 0 ? markerSize : 7}`, oursPath, basePath, theirsPath], { encoding: 'utf8', windowsHide: true });
    if (ran.error || ran.status === null || ran.status < 0 || ran.status > 127) {
      err(`merge-backlog: git merge-file failed: ${ran.stderr?.trim() ?? ran.error?.message}`);
      return 2;
    }
    return ran.status === 0 ? 0 : 1;
  }
  try {
    writeAtomic(oursPath, merged.text);
  } catch (error) {
    err(`merge-backlog: ${error.message}`);
    return 2;
  }
  return merged.conflicts ? 1 : 0;
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));
