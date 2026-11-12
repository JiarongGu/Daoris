#!/usr/bin/env node
/**
 * The reached checks of a merge: the test units the merge's changed paths reach, in the suites a merge can run in minutes,
 * run on the merged tree before it is committed (`tools/merge-branch/reached.mjs` chooses and bounds them; the census of
 * test units, `tools/test-map.mjs`, says what each reaches).
 *
 * ## Why
 *
 * The CI beside runs every suite, hours after a merge, so a list, a census or a structure red a branch brings lands and
 * waits there for its fix pass. The checks that can see the change run at the merge instead: the merge tool runs this as
 * its `reached` gate, with the CLI's verify, the censuses and the web's structure checks beside it.
 *
 *   node tools/reached-checks.mjs [--plan] [--paths <path>[,<path>…]]
 *
 * The changed paths are the checkout's against HEAD (what an open merge changes); `--paths` names them, `--plan` says what
 * would run and runs nothing. With nothing changed there is nothing reached, and it says so.
 *
 * Exit codes: 0 clean or nothing reached · 1 a reached unit failed · 2 a tool error.
 */
import { spawnSync } from 'node:child_process';
import { isMain } from './fsx.mjs';
import { census, treeOf, unitsReached } from './test-map.mjs';
import { reachedCommands, reachedLines, reachedPlan } from './merge-branch/reached.mjs';

/** The paths the checkout changes against HEAD: staged, unstaged, a merge's. */
export function changedPaths(root) {
  const diff = spawnSync('git', ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', 'HEAD'], { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (diff.status !== 0) throw new Error(`git could not read the checkout's changes: ${String(diff.stderr).trim()}`);
  return diff.stdout.split('\0').filter(Boolean);
}

/** The plan for `paths` over the tree at `root`. */
export function planFor(root, paths) {
  if (paths.length === 0) return reachedPlan([], paths);
  const { files, read } = treeOf(root);
  return reachedPlan(unitsReached(paths, census(files, read)), paths);
}

async function main(argv) {
  const root = process.cwd();
  const at = argv.indexOf('--paths');
  const paths = at >= 0 ? (argv[at + 1] ?? '').split(',').filter(Boolean) : changedPaths(root);
  const plan = planFor(root, paths);
  for (const line of reachedLines(plan)) console.log(line);
  const commands = reachedCommands(plan);
  if (argv.includes('--plan')) {
    for (const each of commands) console.log(`  ${each.label}: ${each.command} ${each.args.join(' ')}`);
    return 0;
  }
  const failed = [];
  for (const each of commands) {
    console.log(`reached: ${each.label}: ${each.command} ${each.args.slice(0, 2).join(' ')} …`);
    // npm is a script on Windows and needs a shell; its arguments are paths, so nothing in them is read by it.
    const ran = spawnSync(each.command, each.args, { cwd: each.cwd, stdio: 'inherit', windowsHide: true, shell: each.command === 'npm' && process.platform === 'win32' });
    if (ran.status !== 0) failed.push(each.label);
  }
  if (failed.length) {
    console.log(`reached: failed: ${failed.join(', ')}`);
    return 1;
  }
  return 0;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`reached-checks: ${error.stack ?? error.message}`);
      process.exitCode = 2;
    },
  );
}
