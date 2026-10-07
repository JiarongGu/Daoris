#!/usr/bin/env node
/**
 * The parent's one merge tool (MOD9, `docs/2026-09-30-parallel-development-design.md` §3 rule 6).
 *
 * ## Why
 *
 * The parent merges subagents' branches into main many times a day, and until this tool it typed each
 * gate chain by hand. On 2026-09-30 that went wrong twice: a hand-typed chain lost a rehearsal's reason,
 * and a batch of a "web only" branch skipped the .NET suites, so two C# tests that read the web's
 * catalogues broke on main unseen. So the plan is read from what the repository already declares.
 *
 * Every merge ran all of it, and that became the cost (GATE3, 2026-10-04): the driver's real-process half
 * took 30, 48 and 126 minutes on one day's three integrations, and everything else about 25. So a merge
 * now runs the gates its changed paths can reach, by the lane table below (`REACH`), and never the long
 * ones (GATE5: the real-process halves and the deployment rehearsal, which any driver change still
 * reached), which only the full set runs. The lesson of that batch is kept in two places. The table is held by tests to what each suite reads and what each
 * rehearsal runs, so a skipped gate is one that could not see the change. And the full set still runs
 * before the install is built: `publish:desktop` refuses a tree no full set passed (`--passed` below),
 * the same day, so a table that is wrong is caught before the person runs the build.
 *
 * ## What it does
 *
 * 1. Refuses unless the checkout is on `main` with a clean tree, and the branch exists and adds commits.
 * 2. Prints the lanes the branch touched, by id and title, from the repository's `daoris.lanes.json`
 *    (D115 §2.1, the design's §5), and names the steward's records if it edited them. It informs; it
 *    never refuses on a lane.
 * 3. The commit check: every commit the branch adds carries its `Co-Authored-By:` line (a merge of main
 *    into the branch needs none), and the branch's worktree, if it has one, holds nothing uncommitted
 *    (work a hand-back calls done and left out of the merge). `--no-commit-check` skips it.
 * 4. The prune (below), unless `--no-prune`: one line per branch removed or kept.
 * 5. `git merge --no-ff --no-commit`. A conflict stops here: the files are named and the merge is left
 *    for the parent. Code is never resolved for anyone; the append-only records resolve themselves by
 *    their `merge=union` attribute (D106). The orientation index is written from the merged tree and
 *    staged before the gates choose (`GENERATED`, ORIENT1a), so a conflict in it alone does not stop the
 *    merge, and one beside others is written once they are resolved. Before it, each decision file the merge
 *    changed has a blank line set before a note's label union left under another note, one line said per
 *    note (`setApart`, MERGEJOIN1).
 * 6. The gates, one at a time, in a fixed order, fast first. The plan is every gate `daoris.gates.json`
 *    declares, plus every `npm run` step the release workflow runs that the declaration does not (the
 *    release and family rehearsals). The order is by kind (a devkit check, then the suites, then the
 *    rehearsals); within a kind the declared order holds, and the workflow's own rehearsals go before the
 *    declared ones, so the declaration's last gate, the deployment rehearsal, ends the run. That rehearsal
 *    starts after `dotnet build-server shutdown`: on 2026-09-30 a long-lived build server carried a broken
 *    environment into a publish.
 *    Of the plan, a merge runs the baseline (`BASELINE`: the universal gates, the code map, the orientation
 *    index's check and the CLI's `verify`, at every merge) and each gate a path the merge changes can reach (`REACH`). A path no rule
 *    places reaches every gate, and so does a gate the table never names. A long gate (`isLongGate`) is
 *    never run by a merge, reached or not: it says it is in the full set before staging. The lines before
 *    the run say why each gate runs or is skipped. `--full` runs the whole plan, and `--rerun` runs a long
 *    gate when it is named. The merge is gated before it is committed, so the devkit's universal gates, in their
 *    own gate and in `verify`, run through `tools/as-merged.mjs` (GATE1): its docs gate dates each path from
 *    HEAD's history, and there HEAD is the commit the merge would make.
 * 7. Each gate's whole output goes to `local/scratch/merge-<branch>/<gate>.log` (gitignored), written
 *    beside and renamed when the gate ends, and one line per gate says its result and that file. The
 *    first failing gate stops the run (`--keep-going` runs the rest), and what did not run is named. A
 *    Process gate also writes `<gate>.trx` there, each test's duration, and the ten slowest classes are
 *    printed after its line (PROC1), so where its time goes is measured at every merge that runs it.
 * 8. Each verdict is recorded with the tree it ran on, in `local/gate-verdicts.json` (gitignored), which
 *    `--rerun`, `--stale` and the stage read (below).
 * 9. Once the gates pass, the knowledge server agents ask is rebuilt from the merged tree when the service's
 *    sources changed (`tools/knowledge-server.mjs build`, ORIENT1c); one line says so, and a build that fails
 *    is said and fails nothing.
 * 10. Nothing is committed. Committing stays the parent's, after it reads the diff and writes the records.
 *
 * ## A fixed gate re-runs alone (GATE4)
 *
 * `--rerun <gate>…` runs the named gates again on the merge in place, then any gate of its selection that
 * has no verdict yet (the ones a failure stopped, or one a fix's paths now reach), and any whose verdict a
 * path changed since reaches (GATE6, below); it keeps every other verdict. The summary names each kept
 * verdict and when it was given, and each run again with the path that made it stale. `--continue` runs
 * every selected gate again, as it always did. With no merge open it acts on the checkout (GATE6b, below).
 *
 * ## The full set before the install (GATE3)
 *
 * `--full` with no branch runs the whole plan on the checkout as it stands, merging nothing. `--passed`
 * answers whether the full set passed the checkout's tree: every gate's newest verdict that still stands
 * there is a pass. `tools/desktop-publish.mjs` asks it before it publishes anywhere but a scratch folder
 * under `_fixtures`, and a refusal prints its lines. A tree is the checkout's content as `git add -A` would
 * stage it, written with an index of its own in the git folder, so an uncommitted fix and an untracked
 * file count, and the person's index is untouched.
 *
 * ## A verdict stands until a path its gate reaches changes (GATE6)
 *
 * A verdict given on another tree still stands for this one when every path the two differ by is a record
 * the parent writes after the gates (the steward's lane and the records that merge by union, which the
 * install carries none of), or a path that cannot reach its gate by the lane table (`pathsReaching`, which
 * reads `REACH` as a merge's selection does: every path reaches the baseline). Whole trees were compared
 * before, so a one-line fix to a browser test, re-gated by the web gate alone, voided every other verdict
 * and cost a second full run before a stage. A gate whose verdict no longer stands is said as stale, with
 * the paths that made it so, at `--passed` and at `--rerun`, which runs it again rather than keep it.
 *
 * ## A gate re-run on the checkout as it stands (GATE6b)
 *
 * `--rerun <gate>…` with no merge open runs the named gates on the checkout, merging nothing, and every gate the stage
 * calls stale with them; `--stale` runs the stale ones alone. Each verdict is recorded where `--passed` reads it, every
 * other verdict is kept as the stage counts it, and the run ends with the stage's answer. A gate with no verdict at
 * all runs only when named. Outside a merge `--rerun` used to refuse, so one gate that flaked in the full set, or a fix
 * committed after it, left `--full` again, about an hour and a half, as the only way to a stage. Both refuse a tree that
 * is not clean, naming each path: a re-run gates a commit, and `--full` is the verb for the tree as it stands.
 *
 * A refused stage, `--passed` and a failed `--full` end with the smallest command that would pass the checkout
 * (`stageRemedy`): `--stale` when every gate it lacks is stale, `--rerun` naming each that failed or has no verdict
 * when that runs fewer than the plan, else `--full`. `publish:desktop` names the same command in its refusal.
 *
 * ## The prune (GATE2)
 *
 * Every subagent leaves a branch and a worktree, and so does every integration: 36 of 41 local branches
 * were merged into main with 41 worktrees standing when this was built. So each merge starts by removing
 * the merged ones, after its refusals and before `git merge` (a refusal still touches nothing), and a
 * batch's last `--continue` does it again. `--prune [--plan]` runs it alone; its plan changes nothing.
 *
 * A candidate is a local branch merged into main, but main, the branch the main checkout has out, the one
 * this runs in, and a batch's branches still to merge. It goes with its worktree, if one has it out, only
 * when that worktree is not locked (the harness locks an agent's worktree while the agent runs, so a lock
 * is in use: a locked worktree is never looked into or unlocked, and a branch at main's tip is merged),
 * has no modified or staged tracked file, has no untracked file (`git worktree remove` refuses those
 * without `--force`, and one may be work), and holds nothing under its gitignored `local/` (an agent's
 * private output, which nothing else keeps). Removal is `git worktree remove`, never forced: ignored
 * build output does not stop it, and whatever does is work to keep. Then `git branch -d`, never `-D`,
 * and one `git worktree prune` at the end. What git refuses is kept and says why. A folder git does not
 * list is never touched. The prune never stops a merge: what it cannot do it says, and the merge goes on.
 *
 * ## Flakes
 *
 * Real-process tests fail under load and pass alone (FLAKE1 in `TASKS.md`). When a `dotnet test` gate
 * fails and every test it names is in one of FLAKE1's classes, each is re-run alone, once. If they all
 * pass the gate reads FLAKE, not PASS, and the summary counts it: a flake is reported, never hidden. A
 * failure outside those classes is never re-run, because a test that passes alone after failing in the
 * suite may be one test polluting another, and that is a defect.
 *
 * A rehearsal that died is run again once, whole, with the steps before it (LEFT1): one whose exit is a
 * process ending (a shell's 126 or 127, cmd's 9009, a signal, a Windows crash status), or that printed
 * nothing of its own. The family rehearsal once exited 127 with no transcript while three worktrees
 * built, and passed alone. If the second run passes, the gate reads FLAKE. A rehearsal that reported a
 * failed check has failed whatever its exit, and so has one that said why it stopped; neither is run again.
 *
 * ## A batch
 *
 * `--batch` names more branches. A merge left uncommitted blocks the next one (git refuses to merge over
 * an open merge), and this tool never commits, so a batch goes one merge at a time: each is gated by
 * the checks and suites, the parent commits it, and `--continue` merges the next. The rehearsals run
 * once, after the last. The batch is recorded in `local/scratch/merge-batch.json`.
 *
 *   node tools/merge-branch.mjs <branch> [--batch <branch>…] [--full] [--keep-going] [--no-commit-check] [--no-prune]
 *   node tools/merge-branch.mjs --continue [--full] [--keep-going] [--no-prune]
 *   node tools/merge-branch.mjs --rerun <gate>… [--keep-going]
 *   node tools/merge-branch.mjs --stale [--keep-going]
 *   node tools/merge-branch.mjs --plan <branch> [--batch <branch>…] [--full]
 *   node tools/merge-branch.mjs --full [--keep-going]
 *   node tools/merge-branch.mjs --passed
 *   node tools/merge-branch.mjs --prune [--plan]
 *   node tools/merge-branch.mjs --drop-batch
 *
 * Exit codes: 0 merged and every gate passed (flakes named), or pruned (a branch kept is the rule working),
 * or the full set passed the checkout, after a re-run on it or not · 1 a conflict, a failed gate, a failed
 * commit check, or a checkout the full set has not passed · 2 refused (usage, not on main, a dirty tree, an
 * unknown branch or gate) or a tool error.
 */
import { spawn, spawnSync } from 'node:child_process';
import {
  closeSync, existsSync, mkdirSync, openSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync, writeSync,
} from 'node:fs';
import { basename, dirname, join, relative, resolve } from 'node:path';
import { gluedLabels, records } from './doc-duplicates.mjs';
import { isMain, renameHeld, stagedTree, writeAtomic } from './fsx.mjs';

export const MAIN = 'main';
const SCRATCH = 'local/scratch';
const STATE = `${SCRATCH}/merge-batch.json`;
const SHUTDOWN = 'dotnet build-server shutdown';
const KINDS = ['check', 'suite', 'rehearsal'];

/**
 * Which gates passed which tree (GATE3): gitignored, under `local/`, and outside `local/scratch/`, which
 * holds what a merge can throw away. `--rerun` reads the merge's own verdicts from the batch record; this
 * one outlives the batch, for the stage.
 */
export const VERDICTS_FILE = 'local/gate-verdicts.json';

/** The command that runs the full set on the checkout as it stands: what a refused stage names when nothing smaller would do. */
export const FULL_COMMAND = 'node tools/merge-branch.mjs --full';

/** The command that runs each stale gate again on the checkout as it stands (GATE6b): what a refused stage names when it would do. */
export const STALE_COMMAND = 'node tools/merge-branch.mjs --stale';

/**
 * The gates that run the `Process` test category (MOD8): every test in one starts a real process or runs a
 * real tick, so a failure there may be the load and is re-run alone once. Named by the settings file those
 * gates run with, not by a list of classes: a list goes stale the day a new real-process class is written,
 * and one did (a landing plugin's and a protocol chat's flakes read as plain failures).
 */
export const isProcessGate = (gate) => /process\.runsettings/.test(gate.run);

/**
 * The long gates (GATE5): the real-process halves and the deployment rehearsal, which took 30 to 70 minutes
 * of a merge between them. A merge never runs one; the full set does, and a stage requires it. Named by
 * what they run, as `isProcessGate` is, so a renamed gate or a new Process half is still one.
 */
export const isLongGate = (gate) => isProcessGate(gate) || /\brehearse:deploy\b/.test(gate.run);

export const USAGE = [
  'usage: node tools/merge-branch.mjs <branch> [--batch <branch>…] [--full] [--keep-going] [--no-commit-check] [--no-prune]',
  '       node tools/merge-branch.mjs --continue [--full] [--keep-going] [--no-prune]',
  '       node tools/merge-branch.mjs --rerun <gate>… [--keep-going]',
  '       node tools/merge-branch.mjs --stale [--keep-going]',
  '       node tools/merge-branch.mjs --plan <branch> [--batch <branch>…] [--full]',
  '       node tools/merge-branch.mjs --full [--keep-going]',
  '       node tools/merge-branch.mjs --passed',
  '       node tools/merge-branch.mjs --prune [--plan]',
  '       node tools/merge-branch.mjs --drop-batch',
].join('\n');

/** Why the tool stopped before doing its job. Exit 2 is a refusal; 1 is the branch failing a rule. */
export class Refusal extends Error {
  constructor(message, exitCode = 2, usage = false) {
    super(message);
    this.exitCode = exitCode;
    this.usage = usage;
  }
}

const usageError = (message) => new Refusal(message, 2, true);

// ---------------------------------------------------------------------------------------------------
// Arguments

