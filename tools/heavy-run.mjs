#!/usr/bin/env node
/**
 * The heavy tier's run after a merge lands (`tools/merge/heavy-after.ts` says why and what it records), started in the background by the
 * merge queue on main's new tip, never by hand in the main checkout:
 *
 *   node tools/heavy-run.mjs --id <id> --commit <sha> --base <sha> [--branches <a,b>] [--now]
 *
 * The landing is judged first (`admitLanding`, `merge/heavy-batch.ts`): with no reason it settles `skipped` and queues nothing, else it
 * waits for a pause in landings. `--now` is the person's asking: judged by no one, it takes the heavy tree at once over `--base` to `--commit`.
 *
 * It sets the heavy tree (`local/heavy-tree`, a worktree of its own, so main moves on beside it) to the commit, readies it as a builder's
 * worktree is (`tools/worktree-setup.mjs`), and runs `tools/reached-checks.mjs --tier heavy --base <base>` there: every Process, git
 * and rehearsal class the merge's paths reach, each pass recorded in the store the next merge replays from. What it ends with is
 * appended to `local/heavy-runs.jsonl`; a red is filed as a finding in the CI's record, which the next merge reads as main's red.
 *
 * One tier holds the heavy tree at a time, and a newer landing's supersedes the one before it (`merge/heavy-stack.ts`,
 * `local/heavy-stack.json`): it waits, and asks the holder to stop through the holder's halt file (`--halt-file`, `merge/heavy-halt.ts`).
 * The holder starts no class more, ends or finishes the group in flight, and hands its range and unrun classes over; the newer tier
 * runs the union once, from the oldest base, on its own commit. Each says the handover on a `heavy tier:` line of its log and in its
 * record (`handedTo`, `handed`; `took`, `from`). So two tiers never run their git halves at once, and a run waiting for the tree, or
 * (in its reached run) for the machine's admission, past `WAIT_BOUND_MS` says so; one waiting for the tree past `WAIT_DEADLINE_MS`
 * gives up, its range carried by the next tier.
 * Exit codes: 0 green · 1 red, superseded or an error · 2 a tool error.
 */
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { isMain, writeAtomic } from './fsx.mjs';
import { readFindings, writeFindings, fileSighting } from './ci-background.mjs';
import { processAlive } from './machine-admission.mjs';
import { startBackground } from './merge/background.ts';
import { HEAVY_PREFIX, HEAVY_TREE, appendHeavy, settleHeavy, skipHeavy } from './merge/heavy-after.ts';
import { HEAVY_PENDING, HEAVY_QUIET_MS, HEAVY_QUIET_POLL_MS, bump, changePending, enlist, judgeLanding, look as lookPending, rangesOf } from './merge/heavy-batch.ts';
import { readVerifyBudget } from './merge/verification.ts';
import { readHalt } from './merge/heavy-halt.ts';
import {
  HEAVY_STACK, STACK_BEAT_MS, STACK_POLL_MS, STACK_STALE_MS, WAIT_BOUND_MS, WAIT_DEADLINE_MS,
  arrive, beat, changeStack, giveUp, handOver, haltFileOf, look, readStack, release,
} from './merge/heavy-stack.ts';
import { parseOutcome } from './merge/reached-classify.ts';

/** A run settled into the checkout's records: its line appended, each red filed as a finding. Answers the lines to say. */
export function settleInto(home, run, outcome, code, now = new Date().toISOString()) {
  const settled = settleHeavy(run, outcome, code, now);
  let findings = readFindings(home);
  for (const sighting of settled.sightings) findings = fileSighting(findings, sighting).record;
  if (settled.sightings.length) writeFindings(home, findings);
  appendHeavy(home, settled.run);
  return { run: settled.run, lines: settled.lines };
}

const git = (cwd, args) => spawnSync('git', args, { cwd, encoding: 'utf8', windowsHide: true });

/** The heavy tree at `commit`: made where none stands, moved there where it holds another. */
function prepareTree(checkout, commit) {
  const path = join(checkout, HEAVY_TREE);
  if (!existsSync(path)) {
    const made = git(checkout,['worktree', 'add', '--detach', path, commit]);
    if (made.status !== 0) throw new Error(`the heavy tree could not be made: ${String(made.stderr).trim()}`);
    return path;
  }
  const moved = git(path, ['checkout', '--quiet', '--detach', commit]);
  if (moved.status !== 0) throw new Error(`the heavy tree could not move to ${commit.slice(0, 7)}: ${String(moved.stderr).trim()}`);
  return path;
}

const runLogged = (command, args, cwd) => new Promise((done) => {
  let text = '';
  const child = spawn(command, args, { cwd, windowsHide: true, stdio: ['ignore', 'pipe', 'inherit'] });
  child.stdout.on('data', (chunk) => { text += chunk; process.stdout.write(chunk); });
  child.on('error', () => done({ code: null, text }));
  child.on('close', (code) => done({ code, text }));
});

