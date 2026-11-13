/**
 * The temp files git writes at the checkout's root while it runs a merge driver (`.merge_file_` and six characters: the base, ours
 * and theirs it hands the driver). Git removes them as the driver returns; a git that is stopped in the middle, or that cannot
 * unlink one at once on Windows, leaves them, and an untracked file at the root makes the checkout dirty for the next stage. A
 * merge ended by the time this runs has no driver alive, so what stands is stray.
 */
import { readdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';

const TEMP = /^\.merge_file_[A-Za-z0-9]{6}$/;

/** The stray driver temp files at `root`, removed; their names. */
export function sweepMergeTemps(root: string): string[] {
  const names = readdirSync(root).filter((name) => TEMP.test(name));
  for (const name of names) rmSync(join(root, name), { force: true });
  return names;
}

/** The sweep, with a line to `say` for any it removes. */
export function sweepAndSay(root: string, say: (line: string) => void = console.log): void {
  const names = sweepMergeTemps(root);
  if (names.length) say(`removed ${names.length} stray git merge temp file${names.length === 1 ? '' : 's'} at the root: ${names.join(', ')}`);
}
