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
 * left to the nightly run, and said. The fast tier runs the end-to-end specs nearest the change too (`merge/heavy-tier.ts`).
 *
 *   node tools/reached-checks.mjs [--plan] [--unbounded] [--tier fast|heavy|all] [--paths <path>[,<path>…]] [--base <ref>] [--classes <name>[,<name>…]] [--halt-file <path>]
 * `--tier fast` is a merge's, `--tier heavy` the run after its landing's (`merge/heavy-tier.ts`); with none, both.
 *
 * The changed paths are the checkout's since `--base` (HEAD where none is named: an open merge's); a committed tree's need its base,
 * which the merge tool hands its gate in `{base}`. `--paths` names them, `--plan` runs nothing. With nothing changed nothing is reached.
 *
 * A builder's own checks: `--base main` takes what its branch changed, `--classes` names classes (`--measure` times them whatever a
 * standing pass says, recording none); each pass is recorded as its group ends, for the merge and the next check to replay. A failed class runs again alone once (a pass alone is a FLAKE); the last line, `reached-classes: {…}`, is the record's.
 *
 * Its time goes to tests: the census is read once per content tree (`merge/census-cache.ts`), each test project built once and its
 * classes run in one `dotnet test --no-build --no-restore` (`merge/reached-build.ts`), the light suites' runs beside the build and the
 * .NET runs rather than after them (`merge/reached-run.ts`), and each step says a `reached-step: {…}` line
 * as it starts and ends, so the merge records them and names the step a kill cut (`merge/reached-steps.ts`).
 *
 * A heavy group (Process tier, a rehearsal, a whole suite: `merge/reached-admit.ts`) takes one place in the machine's admission, the
 * one the merge queue's gates take, and says what it waits for; a light group runs at once. The place is let go as the group ends,
 * and by this process's death where it is killed. The merge's own `reached` gate runs inside the queue's place, with no Daoris home,
 * so it takes none of its own.
 *
 * The heavy tier after a landing runs it with `--halt-file <path>` (`tools/heavy-run.mjs`): when a newer landing's tier writes that
 * file, no group starts, the group in flight is ended where the newer commit changes what its classes read and finished otherwise,
 * and the classes left are named in the last line's `handed` (`merge/heavy-halt.ts`). A heavy tier's wait for the machine's admission
 * past `WAIT_BOUND_MS` is said on a `heavy tier:` line.
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
import { unitHops } from './test-map.mjs';
import { processSuitesOf, reachedCommands, reachedLines, reachedPlan, unitsOfMerge } from './merge/reached.ts';
import { PassedClasses, classKey, splitByReuse, unitId } from './merge/class-reuse.ts';
import { deferredLines, splitTier, tierNamed } from './merge/heavy-tier.ts';
import { nightLines, tiersOf } from './merge/tiers.ts';
import { mainStandsRed } from './merge/reached-classify.ts';
import { runPlan } from './merge/reached-run.ts';
import { aheadOf, buildAhead, buildDecision, buildInputs, buildOnce, inputsDigest, outputsDigest, readStamp, treeIds } from './merge/reached-build.ts';
import { isHeavySuite } from './merge/heavy-tier.ts';
import { StepClock } from './merge/reached-steps.ts';
import { inPlace } from './merge/reached-admit.ts';
import { createAdmission } from './machine-admission.mjs';
import { loadSampler, stampLoad } from './time-load.mjs';
import { HEAVY_PREFIX } from './merge/heavy-after.ts';
import { createHalt, wired } from './merge/heavy-halt.ts';
import { WAIT_BOUND_MS } from './merge/heavy-stack.ts';
import { endTree } from './merge/step-trees.ts';
import { planVerification, verifyBudgetOf } from './merge/verification.ts';
/** A command run to its end with its output shown; asynchronous, so a hold's beat keeps sounding while it runs. `started` hears its pid. */
const execAsync = (command, args, cwd, started) => new Promise((done) => {
  const child = spawn(command, args, { cwd, stdio: 'inherit', windowsHide: true });
  started?.(child.pid ?? null);
  child.on('error', () => { started?.(null); done(null); });
  child.on('close', (code) => { started?.(null); done(code); });
});

