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
 * A class whose key (every file that reaches it, its suite's declaration, the toolchain) passed before is not run again: a
 * change to a widely referenced project runs exactly the classes that name it, and a docs-only change none. A class that needs a
 * real dependency runs only where a changed path is that dependency's code (`tiers` in `daoris.gates.json`); the others are
 * left to the nightly run, and said.
 *
 *   node tools/reached-checks.mjs [--plan] [--unbounded] [--paths <path>[,<path>…]]
 *
 * The changed paths are the checkout's against HEAD (what an open merge changes); `--paths` names them, `--plan` says what
 * would run and runs nothing. With nothing changed there is nothing reached, and it says so. The passes are kept in
 * `local/reached-classes.json` of the main checkout, or the file `DAORIS_REACHED_STORE` names.
 *
 * Exit codes: 0 clean or nothing reached · 1 a reached unit failed · 2 a tool error.
 */
import { spawnSync } from 'node:child_process';
import { mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { isMain } from './fsx.mjs';
import { verdictsHome } from './merge-branch.mjs';
import { census, treeOf } from './test-map.mjs';
import { reachedCommands, reachedLines, reachedPlan, unitsOfMerge } from './merge-branch/reached.mjs';
import { reuseContext } from './merge-branch/reached-reuse.mjs';
import { PassedClasses, classKey, splitByReuse, unitId } from './merge/class-reuse.ts';
import { toolchainOf } from './merge/flow-deps.ts';
import { nightLines, tiersOf } from './merge/tiers.ts';
import { passesOf, withReport } from './merge-branch/reached-reports.mjs';

/** The paths the checkout changes against HEAD: staged, unstaged, a merge's. */
export function changedPaths(root) {
  const diff = spawnSync('git', ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', 'HEAD'], { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (diff.status !== 0) throw new Error(`git could not read the checkout's changes: ${String(diff.stderr).trim()}`);
  return diff.stdout.split('\0').filter(Boolean);
}

/** The file the class passes are kept in. */
export const storeFile = (root) => process.env.DAORIS_REACHED_STORE || join(verdictsHome(root), 'local', 'reached-classes.json');

/** The plan for `paths` over the tree at `root`, what it replays from standing passes, what it leaves to the night, and each unit's key. */
export function planFor(root, paths, store, { bounds = {} } = {}) {
  if (paths.length === 0) return { plan: reachedPlan([], paths), night: {}, keys: new Map() };
  const { files, read } = treeOf(root);
  const map = census(files, read);
  const gatesText = read('daoris.gates.json');
  const toolchain = toolchainOf();
  const context = reuseContext({ map, files, read, toolchain: `${toolchain.node}+${toolchain.dotnet}`, gates: JSON.parse(gatesText).gates ?? [] });
  // A unit is reached where a changed path is one of its key's inputs: the census's reach, less the paths no test can read.
  const reached = map.units.filter((unit) => paths.some((path) => context.reaches(unit, path)));
  const { units, night } = unitsOfMerge(reached, paths, { tiers: tiersOf(gatesText), reaches: context.reaches });
  const { run, replayed, keys } = splitByReuse(units, (unit) => classKey(unit, context), store);
  const counts = {};
  for (const unit of replayed) counts[unit.suite] = (counts[unit.suite] ?? 0) + 1;
  return { plan: reachedPlan(run, paths, { bounds, replayed: counts }), night, keys };
}

async function main(argv) {
  const root = process.cwd();
  const at = argv.indexOf('--paths');
  const paths = at >= 0 ? (argv[at + 1] ?? '').split(',').filter(Boolean) : changedPaths(root);
  const store = new PassedClasses(storeFile(root));
  // A store is warmed by running every class the paths reach, which no merge does: its bound is the suite's size.
  const bounds = argv.includes('--unbounded') ? { dotnet: Infinity, vitest: Infinity, node: Infinity } : {};
  const { plan, night, keys } = planFor(root, paths, store, { bounds });
  for (const line of [...reachedLines(plan), ...nightLines(night)]) console.log(line);
  const commands = reachedCommands(plan);
  if (argv.includes('--plan')) {
    for (const each of commands) console.log(`  ${each.label}: ${each.command} ${each.args.join(' ')}`);
    return 0;
  }
  const failed = [];
  commands.forEach((each, index) => {
    const group = plan.groups[index];
    const { args, report } = withReport(each, `reached-${each.label}-${index + 1}`);
    mkdirSync(join(root, '_fixtures/test-timing'), { recursive: true });
    rmSync(join(root, report), { force: true });
    console.log(`reached: ${each.label}: ${each.command} ${each.args.slice(0, 2).join(' ')} …`);
    // npm is a script on Windows and needs a shell; its arguments are paths, so nothing in them is read by it.
    const ran = spawnSync(each.command, args, { cwd: each.cwd, stdio: 'inherit', windowsHide: true, shell: each.command === 'npm' && process.platform === 'win32' });
    if (ran.status !== 0) failed.push(each.label);
    for (const unit of passesOf(group, ran.status, join(root, report), root)) {
      store.add(unitId(unit), keys.get(unitId(unit)), new Date().toISOString());
    }
  });
  store.save();
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
