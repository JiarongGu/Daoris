#!/usr/bin/env node
/**
 * The parent's one merge tool (MOD9, `docs/2026-09-30-parallel-development-design.md` §3 rule 6).
 *
 * ## Why
 *
 * The parent merges subagents' branches into main many times a day, and until this tool it typed each
 * gate chain by hand. On 2026-09-30 that went wrong twice: a hand-typed chain lost a rehearsal's reason,
 * and a batch of a "web only" branch skipped the .NET suites, so two C# tests that read the web's
 * catalogues broke on main unseen. So nothing here chooses gates by what a branch touched. The plan is
 * read from what the repository already declares, and every merge runs all of it.
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
 *    their `merge=union` attribute (D106).
 * 6. The gates, one at a time, in a fixed order, fast first: every gate `daoris.gates.json` declares, plus
 *    every `npm run` step the release workflow runs that the declaration does not (the release and
 *    family rehearsals). The order is by kind (a devkit check, then the suites, then the rehearsals);
 *    within a kind the declared order holds, and the workflow's own rehearsals go before the declared
 *    ones, so the declaration's last gate, the deployment rehearsal, ends the run. That rehearsal starts
 *    after `dotnet build-server shutdown`: on 2026-09-30 a long-lived build server carried a broken
 *    environment into a publish.
 * 7. Each gate's whole output goes to `local/scratch/merge-<branch>/<gate>.log` (gitignored), written
 *    beside and renamed when the gate ends, and one line per gate says its result and that file. The
 *    first failing gate stops the run (`--keep-going` runs the rest), and what did not run is named.
 * 8. Nothing is committed. Committing stays the parent's, after it reads the diff and writes the records.
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
 *   node tools/merge-branch.mjs <branch> [--batch <branch>…] [--keep-going] [--no-commit-check] [--no-prune]
 *   node tools/merge-branch.mjs --continue [--keep-going] [--no-prune]
 *   node tools/merge-branch.mjs --plan <branch> [--batch <branch>…]
 *   node tools/merge-branch.mjs --prune [--plan]
 *   node tools/merge-branch.mjs --drop-batch
 *
 * Exit codes: 0 merged and every gate passed (flakes named), or pruned (a branch kept is the rule working)
 * · 1 a conflict, a failed gate, or a failed commit check · 2 refused (usage, not on main, a dirty tree,
 * an unknown branch) or a tool error.
 */
import { spawn, spawnSync } from 'node:child_process';
import {
  closeSync, existsSync, mkdirSync, openSync, readFileSync, readdirSync, renameSync, rmSync, statSync, writeFileSync, writeSync,
} from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { isMain } from './fsx.mjs';

export const MAIN = 'main';
const SCRATCH = 'local/scratch';
const STATE = `${SCRATCH}/merge-batch.json`;
const SHUTDOWN = 'dotnet build-server shutdown';
const KINDS = ['check', 'suite', 'rehearsal'];

/**
 * The gates that run the `Process` test category (MOD8): every test in one starts a real process or runs a
 * real tick, so a failure there may be the load and is re-run alone once. Named by the settings file those
 * gates run with, not by a list of classes: a list goes stale the day a new real-process class is written,
 * and one did (a landing plugin's and a protocol chat's flakes read as plain failures).
 */
export const isProcessGate = (gate) => /process\.runsettings/.test(gate.run);

export const USAGE = [
  'usage: node tools/merge-branch.mjs <branch> [--batch <branch>…] [--keep-going] [--no-commit-check] [--no-prune]',
  '       node tools/merge-branch.mjs --continue [--keep-going] [--no-prune]',
  '       node tools/merge-branch.mjs --plan <branch> [--batch <branch>…]',
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
  };
  let batch = false;
  let positional = 0;
  let batched = 0;
  for (const arg of argv) {
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
    else if (arg.startsWith('-')) throw usageError(`unknown option '${arg}'`);
    else {
      if (batch) batched += 1;
      else if (++positional > 1) throw usageError('two branches without --batch: name the rest after --batch');
      if (options.branches.includes(arg)) throw usageError(`'${arg}' is named twice`);
      options.branches.push(arg);
    }
  }
  // `--plan` is a verb of its own, or the prune's plan.
  if ([options.resume, options.dropBatch, options.plan && !options.prune, options.prune].filter(Boolean).length > 1) {
    throw usageError('give one of --continue, --drop-batch, --plan and --prune (--prune takes --plan)');
  }
  if (batch && batched === 0) throw usageError('--batch names at least one branch after it');
  if (options.prune) {
    if (!options.autoPrune) throw usageError('--no-prune is for a merge; --prune is the prune itself');
    if (options.branches.length) throw usageError('--prune takes no branch: it acts on every branch merged into main');
  } else if (options.resume || options.dropBatch) {
    if (options.branches.length) {
      throw usageError(`${options.resume ? '--continue' : '--drop-batch'} takes no branch: it acts on the batch already recorded`);
    }
  } else if (options.branches.length === 0) {
    throw usageError('name the branch to merge');
  }
  return options;
}