export function parseArgs(argv) {
  const options = {
    branches: [], keepGoing: false, commitCheck: true, resume: false, dropBatch: false, plan: false, prune: false, autoPrune: true,
    full: false, rerun: [], passed: false, stale: false,
  };
  let batch = false;
  let rerun = false;
  let positional = 0;
  let batched = 0;
  const flags = [];
  for (const arg of argv) {
    if (arg.startsWith('-')) flags.push(arg);
    if (arg === '--batch') {
      if (batch) throw usageError('--batch is given once, before the branches it adds');
      if (positional === 0) throw usageError('--batch follows the first branch: <branch> --batch <branch>…');
      batch = true;
    } else if (arg === '--keep-going') options.keepGoing = true;
    else if (arg === '--no-commit-check') options.commitCheck = false;
    else if (arg === '--continue') options.resume = true;
    else if (arg === '--drop-batch') options.dropBatch = true;
    else if (arg === '--plan') options.plan = true;
    else if (arg === '--prune') options.prune = true;
    else if (arg === '--no-prune') options.autoPrune = false;
    else if (arg === '--full') options.full = true;
    else if (arg === '--passed') options.passed = true;
    else if (arg === '--stale') options.stale = true;
    else if (arg === '--rerun') rerun = true;
    else if (arg.startsWith('-')) throw usageError(`unknown option '${arg}'`);
    else if (rerun) {
      // GATE4: what follows --rerun are gates, not branches.
      if (options.rerun.includes(arg)) throw usageError(`'${arg}' is named twice`);
      options.rerun.push(arg);
    } else {
      if (batch) batched += 1;
      else if (++positional > 1) throw usageError('two branches without --batch: name the rest after --batch');
      if (options.branches.includes(arg)) throw usageError(`'${arg}' is named twice`);
      options.branches.push(arg);
    }
  }
  // `--plan` is a verb of its own, or the prune's plan; `--full` is one when it names no branch and modifies nothing else.
  const fullAlone = options.full && options.branches.length === 0 && !options.plan && !options.resume && !options.prune && !rerun && !options.stale;
  const verbs = [options.resume, options.dropBatch, options.plan && !options.prune, options.prune, rerun, options.passed, options.stale, fullAlone];
  if (verbs.filter(Boolean).length > 1) {
    throw usageError('give one of --continue, --rerun, --stale, --drop-batch, --plan, --passed, --prune (which takes --plan) and --full on its own');
  }
  if (batch && batched === 0) throw usageError('--batch names at least one branch after it');
  if (rerun) {
    if (options.rerun.length === 0) throw usageError('--rerun names the gates to run again: --rerun <gate>…');
    if (flags.some((flag) => flag !== '--rerun' && flag !== '--keep-going')) throw usageError('--rerun takes gate names and --keep-going');
    if (options.branches.length) throw usageError('--rerun takes no branch: it acts on the merge in place, or on the checkout when none is open');
  } else if (options.stale) {
    if (options.branches.length) throw usageError('--stale takes no branch: it acts on the checkout as it stands');
    if (flags.some((flag) => flag !== '--stale' && flag !== '--keep-going')) throw usageError('--stale takes only --keep-going');
  } else if (options.passed) {
    if (options.branches.length) throw usageError('--passed takes no branch: it asks about the checkout as it stands');
  } else if (options.prune) {
    if (!options.autoPrune) throw usageError('--no-prune is for a merge; --prune is the prune itself');
    if (options.full) throw usageError('--full is for a merge or the checkout; --prune runs no gate');
    if (options.branches.length) throw usageError('--prune takes no branch: it acts on every branch merged into main');
  } else if (options.resume || options.dropBatch) {
    if (options.branches.length) {
      throw usageError(`${options.resume ? '--continue' : '--drop-batch'} takes no branch: it acts on the batch already recorded`);
    }
  } else if (options.branches.length === 0 && !(options.full && !options.plan)) {
    // `--full` alone gates the checkout as it stands (GATE3); everything else names a branch.
    throw usageError('name the branch to merge');
  }
  return options;
}

// ---------------------------------------------------------------------------------------------------
// The plan: the declared gates, plus the workflow's own rehearsals, fast first

/** A name safe as one path segment: a log file, a scratch folder. */
export const slug = (name) => name.replace(/[^A-Za-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '') || 'gate';

/**
 * What kind of gate a command is, which orders it and never drops it: a devkit `dotnet run` and a tool's
 * `--check` (the orientation index, ORIENT1a) are checks, `npm run rehearse*` and `npm run test:*` are
 * rehearsals, and anything else is a suite. A command run through `tools/as-merged.mjs` (GATE1) is the kind of
 * what it runs: the universal gates read history as the merge would commit it, and are still the devkit's check.
 */
export function gateKind(run) {
  const command = run.trim().replace(/^node tools\/as-merged\.mjs\s+/, '');
  if (/^dotnet run\b/.test(command) || /^node tools\/\S+\.mjs --check$/.test(command)) return 'check';
  const script = /^npm run (\S+)$/.exec(command)?.[1];
  if (script && /^(rehearse|test:)/.test(script)) return 'rehearsal';
  return 'suite';
}

/** The scripts a workflow runs as whole steps (`run: npm run <script>`), in file order, each once. */
export function workflowScripts(text) {
  const scripts = text.replace(/\r\n/g, '\n').split('\n')
    .filter((line) => !/^\s*#/.test(line))
    .map((line) => /^\s*(?:-\s+)?run:\s*npm run (\S+)\s*$/.exec(line)?.[1])
    .filter(Boolean);
  return [...new Set(scripts)];
}

/**
 * The ordered gates: every declared gate, and every workflow script the declaration does not run. By
 * kind, fast first; within a kind, workflow-only gates first, then the declared order.
 */
export function gatePlan(declared, workflow = '') {
  const runs = new Set(declared.map((gate) => gate.run.trim()));
  const extras = workflowScripts(workflow)
    .filter((script) => !runs.has(`npm run ${script}`))
    .map((script) => ({ name: script, run: `npm run ${script}` }));
  const taken = new Set();
  return [...extras, ...declared]
    .map((gate, index) => ({ gate, index, kind: gateKind(gate.run) }))
    .sort((a, b) => KINDS.indexOf(a.kind) - KINDS.indexOf(b.kind) || a.index - b.index)
    .map(({ gate, kind }) => {
      let name = slug(gate.name);
      for (let n = 2; taken.has(name); n += 1) name = `${slug(gate.name)}-${n}`;
      taken.add(name);
      return {
        name,
        run: gate.run.trim(),
        kind,
        before: /\brehearse:deploy\b/.test(gate.run) ? [SHUTDOWN] : [],
        ...(gate.cwd ? { cwd: gate.cwd } : {}),
      };
    });
}

/** `daoris.gates.json`'s gates, read as the devkit reads them: a name, a command line, an optional cwd. */
export function readDeclared(root) {
  const file = join(root, 'daoris.gates.json');
  if (!existsSync(file)) throw new Refusal(`no daoris.gates.json at ${root}: there is no declared gate to run`);
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new Refusal(`daoris.gates.json is not valid JSON: ${error.message}`);
  }
  const gates = Array.isArray(parsed?.gates) ? parsed.gates : [];
  if (gates.length === 0) throw new Refusal('daoris.gates.json declares no gate');
  for (const gate of gates) {
    if (typeof gate?.name !== 'string' || typeof gate?.run !== 'string') {
      throw new Refusal("daoris.gates.json: every gate needs both 'name' and 'run'");
    }
  }
  return gates.map(({ name, run, cwd }) => (typeof cwd === 'string' ? { name, run, cwd } : { name, run }));
}

export function readPlan(root) {
  const workflow = join(root, '.github', 'workflows', 'release.yml');
  return gatePlan(readDeclared(root), existsSync(workflow) ? readFileSync(workflow, 'utf8') : '');
}

// ---------------------------------------------------------------------------------------------------
// GATE3: the gates a merge's paths can reach

/**
 * The gates every merge runs, whatever it changed: the universal gates scan every file, the code map
 * maps all the code, the orientation index reads every lane (ORIENT1a), and the CLI's `verify` holds the
 * records, the lanes, the canon and the tools' tests.
 */
export const BASELINE = Object.freeze(['universal', 'code-map', 'orient-index', 'cli']);

// The .NET suites with a test that reads the page's catalogues or sources: MOD9's incident was two of them.
const PAGE_READERS = ['service', 'driver', 'modules'];
const DESKTOP_SUITES = ['driver', 'modules', 'driver-process', 'modules-process'];
// What the CLI's sources reach; a narrow rule for one of its files names these as well, since the first rule decides.
const CLI_READERS = ['driver', 'rehearse', 'rehearse-family', 'web', 'deployment'];
// What the modules lane's sources reach, named by its narrow rule for the C# sources as well (MOD9c).
const MODULES_READERS = ['modules', 'modules-process', 'deployment'];

/**
 * Which gates can see a changed path, beyond the baseline: the first rule that places the path decides.
 * A rule places paths by glob (`paths`, the lanes file's grammar) or by a lane of `daoris.lanes.json`
 * (`lane`). Narrow rules come before the lanes, and the laneless paths after. `'*'` is every gate. A
 * path no rule places runs every gate, and so does a gate no rule names. A gate named here that the
 * repository does not run names nothing.
 *
 * Held by `merge-branch.test.ts` to what each .NET suite reads of the repository, to what each rehearsal
 * imports and starts, and to every tracked file and lane. What a fast suite may read is given generously:
 * it costs minutes. The precision is in the Process halves and the rehearsals, which cost the hours.
 */
export const REACH = Object.freeze([
  {
    paths: ['daoris.gates.json', 'daoris.lanes.json', 'package.json', 'package-lock.json', '.gitattributes', '.gitignore', 'tools/fsx.mjs'],
    gates: '*',
    why: 'it changes how every gate runs or reaches a path: the gates, lanes, scripts, line endings, what is tracked, or the file helpers every rehearsal imports',
  },
  { paths: ['tools/rehearsal-kit.mjs'], gates: ['rehearse', 'rehearse-family', 'deployment'], why: "the rehearsals' kit" },
  { paths: ['tools/release-rehearsal.mjs', 'tools/release-prep.mjs', 'tools/service-publish.mjs'], gates: ['rehearse'], why: 'release tooling' },
  { paths: ['tools/stage-package.mjs'], gates: ['rehearse', 'deployment'], why: "every pack runs it: the release rehearsal's, and the install's doctrine tool" },
  { paths: ['tools/family-rehearsal.mjs', 'tools/setup-kit.mjs', 'tools/usage-report.mjs'], gates: ['rehearse-family'], why: 'the family rehearsal, what it imports, and what it runs' },
  {
    paths: ['tools/desktop-publish.mjs', 'tools/processes.mjs'],
    gates: ['deployment', 'rehearse-family'],
    why: 'the publish the deployment rehearsal drives, and what it imports; the family rehearsal runs usage-report, which imports it',
  },
  { paths: ['tools/deployment-rehearsal.mjs', 'tools/desktop.mjs', 'tools/cdp.mjs'], gates: ['deployment'], why: 'the deployment rehearsal and what it imports' },
  { paths: ['src/Daoris.Devkit/**'], gates: ['devkit'], why: 'its own suite; it is also the universal gates and the code map, which every merge runs' },
  { paths: ['src/Daoris.Desktop/process.runsettings'], gates: ['driver-process', 'modules-process'], why: "the Process halves' settings (MOD8)" },
  // Before the driver's tests, which would place it first: the page's twin reads it too.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/kept-names.json'],
    gates: ['driver', 'web'],
    why: "the driver's KeptNamesTwinTests and the page's sessionActs vitest read the kept names' table (SESSDEL1c)",
  },
  {
    paths: ['src/Daoris.Desktop/Directory.Build.props', 'src/Daoris.Desktop/Directory.Packages.props'],
    gates: [...DESKTOP_SUITES, 'rehearse-family', 'deployment'],
    why: 'every desktop project builds with it: the suites, the headless host the family rehearsal drives, and the deployed shell',
  },
  { paths: ['src/Daoris.Desktop/README.md'], gates: ['modules', 'service'], why: "the modules' route test reads its table, and the service's suite reads this repository's documents" },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/**'], gates: ['driver', 'driver-process'], why: "the driver's tests, both halves" },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/**'], gates: ['modules', 'modules-process'], why: "the modules' tests, both halves" },
  // Before the service's tests, which would place it first: the driver's twin reads it too.
  {
    paths: ['src/Daoris.Service/Daoris.Service.Tests/fixtures/history-words.json'],
    gates: ['service', 'driver'],
    why: "the service's and the driver's HistoryWordsTwinTests read the history words' table (HIST1m)",
  },
  { paths: ['src/Daoris.Service/Daoris.Service.Tests/**', 'src/Daoris.Service/Daoris.Service.Http.Tests/**'], gates: ['service'], why: "the service's tests" },
  { paths: ['src/Daoris.Cli/test/fixtures/vendor/**'], gates: ['driver', 'driver-process'], why: "the driver's release-channel tests read the vendor's files" },
  { paths: ['src/Daoris.Cli/test/fixtures/shell-words.json'], gates: ['driver', 'web'], why: "the driver's ShellWord twin and the page's shellWord read the shell-word table (ACCTQUOTE1b)" },
  { paths: ['src/Daoris.Cli/test/fixtures/account-reads.json'], gates: ['driver'], why: "the driver's AccountReads twin reads the shared reads table (AGENTREAD1b)" },
  { paths: ['src/Daoris.Cli/test/fixtures/name-case.json'], gates: ['driver'], why: "the driver's NameCaseTests reads the shared name-case table (CASEFOLD1d)" },
  { paths: ['tools/orient-index-fixtures/**'], gates: ['service'], why: "the service's DecisionNotes twin reads the digest's note table (ORIENT1h)" },
  {
    paths: ['src/Daoris.Cli/src/documents.ts'],
    gates: ['service', ...CLI_READERS],
    why: "the CLI's own, and the service's roles twin reads its ROLES from the file (ORIENT2e2)",
  },
  {
    paths: ['.claude/knowledge/adoption.md'],
    gates: ['service', 'driver'],
    why: "the driver's SetupBrief twin reads the playbook's steps (LAYOUT7, MOD9b), and the service's suite scans this repository's documents",
  },
  // MOD9c: the driver's scans enumerate every C# source of the modules and the app, to hold the next spawn site or ask
  // written there, so the files are not named one by one: today's would miss that next one.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Modules/**/*.cs', 'src/Daoris.Desktop/Daoris.Desktop.App/**/*.cs'],
    gates: [...MODULES_READERS, 'driver'],
    why: "the modules' own, and the driver's NoConsoleWindow and PullRequestOccasion scans read every C# source of the modules and the app (MOD9c)",
  },
  { paths: ['src/Daoris.Cli/test/**'], gates: [], why: "the CLI's tests, which verify runs at every merge" },
  // GATE6: a fix to a browser test was re-gated by the web gate alone, and the stage then voided every other verdict.
  { paths: ['src/Daoris.Web/e2e/**'], gates: ['web'], why: "the page's end-to-end specs, which only the web gate runs: no .NET suite reads them" },
  { lane: 'web-shell', gates: ['web', ...PAGE_READERS], why: "the page's own suites, and the .NET suites that read its catalogues and sources (MOD9's incident)" },
  { lane: 'web-settings', gates: ['web', ...PAGE_READERS], why: "the page's own suites, and the .NET suites that read its catalogues and sources (MOD9's incident)" },
  {
    lane: 'driver',
    gates: [...DESKTOP_SUITES, 'rehearse-family', 'deployment'],
    why: 'the modules build on it, the family rehearsal drives its headless host, and the deployed shell runs its loop (D60): ServiceHostLocator, the staged build, a session',
  },
  { lane: 'modules', gates: MODULES_READERS, why: 'the application, its launcher and the modules it hosts: the deployed shell (D60)' },
  {
    lane: 'service',
    gates: ['service', 'devkit', 'driver', 'rehearse-family', 'web', 'deployment'],
    why: "its suite; the devkit's and the driver's read its sources; the family rehearsal, the page's end-to-end suite and the install run its HTTP host",
  },
  {
    lane: 'cli',
    gates: CLI_READERS,
    why: "the release rehearsal packs it, the family rehearsal and the page's end-to-end suite run it, the install carries it, and the driver's tests read its permissions",
  },
  { lane: 'tools', gates: [], why: "the rest of the tooling, which the CLI's suite tests" },
  { lane: 'records', gates: ['service'], why: "the service's suite scans this repository's own documents" },
  { paths: ['canon/**'], gates: ['service', 'rehearse', 'rehearse-family'], why: 'the package ships it, the examples must be current with it, and the service scans the doctrine synced from it' },
  {
    paths: ['examples/plugins/**'],
    gates: ['service', 'driver', 'rehearse-family', 'web', 'deployment'],
    why: "the example family the rehearsals and the end-to-end suite run on, and the offers the install lays out, which the driver's PluginOffer test reads as they stand (D103, MOD9b)",
  },
  { paths: ['examples/**'], gates: ['service', 'rehearse-family', 'web'], why: 'the example family the family rehearsal and the end-to-end suite run on' },
  { paths: ['README.md', 'LICENSE'], gates: ['service', 'rehearse'], why: 'staged into the package the release rehearsal packs' },
  {
    paths: ['docs/index/**'],
    gates: [],
    why: 'written into every merge by tools/orient-index.mjs and held by its check, which every merge runs; no suite reads it',
  },
  {
    paths: ['docs/**', 'CHANGELOG.md', 'CLAUDE.md', 'ROADMAP.md', 'AGENTS.md', 'daoris.json', 'daoris.lock', '.claude/**'],
    gates: ['service'],
    why: "the service's suite scans this repository's own doctrine and decisions",
  },
  { paths: ['.mcp.json'], gates: [], why: "the harness's settings" },
]);

