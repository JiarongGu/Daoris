#!/usr/bin/env node
/**
 * The reached checks of a merge: the test units the merge's changed paths reach, in the suites a merge can run in minutes,
 * run on the merged tree before it is committed (`tools/merge/reached.ts` chooses and bounds them; the census of
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
 *   node tools/reached-checks.mjs [--plan] [--unbounded] [--paths <path>[,<path>…]] [--base <ref>] [--classes <name>[,<name>…]]
 *
 * The changed paths are the checkout's against HEAD (what an open merge changes); `--paths` names them, `--plan` says what
 * would run and runs nothing. With nothing changed there is nothing reached, and it says so.
 *
 * A builder's own checks: `--base main` takes what its branch changed, `--classes` names classes; each pass is recorded for the merge
 * to replay. A failed class runs again alone once (a pass alone is a FLAKE); the last line, `reached-classes: {…}`, is the record's.
 *
 * A heavy group (Process tier, a rehearsal, a whole suite: `merge/reached-admit.ts`) takes one place in the machine's admission, the
 * one the merge queue's gates take, and says what it waits for; a light group runs at once. The place is let go as the group ends,
 * and by this process's death where it is killed. The merge's own `reached` gate runs inside the queue's place, with no Daoris home,
 * so it takes none of its own.
 *
 * Exit codes: 0 clean or nothing reached · 1 a reached unit failed · 2 a tool error.
 */
import { spawn, spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { isMain } from './fsx.mjs';
import { CI_FINDINGS } from './ci-background.mjs';
import { CLASS_VERDICTS_FILE, verdictsHome } from './merge-branch.mjs';
import { keyContextOf, storeFile as storeIn } from './merge-branch/full-set.mjs';
import { processSuitesOf, reachedCommands, reachedLines, reachedPlan, unitsOfMerge } from './merge/reached.ts';
import { PassedClasses, classKey, splitByReuse } from './merge/class-reuse.ts';
import { nightLines, tiersOf } from './merge/tiers.ts';
import { mainStandsRed } from './merge/reached-classify.ts';
import { runPlan } from './merge/reached-run.ts';
import { inPlace } from './merge/reached-admit.ts';
import { createAdmission } from './machine-admission.mjs';

/** A command run to its end with its output shown; asynchronous, so a hold's beat keeps sounding while it runs. */
const execAsync = (command, args, cwd) => new Promise((done) => {
  const child = spawn(command, args, { cwd, stdio: 'inherit', windowsHide: true });
  child.on('error', () => done(null));
  child.on('close', (code) => done(code));
});

/** The machine's admission as a builder takes it, its holds let go where an interrupt ends the process before a `finally` runs. */
export function builderAdmission(admission = createAdmission({ kind: 'session' })) {
  const held = new Set();
  const letGo = () => held.forEach((hold) => hold.release());
  for (const signal of ['SIGINT', 'SIGTERM']) process.once(signal, () => { letGo(); process.exit(signal === 'SIGINT' ? 130 : 143); });
  return {
    take: async (options) => {
      const hold = await admission.take(options);
      if (!hold) return null;
      held.add(hold);
      return { release: () => { held.delete(hold); hold.release(); } };
    },
  };
}

/** The paths changed against HEAD (an open merge's), or against where the branch left `base` (a builder's, new files too). */
export function changedPaths(root, base = null) {
  const read = (args) => {
    const ran = spawnSync('git', args, { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    if (ran.status !== 0) throw new Error(`git could not read the checkout's changes: ${String(ran.stderr).trim()}`);
    return ran.stdout;
  };
  const from = base ? read(['--no-optional-locks', 'merge-base', base, 'HEAD']).trim() : 'HEAD';
  const changed = read(['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', from]).split('\0').filter(Boolean);
  const added = base ? read(['--no-optional-locks', 'ls-files', '--others', '--exclude-standard', '-z']).split('\0').filter(Boolean) : [];
  return [...new Set([...changed, ...added])];
}

export const storeFile = (root) => storeIn(verdictsHome(root));

/** The plan for `paths` over the tree at `root`, what it replays from standing passes, what it leaves to the night, and each unit's key. */
export function planFor(root, paths, store, { bounds = {}, classes = [] } = {}) {
  if (paths.length === 0 && classes.length === 0) return { plan: reachedPlan([], paths, { processSuites: new Set() }), night: {}, keys: new Map(), replayed: 0 };
  const { map, gates, gatesText, context } = keyContextOf(root);
  const processSuites = processSuitesOf(gates);
  // A unit is reached where a changed path is one of its key's inputs: the census's reach, less the paths no test can read.
  // Classes a builder names are run whatever they reach, and the dependency classes among them too.
  const reached = classes.length
    ? map.units.filter((unit) => classes.some((name) => unit.unit === name || unit.unit.endsWith(`.${name}`) || unit.unit.endsWith(`/${name}`)))
    : map.units.filter((unit) => paths.some((path) => context.reaches(unit, path)));
  const { units, night } = classes.length
    ? { units: reached, night: {} }
    : unitsOfMerge(reached, paths, { tiers: tiersOf(gatesText), reaches: context.reaches, processSuites });
  const { run, replayed, keys } = splitByReuse(units, (unit) => classKey(unit, context), store);
  const counts = {};
  for (const unit of replayed) counts[unit.suite] = (counts[unit.suite] ?? 0) + 1;
  return { plan: reachedPlan(run, paths, { bounds, replayed: counts, processSuites }), night, keys, replayed: replayed.length };
}

const readJson = (file) => {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch {
    return {};
  }
};

/** The value of `--name <value>` among `argv`, or null. */
const valueOf = (argv, name) => {
  const at = argv.indexOf(name);
  return at >= 0 ? (argv[at + 1] ?? null) : null;
};

async function main(argv) {
  const root = process.cwd();
  const base = valueOf(argv, '--base');
  const classes = (valueOf(argv, '--classes') ?? '').split(',').filter(Boolean);
  const named = valueOf(argv, '--paths');
  const paths = named !== null ? named.split(',').filter(Boolean) : changedPaths(root, base);
  // One store for every worktree, the main checkout's, so a builder's passes are the ones its merge replays.
  const store = new PassedClasses(storeFile(verdictsHome(root)));
  // A store is warmed by running every class the paths reach, which no merge does: its bound is the suite's size.
  const everything = argv.includes('--unbounded') || classes.length > 0;
  const bounds = everything ? { dotnet: Infinity, vitest: Infinity, node: Infinity } : {};
  const { plan, night, keys, replayed } = planFor(root, paths, store, { bounds, classes });
  for (const line of [...reachedLines(plan), ...nightLines(night)]) console.log(line);
  if (argv.includes('--plan')) {
    for (const each of reachedCommands(plan)) console.log(`  ${each.label}: ${each.command} ${each.args.join(' ')}`);
    return 0;
  }
  const checkout = verdictsHome(root);
  const findings = readJson(join(checkout, CI_FINDINGS)).findings ?? [];
  const verdicts = readJson(join(checkout, CLASS_VERDICTS_FILE)).gates ?? {};
  const admission = builderAdmission();
  const processSuites = processSuitesOf(keyContextOf(root).gates);
  const outcome = await runPlan({
    root, plan, keys, store, replayed, say: (line) => console.log(line),
    exec: execAsync,
    inPlace: (group, fn) => inPlace(group, { admission, processSuites }, fn),
    mainRedOf: (unit) => mainStandsRed(unit, findings, verdicts),
  });
  store.save();
  return outcome.red.length ? 1 : 0;
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