/** The paths `commit` changed against this tree's HEAD: what a newer heavy tier's commit changes, for its halt. */
function newerPaths(root, commit) {
  const ran = spawnSync('git', ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', 'HEAD', commit], { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (ran.status !== 0) throw new Error(`git could not read what ${commit} changed: ${String(ran.stderr).trim()}`);
  return ran.stdout.split('\0').filter(Boolean);
}

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
    pause: (options) => admission.pause?.(options),
  };
}

/** The paths changed since where the checkout left `base`, new files too: since HEAD, an open merge's; since a commit, its branch's. */
export function changedPaths(root, base = 'HEAD') {
  const read = (args) => {
    const ran = spawnSync('git', args, { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    if (ran.status !== 0) throw new Error(`git could not read the checkout's changes: ${String(ran.stderr).trim()}`);
    return ran.stdout;
  };
  const from = read(['--no-optional-locks', 'merge-base', base, 'HEAD']).trim();
  const changed = read(['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', from]).split('\0').filter(Boolean);
  const added = read(['--no-optional-locks', 'ls-files', '--others', '--exclude-standard', '-z']).split('\0').filter(Boolean);
  return [...new Set([...changed, ...added])];
}

export const storeFile = (root) => storeIn(verdictsHome(root));

/** The plan for `paths` over the tree at `root` (its key context `world`), what it replays and leaves to the night, and each unit's key. */
export function planFor(root, paths, store, { bounds = {}, classes = [], tier = 'all', world = null } = {}) {
  if (paths.length === 0 && classes.length === 0) return { plan: reachedPlan([], paths, { processSuites: new Set() }), night: {}, keys: new Map(), replayed: 0, deferred: [] };
  const read = world ?? keyContextOf(root);
  const { map, gates, gatesText, context } = read;
  const processSuites = processSuitesOf(gates);
  // A unit is reached where a changed path is one of its key's inputs: the census's reach, less the paths no test can read.
  // Classes a builder names are run whatever they reach, and the dependency classes among them too.
  const reached = classes.length
    ? map.units.filter((unit) => classes.some((name) => unit.unit === name || unit.unit.endsWith(`.${name}`) || unit.unit.endsWith(`/${name}`)))
    : map.units.filter((unit) => paths.some((path) => context.reaches(unit, path)));
  const { units: considered, night } = classes.length
    ? { units: reached, night: {} }
    : unitsOfMerge(reached, paths, { tiers: tiersOf(gatesText), reaches: context.reaches, processSuites, everything: tier === 'heavy' });
  const { kept: units, deferred: heavy } = splitTier(considered, tier, processSuites, { changed: paths, hopsOf: (unit) => unitHops(unit, paths, map) });
  const keyOf = read.classKeyOf ?? ((unit) => classKey(unit, context));
  const { run, replayed, keys } = splitByReuse(units, keyOf, store);
  const deferred = splitByReuse(heavy, keyOf, store).run;
  const counts = {};
  for (const unit of replayed) counts[unit.suite] = (counts[unit.suite] ?? 0) + 1;
  const counted = run.map((unit) => ({ ...unit, hops: unitHops(unit, paths, map) }));
  const reach = { reached: reached.length, of: map.units.length, what: 'classes' };
  return { plan: reachedPlan(counted, paths, { bounds, replayed: counts, processSuites }), night, keys, replayed: replayed.length, deferred, reach };
}

/** The digest of what the build of `project` reads on the tree `world` reads, or null where git cannot list it. */
function inputsOf(root, world, project) {
  if (!world?.tree) return null;
  try {
    const inputs = buildInputs(project, world.files, world.read);
    return inputsDigest(inputs, (paths) => treeIds(root, world.tree, paths), `${world.toolchain.dotnet}\0${world.privates}`);
  } catch {
    return null;
  }
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
  const base = valueOf(argv, '--base') ?? 'HEAD';
  const classes = (valueOf(argv, '--classes') ?? '').split(',').filter(Boolean);
  const named = valueOf(argv, '--paths');
  const paths = named !== null ? named.split(',').filter(Boolean) : changedPaths(root, base);
  // One store for every worktree, the main checkout's, so a builder's passes are the ones its merge replays.
  const measure = argv.includes('--measure');
  const store = new PassedClasses(measure ? join(root, '_fixtures', 'no-standing-passes.json') : storeFile(verdictsHome(root)));
  // A store is warmed by running every class the paths reach, which no merge does: its bound is the suite's size.
  const tier = tierNamed(valueOf(argv, '--tier')) ?? 'all';
  const everything = argv.includes('--unbounded') || classes.length > 0 || tier === 'heavy';
  const bounds = everything ? { dotnet: Infinity, vitest: Infinity, node: Infinity } : {};
  const say = (line) => console.log(line);
  const clock = new StepClock(say);
  const world = paths.length || classes.length ? await clock.time('census', () => keyContextOf(root)) : null;
  if (world) say(`reached: the census of this tree was ${world.hit ? 'read from its cache' : 'read, and kept for this tree'}`);
  const processSuites = world ? processSuitesOf(world.gates) : new Set();
  const haltFile = valueOf(argv, '--halt-file');
  const buildSeams = { root, exec: execAsync, say, time: (step, fn) => clock.time(step, fn), inputsOf: (project) => inputsOf(root, world, project) };
  // The projects whose build the change makes stale start building before the plan, which holds the process: it runs beside them.
  const ahead = world && !haltFile && !classes.length && tier !== 'heavy' && !argv.includes('--plan')
    ? buildAhead(aheadOf(world.map.units.filter((unit) => unit.kind === 'dotnet' && !isHeavySuite(unit.suite, processSuites)), paths, {
      inputsOf: (project) => {
        try {
          return buildInputs(project, world.files, world.read);
        } catch {
          return null;
        }
      },
      decide: (project) => {
        const inputs = buildSeams.inputsOf(project);
        return inputs === null ? { build: true, why: 'its inputs could not be read' } : buildDecision({ inputs, outputs: outputsDigest(root, project), stamp: readStamp(root, project) });
      },
    }), buildSeams).catch(() => undefined)
    : null;
  const { plan, night, keys, replayed, deferred, reach } = await clock.time('plan', () => {
    const planned = planFor(root, paths, store, { bounds, classes, tier, world });
    world?.keep();
    return planned;
  });
  const verifying = world && !classes.length ? planVerification(verifyBudgetOf(world.gatesText), paths, reach) : null;
  if (verifying) say(`reached: ${verifying.line}`);
  for (const line of [...reachedLines(plan), ...nightLines(night), ...deferredLines(deferred)]) say(line);
  if (argv.includes('--plan')) {
    for (const each of reachedCommands(plan)) say(`  ${each.label}: ${each.command} ${each.args.join(' ')}`);
    return 0;
  }
  const checkout = verdictsHome(root);
  const findings = readJson(join(checkout, CI_FINDINGS)).findings ?? [];
  const verdicts = readJson(join(checkout, CLASS_VERDICTS_FILE)).gates ?? {};
  // A heavy tier's wait for the machine past its bound is said on the line the steward's monitor reads.
  const bound = tier === 'heavy'
    ? { ms: WAIT_BOUND_MS, say: (waited, sentence) => say(`${HEAVY_PREFIX} its reached run has waited ${Math.round(waited / 60_000)} min for the machine's heavy-run admission, past its bound of ${WAIT_BOUND_MS / 60_000} min: ${sentence}`) }
    : null;
  const admission = builderAdmission(createAdmission({ kind: 'session', bound }));
  const halt = haltFile && world
    ? createHalt({ file: haltFile, changedSince: (commit) => newerPaths(root, commit), reaches: (unit, path) => world.context.reaches(unit, path), end: (pid) => { endTree(pid); }, say })
    : null;
  const unwatch = halt?.watch(2_000);
  const place = (group, fn) => inPlace(group, { admission, processSuites, ...(halt ? { halted: halt.asked } : {}) }, fn);
  const run = halt ? wired(halt, execAsync, place) : { exec: execAsync, inPlace: place };
  const startedAt = Date.now();
  const load = loadSampler(); // the records this run writes are marked with the heavy runs under way
  const outcome = await runPlan({
    root, plan, keys, store, replayed, night, deferred: deferred.map((unit) => unitId(unit)), say, clock, processSuites, record: !measure,
    exec: run.exec,
    inPlace: run.inPlace,
    ...(halt ? { halt } : {}),
    mainRedOf: (unit) => mainStandsRed(unit, findings, verdicts),
    build: async (projects) => {
      await ahead;
      return buildOnce(projects, buildSeams);
    },
  });
  unwatch?.();
  load.stop();
  stampLoad(join(root, '_fixtures', 'test-timing'), load.peak(), startedAt);
  if (!measure) store.save();
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