const short = (sha) => String(sha ?? '').slice(0, 7);
const spanOf = (ms) => `${Math.floor(ms / 60_000)}m ${Math.round((ms % 60_000) / 1000)}s`;

/**
 * A landing's runner before its tier: judged by the change it landed, a landing with no reason settles `skipped`; a reasoned one joins the
 * pending batch and waits until no landing has come for `quietMs`, settling `superseded` if a newer one carries it. Answers
 * `{ outcome, run }` where it ended here, or `{ outcome: 'start', batch }`.
 */
export async function admitLanding(input, {
  checkout, judge, settle, say = console.log, now = Date.now, sleep = (ms) => new Promise((done) => { setTimeout(done, ms); }),
  pollMs = HEAVY_QUIET_POLL_MS, quietMs = HEAVY_QUIET_MS, append = (record) => appendHeavy(checkout, record),
}) {
  const file = join(checkout, HEAVY_PENDING);
  const stamp = () => new Date(now()).toISOString();
  const tell = (line) => say(`${HEAVY_PREFIX} ${line}`);
  const run = { id: input.id, state: 'running', commit: input.commit, base: input.base, branches: input.branches, at: input.at };
  let judged;
  try {
    judged = await judge(input);
  } catch (error) {
    judged = { run: true, reason: null, said: `the landing could not be judged (${error.message}): its tier runs` };
  }
  if (!judged.run) {
    changePending(file, (pending) => ({ pending: bump(pending, input.at) }));
    const skipped = skipHeavy(run, judged.said, stamp());
    append(skipped.run);
    for (const line of skipped.lines) say(line);
    return { outcome: 'skipped', run: skipped.run };
  }
  const joined = changePending(file, (pending) => ({ ...enlist(pending, input) }));
  if (joined.superseded) tell(`${input.id} on ${short(input.commit)} carries ${joined.superseded.id} on ${short(joined.superseded.commit)}, which was waiting for a pause in landings`);
  tell(`${input.id} on ${short(input.commit)} bears a reason for a heavy run (${judged.said}): its tier starts once no landing has come for ${spanOf(quietMs)}`);
  for (;;) {
    await sleep(pollMs);
    let seen;
    try {
      seen = changePending(file, (pending) => lookPending(pending, input.id, now(), quietMs));
    } catch {
      continue;
    }
    if (seen.role === 'superseded') return { outcome: 'superseded', run: (await settle({ ...run, handedTo: { id: seen.by.id, commit: seen.by.commit } }, null, null)).run };
    if (seen.role === 'gone') {
      const note = 'the pending batch was gone before its tier started; its range is carried by the next reasoned landing';
      const gone = { ...run, state: 'error', ended: stamp(), note };
      append(gone);
      say(`${HEAVY_PREFIX} ERROR ${input.id} on ${short(input.commit)}: ${note}`);
      return { outcome: 'gone', run: gone };
    }
    if (seen.role === 'start') return { outcome: 'start', batch: seen.batch };
  }
}