// ---------------------------------------------------------------------------------------------------
// The plan: the declared gates, plus the workflow's own rehearsals, fast first

/** A name safe as one path segment: a log file, a scratch folder. */
export const slug = (name) => name.replace(/[^A-Za-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '') || 'gate';

/**
 * What kind of gate a command is, which orders it and never drops it: a devkit `dotnet run` is a
 * check, `npm run rehearse*` and `npm run test:*` are rehearsals, and anything else is a suite.
 */
export function gateKind(run) {
  const command = run.trim();
  if (/^dotnet run\b/.test(command)) return 'check';
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

function git(cwd, args, { allowFail = false } = {}) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
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

/** Why a merge cannot start from what git reported, or null. */
export function startRefusal({ branch, merging, changed, ignored }) {
  if (branch !== MAIN) {
    return `the current branch is '${branch || 'a detached HEAD'}', not ${MAIN}: merges land on ${MAIN}, from its own checkout`;
  }
  if (merging) return 'a merge is already in progress: resolve it and run --continue, or git merge --abort';
  if (changed.length) {
    const shown = changed.slice(0, 12).map(({ code, path }) => `  ${code} ${path}`).join('\n');
    return `the tree is not clean:\n${shown}${changed.length > 12 ? `\n  … ${changed.length - 12} more` : ''}`;
  }
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
  const file = statePath(root);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(`${file}.partial`, `${JSON.stringify(state, null, 2)}\n`);
  renameSync(`${file}.partial`, file);
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

/** A log is written beside and renamed when its gate ends, so a half-written one never reads as whole. */
async function withLog(file, work) {
  const partial = `${file}.partial`;
  writeFileSync(partial, '');
  const fd = openSync(partial, 'a');
  try {
    return await work(fd, partial);
  } finally {
    closeSync(fd);
    renameSync(partial, file);
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

/** A gate's steps before it, then the gate, into one log: the gate's exit. */
function runSteps(root, gate, log, step) {
  const cwd = gate.cwd ? join(root, gate.cwd) : root;
  return withLog(log, async (fd) => {
    for (const before of gate.before) {
      const beforeCode = await step(before, root, fd);
      writeSync(fd, `\n(exited ${beforeCode}${beforeCode ? '; carried on: it only clears a server' : ''})\n\n`);
    }
    const exit = await step(gate.run, cwd, fd);
    writeSync(fd, `\n(exited ${exit})\n`);
    return exit;
  });
}

/** One gate, its steps before it, and its flake re-run: a result with its verdict, note and log. */
export async function runGate(root, gate, dir, { step = runStep } = {}) {
  const log = join(dir, `${gate.name}.log`);
  const started = Date.now();
  const code = await runSteps(root, gate, log, step);
  const result = { gate, code, log, verdict: code === 0 ? 'PASS' : 'FAIL', note: code === 0 ? '' : `exit ${code}` };
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

/**
 * The gates in order, one line each as it ends. The first failure stops the run and every gate after it
 * is named as not run, unless `keepGoing`.
 */
export async function runGates(root, gates, dir, { keepGoing = false, run = runGate, print = console.log } = {}) {
  const results = [];
  for (let i = 0; i < gates.length; i += 1) {
    const result = await run(root, gates[i], dir);
    results.push(result);
    print(resultLine(root, result));
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
    if (/\.(log|partial)$/.test(name)) rmSync(join(dir, name), { force: true });
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

async function gateMerge(root, state, plan) {
  const branch = state.branches[state.at];
  const last = state.at === state.branches.length - 1;
  const gates = last ? plan : plan.filter((gate) => gate.kind !== 'rehearsal');
  const dir = join(root, SCRATCH, `merge-${slug(branch)}`);
  clearLogs(dir);

  console.log(`gates for ${branch}: ${gates.length}, fast first${last ? '' : `; the rehearsals wait for the batch's last branch (${state.branches.at(-1)})`}`);
  const results = await runGates(root, gates, dir, { keepGoing: state.keepGoing });
  const failed = results.filter((result) => result.verdict === 'FAIL');
  const flakes = results.filter((result) => result.verdict === 'FLAKE');
  const notRun = gates.length - results.length;

  console.log('');
  if (failed.length) {
    console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed, and ${count(failed.length, 'gate')} failed: ${failed.map((result) => result.gate.name).join(', ')}${notRun ? `; ${notRun} not run` : ''}.`);
    console.log('  The merge is left in place. Read the failing log, fix it in the merge, and run --continue to gate it again, or git merge --abort.');
    return 1;
  }
  const flakeNote = flakes.length ? `, with ${count(flakes.length, 'flake')} (${flakes.map((result) => result.gate.name).join(', ')}): record it under FLAKE1` : '';
  console.log(`merge-branch: ${branch} is merged into ${MAIN}, NOT committed; ${count(results.length, 'gate')} passed${flakeNote}.`);
  if (last) {
    console.log('  Next: read the diff (git diff --cached), write the records, and commit the merge.');
  } else {
    console.log(`  Next: read the diff (git diff --cached), write the records, commit the merge; then node tools/merge-branch.mjs --continue merges ${state.branches[state.at + 1]}.`);
  }
  return 0;
}

function reportConflict(branch, unmerged) {
  console.log(`merge-branch: merging ${branch} stopped on a conflict in ${count(unmerged.length, 'file')}:`);
  for (const file of unmerged) console.log(`  ${file}`);
  console.log('  Resolve them (code is never resolved for you; the union records resolve themselves), git add them,');
  console.log('  then node tools/merge-branch.mjs --continue to run the gates. Or git merge --abort.');
  return 1;
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
    const unmerged = mergeInProgress(root) ? unmergedPaths(root) : [];
    if (unmerged.length) return reportConflict(branch, unmerged);
    throw new Refusal(`git merge ${branch} failed:\n${merged.err || merged.out}`);
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
  });
}

async function resume(root, options) {
  const state = readState(root);
  if (!state) throw new Refusal('nothing to continue: this tool has no merge recorded here');
  state.keepGoing = state.keepGoing || options.keepGoing;
  state.commitCheck = state.commitCheck && options.commitCheck;
  // A batch recorded before the prune existed has no field, and prunes.
  state.autoPrune = state.autoPrune !== false && options.autoPrune;
  const branch = state.branches[state.at];

  if (mergeInProgress(root)) {
    requireMain(root);
    const open = git(root, ['rev-parse', 'MERGE_HEAD']).out.trim();
    if (open !== state.tips[branch]) throw new Refusal(`the merge in progress is not ${branch}'s, which is the one recorded`);
    const unmerged = unmergedPaths(root);
    if (unmerged.length) return reportConflict(branch, unmerged);
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
  console.log(`plan (nothing is merged; the current branch is ${treeState(root).branch || 'a detached HEAD'}):`);
  options.branches.forEach((branch, i) => {
    requireBranch(root, branch);
    const commits = parseCommits(git(root, ['log', COMMIT_FORMAT, `HEAD..${branch}`]).out);
    console.log('');
    console.log(`${i + 1}. ${branch}: ${count(commits.length, 'commit')} ${MAIN} lacks`);
    for (const line of laneReport(root, branch)) console.log(`  ${line}`);
    const problems = options.commitCheck ? commitProblems(root, branch, commits) : [];
    console.log(problems.length ? `  commit check would refuse it:\n${problems.map((problem) => `    ${problem}`).join('\n')}` : '  commit check: nothing to refuse');
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
  console.log(`gates, in order${options.branches.length > 1 ? ' (the rehearsals run after the last branch only)' : ''}:`);
  for (const line of planLines(plan)) console.log(line);
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
  if (options.resume) return resume(root, options);
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