// ---------------------------------------------------------------------------------------------------
// ORIENT1a: what a merge writes rather than merges

/**
 * Folders a tool writes from the tree, never merged: the orientation index's line numbers move with nearly
 * every change, so two branches' copies disagree wherever both touched a large file, and neither copy speaks
 * for the merge. So each merge writes them again from the merged tree before its gates, a conflict in one is
 * resolved by that writing, and the parent reads the result in the merge's diff. A repository without the
 * tool writes nothing.
 */
export const GENERATED = Object.freeze([{ folder: 'docs/index', tool: 'tools/orient-index.mjs' }]);

/** Whether a path is one a merge writes rather than merges. */
export const isGenerated = (path) => GENERATED.some(({ folder }) => path.startsWith(`${folder}/`));

/** Each generated folder written from the tree as it stands, and staged; one line each. A tool that fails refuses the merge's gates. */
function writeGenerated(root) {
  for (const { folder, tool } of GENERATED) {
    if (!existsSync(join(root, tool))) continue;
    const run = spawnSync(process.execPath, [tool], { cwd: root, encoding: 'utf8' });
    if (run.status !== 0) throw new Refusal(`${tool} could not write ${folder}/ into the merge:\n${(run.stderr || run.stdout).trim()}`, 1);
    git(root, ['add', '-A', '--', folder]);
    console.log(`${folder}/: written from the merged tree (${run.stdout.trim().replace(/^[\w-]+: /, '')})`);
  }
}

// ---------------------------------------------------------------------------------------------------
// MERGEJOIN1: two notes a union merge leaves touching

/**
 * A decision's file with one blank line set before each note's label that follows a non-blank line outside a fence,
 * and every other byte as it was; with each label set apart and its line once set (1-based).
 *
 * Two branches that each append a note to one decision both open it with a blank line. Git's union merge takes that
 * line, the same on both sides, as common to them and keeps it once, then keeps both notes one after the other, so
 * the second label lands straight under the first note's last line. `doc-duplicates` refuses that (D134 §3.4), and
 * on 2026-10-04 three merges in a row failed `verify` on it, each mended by hand with one blank line. What is set
 * apart is the check's own fact (`gluedLabels`), so the two never disagree. A CRLF file gains a CRLF blank line.
 */
export function setApart(text) {
  const lines = text.split('\n');
  const glued = new Set(gluedLabels(lines));
  if (glued.size === 0) return { text, set: [] };
  const out = [];
  const set = [];
  lines.forEach((line, i) => {
    if (glued.has(i)) {
      out.push(lines[i - 1].endsWith('\r') ? '\r' : '');
      set.push({ line: out.length + 1, label: line.replace(/\r$/, '') });
    }
    out.push(line);
  });
  return { text: out.join('\n'), set };
}

/** How a note set apart is named: by the first task its label names (`UX6e`, not the decision `D130`), else by its label. */
export function noteName(label) {
  const span = label.replace(/^\*+/, '').split('*')[0].trim();
  const task = /\b[A-Z]{2,}\d+[a-z]*\b/.exec(span)?.[0];
  return task ? `${task} note` : `note "${span.length > 60 ? `${span.slice(0, 60)}…` : span}"`;
}

/**
 * Each decision file the merge in place changed, with its glued notes set apart and staged; one line per note. Only
 * the decisions record as a folder carries the fact: the other union records `doc-duplicates` checks are checked for
 * a line twice, which a join cannot make, and a record still one file is checked for a number twice.
 */
function setApartNotes(root) {
  const folders = records(root).filter((record) => record.kind === 'decisions').map((record) => record.file.slice(0, -'/*.md'.length));
  if (folders.length === 0) return;
  // What the merge brought, the parent's resolutions with it; a deleted file has nothing to set apart.
  const changed = zPaths(git(root, ['diff', '-z', '--name-only', '--no-renames', '--diff-filter=d', 'HEAD']).out);
  for (const path of changed) {
    if (!folders.some((folder) => path.startsWith(`${folder}/`) && /^[^/]+\.md$/.test(path.slice(folder.length + 1)))) continue;
    const file = join(root, path);
    const { text, set } = setApart(readFileSync(file, 'utf8'));
    if (set.length === 0) continue;
    writeAtomic(file, text);
    git(root, ['add', '--', path]);
    for (const note of set) console.log(`set apart: ${basename(path, '.md')}'s ${noteName(note.label)} (${path}:${note.line})`);
  }
}

// ORIENT1c: the workspace's knowledge server, rebuilt from the merged tree

/** The tool that builds the knowledge server agents ask; a repository without it builds nothing. */
export const KNOWLEDGE_SERVER = 'tools/knowledge-server.mjs';

/**
 * Once a merge's gates pass, the knowledge server is rebuilt from the merged tree when the service's sources
 * changed, so a session started after the merge, in any worktree, runs what main holds and never builds it at
 * its start. An instrument, not a gate: a build that fails is said and fails nothing, and sessions keep the
 * build they had.
 */