function landedPaths(checkout, base, commit) {
  const ran = git(checkout, ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', base, commit]);
  if (ran.status !== 0) throw new Error(`git could not read what ${short(commit)} changed: ${String(ran.stderr).trim()}`);
  return String(ran.stdout).split('\0').filter(Boolean);
}

/** The census's share a landing's paths reach, read from the checkout's own census, which the merge that landed it just read. */
async function reachOfPaths(checkout, paths) {
  const { keyContextOf } = await import('./merge-branch/full-set.mjs');
  const world = await keyContextOf(checkout);
  const units = world.map.units;
  return { reached: units.filter((unit) => paths.some((path) => world.context.reaches(unit, path))).length, of: units.length, what: 'classes' };
}

export const judgeIn = (checkout) => async ({ base, commit }) => {
  const rule = readVerifyBudget(checkout);
  const paths = rule ? landedPaths(checkout, base, commit) : [];
  const measured = rule && !judgeLanding(rule, paths, () => undefined).run ? await reachOfPaths(checkout, paths) : undefined;
  return judgeLanding(rule, paths, () => measured);
};

/**
 * One tier through the heavy stack (`merge/heavy-stack.ts`): it holds the tree or waits for it, superseding the tier before it, then
 * prepares the tree, runs its checks from the oldest base it carries, and hands over or lets the tree go. Answers what `settle`
 * answers. The seams (`alive`, `now`, `sleep`, `prepare`, `check`, `settle`, `append`, and the timings) are a test's.
 */
export async function runTier(input, {
  checkout, say = console.log, alive = processAlive, pid = process.pid, now = Date.now, sleep = (ms) => new Promise((done) => { setTimeout(done, ms); }),
  pollMs = STACK_POLL_MS, beatMs = STACK_BEAT_MS, staleMs = STACK_STALE_MS, boundMs = WAIT_BOUND_MS, deadlineMs = WAIT_DEADLINE_MS,
  prepare, check, settle, append = (record) => appendHeavy(checkout, record),
}) {
  const file = join(checkout, HEAVY_STACK);
  const stamp = () => new Date(now()).toISOString();
  const me = { id: input.id, pid, commit: input.commit, base: input.base, branches: input.branches, since: stamp(), beat: stamp(), ...(input.ranges ? { ranges: input.ranges } : {}) };
  const live = (tier) => alive(tier.pid) && now() - Date.parse(tier.beat) < staleMs;
  const run = {
    id: input.id, state: 'running', commit: input.commit, base: input.base, branches: input.branches, at: input.at,
    ...(input.carried?.length ? { took: input.carried } : {}),
  };
  const tell = (line) => say(`${HEAVY_PREFIX} ${line}`);
  // A tier whose process ended without settling is settled here, its range carried by this one.
  const bury = (dead) => {
    for (const tier of dead) {
      append({ id: tier.id, state: 'error', commit: tier.commit, base: tier.base, branches: tier.branches, at: tier.since, ended: stamp(), note: `its runner ended without settling; its range is carried by ${me.id}` });
      tell(`${tier.id} on ${short(tier.commit)} ended without handing over: ${me.id} carries its range`);
    }
  };
  let arrival;
  try {
    arrival = changeStack(file, (stack) => arrive(stack, me, live));
  } catch (error) {
    tell(`the heavy stack could not be read or written (${error.message}): ${me.id} runs without it`);
    arrival = { role: 'hold', took: null, halt: null, superseded: null, dead: [] };
  }
  bury(arrival.dead);
  if (arrival.superseded) tell(`${arrival.superseded.id} on ${short(arrival.superseded.commit)} was waiting for the heavy tree: ${me.id} carries its range`);
  let took = arrival.took;
  if (arrival.role === 'wait') {
    const holder = arrival.halt;
    const halt = haltFileOf(checkout, holder.id);
    try {
      mkdirSync(dirname(halt), { recursive: true });
      writeAtomic(halt, `${JSON.stringify({ by: me.id, commit: me.commit })}\n`);
    } catch (error) {
      tell(`${holder.id}'s halt could not be written (${error.message}): it runs its classes to their end before it hands over`);
    }
    tell(`${me.id} on ${short(me.commit)} supersedes ${holder.id} on ${short(holder.commit)}: it starts no class more, and hands what it leaves unrun here`);
    const waitedFrom = now();
    let said = 0;
    for (;;) {
      await sleep(pollMs);
      let seen;
      try {
        seen = changeStack(file, (stack) => look(stack, me, live, stamp()));
      } catch {
        continue;
      }
      bury(seen.dead);
      if (seen.role === 'superseded') {
        if (seen.by) return settle({ ...run, handedTo: { id: seen.by.id, commit: seen.by.commit } }, null, null);
        append({ ...run, state: 'error', ended: stamp(), note: 'its place in the heavy stack was taken as stale; its range is carried by the next tier' });
        return { run: { ...run, state: 'error' }, lines: [] };
      }
      if (seen.role === 'hold') {
        took = seen.took;
        break;
      }
      const waited = now() - waitedFrom;
      const on = seen.stack.holder;
      if (waited >= deadlineMs) {
        try {
          changeStack(file, (stack) => ({ stack: giveUp(stack, me) }));
        } catch {
          // Left: this tier stops beating, so the next one to look carries its range.
        }
        const note = `it waited ${spanOf(waited)} for ${on?.id ?? 'the holder'} to hand the heavy tree over and gave up; its range is carried by the next tier`;
        const gaveUp = { ...run, state: 'error', ended: stamp(), note };
        append(gaveUp);
        return { run: gaveUp, lines: [`${HEAVY_PREFIX} ERROR ${me.id} on ${short(me.commit)}: ${note}`] };
      }
      if (on && waited >= boundMs * (said + 1)) {
        said = Math.floor(waited / boundMs);
        tell(`${me.id} has waited ${spanOf(waited)} for ${on.id} (pid ${on.pid}, on ${short(on.commit)}, since ${on.since}) to hand the heavy tree over, past its bound of ${spanOf(boundMs)}: it waits while that run lives and beats, and gives up at ${spanOf(deadlineMs)}`);
      }
    }
  }
  const from = took?.base ?? input.base;
  const carrying = took ? { ...run, from, took: [...(run.took ?? []), ...took.from.map(({ id, commit, branches }) => ({ id, commit, branches }))] } : run;
  const ranges = input.ranges ? [...(took?.from.flatMap((each) => each.ranges ?? [{ base: each.base, commit: each.commit }]) ?? []), ...input.ranges] : null;
  if (took) tell(`${me.id} carries ${took.from.map((each) => `${each.id} (${short(each.commit)}, ${each.unrun ? `${each.unrun.length} unrun` : 'its whole range'})`).join('; ')}: it runs from ${short(from)} on ${short(me.commit)}`);
  const timer = setInterval(() => {
    try {
      changeStack(file, (stack) => ({ stack: beat(stack, me, stamp()) }));
    } catch {
      // A beat missed is the next one's.
    }
  }, beatMs);
  timer.unref?.();
  const halt = haltFileOf(checkout, me.id);
  const askedOf = (stack) => (stack.holder?.id === me.id ? stack.holder.supersededBy : undefined);
  try {
    let outcome = null;
    let code = null;
    try {
      const tree = await prepare(me.commit);
      if (!askedOf(readStack(file))) ({ code, outcome } = await check({ tree, commit: me.commit, base: from, ranges, halt }));
    } catch (error) {
      tell(error.message);
    }
    let ended = { to: null, asked: null };
    try {
      ended = changeStack(file, (stack) => {
        const asked = askedOf(stack);
        if (!asked || (outcome && !outcome.handed?.length)) return { stack: release(stack, me), to: null, asked: null };
        return { ...handOver(stack, me, { unrun: outcome ? outcome.handed : null, from, took }), asked };
      });
    } catch (error) {
      tell(`the heavy stack could not be written as ${me.id} ended (${error.message}): the next tier takes the tree once this process is gone`);
    }
    const ask = ended.asked ? readHalt(halt) : null;
    const to = ended.to ?? (ended.asked ? { id: ask?.by ?? ended.asked, commit: ask?.commit ?? '' } : null);
    if (!to) return settle(carrying, outcome, code);
    tell(`${me.id} hands ${outcome ? `${outcome.handed.length} unrun classes` : 'its whole range'} to ${to.id} on ${short(to.commit)}`);
    return settle({ ...carrying, handedTo: { id: to.id, commit: to.commit } }, outcome, code);
  } finally {
    clearInterval(timer);
    rmSync(halt, { force: true });
  }
}

const valueOf = (argv, name) => {
  const at = argv.indexOf(name);
  return at >= 0 ? (argv[at + 1] ?? null) : null;
};

async function main(argv) {
  const id = valueOf(argv, '--id');
  const commit = valueOf(argv, '--commit');
  const base = valueOf(argv, '--base');
  if (!id || !commit || !base) {
    console.error('usage: node tools/heavy-run.mjs --id <id> --commit <sha> --base <sha> [--branches <a,b>]');
    return 2;
  }
  const home = resolve(process.cwd());
  const lowered = startBackground();
  if (lowered) console.log(`${HEAVY_PREFIX} it could not lower its priority (${lowered}); it runs at the priority it has, its places still background ones`);
  const landed = { id, commit, base, branches: (valueOf(argv, '--branches') ?? '').split(',').filter(Boolean), at: new Date().toISOString() };
  const settle = (run, outcome, code) => settleInto(home, run, outcome, code);
  const admitted = argv.includes('--now') ? { outcome: 'start', batch: null } : await admitLanding(landed, { checkout: home, judge: judgeIn(home), settle });
  if (admitted.outcome !== 'start') return admitted.run.state === 'skipped' ? 0 : 1;
  const batch = admitted.batch;
  const input = batch
    ? { ...landed, id: batch.id, commit: batch.commit, base: batch.base, branches: batch.branches, ranges: rangesOf(batch), carried: batch.carried }
    : landed;
  const settled = await runTier(input, {
    checkout: home,
    prepare: async (at) => {
      const tree = prepareTree(home, at);
      mkdirSync(join(tree, 'local'), { recursive: true });
      // Run beside the event loop, so the tier's beat keeps sounding while the tree is readied.
      const setup = await runLogged(process.execPath, ['tools/worktree-setup.mjs'], tree);
      if (setup.code !== 0) throw new Error(`the heavy tree's setup failed (exit ${setup.code})`);
      return tree;
    },
    check: async ({ tree, base: from, ranges, halt }) => {
      const reach = ranges?.length ? ['--ranges', ranges.map((each) => `${each.base}..${each.commit}`).join(',')] : ['--base', from];
      const ran = await runLogged(process.execPath, ['tools/reached-checks.mjs', '--tier', 'heavy', ...reach, '--halt-file', halt], tree);
      return { code: ran.code, outcome: parseOutcome(ran.text) };
    },
    settle,
  });
  for (const line of settled.lines) console.log(line);
  return settled.run.state === 'green' ? 0 : 1;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`heavy-run: ${error.stack ?? error.message}`);
      process.exitCode = 2;
    },
  );
}