function refreshKnowledgeServer(root) {
  if (!existsSync(join(root, KNOWLEDGE_SERVER))) return;
  const run = spawnSync(process.execPath, [KNOWLEDGE_SERVER, 'build'], { cwd: root, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const last = (text) => (text ?? '').trim().split('\n').filter(Boolean).at(-1) ?? '';
  console.log(run.status === 0
    ? `knowledge server: ${last(run.stdout).replace(/^knowledge-server: /, '')}`
    : `knowledge server: NOT rebuilt, and the merge stands (${last(run.stderr) || last(run.stdout) || `exit ${run.status}`}); npm run knowledge:build says why`);
}

/** The first rule that places a path, or null: by glob, or by a lane whose paths own it. */
function ruleFor(path, rules, lanes) {
  return rules.find((rule) => {
    if (rule.paths) return laneMatcher(rule.paths)(path);
    const lane = lanes.find((candidate) => candidate.id === rule.lane);
    return lane ? laneMatcher(lane.paths)(path) : false;
  }) ?? null;
}

const andMore = (paths) => `${paths[0]}${paths.length > 1 ? ` and ${paths.length - 1} more` : ''}`;

/** Every gate some rule of the table names: a gate none names is reached by every path. */
const namedGates = (reach) => new Set(reach.flatMap((rule) => (rule.gates === '*' ? [] : rule.gates)));

/**
 * Which of the plan's gates a merge runs, and why each runs or is skipped, in the plan's order. A gate is
 * `reached` when it is the baseline, a changed path's rule names it, a path has no rule, or no rule names
 * the gate at all. A merge runs what is reached but the long gates (GATE5), which say they are in the full
 * set before staging; `full` runs every gate. `reached` is the lane table's answer, run or not, which is what
 * its tests hold to the code. `unplaced` is the paths no rule placed.
 */
export function selectGates(plan, changed, { lanes = [], full = false, reach = REACH } = {}) {
  const names = new Set(plan.map((gate) => gate.name));
  const named = namedGates(reach);
  const reachedBy = new Map();
  const unplaced = [];
  for (const path of changed) {
    const rule = ruleFor(path, reach, lanes);
    if (!rule) {
      unplaced.push(path);
      continue;
    }
    for (const gate of rule.gates === '*' ? names : rule.gates) {
      if (!names.has(gate)) continue;
      if (!reachedBy.has(gate)) reachedBy.set(gate, { paths: [], rule });
      reachedBy.get(gate).paths.push(path);
    }
  }
  const reachOf = (gate) => {
    if (unplaced.length) return { reached: true, why: `everything runs: no rule of the lane table places ${andMore(unplaced)}` };
    const by = reachedBy.get(gate.name);
    if (by) return { reached: true, why: `${andMore(by.paths)} (${by.rule.lane ? `${by.rule.lane}: ` : ''}${by.rule.why})` };
    if (!named.has(gate.name)) return { reached: true, why: 'the lane table does not name it, so it runs at every merge' };
    return { reached: false, why: 'no changed path reaches it' };
  };
  const gates = plan.map((gate) => {
    if (BASELINE.includes(gate.name)) return { gate, run: true, reached: true, why: 'runs at every merge' };
    const { reached, why } = reachOf(gate);
    if (full) return { gate, run: true, reached, why: '--full runs every gate' };
    if (isLongGate(gate)) {
      return { gate, run: false, reached, why: `in the full set before staging${reached ? `; ${why.replace(/^everything runs: /, '')}` : ''}` };
    }
    return { gate, run: reached, reached, why };
  });
  return { gates, unplaced };
}

/**
 * Of `paths`, the ones that reach `gate` by the lane table, read as `selectGates` reads it (GATE6): every path reaches
 * a baseline gate and a gate no rule names, a path no rule places reaches every gate, and any other reaches the gates
 * its rule names. A verdict stays good until one of these changes, so the stage and `--rerun` ask this of the paths
 * changed since it was given. The test of the table checks that it and `selectGates` agree on every case.
 */
export function pathsReaching(gate, paths, { lanes = [], reach = REACH } = {}) {
  if (BASELINE.includes(gate) || !namedGates(reach).has(gate)) return [...paths];
  return paths.filter((path) => {
    const rule = ruleFor(path, reach, lanes);
    return !rule || rule.gates === '*' || rule.gates.includes(gate);
  });
}

/** The selection as lines: each gate run, numbered, with why; then each skipped, with why. */
export function selectionLines(gates) {
  const run = gates.filter((entry) => entry.run);
  const skipped = gates.filter((entry) => !entry.run);
  const out = run.map((entry, i) => `  ${pad(`${i + 1}.`, 4)}${pad(entry.gate.name, 16)} ${pad(entry.gate.kind, 10)} ${entry.why}`);
  if (skipped.length) {
    out.push(`  skipped (${skipped.length}):`);
    for (const entry of skipped) out.push(`      ${pad(entry.gate.name, 16)} ${pad(entry.gate.kind, 10)} ${entry.why}`);
  }
  return out;
}

// ---------------------------------------------------------------------------------------------------
// GATE4: a fixed gate re-runs alone

/**
 * What `--rerun` runs and keeps, in the plan's order: each named gate, each chosen gate with no verdict yet
 * (one a failure stopped, or one a fix's paths now reach), and each chosen gate whose verdict a path changed
 * since reaches (GATE6: `stale` maps it to those paths, none when its tree cannot be compared); every other
 * chosen gate's verdict is kept. `fresh` is the gates run for want of a verdict, and `stale` the ones run
 * again for a changed path, each with the verdict it no longer keeps.
 */
export function rerunPlan(plan, chosen, results, named, stale = {}) {
  const isStale = (name) => Object.hasOwn(stale, name);
  const run = plan.filter((name) => named.includes(name) || (chosen.includes(name) && (!results[name] || isStale(name))));
  const kept = plan.filter((name) => chosen.includes(name) && !named.includes(name) && results[name] && !isStale(name))
    .map((name) => ({ name, verdict: results[name].verdict, at: results[name].at }));
  const fresh = run.filter((name) => !named.includes(name) && !results[name]);
  const again = run.filter((name) => !named.includes(name) && results[name])
    .map((name) => ({ name, verdict: results[name].verdict, at: results[name].at, paths: stale[name] }));
  return { run, kept, fresh, stale: again };
}

/** A moment as people read it in a summary: minutes, in UTC, which is how the record keeps it. */
export const when = (iso) => (typeof iso === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(iso) ? `${iso.slice(0, 10)} ${iso.slice(11, 16)} UTC` : 'an unknown time');

const nowIso = () => new Date().toISOString().replace(/\.\d+Z$/, 'Z');

// ---------------------------------------------------------------------------------------------------
// The verdicts record (GATE3): which gate passed which tree

/** `local/gate-verdicts.json`, or an empty record where there is none or it does not read. */
export function readVerdicts(root) {
  const file = join(root, VERDICTS_FILE);
  if (!existsSync(file)) return { schema: 1, verdicts: [] };
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8'));
    return { schema: 1, verdicts: Array.isArray(parsed?.verdicts) ? parsed.verdicts.filter((entry) => typeof entry?.gate === 'string') : [] };
  } catch {
    return { schema: 1, verdicts: [] };
  }
}

/** Verdicts appended, newest last, the oldest dropped past `keep`: written beside, then renamed. */
export function addVerdicts(root, entries, { keep = 400 } = {}) {
  const file = join(root, VERDICTS_FILE);
  const verdicts = [...readVerdicts(root).verdicts, ...entries].slice(-keep);
  const record = {
    schema: 1,
    _why: 'Written by tools/merge-branch.mjs (GATE3): each gate verdict with the tree it ran on. publish:desktop asks `merge-branch --passed`, which reads it.',
    verdicts,
  };
  writeAtomic(file, `${JSON.stringify(record, null, 2)}\n`);
}

/**
 * The checkout's content as a tree id, as `git add -A` would stage it (`stagedTree`, which `as-merged.mjs`
 * builds its commit on too): the person's index and a merge in progress untouched. Null when git cannot say.
 */
export function contentTree(root) {
  try {
    return stagedTree(root);
  } catch {
    return null;
  }
}

/** The paths that differ between two trees, or null when git cannot say (a tree long gone). */
function treeDrift(root, from, to) {
  if (!from || !to) return null;
  if (from === to) return [];
  const diff = git(root, ['diff', '--name-only', '--no-renames', from, to], { allowFail: true });
  return diff.status === 0 ? lines(diff.out) : null;
}

/**
 * The known records the parent writes after gates, when declared in the steward's lane or as union.
 * Neither declaration may make gate policy or source a record: both come from the current checkout,
 * so trusting their patterns alone would let an edit forgive itself and untested code. A union pattern
 * names the record at the root here, where git reads a pattern with no slash at any depth: the canon's
 * own `CHANGELOG.md` merges by union too, and the package ships it.
 */
export function recordMatcher({ lanes = [], union = [] } = {}) {
  const steward = lanes.find((lane) => lane.steward);
  const stewardOwns = steward ? laneMatcher(steward.paths) : () => false;
  const unionPatterns = union.map((pattern) => globToRegExp(pattern.replace(/^\//, '')));
  const known = new Set(['TASKS.md', 'docs/task-archive.md', 'CHANGELOG.md', 'docs/FIX-LOG.md', 'docs/README.md', '.claude/knowledge/twins.md']);
  const safe = (path) => known.has(path) || /^docs\/decisions\/D\d+\.md$/.test(path);
  return (path) => safe(path) && (stewardOwns(path) || unionPatterns.some((pattern) => pattern.test(path)));
}

/**
 * What a verdict is judged by against the checkout's `tree` (GATE6): the paths another tree differs from it by, asked
 * of git once per tree; which of them are the records; which reach a gate, by the lane table and this repository's
 * lanes. `--passed` and `--rerun` judge by the same.
 */
function standingFor(root, tree) {
  const lanes = readLanes(root)?.lanes ?? [];
  const drifts = new Map();
  return {
    tree,
    drift: (from) => {
      if (!drifts.has(from)) drifts.set(from, treeDrift(root, from, tree));
      return drifts.get(from);
    },
    isRecord: recordMatcher({ lanes, union: unionRecords(root) }),
    reaching: (gate, paths) => pathsReaching(gate, paths, { lanes }),
  };
}

/** A verdict that passed: PASS, or FLAKE, a pass that was said. */
const passing = (verdict) => verdict === 'PASS' || verdict === 'FLAKE';

/**
 * How a verdict given on tree `from` stands for `tree`, for its gate (GATE6), as `{ standing, paths, records }`:
 * `exact` on this tree; `records` when the two differ only by the records (`isRecord`, forgiven since GATE3);
 * `unreached` when every other path they differ by is one the gate cannot see (`reaching`, the lane table), those
 * paths in `paths`; else `stale`, `paths` naming the ones that reach it, or none when the trees cannot be compared
 * (a verdict with no tree, or one git no longer has: `drift` says null). `records` is the records they differ by.
 */
export function verdictStanding({ from, tree, gate, drift, isRecord, reaching }) {
  if (!from || !tree) return { standing: 'stale', paths: [], records: [] };
  if (from === tree) return { standing: 'exact', paths: [], records: [] };
  const changed = drift(from);
  if (!changed) return { standing: 'stale', paths: [], records: [] };
  const records = changed.filter(isRecord);
  const rest = changed.filter((path) => !isRecord(path));
  if (!rest.length) return { standing: 'records', paths: [], records };
  const reached = reaching(gate, rest);
  return reached.length ? { standing: 'stale', paths: reached, records } : { standing: 'unreached', paths: rest, records };
}

/**
 * Whether the full set passed `tree`: for each gate of the plan, its newest verdict that still stands for this tree
 * (`verdictStanding`: on it, or on one that differs from it only by the records and by paths that gate cannot reach)
 * must pass. `missing` names each gate that has no such passing verdict. Each gate carries the verdict that counts
 * and how it stands; a gate that does not pass, when a newer verdict no longer stands, carries that one as `stale`,
 * with the paths that made it so.
 */
export function stageJudgement({ plan, verdicts, tree, drift, isRecord, reaching }) {
  const drifts = new Map();
  const driftOf = (from) => {
    if (!drifts.has(from)) drifts.set(from, drift(from));
    return drifts.get(from);
  };
  const gates = plan.map((name) => {
    let newest = null;
    let standing = null;
    let stale = null;
    for (const entry of verdicts) {
      if (entry.gate !== name) continue;
      const how = verdictStanding({ from: entry.tree, tree, gate: name, drift: driftOf, isRecord, reaching });
      if (how.standing !== 'stale') {
        if (!newest || String(entry.at) >= String(newest.at)) [newest, standing] = [entry, how];
      } else if (!stale || String(entry.at) >= String(stale.verdict.at)) {
        stale = { ...how, verdict: entry };
      }
    }
    // A newer verdict that no longer stands explains a gate that does not count; one that counts needs no excuse.
    const said = stale && (!newest || (String(stale.verdict.at) > String(newest.at) && !passing(newest.verdict)));
    return { name, verdict: newest, exact: standing?.standing === 'exact', standing, stale: said ? stale : null };
  });
  const missing = gates.filter((gate) => !gate.verdict || !passing(gate.verdict.verdict)).map((gate) => gate.name);
  return { passed: missing.length === 0, missing, gates };
}

// ---------------------------------------------------------------------------------------------------
// GATE6b: a gate re-run on the checkout as it stands

/**
 * The smallest command that would make the stage pass the checkout, from its judgement's `gates` (in the plan's order),
 * or null when it passes: `--stale` when every gate it lacks is stale; `--rerun` naming each that failed or has no
 * verdict, when that runs fewer gates than the plan (it runs each stale one too, `checkoutRerun`); else `--full`. `runs`
 * is what the command would run. A refusal named `--full` whatever was missing, which cost a whole run, about an hour
 * and a half, for one gate that flaked or one fix committed after the full set.
 */
export function stageRemedy(gates) {
  const stale = gates.filter((gate) => gate.stale).map((gate) => gate.name);
  const lacking = gates.filter((gate) => !gate.stale && !(gate.verdict && passing(gate.verdict.verdict))).map((gate) => gate.name);
  if (!stale.length && !lacking.length) return null;
  if (!lacking.length) return { command: STALE_COMMAND, what: stale.length === 1 ? 'the stale gate' : `the ${stale.length} stale gates`, runs: stale };
  const runs = gates.map((gate) => gate.name).filter((name) => stale.includes(name) || lacking.includes(name));
  if (runs.length === gates.length) return { command: FULL_COMMAND, what: 'every gate', runs };
  return { command: `node tools/merge-branch.mjs --rerun ${lacking.join(' ')}`, what: runs.length === 1 ? 'the gate it lacks' : `the ${runs.length} gates it lacks`, runs };
}

/** The line a refused stage ends with, which `publish:desktop` reads for the command to name. */
const remedyLine = (remedy) => `  Run ${remedy.what} on it: ${remedy.command}`;

/**
 * What `--rerun <gate>…` and `--stale` run on the checkout, from the stage's judgement of its `gates`, as `rerunPlan`
 * reads a merge's, in the plan's order: each named gate, then each the stage calls stale (its newest verdict no longer
 * stands for this tree, and no newer one that does passed); every other gate's verdict is kept as the stage counts it,
 * a failure a failure. A gate with no verdict at all runs only when named, and `none` lists the rest of those: the
 * full set never ran them on anything this tree can be judged by.
 */
export function checkoutRerun(gates, named) {
  const results = {};
  const stale = {};
  for (const gate of gates) {
    if (gate.stale) {
      results[gate.name] = gate.stale.verdict;
      stale[gate.name] = gate.stale.paths;
    } else if (gate.verdict) results[gate.name] = gate.verdict;
  }
  const names = gates.map((gate) => gate.name);
  const { run, kept, stale: again } = rerunPlan(names, names.filter((name) => results[name]), results, named, stale);
  return { run, kept, stale: again, none: names.filter((name) => !results[name] && !named.includes(name)) };
}

// ---------------------------------------------------------------------------------------------------
// Flakes

const FAILED_TEST = /^\s+Failed (\S.*?) \[[^\]]*\]\s*$/;
const SUMMARY = /^\s*(?:Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)/;

/** The simple name of a test's class: the segment before the method, a nested class's outer name. */
export const testClass = (test) => (test.split('.').at(-2) ?? '').split('+')[0];
const short = (test) => test.split('.').slice(-2).join('.');

/**
 * What a `dotnet test` run says it failed: each failed test once (a theory by its method), how many
 * `Failed` lines there were, and the summary lines' counts summed (a solution prints one per project).
 */
export function testSummary(output) {
  const summary = { failed: [], failedLines: 0, failedCount: 0, passedCount: 0, summaries: 0 };
  for (const line of output.replace(/\r\n/g, '\n').split('\n')) {
    const failed = FAILED_TEST.exec(line);
    if (failed) {
      summary.failedLines += 1;
      const test = failed[1].replace(/\(.*$/, '');
      if (!summary.failed.includes(test)) summary.failed.push(test);
      continue;
    }
    const counts = SUMMARY.exec(line);
    if (counts) {
      summary.summaries += 1;
      summary.failedCount += Number(counts[1]);
      summary.passedCount += Number(counts[2]);
    }
  }
  return summary;
}

/**
 * Whether a failed run's failures are re-run alone: all of them named, and all real-process tests. The caller
 * asks only of a Process gate, where every test is one, so the default says yes.
 */
export function flakeDecision(output, isProcess = () => true) {
  const summary = testSummary(output);
  if (summary.summaries === 0) return { rerun: [], reason: 'the suite printed no result: it did not build, or its run was cut off' };
  if (summary.failedLines === 0) return { rerun: [], reason: 'no failing test is named: the run failed outside a test' };
  if (summary.failedLines !== summary.failedCount) {
    return { rerun: [], reason: `${summary.failedCount} failed but ${summary.failedLines} are named` };
  }
  const others = summary.failed.filter((test) => !isProcess(test));
  if (others.length) return { rerun: [], reason: `not a real-process test: ${others.map(short).join(', ')}` };
  return { rerun: summary.failed };
}

export const rerunCommand = (run, test) => `${run.trim()} --no-build --filter "FullyQualifiedName=${test}"`;

// ---------------------------------------------------------------------------------------------------
// PROC1: where a Process half's time goes

/** A Process gate's command with a trx logger writing each test's duration to `file`. Its re-runs add none. */
export const trxCommand = (run, file) => `${run.trim()} --logger "trx;LogFileName=${file}"`;

const XML_ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" };
const attributes = (tag) => Object.fromEntries([...tag.matchAll(/([A-Za-z_:][\w:.-]*)="([^"]*)"/g)]
  .map(([, name, value]) => [name, value.replace(/&(amp|lt|gt|quot|apos);/g, (_, entity) => XML_ENTITIES[entity])]));

/** A trx duration, `hh:mm:ss.fffffff`, in milliseconds; absent is none. */
function trxMs(duration) {
  const parts = /^(\d+):(\d{2}):(\d{2}(?:\.\d+)?)$/.exec(duration ?? '');
  return parts ? Math.round(((Number(parts[1]) * 60 + Number(parts[2])) * 60 + Number(parts[3])) * 1000) : 0;
}

/**
 * The slowest classes of a trx, at most `count`: each class's tests' durations summed, slowest first, by the
 * class's simple name (a nested class keeps its outer name, `Outer+Inner`). A test's class is its
 * definition's `TestMethod className`, matched to its result by `testId`; a test with no definition is
 * placed by its name. The Process half runs one class at a time, so the sums are its wall time.
 */
export function slowestClasses(trx, count = 10) {
  const classOf = new Map();
  for (const [, id, body] of trx.matchAll(/<UnitTest\b[^>]*?\bid="([^"]+)"[^>]*>([\s\S]*?)<\/UnitTest>/g)) {
    const method = /<TestMethod\b[^>]*>/.exec(body);
    if (method) classOf.set(id, attributes(method[0]).className ?? '');
  }
  const classes = new Map();
  for (const [tag] of trx.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const result = attributes(tag);
    const fullName = classOf.get(result.testId) || (result.testName ?? '').replace(/\(.*$/, '').split('.').slice(0, -1).join('.');
    if (!fullName) continue;
    const name = fullName.split('.').at(-1);
    const entry = classes.get(name) ?? { name, ms: 0, tests: 0 };
    entry.ms += trxMs(result.duration);
    entry.tests += 1;
    classes.set(name, entry);
  }
  return [...classes.values()].sort((a, b) => b.ms - a.ms || a.name.localeCompare(b.name)).slice(0, count);
}

/** A re-run passes only when a test ran and none failed: a filter that matches nothing exits 0 too. */
export function rerunPassed(output) {
  const summary = testSummary(output);
  return summary.summaries > 0 && summary.failedCount === 0 && summary.passedCount > 0;
}

// ---------------------------------------------------------------------------------------------------
// A rehearsal that died, rather than failed (LEFT1)

/**
 * An exit that says a process ended rather than that a gate judged: a POSIX shell that could not run the
 * command (126, 127), cmd's for one it cannot find (9009), a signal (128 to 255; this tool reads one as
 * 128), and a Windows crash status, which reaches this tool through cmd and npm unsigned (0xC0000142,
 * a process that could not start under load, is 3221225794) and elsewhere in its signed form.
 */
export function processExit(code) {
  return code === 126 || code === 127 || code === 9009 || (code >= 128 && code <= 255) || code >= 0xC0000000 || code < 0;
}

/**
 * The lines of a gate's log that are the gate's own: after the steps before it (`command` names the
 * gate's, and its output starts after the last `$ <command>` line), and without what this tool writes
 * around a step (`$ …`, `(exited …)`, `could not start: …`), npm's lines around a script
 * (`> pkg@version script`, `> command`, `npm error …`), and blank lines.
 */
export function ownOutput(log, command) {
  const all = log.replace(/\r\n/g, '\n').split('\n');
  const from = command === undefined ? -1 : all.lastIndexOf(`$ ${command}`);
  return all.slice(from + 1).filter((line) => line.trim()
    && !/^\$ /.test(line)
    && !/^\(exited /.test(line)
    && !/^could not start: /.test(line)
    && !/^> /.test(line)
    && !/^npm (?:error|ERR!|warn|WARN|notice)\b/.test(line));
}

/**
 * A rehearsal's report of a failed check: the rehearsal kit's `FAIL` line (and vitest's for a failed
 * file, inside `test:web`), vitest's summary, and Playwright's.
 */
const FAILED_CHECKS = [/^\s*FAIL\s/, /^\s*(?:Test Files|Tests)\s+\d+ failed\b/, /^\s*\d+ failed\s*$/];

/**
 * Whether a rehearsal that failed is run again, once. One that reported failed checks has failed,
 * whatever exit follows the report. One whose process ended, or that printed nothing of its own, never
 * reached a verdict: on 2026-09-30 the family rehearsal exited 127 straight after building the HTTP host,
 * with no transcript, while three worktrees built beside it, and passed run alone (FLAKE1). Anything else
 * said why it stopped, and has failed. `command` is the gate's, so a step before it is not its output.
 */
export function rehearsalDecision(log, code, command) {
  const own = ownOutput(log, command);
  if (own.some((line) => FAILED_CHECKS.some((pattern) => pattern.test(line)))) {
    return { rerun: false, reason: 'it reported failed checks, so it is never run again' };
  }
  if (processExit(code)) return { rerun: true, reason: `exit ${code} ended its process before a verdict` };
  if (own.length === 0) return { rerun: true, reason: 'it printed nothing of its own' };
  return { rerun: false, reason: 'it said why it stopped and reported no failed check, so it is never run again' };
}

// ---------------------------------------------------------------------------------------------------
// Lanes

const escapeRegExp = (text) => text.replace(/[.+^${}()|[\]\\]/g, '\\$&');

/** A lane path as a pattern: `*` stays in a folder, `**` crosses folders, `{a,b}` is either. */
export function globToRegExp(glob) {
  let source = '';
  for (let i = 0; i < glob.length; i += 1) {
    const c = glob[i];
    if (c === '*') {
      if (glob[i + 1] === '*') {
        i += 1;
        if (glob[i + 1] === '/') {
          i += 1;
          source += '(?:.*/)?';
        } else source += '.*';
      } else source += '[^/]*';
    } else if (c === '?') source += '[^/]';
    else if (c === '{' && glob.indexOf('}', i) > i) {
      const end = glob.indexOf('}', i);
      source += `(?:${glob.slice(i + 1, end).split(',').map(escapeRegExp).join('|')})`;
      i = end;
    } else source += escapeRegExp(c);
  }
  return new RegExp(`^${source}$`);
}

/**
 * A lane's paths as one test: a path is the lane's when one of its globs matches it and none of its `!`
 * globs does. A `!` glob carves a narrower lane's paths out of a wider one: the web's shell owns the
 * page but its Settings (LEFT1).
 */
export function laneMatcher(paths) {
  const include = paths.filter((path) => !path.startsWith('!')).map(globToRegExp);
  const exclude = paths.filter((path) => path.startsWith('!')).map((path) => globToRegExp(path.slice(1)));
  return (path) => include.some((pattern) => pattern.test(path)) && !exclude.some((pattern) => pattern.test(path));
}

/** The repository's lanes, at its root beside `daoris.json` and `daoris.gates.json` (D115 §2.1, DEV2). */
export const LANES_FILE = 'daoris.lanes.json';

/** How a quest will address a lane (`repository:lane`), so an id is one word of a known alphabet. */
const LANE_ID = /^[a-z][a-z0-9-]*$/;

/**
 * Why a parsed lanes file cannot be read, one line each, or none. Absence is a rule (D115 §2.1): no
 * `lanes` and an empty list both mean no lanes. A malformed or repeated id, a lane that owns nothing,
 * two stewards, and a `gates` entry naming no gate in `gateNames` (the declared set) each make it
 * unreadable. Fields it has no rule for are left alone.
 */
export function lanesProblems(parsed, gateNames = []) {
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) return ['the file is not a JSON object'];
  const problems = [];
  if (parsed.laneless !== undefined && !Array.isArray(parsed.laneless)) problems.push("'laneless' is not a list");
  if (parsed.lanes === undefined) return problems;
  if (!Array.isArray(parsed.lanes)) return [...problems, "'lanes' is not a list"];
  const seen = new Set();
  parsed.lanes.forEach((lane, i) => {
    const id = lane?.id;
    const name = typeof id === 'string' ? `lane '${id}'` : `lane ${i + 1}`;
    if (typeof id !== 'string' || !LANE_ID.test(id)) {
      problems.push(`${name}: its id must be lower-case letters, digits and dashes, starting with a letter`);
    } else if (seen.has(id)) problems.push(`${name}: the id is used twice`);
    else seen.add(id);
    const paths = Array.isArray(lane?.paths) ? lane.paths : [];
    if (!paths.some((path) => typeof path === 'string' && path && !path.startsWith('!'))) problems.push(`${name}: it has no paths`);
    if (lane?.gates !== undefined) {
      if (!Array.isArray(lane.gates)) problems.push(`${name}: 'gates' is not a list`);
      else {
        for (const gate of lane.gates.filter((entry) => !gateNames.includes(entry))) {
          problems.push(`${name}: its gate '${gate}' is not declared in daoris.gates.json`);
        }
      }
    }
  });
  const stewards = parsed.lanes
    .map((lane, i) => (lane?.steward === true ? (typeof lane.id === 'string' ? lane.id : `lane ${i + 1}`) : null))
    .filter(Boolean);
  if (stewards.length > 1) problems.push(`two stewards (${stewards.join(', ')}): at most one lane keeps the records`);
  return problems;
}

/**
 * `daoris.lanes.json`, or null where a repository has none. An unreadable file is refused, naming each
 * problem. `laneless` is read flat: its groups are for the person reading the map, each saying why its
 * paths have no lane.
 */
export function readLanes(root) {
  const file = join(root, LANES_FILE);
  if (!existsSync(file)) return null;
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new Refusal(`${LANES_FILE} is not valid JSON: ${error.message}`);
  }
  const gateNames = existsSync(join(root, 'daoris.gates.json')) ? readDeclared(root).map((gate) => gate.name) : [];
  const problems = lanesProblems(parsed, gateNames);
  if (problems.length) throw new Refusal(`${LANES_FILE} cannot be read:\n${problems.map((problem) => `  ${problem}`).join('\n')}`);
  return {
    lanes: (parsed.lanes ?? []).map((lane) => ({
      id: lane.id,
      title: typeof lane.title === 'string' ? lane.title : '',
      summary: typeof lane.summary === 'string' ? lane.summary : '',
      steward: lane.steward === true,
      paths: lane.paths,
      ...(lane.gates ? { gates: lane.gates } : {}),
    })),
    laneless: (parsed.laneless ?? []).flatMap((group) => group?.paths ?? []),
  };
}

/** The paths `.gitattributes` marks `merge=union`: the records that resolve themselves. */
export function unionRecords(root) {
  const file = join(root, '.gitattributes');
  if (!existsSync(file)) return [];
  return readFileSync(file, 'utf8').replace(/\r\n/g, '\n').split('\n')
    .map((line) => /^(\S+)\s(?:.*\s)?merge=union(?:\s|$)/.exec(line)?.[1])
    .filter((pattern) => pattern && !pattern.startsWith('#'));
}

/** A `.gitattributes` pattern with no slash matches at any depth, as git reads it. */
const attributePattern = (pattern) => (pattern.includes('/')
  ? globToRegExp(pattern.replace(/^\//, ''))
  : new RegExp(`^(?:.*/)?${globToRegExp(pattern).source.slice(1)}`));

/**
 * Where each changed path belongs, in D115 §2.3's order: the steward's lane first (its records, which a
 * subagent never edits, the parent's list before DEV2), then the records that merge by union, then the
 * first other lane that owns it, then the paths the map declares laneless (the docs, the doctrine),
 * else outside every lane: a path the map does not place, which the lanes test refuses (LEFT1). Lanes
 * come back by id and title in the map's order, only those touched; the steward's paths come back on
 * their own, where the parent's did.
 */
export function classify(paths, { lanes = [], union = [], laneless = [] } = {}) {
  const steward = lanes.find((lane) => lane.steward);
  const stewardOwns = steward ? laneMatcher(steward.paths) : () => false;
  const lanePatterns = lanes.filter((lane) => lane !== steward)
    .map((lane) => ({ id: lane.id, title: lane.title, owns: laneMatcher(lane.paths), files: [] }));
  const unionPatterns = union.map(attributePattern);
  const declared = laneMatcher(laneless);
  const placed = { lanes: [], steward: [], shared: [], laneless: [], outside: [] };
  for (const path of paths) {
    if (stewardOwns(path)) placed.steward.push(path);
    else if (unionPatterns.some((pattern) => pattern.test(path))) placed.shared.push(path);
    else {
      const lane = lanePatterns.find((candidate) => candidate.owns(path));
      if (lane) lane.files.push(path);
      else if (declared(path)) placed.laneless.push(path);
      else placed.outside.push(path);
    }
  }
  placed.lanes = lanePatterns.filter((lane) => lane.files.length).map(({ id, title, files }) => ({ id, title, files }));
  return placed;
}

// ---------------------------------------------------------------------------------------------------
// Git

function git(cwd, args, { allowFail = false, env } = {}) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, ...(env ? { env: { ...process.env, ...env } } : {}) });
  if (result.error) throw new Refusal(`git could not start: ${result.error.message}`);
  if (result.status !== 0 && !allowFail) {
    throw new Refusal(`git ${args.join(' ')} failed: ${(result.stderr || result.stdout).trim()}`);
  }
  return { status: result.status, out: result.stdout.trimEnd(), err: result.stderr.trim() };
}

const lines = (text) => text.split('\n').filter((line) => line.trim());

// Each git start costs tens of milliseconds on Windows, so the checkout's git folder is asked for once,
// with its top level, and an open merge is read from the MERGE_HEAD file git writes there.
const gitDirs = new Map();
function locate(cwd) {
  const [top, gitDir] = git(cwd, ['rev-parse', '--show-toplevel', '--absolute-git-dir']).out.split('\n').map((line) => line.trim());
  const root = resolve(top);
  gitDirs.set(root, resolve(gitDir));
  return root;
}
const mergeInProgress = (root) => existsSync(join(gitDirs.get(root), 'MERGE_HEAD'));
const unmergedPaths = (root) => lines(git(root, ['diff', '--name-only', '--diff-filter=U']).out);

/**
 * `git status --porcelain=v2 --branch`, read: the current branch ('' when detached) and each changed or
 * untracked path with its two-letter code (`??` untracked).
 */
export function parseStatus(text) {
  let branch = '';
  const changed = [];
  for (const line of text.replace(/\r\n/g, '\n').split('\n')) {
    if (line.startsWith('# branch.head ')) {
      const head = line.slice('# branch.head '.length).trim();
      branch = head === '(detached)' ? '' : head;
    } else if (line.startsWith('? ')) changed.push({ code: '??', path: line.slice(2) });
    else if (/^[12u] /.test(line)) {
      // Fields before the path: 8 for an ordinary entry, 9 for a rename (then `path<TAB>from`), 10 unmerged.
      const fields = line.split(' ');
      const skip = { 1: 8, 2: 9, u: 10 }[line[0]];
      changed.push({ code: fields[1], path: fields.slice(skip).join(' ').split('\t')[0] });
    }
  }
  return { branch, changed };
}

/** A tree that is not clean, each changed or untracked path with its code, the first twelve. */
const notClean = (changed) => {
  const shown = changed.slice(0, 12).map(({ code, path }) => `  ${code} ${path}`).join('\n');
  return `the tree is not clean:\n${shown}${changed.length > 12 ? `\n  … ${changed.length - 12} more` : ''}`;
};

/** Why a merge cannot start from what git reported, or null. */
export function startRefusal({ branch, merging, changed, ignored }) {
  if (branch !== MAIN) {
    return `the current branch is '${branch || 'a detached HEAD'}', not ${MAIN}: merges land on ${MAIN}, from its own checkout`;
  }
  if (merging) return 'a merge is already in progress: resolve it and run --continue, or git merge --abort';
  if (changed.length) return notClean(changed);
  if (!ignored) return `${SCRATCH}/ is not ignored here, so the gate logs would dirty the tree`;
  return null;
}

const treeState = (root) => parseStatus(git(root, ['status', '--porcelain=v2', '--branch']).out);

function requireMain(root) {
  const refusal = startRefusal({ branch: treeState(root).branch, merging: false, changed: [], ignored: true });
  if (refusal) throw new Refusal(refusal);
}

/** On main, no merge open, a clean tree, and a scratch folder git ignores (or the logs would dirty it). */
function preconditions(root) {
  const facts = { ...treeState(root), merging: mergeInProgress(root), ignored: true };
  const refusal = startRefusal(facts);
  if (refusal) throw new Refusal(refusal);
  // Asked last, and only of a checkout that passed the rest: it is one more git start.
  if (git(root, ['check-ignore', '-q', `${SCRATCH}/merge-probe.log`], { allowFail: true }).status !== 0) {
    throw new Refusal(startRefusal({ ...facts, ignored: false }));
  }
  requireVerdictsIgnored(root);
}

/** The verdicts record is written as each gate ends: tracked or untracked, it would change the tree it judges. */
function requireVerdictsIgnored(root) {
  if (git(root, ['check-ignore', '-q', VERDICTS_FILE], { allowFail: true }).status !== 0) {
    throw new Refusal(`${VERDICTS_FILE} is not ignored here, so each verdict written would change the tree it is a verdict on`);
  }
}

/** The branch's tip, or a refusal naming it. */
function requireBranch(root, branch) {
  const tip = git(root, ['rev-parse', '--verify', '--quiet', `${branch}^{commit}`], { allowFail: true });
  if (tip.status !== 0) throw new Refusal(`no branch '${branch}'`);
  return tip.out.trim();
}

/** The log format `parseCommits` reads: short sha, parents, subject, the Co-Authored-By values. */
export const COMMIT_FORMAT = '--format=%h%x1f%p%x1f%s%x1f%(trailers:key=Co-Authored-By,valueonly)%x1e';

/** A branch's commits from `git log` in `COMMIT_FORMAT`: sha, whether it is a merge, subject, trailer. */
export function parseCommits(text) {
  return text.split('\x1e').map((entry) => entry.trim()).filter(Boolean).map((entry) => {
    const [sha = '', parents = '', subject = '', trailer = ''] = entry.split('\x1f');
    return { sha, merge: parents.trim().split(/\s+/).length > 1, subject, trailer: trailer.trim() };
  });
}

/** The commits main lacks, one git start for both the count and the commit check. */
function branchCommits(root, branch) {
  const commits = parseCommits(git(root, ['log', COMMIT_FORMAT, `HEAD..${branch}`]).out);
  if (commits.length === 0) throw new Refusal(`'${branch}' has nothing main lacks: it is merged already`);
  return commits;
}

/**
 * `git worktree list --porcelain`, read: each worktree's path, the branch it has out ('' when detached),
 * whether it is locked and git's reason, and whether git says its folder is gone. The first is the main
 * checkout.
 */
export function parseWorktrees(porcelain) {
  const trees = [];
  for (const line of porcelain.replace(/\r\n/g, '\n').split('\n')) {
    const tree = trees.at(-1);
    if (line.startsWith('worktree ')) {
      trees.push({ path: line.slice('worktree '.length).trim(), branch: '', locked: false, lockReason: '', prunable: false, bare: false });
    } else if (!tree) continue;
    else if (line.startsWith('branch refs/heads/')) tree.branch = line.slice('branch refs/heads/'.length).trim();
    else if (/^locked(?: |$)/.test(line)) Object.assign(tree, { locked: true, lockReason: line.slice('locked'.length).trim() });
    else if (/^prunable(?: |$)/.test(line)) tree.prunable = true;
    else if (line.trim() === 'bare') tree.bare = true;
  }
  return trees;
}

/** The path of the worktree `git worktree list --porcelain` shows a branch checked out in, or null. */
export function worktreeFor(porcelain, branch) {
  return (branch && parseWorktrees(porcelain).find((tree) => tree.branch === branch)?.path) || null;
}

/**
 * What the commit check finds wrong: a commit of the branch's own without the trailer (a merge of main
 * into it carries none, and needs none), and work left uncommitted in its worktree.
 */
function commitProblems(root, branch, commits) {
  const problems = commits.filter((commit) => !commit.merge && !commit.trailer)
    .map((commit) => `${commit.sha} ${commit.subject}: no Co-Authored-By line`);
  const tree = worktreeFor(git(root, ['worktree', 'list', '--porcelain']).out, branch);
  if (tree && existsSync(tree)) {
    const left = lines(git(tree, ['status', '--porcelain']).out);
    if (left.length) {
      problems.push(`its worktree holds uncommitted work, which the merge would leave out:\n${left.slice(0, 12).map((line) => `    ${line}`).join('\n')}`);
    }
  }
  return problems;
}

// ---------------------------------------------------------------------------------------------------
// The prune (GATE2): merged branches go, with their worktrees, unless a worktree is in use or holds work

/**
 * The branches a prune considers: each local branch merged into main, but main, the branch the main
 * checkout has out, the one this runs in, and the `hold` names: a batch's branches still to merge, since
 * one merged already is refused by name at its turn, and a deleted one would read as a misspelling. Each
 * comes with the worktree that has it out, if any; a held one says why it is held.
 */
export function pruneCandidates({ merged, worktrees, current, hold = [] }) {
  const mainCheckout = worktrees[0]?.branch ?? '';
  return merged.filter((branch) => branch !== MAIN).map((branch) => {
    let held = null;
    if (branch === mainCheckout) held = 'the main checkout has it checked out';
    else if (branch === current) held = 'this runs in its checkout';
    else if (hold.includes(branch)) held = 'named in this batch, still to merge';
    return { branch, worktree: worktrees.find((tree) => tree.branch === branch) ?? null, held };
  });
}

const listed = (files, max = 3) => `${files.slice(0, max).join(', ')}${files.length > max ? `, … ${files.length - max} more` : ''}`;

/**
 * Whether a candidate goes, and if it stays, why. `facts` is what was found in its worktree, and null
 * where nothing was looked into: a locked worktree is in use, so the lock decides and what it holds does
 * not, and nothing runs in it. Every reason to keep is said, not only the first.
 */
export function pruneVerdict(candidate, facts) {
  if (candidate.held) return { remove: false, why: candidate.held };
  const tree = candidate.worktree;
  if (!tree) return { remove: true, why: '' };
  if (tree.locked) {
    return { remove: false, why: `its worktree is locked, so in use${tree.lockReason ? ` (${tree.lockReason})` : ''}; it is never looked into or unlocked here` };
  }
  if (!facts) return { remove: false, why: 'its worktree was not looked into' };
  if (facts.gone) return { remove: true, why: '', gone: true };
  const has = [];
  if (facts.error) has.push(`a state git could not read (${facts.error})`);
  if (facts.tracked.length) has.push(`modified or staged tracked files (${listed(facts.tracked)})`);
  if (facts.local) has.push(`private output under local/ (${facts.local}), which nothing else keeps`);
  if (facts.untracked.length) has.push(`untracked files git removes only with --force (${listed(facts.untracked)})`);
  return has.length ? { remove: false, why: `its worktree has ${has.join('; ')}` } : { remove: true, why: '' };
}

/** The line of git's refusal worth printing: its `error:` or `fatal:` line, else its first. */
const gitSays = (text) => {
  const all = lines(text.replace(/\r\n/g, '\n')).map((line) => line.trim());
  return all.find((line) => /^(?:error|fatal):/.test(line)) ?? all[0] ?? 'no reason given';
};

const samePath = (a, b) => {
  const norm = (path) => resolve(path).split('\\').join('/').replace(/\/+$/, '');
  return process.platform === 'win32' ? norm(a).toLowerCase() === norm(b).toLowerCase() : norm(a) === norm(b);
};

/**
 * The first file under `dir`, as a `/` path from the folder that holds it, or null when it holds none.
 * Links are not followed. A folder that cannot be read throws, and its worktree is kept.
 */
export function firstFile(dir) {
  const walk = (at) => {
    let entries;
    try {
      entries = readdirSync(at, { withFileTypes: true });
    } catch (error) {
      if (error?.code === 'ENOENT') return null;
      throw error;
    }
    for (const entry of entries) {
      const found = entry.isDirectory() ? walk(join(at, entry.name)) : join(at, entry.name);
      if (found) return found;
    }
    return null;
  };
  const found = walk(dir);
  return found && relative(dirname(dir), found).split('\\').join('/');
}

/** What a prune needs to know of an unlocked worktree: its folder gone, its changes, its `local/`. */
function inspectWorktree(tree) {
  if (tree.prunable || !existsSync(tree.path)) return { gone: true };
  const facts = { tracked: [], untracked: [], local: null };
  // git walks up: in a folder that lost its .git file, a status would answer for the repository above it.
  if (!existsSync(join(tree.path, '.git'))) return { ...facts, error: 'its folder has no .git file' };
  const status = git(tree.path, ['--no-optional-locks', 'status', '--porcelain'], { allowFail: true });
  if (status.status !== 0) return { ...facts, error: gitSays(status.err || status.out) };
  for (const line of lines(status.out)) (line.startsWith('?? ') ? facts.untracked : facts.tracked).push(line.slice(3));
  try {
    facts.local = firstFile(join(tree.path, 'local'));
  } catch (error) {
    facts.local = `local/ itself, which could not be read: ${error?.code ?? error?.message}`;
  }
  return facts;
}

/**
 * The prune's plan, changing nothing: every candidate with its verdict, and the branch the checkout this
 * runs in has out, since `git branch -d` asks whether a branch is merged into that one.
 */
export function planPrune(root, { hold = [] } = {}) {
  const merged = lines(git(root, ['for-each-ref', `--merged=refs/heads/${MAIN}`, '--format=%(refname)', 'refs/heads/']).out)
    .map((ref) => ref.trim().replace(/^refs\/heads\//, ''));
  const worktrees = parseWorktrees(git(root, ['worktree', 'list', '--porcelain']).out);
  const current = worktrees.find((tree) => samePath(tree.path, root))?.branch ?? '';
  const entries = pruneCandidates({ merged, worktrees, current, hold }).map((candidate) => {
    const tree = candidate.worktree;
    const facts = !candidate.held && tree && !tree.locked ? inspectWorktree(tree) : null;
    return { ...candidate, ...pruneVerdict(candidate, facts) };
  });
  return { current, entries };
}

/** One removal: the worktree, then the branch. What git refuses is kept, with git's own reason. */
function removeOne(root, entry) {
  const tree = entry.worktree;
  let left = '';
  if (tree) {
    const removed = git(root, ['worktree', 'remove', tree.path], { allowFail: true });
    if (removed.status !== 0) {
      const still = parseWorktrees(git(root, ['worktree', 'list', '--porcelain']).out).some((known) => samePath(known.path, tree.path));
      if (still) return { ...entry, remove: false, why: `git refused to remove its worktree: ${gitSays(removed.err || removed.out)}` };
      // git can let go of a worktree whose folder it could not wholly delete (a file held open): the
      // branch is free, and what is left of the folder is no longer git's.
      left = `; git could not delete all of its folder (${gitSays(removed.err || removed.out)}), and what is left is no longer git's`;
    }
  }
  const deleted = git(root, ['branch', '-d', entry.branch], { allowFail: true });
  if (deleted.status !== 0) {
    return { ...entry, remove: false, why: `${tree ? 'its worktree is removed, but ' : ''}git refused to delete the branch, which is never forced: ${gitSays(deleted.err || deleted.out)}` };
  }
  return { ...entry, removed: true, left };
}

/**
 * The plan, applied: `git worktree remove`, never forced (ignored build output does not stop it, and
 * whatever does is work to keep), then `git branch -d`, never `-D`, then one `git worktree prune`.
 * `onEach` hears each entry as it ends, so a long removal is not a silence.
 */
export function applyPrune(root, entries, onEach = () => {}) {
  const done = entries.map((entry) => {
    const outcome = entry.remove ? removeOne(root, entry) : entry;
    onEach(outcome);
    return outcome;
  });
  if (entries.some((entry) => entry.remove)) git(root, ['worktree', 'prune'], { allowFail: true });
  return done;
}

function pruneHeader(entries, { plan = false, atMerge = false } = {}) {
  const what = entries.length === 0
    ? `no branch but ${MAIN} is merged into ${MAIN}`
    : `${entries.length} ${entries.length === 1 ? 'branch' : 'branches'} merged into ${MAIN}`;
  const note = [atMerge && "at the merge's start", plan && 'a plan, so nothing is removed'].filter(Boolean).join('; ');
  return `prune${note ? ` (${note})` : ''}: ${what}`;
}

/** One branch's line, in the lane report's columns: what happens to it (or would), and why. */
function pruneLine(root, entry, applied) {
  const goes = applied ? entry.removed === true : entry.remove;
  const verb = applied ? (goes ? 'removed' : 'kept') : (goes ? 'remove' : 'keep');
  let text = entry.why;
  if (goes) {
    const at = entry.worktree && shown(root, entry.worktree.path);
    if (!entry.worktree) text = 'the branch (no worktree has it checked out)';
    else if (entry.gone) text = `its worktree's record (the folder ${at} is gone already) and the branch`;
    else text = `its worktree ${at} and the branch${entry.left ?? ''}`;
  }
  return `  ${pad(verb, 8)} ${pad(entry.branch, 34)} ${text}`;
}

/** The prune a merge runs at its start and a batch at its end. It never stops the merge. */
function pruneAround(root, hold) {
  try {
    const { current, entries } = planPrune(root, { hold });
    if (current !== MAIN) {
      console.log(`prune: skipped: this checkout has ${current || 'a detached HEAD'} out, not ${MAIN}`);
      return;
    }
    console.log(pruneHeader(entries));
    applyPrune(root, entries, (entry) => console.log(pruneLine(root, entry, true)));
  } catch (error) {
    console.log(`prune: skipped (${error?.message ?? error})`);
  }
}

/** `--prune`: the prune alone, or its plan. Applying it runs from main's checkout, as a merge does. */
function pruneOnly(root, options) {
  const { current, entries } = planPrune(root);
  if (options.plan) {
    console.log(pruneHeader(entries, { plan: true }));
    for (const entry of entries) console.log(pruneLine(root, entry, false));
    return 0;
  }
  if (current !== MAIN) {
    throw new Refusal(`--prune runs from ${MAIN}'s checkout: git branch -d asks whether a branch is merged into the one checked out, and here that is ${current || 'a detached HEAD'}`);
  }
  console.log(pruneHeader(entries));
  applyPrune(root, entries, (entry) => console.log(pruneLine(root, entry, true)));
  return 0;
}

// ---------------------------------------------------------------------------------------------------
// The batch record

const statePath = (root) => join(root, STATE);
const readState = (root) => (existsSync(statePath(root)) ? JSON.parse(readFileSync(statePath(root), 'utf8')) : null);

function writeState(root, state) {
  writeAtomic(statePath(root), `${JSON.stringify(state, null, 2)}\n`);
}

const dropState = (root) => rmSync(statePath(root), { force: true });

// ---------------------------------------------------------------------------------------------------
// Running gates

const shown = (root, file) => relative(root, file).split('\\').join('/');
const pad = (text, width) => (text.length >= width ? `${text} ` : text.padEnd(width));
const count = (n, word) => `${n} ${word}${n === 1 ? '' : 's'}`;

function duration(ms) {
  const seconds = Math.round(ms / 1000);
  return seconds < 60 ? `${seconds} s` : `${Math.floor(seconds / 60)} m ${seconds % 60} s`;
}

/**
 * One command through the platform shell, as the devkit runs a declared gate, its output into `fd`.
 * The gate runners take it as a parameter, so their logic is tested without starting a process.
 */
export function runStep(command, cwd, fd) {
  writeSync(fd, `$ ${command}\n`);
  return new Promise((done) => {
    let settled = false;
    const finish = (code) => {
      if (!settled) {
        settled = true;
        done(code);
      }
    };
    const child = spawn(command, { cwd, shell: true, stdio: ['ignore', fd, fd], windowsHide: true });
    child.on('error', (error) => {
      writeSync(fd, `could not start: ${error.message}\n`);
      finish(127);
    });
    child.on('close', (code, signal) => finish(code ?? (signal ? 128 : 1)));
  });
}

/**
 * A log is written beside and renamed when its gate ends, so a half-written one never reads as whole. Not `writeAtomic`
 * (REFAC2): the gate streams into it for minutes, a re-run reads it as it grows, and it lands whatever the gate did,
 * where `writeAtomic` throws away what a failure leaves.
 */
async function withLog(file, work) {
  const partial = `${file}.partial`;
  writeFileSync(partial, '');
  const fd = openSync(partial, 'a');
  try {
    return await work(fd, partial);
  } finally {
    closeSync(fd);
    renameHeld(partial, file);
  }
}

async function rerunAlone(root, gate, dir, tests, step) {
  const log = join(dir, `${gate.name}.rerun.log`);
  const cwd = gate.cwd ? join(root, gate.cwd) : root;
  const passed = await withLog(log, async (fd, partial) => {
    let all = true;
    for (const test of tests) {
      const from = statSync(partial).size;
      const code = await step(rerunCommand(gate.run, test), cwd, fd);
      const output = readFileSync(partial).subarray(from).toString('utf8');
      writeSync(fd, `\n(exited ${code})\n\n`);
      if (code !== 0 || !rerunPassed(output)) all = false;
    }
    return all;
  });
  return { passed, log };
}

/** A gate's steps before it, then the gate (as `command`, its own run unless given), into one log: the gate's exit. */
function runSteps(root, gate, log, step, command = gate.run) {
  const cwd = gate.cwd ? join(root, gate.cwd) : root;
  return withLog(log, async (fd) => {
    for (const before of gate.before) {
      const beforeCode = await step(before, root, fd);
      writeSync(fd, `\n(exited ${beforeCode}${beforeCode ? '; carried on: it only clears a server' : ''})\n\n`);
    }
    const exit = await step(command, cwd, fd);
    writeSync(fd, `\n(exited ${exit})\n`);
    return exit;
  });
}

/** One gate, its steps before it, and its flake re-run: a result with its verdict, note and log. */
export async function runGate(root, gate, dir, { step = runStep } = {}) {
  const log = join(dir, `${gate.name}.log`);
  const started = Date.now();
  // PROC1: a Process half also leaves each test's duration beside its log, read once it ends.
  const trx = isProcessGate(gate) ? join(dir, `${gate.name}.trx`) : null;
  if (trx) rmSync(trx, { force: true });
  const code = await runSteps(root, gate, log, step, trx ? trxCommand(gate.run, trx) : gate.run);
  const result = { gate, code, log, verdict: code === 0 ? 'PASS' : 'FAIL', note: code === 0 ? '' : `exit ${code}` };
  if (trx && existsSync(trx)) Object.assign(result, { trx, slowest: slowestClasses(readFileSync(trx, 'utf8')) });
  if (code !== 0 && gateKind(gate.run) === 'rehearsal') {
    // A rehearsal that died is run again once, whole, into a log of its own beside the first (LEFT1).
    const decision = rehearsalDecision(readFileSync(log, 'utf8'), code, gate.run);
    if (!decision.rerun) {
      result.note = `exit ${code}; ${decision.reason}`;
    } else {
      const again = join(dir, `${gate.name}.rerun.log`);
      const second = await runSteps(root, gate, again, step);
      if (second === 0) {
        Object.assign(result, { verdict: 'FLAKE', flakes: [gate.name], note: `${decision.reason}, and it passed when run again; ${shown(root, again)}` });
      } else {
        result.note = `${decision.reason}; run again, it failed again (exit ${second}); ${shown(root, again)}`;
      }
    }
  } else if (code !== 0 && /^dotnet test\b/.test(gate.run) && !isProcessGate(gate)) {
    result.note = `exit ${code}; outside the Process category, a failure is real and never re-run`;
  } else if (code !== 0 && /^dotnet test\b/.test(gate.run)) {
    const decision = flakeDecision(readFileSync(log, 'utf8'));
    if (decision.rerun.length === 0) {
      result.note = `exit ${code}; ${decision.reason}`;
    } else {
      const rerun = await rerunAlone(root, gate, dir, decision.rerun, step);
      const names = decision.rerun.map(short).join(', ');
      if (rerun.passed) {
        Object.assign(result, { verdict: 'FLAKE', flakes: decision.rerun, note: `failed in the full run and passed alone: ${names}; ${shown(root, rerun.log)}` });
      } else {
        result.note = `exit ${code}; failed again alone: ${names}; ${shown(root, rerun.log)}`;
      }
    }
  }
  result.ms = Date.now() - started;
  return result;
}

function resultLine(root, result) {
  const note = result.note ? `  (${result.note})` : '';
  return `  ${pad(result.verdict, 8)} ${pad(result.gate.name, 16)} ${pad(duration(result.ms), 9)} ${shown(root, result.log)}${note}`;
}

/** PROC1: the slowest classes of a Process gate's run, under its line. */
function slowestLines(root, result) {
  if (!result.slowest?.length) return [];
  return [
    `           slowest classes of ${result.gate.name}, from ${shown(root, result.trx)}:`,
    ...result.slowest.map((slow, i) => `           ${pad(`${i + 1}.`, 4)}${pad(duration(slow.ms), 10)} ${pad(count(slow.tests, 'test'), 10)} ${slow.name}`),
  ];
}

/**
 * The gates in order, one line each as it ends (and a Process gate's slowest classes under it). The first
 * failure stops the run and every gate after it is named as not run, unless `keepGoing`.
 */
export async function runGates(root, gates, dir, { keepGoing = false, run = runGate, print = console.log } = {}) {
  const results = [];
  for (let i = 0; i < gates.length; i += 1) {
    const result = await run(root, gates[i], dir);
    results.push(result);
    print(resultLine(root, result));
    for (const line of slowestLines(root, result)) print(line);
    if (result.verdict === 'FAIL' && !keepGoing) {
      for (const skipped of gates.slice(i + 1)) print(`  ${pad('NOT RUN', 8)} ${skipped.name}`);
      break;
    }
  }
  return results;
}

function clearLogs(dir) {
  mkdirSync(dir, { recursive: true });
  for (const name of readdirSync(dir)) {
    if (/\.(log|partial|trx)$/.test(name)) rmSync(join(dir, name), { force: true });
  }
}

// ---------------------------------------------------------------------------------------------------
// Reports

/** A lane's row label: its id, then its title, so a lane reads as a dispatch prompt names it. */
const laneLabel = (lane) => `${pad(lane.id, 14)}${lane.title}`;

function laneReport(root, branch) {
  const changed = lines(git(root, ['diff', '--name-only', `HEAD...${branch}`]).out);
  const map = readLanes(root);
  const out = [`lanes: ${branch} changes ${count(changed.length, 'file')}`];
  if (!map || map.lanes.length === 0) {
    out.push(`  (${map ? `${LANES_FILE} declares no lanes` : `no ${LANES_FILE} here`})`);
    return out;
  }
  const placed = classify(changed, { ...map, union: unionRecords(root) });
  const steward = map.lanes.find((lane) => lane.steward);
  const row = (label, text) => `  ${pad(label, 32)} ${text}`;
  const list = (files, max = 6) => `${files.slice(0, max).join(', ')}${files.length > max ? `, … ${files.length - max} more` : ''}`;
  for (const lane of placed.lanes) out.push(row(laneLabel(lane), count(lane.files.length, 'file')));
  if (placed.steward.length) out.push(row(laneLabel(steward), `${count(placed.steward.length, 'file')} (the steward's)`));
  if (placed.shared.length) out.push(row('shared records', `${count(placed.shared.length, 'file')}: ${list(placed.shared)} (merge by union)`));
  if (placed.laneless.length) out.push(row('no lane', `${count(placed.laneless.length, 'file')}: ${list(placed.laneless)} (the map declares them laneless)`));
  if (placed.outside.length) out.push(row('outside every lane', `${count(placed.outside.length, 'file')}: ${list(placed.outside)} (the map places no such path; the lanes test refuses it)`));
  if (placed.lanes.length > 1) out.push(`  note: it crosses ${placed.lanes.length} lanes`);
  if (placed.steward.length) out.push(`  note: it edits the steward's records (${placed.steward.join(', ')}); a subagent never does`);
  return out;
}

function planLines(plan) {
  return plan.map((gate, i) => {
    const before = gate.before.length ? `   (after ${gate.before.join(', ')})` : '';
    return `  ${pad(`${i + 1}.`, 4)}${pad(gate.name, 16)} ${pad(gate.kind, 10)} ${gate.run}${before}`;
  });
}

// ---------------------------------------------------------------------------------------------------
// The verbs

/** Paths from a `-z` listing: git quotes nothing there, so a name with any character comes back whole. */
const zPaths = (text) => text.split('\0').map((path) => path.trim()).filter(Boolean);

/** What the merge in place changes on main: tracked paths against HEAD, the parent's fixes included, and new files. */
function mergeChanges(root) {
  const tracked = zPaths(git(root, ['diff', '-z', '--name-only', '--no-renames', 'HEAD']).out);
  const untracked = zPaths(git(root, ['ls-files', '-z', '--others', '--exclude-standard']).out);
  return [...new Set([...tracked, ...untracked])];
}

/** What a branch changes since it left main: a plan's question, asked before anything is merged. */
const branchChanges = (root, branch) => zPaths(git(root, ['diff', '-z', '--name-only', '--no-renames', `HEAD...${branch}`]).out);

/**
 * The gates the merge in place runs, and every gate of the plan with why it runs or is skipped. Checks and
 * suites run at each merge of a batch by its own paths; the rehearsals run once, at the batch's last merge,
 * each one any of the batch's merges reached. `state.reached` keeps what each merge reached, for that.
 */
function chooseGates(root, state, plan) {
  const branch = state.branches[state.at];
  const last = state.at === state.branches.length - 1;
  const selection = selectGates(plan, mergeChanges(root), { lanes: readLanes(root)?.lanes ?? [], full: state.full === true });
  state.reached = { ...(state.reached ?? {}), [branch]: selection.gates.filter((entry) => entry.run).map((entry) => entry.gate.name) };
  const waiting = new Set();
  const display = selection.gates.map((entry) => {
    if (entry.gate.kind !== 'rehearsal') return entry;
    if (!last) {
      if (!entry.run) return entry;
      waiting.add(entry.gate.name);
      return { ...entry, run: false, why: `waits for the batch's last branch (${state.branches.at(-1)}); ${entry.why}` };
    }
    if (entry.run) return entry;
    const by = state.branches.filter((other) => other !== branch && state.reached[other]?.includes(entry.gate.name));
    return by.length ? { ...entry, run: true, why: `reached by ${by.join(', ')}, earlier in this batch` } : entry;
  });
  return {
    gates: display,
    chosen: display.filter((entry) => entry.run).map((entry) => entry.gate),
    // What the lane table left out of this merge, not counting a rehearsal that waits for the batch's end.
    skipped: display.filter((entry) => !entry.run && !waiting.has(entry.gate.name)).length,
  };
}

const headCommit = (root) => git(root, ['rev-parse', 'HEAD'], { allowFail: true }).out.trim() || null;

/**
 * The gates run in order, each verdict recorded with the tree it ran on (GATE3), as its gate ends, so a run
 * cut short keeps what it learned. `onResult` hears each result.
 */
function runRecorded(root, gates, dir, { keepGoing = false, branch = null, onResult = () => {} } = {}) {
  const commit = headCommit(root);
  return runGates(root, gates, dir, {
    keepGoing,
    run: async (where, gate, logs) => {
      const tree = contentTree(where);
      const started = nowIso();
      const result = await runGate(where, gate, logs);
      Object.assign(result, { tree, at: started });
      addVerdicts(where, [{ gate: gate.name, verdict: result.verdict, tree, at: started, commit, branch }]);
      onResult(result);
      return result;
    },
  });
}

/** The line that ends a merge's run with gates skipped: the full set is still owed before a stage. */
const owedLine = (skipped) => (skipped
  ? `  ${count(skipped, 'gate')} of the plan skipped; the full set runs before the install is staged (${FULL_COMMAND}, or --full on a merge).`
  : null);

async function gateMerge(root, state, plan) {
  const branch = state.branches[state.at];
  const last = state.at === state.branches.length - 1;
  // Before the gates choose, so the merge's changed paths include what the writing changed (ORIENT1a). The notes
  // first (MERGEJOIN1): a blank line moves the lines the decisions digest points to.
  setApartNotes(root);
  writeGenerated(root);
  const { gates: selection, chosen: gates, skipped } = chooseGates(root, state, plan);
  const dir = join(root, SCRATCH, `merge-${slug(branch)}`);
  clearLogs(dir);
  // `--continue` gates the merge again whole: the verdicts of an earlier run are dropped.
  state.results = { [branch]: {} };
  writeState(root, state);

  console.log(`gates for ${branch}: ${gates.length} of ${plan.length}, fast first${state.full ? ' (--full)' : ', by the lanes it reaches'}${last ? '' : `; the rehearsals wait for the batch's last branch (${state.branches.at(-1)})`}`);
  for (const line of selectionLines(selection)) console.log(line);
  const results = await runRecorded(root, gates, dir, {
    keepGoing: state.keepGoing,
    branch,
    onResult: (result) => {
      state.results[branch][result.gate.name] = { verdict: result.verdict, at: result.at, tree: result.tree, note: result.note, log: shown(root, result.log) };
      writeState(root, state);
    },
  });
  const failed = results.filter((result) => result.verdict === 'FAIL');
  const flakes = results.filter((result) => result.verdict === 'FLAKE');
  const notRun = gates.length - results.length;

  console.log('');
  if (failed.length) {
    const names = failed.map((result) => result.gate.name).join(' ');
    console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed, and ${count(failed.length, 'gate')} failed: ${failed.map((result) => result.gate.name).join(', ')}${notRun ? `; ${notRun} not run` : ''}.`);
    console.log('  The merge is left in place. Read the failing log and fix it in the merge; then');
    console.log(`  node tools/merge-branch.mjs --rerun ${names} runs it again, keeping each other verdict no path your fix changes reaches (and runs what did not run),`);
    console.log('  or --continue gates the merge again whole. Or git merge --abort.');
    return 1;
  }
  const flakeNote = flakes.length ? `, with ${count(flakes.length, 'flake')} (${flakes.map((result) => result.gate.name).join(', ')}): record it under FLAKE1` : '';
  console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed; ${count(results.length, 'gate')} passed${flakeNote}.`);
  refreshKnowledgeServer(root);
  const owed = owedLine(skipped);
  if (owed) console.log(owed);
  if (last) {
    console.log('  Next: read the diff (git diff --cached), write the records, and commit the merge.');
  } else {
    console.log(`  Next: read the diff (git diff --cached), write the records, commit the merge; then node tools/merge-branch.mjs --continue merges ${state.branches[state.at + 1]}.`);
  }
  return 0;
}

/**
 * `--rerun <gate>…` (GATE4): the named gates again on the merge in place, then any gate of its selection with
 * no verdict yet, and any whose verdict a path changed since reaches (GATE6), keeping every other verdict and
 * saying when each was given. Every verdict kept is one a stage counts. With no merge open, `rerunCheckout`.
 */
async function rerunGates(root, options) {
  const state = readState(root);
  if (!state) {
    throw new Refusal('a merge is open here that this tool did not leave, so it has no verdicts to keep: '
      + 'commit it or git merge --abort; with no merge open, --rerun gates the checkout');
  }
  requireMain(root);
  const branch = state.branches[state.at];
  if (git(root, ['rev-parse', 'MERGE_HEAD']).out.trim() !== state.tips[branch]) {
    throw new Refusal(`the merge in progress is not ${branch}'s, which is the one recorded`);
  }
  const { blocking, generated } = conflicts(root);
  if (blocking.length) return reportConflict(branch, blocking, generated);
  // A fix made in the merge may have moved what the index points to: it is written again before any gate, and a
  // note a fix glued is set apart first.
  setApartNotes(root);
  writeGenerated(root);
  const plan = readPlan(root);
  const unknown = options.rerun.filter((name) => !plan.some((gate) => gate.name === name));
  if (unknown.length) {
    throw new Refusal(`--rerun: no gate ${unknown.map((name) => `'${name}'`).join(', ')} in the plan: ${plan.map((gate) => gate.name).join(', ')}`);
  }

  const { chosen } = chooseGates(root, state, plan);
  const results = state.results?.[branch] ?? {};
  // GATE6: a verdict is kept only while no path changed since it was given reaches its gate; one a fix's path reaches
  // runs again, so every verdict kept is one the stage counts. A tree git cannot write judges none stale.
  const tree = contentTree(root);
  const judging = standingFor(root, tree);
  const standings = Object.fromEntries(chosen.filter((gate) => results[gate.name] && tree)
    .map((gate) => [gate.name, verdictStanding({ ...judging, from: results[gate.name].tree, gate: gate.name })]));
  const stale = Object.fromEntries(Object.entries(standings).filter(([, how]) => how.standing === 'stale').map(([name, how]) => [name, how.paths]));
  const planned = rerunPlan(plan.map((gate) => gate.name), chosen.map((gate) => gate.name), results, options.rerun, stale);
  const gates = plan.filter((gate) => planned.run.includes(gate.name));
  const dir = join(root, SCRATCH, `merge-${slug(branch)}`);
  mkdirSync(dir, { recursive: true });
  for (const gate of gates) {
    for (const suffix of ['.log', '.rerun.log', '.trx']) rmSync(join(dir, `${gate.name}${suffix}`), { force: true });
  }

  const again = planned.stale.length
    ? `, then ${planned.stale.map((entry) => entry.name).join(', ')}, whose ${planned.stale.length === 1 ? 'verdict' : 'verdicts'} a changed path reaches`
    : '';
  const fresh = planned.fresh.length ? `, then ${planned.fresh.join(', ')}, which ${planned.fresh.length === 1 ? 'has' : 'have'} no verdict yet` : '';
  console.log(`re-run on the merge of ${branch}: ${options.rerun.join(', ')}${again}${fresh}; ${count(planned.kept.length, 'verdict')} kept`);
  for (const entry of planned.stale) {
    const why = entry.paths.length ? `${listed(entry.paths, 4)} changed since, which reaches it` : 'its tree cannot be compared with this one';
    console.log(`  ${pad('stale', 8)} ${pad(entry.name, 16)} ${pad(entry.verdict, 8)} from ${when(entry.at)}: ${why}`);
  }
  for (const kept of planned.kept) {
    const how = standings[kept.name];
    const note = how && how.standing !== 'exact' ? `  (an earlier tree: ${how.standing === 'records' ? 'only the records changed since' : 'no path changed since reaches it'})` : '';
    console.log(`  ${pad('kept', 8)} ${pad(kept.name, 16)} ${pad(kept.verdict, 8)} from ${when(kept.at)}  ${results[kept.name]?.log ?? ''}${note}`);
  }
  state.results = { [branch]: results };
  const ran = await runRecorded(root, gates, dir, {
    keepGoing: state.keepGoing || options.keepGoing,
    branch,
    onResult: (result) => {
      results[result.gate.name] = { verdict: result.verdict, at: result.at, tree: result.tree, note: result.note, log: shown(root, result.log) };
      writeState(root, state);
    },
  });

  const failed = [...ran.filter((result) => result.verdict === 'FAIL').map((result) => result.gate.name),
    ...planned.kept.filter((kept) => kept.verdict === 'FAIL').map((kept) => kept.name)];
  const notRun = gates.length - ran.length;
  const keptNote = planned.kept.length
    ? `${count(planned.kept.length, 'verdict')} kept (${planned.kept.map((kept) => `${kept.name}, from ${when(kept.at)}`).join('; ')})`
    : 'no verdict kept';
  console.log('');
  if (failed.length || notRun) {
    console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed, and ${count(failed.length, 'gate')} failed: ${failed.join(', ')}${notRun ? `; ${notRun} not run` : ''}; ${keptNote}.`);
    console.log('  The merge is left in place: fix it and --rerun the gate again, or --continue gates the merge again whole, or git merge --abort.');
    return 1;
  }
  console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed; ${count(ran.length, 'gate')} run and passed; ${keptNote}.`);
  refreshKnowledgeServer(root);
  console.log('  Next: read the diff (git diff --cached), write the records, and commit the merge.');
  return 0;
}

/** `--full` on its own (GATE3): the whole plan on the checkout as it stands, merging nothing, every verdict recorded. */
async function gateCheckout(root, options) {
  if (mergeInProgress(root)) {
    throw new Refusal('a merge is open here: node tools/merge-branch.mjs --continue --full gates it with every gate, or git merge --abort');
  }
  const head = headCommit(root);
  if (!head) throw new Refusal('this checkout has no commit to gate');
  if (git(root, ['check-ignore', '-q', `${SCRATCH}/full-probe.log`], { allowFail: true }).status !== 0) {
    throw new Refusal(`${SCRATCH}/ is not ignored here, so the gate logs would change the tree being gated`);
  }
  requireVerdictsIgnored(root);
  const plan = readPlan(root);
  const dir = join(root, SCRATCH, `full-${head.slice(0, 7)}`);
  clearLogs(dir);
  const dirty = treeState(root).changed.length > 0;
  console.log(`the full set on ${head.slice(0, 7)}${dirty ? ' and its uncommitted changes' : ''}: ${plan.length} gates, fast first; nothing is merged`);
  const results = await runRecorded(root, plan, dir, { keepGoing: options.keepGoing });
  const failed = results.filter((result) => result.verdict === 'FAIL');
  const flakes = results.filter((result) => result.verdict === 'FLAKE');
  console.log('');
  if (failed.length || results.length < plan.length) {
    console.log(`merge-branch: the full set did NOT pass ${head.slice(0, 7)}: ${failed.map((result) => result.gate.name).join(', ')} failed${results.length < plan.length ? `; ${plan.length - results.length} not run` : ''}.`);
    // GATE6b: once the failure is fixed and committed, or at once when it flaked, the smaller command does the rest.
    const remedy = stageRemedy(judgeCheckout(root, plan)?.gates ?? []);
    if (remedy) console.log(remedyLine(remedy));
    return 1;
  }
  const flakeNote = flakes.length ? `, with ${count(flakes.length, 'flake')} (${flakes.map((result) => result.gate.name).join(', ')}): record it under FLAKE1` : '';
  console.log(`merge-branch: the full set passed ${head.slice(0, 7)}: ${count(results.length, 'gate')}${flakeNote}. A stage of this tree is allowed.`);
  return 0;
}

/**
 * The stage's judgement as lines, a gate each (GATE6): the verdict that counts and why it still stands, or, when a
 * newer one no longer does, that one as stale with the paths that made it so, which is what a refused stage says.
 */
function stageLines(gates) {
  return gates.map((gate) => {
    const head = (label) => `  ${pad(gate.name, 16)} ${pad(label, 6)}`;
    if (gate.stale) {
      const { verdict, paths } = gate.stale;
      const why = paths.length ? `on a tree before ${listed(paths, 4)} changed, which reaches it` : 'on a tree that cannot be compared with this one';
      return `${head('stale')} ${verdict.verdict} ${when(verdict.at)}, ${why}`;
    }
    if (!gate.verdict) return `${head('none')} no verdict on this tree`;
    const { standing, paths, records } = gate.standing;
    const others = standing === 'unreached' ? `paths it does not reach (${listed(paths, 4)})` : '';
    const recorded = records.length ? `the records (${listed(records, 4)})` : '';
    const where = standing === 'exact' ? 'on this tree' : `on a tree that differs from this one only by ${[others, recorded].filter(Boolean).join(' and ')}`;
    return `${head(gate.verdict.verdict)} ${when(gate.verdict.at)}  ${where}`;
  });
}

/**
 * `--passed` (GATE3): whether the full set passed the checkout as it stands, gate by gate, read from the
 * verdicts record: each gate's newest verdict that still stands (GATE6), or the stale one and the paths that
 * made it so. What `publish:desktop` asks before it builds. Exit 0 when it did, 1 when it did not.
 */
function passedOnly(root) {
  const plan = readPlan(root);
  const judged = judgeCheckout(root, plan);
  if (!judged) throw new Refusal("git could not write the checkout's tree, so whether the full set passed it cannot be told");
  const head = git(root, ['rev-parse', '--short', 'HEAD'], { allowFail: true }).out.trim() || 'no commit';
  console.log(`merge-branch: the full set has ${judged.passed ? '' : 'NOT '}passed this checkout (tree ${judged.tree.slice(0, 7)}, HEAD ${head}): `
    + `${plan.length - judged.missing.length} of ${plan.length} gates.`);
  for (const line of stageLines(judged.gates)) console.log(line);
  // GATE6b: the smallest command that would pass it, which the publish's refusal names in turn.
  const remedy = stageRemedy(judged.gates);
  if (remedy) console.log(remedyLine(remedy));
  return judged.passed ? 0 : 1;
}

/** The stage's judgement of the checkout as it stands, with its tree; null when git cannot write the tree. */
function judgeCheckout(root, plan) {
  const tree = contentTree(root);
  if (!tree) return null;
  return { tree, ...stageJudgement({ ...standingFor(root, tree), plan: plan.map((gate) => gate.name), verdicts: readVerdicts(root).verdicts }) };
}

/** Whether the full set passed the checkout, as a clause: what `--passed` and a re-run on the checkout say. */
const stageVerdict = (judged, plan) => `the full set has ${judged.passed ? '' : 'NOT '}passed this checkout: ${plan.length - judged.missing.length} of ${plan.length} gates`;

/**
 * `--rerun <gate>…` with no merge open, and `--stale` (GATE6b): on the checkout as it stands, merging nothing, the named
 * gates and each the stage calls stale (`checkoutRerun`), every verdict recorded where `--passed` reads it; then the
 * stage's answer, and the smallest command for what it still lacks. Outside a merge, `--rerun` used to refuse, so a gate
 * that flaked in the full set, or a fix committed after it, left `--full` again, about an hour and a half, as the only
 * way to a stage.
 *
 * A clean tree only, naming what is not: the verdicts are recorded with HEAD's commit, and a re-run is for a commit (one
 * whose gate flaked, or a fix committed past the full set), so an uncommitted change would be gated as part of a tree
 * no commit holds. `--full` is the verb that gates the working tree as it stands.
 */
async function rerunCheckout(root, options) {
  const verb = options.rerun.length ? '--rerun' : '--stale';
  const head = headCommit(root);
  if (!head) throw new Refusal('this checkout has no commit to gate');
  const { changed } = treeState(root);
  if (changed.length) {
    throw new Refusal(`${notClean(changed)}\n  ${verb} on the checkout gates a commit: commit the change first, or ${FULL_COMMAND} gates the tree as it stands`);
  }
  if (git(root, ['check-ignore', '-q', `${SCRATCH}/full-probe.log`], { allowFail: true }).status !== 0) {
    throw new Refusal(`${SCRATCH}/ is not ignored here, so the gate logs would change the tree being gated`);
  }
  requireVerdictsIgnored(root);
  const plan = readPlan(root);
  const unknown = options.rerun.filter((name) => !plan.some((gate) => gate.name === name));
  if (unknown.length) {
    throw new Refusal(`--rerun: no gate ${unknown.map((name) => `'${name}'`).join(', ')} in the plan: ${plan.map((gate) => gate.name).join(', ')}`);
  }
  const judged = judgeCheckout(root, plan);
  if (!judged) throw new Refusal("git could not write the checkout's tree, so which verdicts still stand on it cannot be told");
  const planned = checkoutRerun(judged.gates, options.rerun);
  const gates = plan.filter((gate) => planned.run.includes(gate.name));
  const sha = head.slice(0, 7);
  // Beside the full set's logs for this commit, which it keeps but for the gates it runs again.
  const dir = join(root, SCRATCH, `full-${sha}`);
  mkdirSync(dir, { recursive: true });
  for (const gate of gates) {
    for (const suffix of ['.log', '.rerun.log', '.trx']) rmSync(join(dir, `${gate.name}${suffix}`), { force: true });
  }

  const staleNames = planned.stale.map((entry) => entry.name);
  const whose = `whose ${staleNames.length === 1 ? 'verdict' : 'verdicts'} a changed path reaches`;
  const kept = `${count(planned.kept.length, 'verdict')} kept; nothing is merged`;
  if (options.rerun.length) {
    console.log(`re-run on the checkout (HEAD ${sha}): ${options.rerun.join(', ')}${staleNames.length ? `, then ${staleNames.join(', ')}, ${whose}` : ''}; ${kept}`);
  } else if (staleNames.length) {
    console.log(`the stale gates on the checkout (HEAD ${sha}): ${staleNames.join(', ')}, ${whose}; ${kept}`);
  } else {
    console.log(`nothing is stale on the checkout (HEAD ${sha}): no verdict a changed path reaches; ${kept}`);
  }
  for (const entry of planned.stale) {
    const why = entry.paths.length ? `${listed(entry.paths, 4)} changed since, which reaches it` : 'its tree cannot be compared with this one';
    console.log(`  ${pad('stale', 8)} ${pad(entry.name, 16)} ${pad(entry.verdict, 8)} from ${when(entry.at)}: ${why}`);
  }
  // The rest as the stage reads them: each verdict that stands, a failure kept, and a gate with none.
  for (const line of stageLines(judged.gates.filter((gate) => !planned.run.includes(gate.name)))) console.log(line);
  const ran = await runRecorded(root, gates, dir, { keepGoing: options.keepGoing });

  const failed = ran.filter((result) => result.verdict === 'FAIL').map((result) => result.gate.name);
  const notRun = gates.length - ran.length;
  const flakes = ran.filter((result) => result.verdict === 'FLAKE').map((result) => result.gate.name);
  // The stage's answer, read again from the record the run just wrote, on the tree as it is now.
  const after = judgeCheckout(root, plan);
  const remedy = after ? stageRemedy(after.gates) : null;
  const stage = after ? stageVerdict(after, plan) : "git could not write the checkout's tree, so whether the full set passed it cannot be told";
  console.log('');
  if (failed.length || notRun) {
    console.log(`merge-branch: the re-run on ${sha} did NOT pass: ${failed.join(', ')} failed${notRun ? `; ${notRun} not run` : ''}; ${stage}.`);
  } else {
    const flakeNote = flakes.length ? `, with ${count(flakes.length, 'flake')} (${flakes.join(', ')}): record it under FLAKE1` : '';
    const ranNote = ran.length ? `${count(ran.length, 'gate')} run and passed on ${sha}${flakeNote}` : `no gate run on ${sha}`;
    const passed = after?.passed === true;
    console.log(`merge-branch: ${ranNote}; ${stage}.${passed ? ' A stage of this tree is allowed.' : ''}`);
  }
  if (remedy) console.log(remedyLine(remedy));
  return !failed.length && !notRun && after?.passed === true ? 0 : 1;
}

function reportConflict(branch, unmerged, generated = []) {
  console.log(`merge-branch: merging ${branch} stopped on a conflict in ${count(unmerged.length, 'file')}:`);
  for (const file of unmerged) console.log(`  ${file}`);
  if (generated.length) console.log(`  (and ${count(generated.length, 'generated file')}, written again from the merged tree once these are resolved)`);
  console.log('  Resolve them (code is never resolved for you; the union records resolve themselves), git add them,');
  console.log('  then node tools/merge-branch.mjs --continue to run the gates. Or git merge --abort.');
  return 1;
}

/** The unmerged paths a person resolves, and the generated ones a merge writes again (ORIENT1a). */
function conflicts(root) {
  const unmerged = unmergedPaths(root);
  return { blocking: unmerged.filter((path) => !isGenerated(path)), generated: unmerged.filter(isGenerated) };
}

async function mergeNext(root, state) {
  const branch = state.branches[state.at];
  const plan = readPlan(root);
  const tip = requireBranch(root, branch);
  const commits = branchCommits(root, branch);
  for (const line of laneReport(root, branch)) console.log(line);

  if (state.commitCheck) {
    const problems = commitProblems(root, branch, commits);
    if (problems.length) {
      throw new Refusal(`the commit check refuses ${branch}:\n${problems.map((problem) => `  ${problem}`).join('\n')}\n  (--no-commit-check merges it anyway)`, 1);
    }
    console.log(`commit check: ${count(commits.length, 'commit')}, each of its own with its Co-Authored-By line; nothing left uncommitted`);
  } else {
    console.log('commit check: skipped (--no-commit-check)');
  }

  // After every refusal, so a refused merge still touches nothing; the batch's branches are its own.
  if (state.autoPrune === false) console.log('prune: skipped (--no-prune)');
  else pruneAround(root, state.branches.slice(state.at));

  state.tips[branch] = tip;
  writeState(root, state);
  const merged = git(root, ['merge', '--no-ff', '--no-commit', branch], { allowFail: true });
  if (merged.status !== 0) {
    const { blocking, generated } = mergeInProgress(root) ? conflicts(root) : { blocking: [], generated: [] };
    if (blocking.length) return reportConflict(branch, blocking, generated);
    // A conflict only in what the merge writes anyway goes on to that writing (ORIENT1a).
    if (!generated.length) throw new Refusal(`git merge ${branch} failed:\n${merged.err || merged.out}`);
  }
  console.log(`merged ${branch} --no-ff --no-commit${state.branches.length > 1 ? ` (${state.at + 1} of ${state.branches.length})` : ''}`);
  return gateMerge(root, state, plan);
}

async function start(root, options) {
  preconditions(root);
  const pending = readState(root);
  if (pending && pending.at < pending.branches.length - 1) {
    throw new Refusal(`a batch is recorded with ${pending.branches.slice(pending.at + 1).join(', ')} still to merge: run --continue, or --drop-batch to forget it`);
  }
  // A misspelt later branch refuses before the first is merged; the first is checked as it merges.
  for (const branch of options.branches.slice(1)) requireBranch(root, branch);
  readPlan(root);
  return mergeNext(root, {
    branches: options.branches, at: 0, tips: {}, keepGoing: options.keepGoing, commitCheck: options.commitCheck, autoPrune: options.autoPrune,
    full: options.full,
  });
}

async function resume(root, options) {
  const state = readState(root);
  if (!state) throw new Refusal('nothing to continue: this tool has no merge recorded here');
  state.keepGoing = state.keepGoing || options.keepGoing;
  state.full = state.full === true || options.full;
  state.commitCheck = state.commitCheck && options.commitCheck;
  // A batch recorded before the prune existed has no field, and prunes.
  state.autoPrune = state.autoPrune !== false && options.autoPrune;
  const branch = state.branches[state.at];

  if (mergeInProgress(root)) {
    requireMain(root);
    const open = git(root, ['rev-parse', 'MERGE_HEAD']).out.trim();
    if (open !== state.tips[branch]) throw new Refusal(`the merge in progress is not ${branch}'s, which is the one recorded`);
    const { blocking, generated } = conflicts(root);
    if (blocking.length) return reportConflict(branch, blocking, generated);
    writeState(root, state);
    return gateMerge(root, state, readPlan(root));
  }

  const landed = git(root, ['merge-base', '--is-ancestor', state.tips[branch], 'HEAD'], { allowFail: true }).status === 0;
  if (state.at === state.branches.length - 1) {
    dropState(root);
    console.log(landed
      ? `merge-branch: the batch is done: ${state.branches.join(', ')} ${state.branches.length === 1 ? 'is' : 'are'} in ${MAIN}.`
      : `merge-branch: the batch is done, but ${branch} is not in ${MAIN}: its merge was abandoned.`);
    // The batch's last branch landed after the last merge's prune ran, so it goes here.
    if (state.autoPrune) pruneAround(root, []);
    return 0;
  }
  preconditions(root);
  if (!landed) {
    throw new Refusal(`the merge of ${branch} is not committed (it is not in ${MAIN}'s history): commit it after reading the diff and writing the records, or --drop-batch`);
  }
  state.at += 1;
  return mergeNext(root, state);
}

function planOnly(root, options) {
  const plan = readPlan(root);
  const lanes = readLanes(root)?.lanes ?? [];
  const reachedAny = new Set();
  console.log(`plan (nothing is merged; the current branch is ${treeState(root).branch || 'a detached HEAD'}):`);
  options.branches.forEach((branch, i) => {
    requireBranch(root, branch);
    const commits = parseCommits(git(root, ['log', COMMIT_FORMAT, `HEAD..${branch}`]).out);
    console.log('');
    console.log(`${i + 1}. ${branch}: ${count(commits.length, 'commit')} ${MAIN} lacks`);
    for (const line of laneReport(root, branch)) console.log(`  ${line}`);
    const problems = options.commitCheck ? commitProblems(root, branch, commits) : [];
    console.log(problems.length ? `  commit check would refuse it:\n${problems.map((problem) => `    ${problem}`).join('\n')}` : '  commit check: nothing to refuse');
    // GATE3: what this branch's paths reach, each gate with why it runs or is skipped.
    const selection = selectGates(plan, branchChanges(root, branch), { lanes, full: options.full });
    const runs = selection.gates.filter((entry) => entry.run);
    console.log(`  gates it reaches: ${runs.length} of ${plan.length}${options.full ? ' (--full)' : ''}`);
    for (const line of selectionLines(selection.gates)) console.log(`  ${line}`);
    for (const entry of runs) reachedAny.add(entry.gate.name);
  });
  console.log('');
  if (options.autoPrune) {
    const { entries } = planPrune(root, { hold: options.branches });
    console.log(pruneHeader(entries, { plan: true, atMerge: true }));
    for (const entry of entries) console.log(pruneLine(root, entry, false));
  } else {
    console.log('prune: skipped (--no-prune)');
  }
  console.log('');
  const order = plan.filter((gate) => reachedAny.has(gate.name));
  console.log(`gates, in order${options.branches.length > 1 ? ' (the rehearsals run after the last branch only, each one any branch reached)' : ''}:`);
  for (const line of planLines(order)) console.log(line);
  const owed = owedLine(plan.length - order.length);
  if (owed) console.log(owed);
  console.log(`logs: ${SCRATCH}/merge-<branch>/<gate>.log`);
  return 0;
}

function dropBatch(root) {
  const state = readState(root);
  dropState(root);
  console.log(state ? `merge-branch: forgot the batch (${state.branches.join(', ')}).` : 'merge-branch: no batch was recorded.');
  if (mergeInProgress(root)) console.log('  A merge is still open in the tree: git merge --abort drops it.');
  return 0;
}

export async function main(argv) {
  const options = parseArgs(argv);
  const root = locate(process.cwd());
  if (options.dropBatch) return dropBatch(root);
  if (options.prune) return pruneOnly(root, options);
  if (options.plan) return planOnly(root, options);
  if (options.passed) return passedOnly(root);
  // GATE6b: with no merge open, --rerun and --stale act on the checkout as it stands.
  if (options.stale) {
    if (mergeInProgress(root)) throw new Refusal('a merge is open here: --rerun <gate>… re-gates it, and runs each gate a fixed path reaches; or git merge --abort');
    return rerunCheckout(root, options);
  }
  if (options.rerun.length) return mergeInProgress(root) ? rerunGates(root, options) : rerunCheckout(root, options);
  if (options.resume) return resume(root, options);
  if (options.full && options.branches.length === 0) return gateCheckout(root, options);
  return start(root, options);
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      if (error instanceof Refusal) {
        console.error(`merge-branch: ${error.message}`);
        if (error.usage) console.error(USAGE);
        process.exitCode = error.exitCode;
      } else {
        console.error(error?.stack ?? String(error));
        process.exitCode = 2;
      }
    },
  );
}
