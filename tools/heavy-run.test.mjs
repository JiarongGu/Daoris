import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { readFindings } from './ci-background.mjs';
import { runTier, settleInto } from './heavy-run.mjs';
import { askTiers } from './desktop-publish/tier-evidence.mjs';
import { PassedClasses, splitByReuse, unitId } from './merge/class-reuse.ts';
import { createHalt, wired } from './merge/heavy-halt.ts';
import { HEAVY_STACK, readStack } from './merge/heavy-stack.ts';
import { runPlan } from './merge/reached-run.ts';
import { mainStandsRed } from './merge/reached-classify.ts';
import { appendHeavy, readHeavy } from './merge/heavy-after.ts';

const commit = '0123456789abcdef0123456789abcdef01234567';
const running = { id: 'heavy-0123456-1', state: 'running', commit, base: 'fedcba9', branches: ['feature-a'], at: '2026-11-14T10:00:00.000Z' };

test('a red heavy run files a finding naming the merge commit, which the next merge reads as main\'s red', () => {
  const dir = join('_fixtures', 'heavy-run-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(join(dir, 'local'), { recursive: true });
  try {
    const outcome = { ran: 3, replayed: 0, flaked: [], mainRed: [], red: ['driver-process|Daoris.Desktop.Driver.Tests.BrokenProcessTests'], deferred: [] };
    const { run, lines } = settleInto(dir, running, outcome, 1, '2026-11-14T10:20:00.000Z');
    assert.equal(run.state, 'red');
    assert.match(lines[0], /heavy tier: RED 0123456/);
    const { findings } = readFindings(dir);
    assert.equal(findings.length, 1);
    assert.equal(findings[0].state, 'red');
    assert.equal(findings[0].gate, 'driver-process');
    assert.equal(findings[0].lastSeen.commit, commit);
    assert.equal(findings[0].firstSeen.kind, 'heavy');
    assert.equal(mainStandsRed({ suite: 'driver-process', unit: 'Daoris.Desktop.Driver.Tests.BrokenProcessTests' }, findings, {}), true);
    assert.equal(readHeavy(dir)[0].state, 'red');
    assert.match(readFileSync(join(dir, 'local', 'heavy-runs.jsonl'), 'utf8'), /"state":"red"/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a green heavy run files no finding and a run with no counts is an error that files none either', () => {
  const dir = join('_fixtures', 'heavy-run-green-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(join(dir, 'local'), { recursive: true });
  try {
    settleInto(dir, running, { ran: 3, replayed: 1, flaked: [], mainRed: [], red: [], deferred: [] }, 0, '2026-11-14T10:20:00.000Z');
    assert.equal(readFindings(dir).findings.length, 0);
    assert.equal(readHeavy(dir)[0].state, 'green');
    const lost = settleInto(dir, { ...running, id: 'heavy-0123456-2' }, null, null, '2026-11-14T10:30:00.000Z');
    assert.equal(lost.run.state, 'error');
    assert.equal(readFindings(dir).findings.length, 0);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

const LINE = ['c0', 'c1', 'c2'];
const CHANGES = { c1: ['Proc.cs', 'Git.cs', 'Rehearse.cs'], c2: ['Git.cs'] };
const READS = { 'driver-process|P1': 'Proc.cs', 'driver-git|G1': 'Git.cs', 'driver-git|G2': 'Git.cs', 'driver-rehearsals|R1': 'Rehearse.cs' };
const between = (from, to) => LINE.slice(LINE.indexOf(from) + 1, LINE.indexOf(to) + 1).flatMap((each) => CHANGES[each] ?? []);
const version = (path, commit) => LINE.slice(1, LINE.indexOf(commit) + 1).filter((each) => (CHANGES[each] ?? []).includes(path)).length;
const UNITS = Object.keys(READS).map((id) => {
  const [suite, unit] = id.split('|');
  return { suite, kind: 'dotnet', unit, name: unit, file: `${unit}.cs` };
});

function machine(dir) {
  const git = { started: 0, finished: 0, ended: 0, running: 0, most: 0 };
  const said = [];
  let inGit = () => undefined;
  const gitStarted = new Promise((done) => { inGit = done; });
  const check = async ({ commit, base, halt: file }) => {
    const paths = new Set(between(base, commit));
    const store = new PassedClasses(join(dir, 'store.json'));
    const reached = UNITS.filter((each) => paths.has(READS[unitId(each)]));
    const { run, replayed, keys } = splitByReuse(reached, (each) => `${unitId(each)}@${version(READS[unitId(each)], commit)}`, store);
    const groups = ['driver-process', 'driver-git', 'driver-rehearsals']
      .map((suite) => ({ runner: 'dotnet', suite, settings: 'process.runsettings', units: run.filter((each) => each.suite === suite) }))
      .filter((each) => each.units.length);
    const ends = new Map();
    const halt = createHalt({ file, changedSince: (newer) => between(commit, newer), reaches: (each, path) => READS[unitId(each)] === path, end: (pid) => ends.get(pid)?.(), say: (line) => said.push(line) });
    const stop = halt.watch(2);
    let current = null;
    let pids = commit === 'c1' ? 100 : 200;
    const place = async (group, fn) => {
      current = group;
      try {
        return await fn();
      } finally {
        current = null;
      }
    };
    const exec = (_command, _args, _cwd, started) => {
      const pid = (pids += 1);
      started(pid);
      if (current?.suite !== 'driver-git') return Promise.resolve(0);
      git.started += 1;
      git.running += 1;
      git.most = Math.max(git.most, git.running);
      inGit();
      return new Promise((done) => {
        const finish = (code, how) => {
          git.running -= 1;
          git[how] += 1;
          done(code);
        };
        ends.set(pid, () => finish(1, 'ended'));
        if (commit !== 'c1') setImmediate(() => finish(0, 'finished'));
      });
    };
    const outcome = await runPlan({
      root: dir, plan: { groups, left: {}, reached: reached.length, replayed: {} }, keys, store, replayed: replayed.length, say: (line) => said.push(line),
      mainRedOf: () => false, halt, ...wired(halt, exec, place),
    });
    stop();
    store.save();
    return { code: outcome.red.length ? 1 : 0, outcome };
  };
  return { git, said, gitStarted, check };
}

const landing = (id, commit, base) => ({ id, commit, base, branches: [`feature-${id}`], at: '2026-11-18T10:00:00.000Z' });

function tierSeams(dir, m, pid, over = {}) {
  return {
    checkout: dir, pid, say: (line) => m.said.push(line), alive: () => true, pollMs: 2, beatMs: 60_000, sleep: (ms) => delay(ms),
    prepare: async () => dir, check: m.check, settle: (run, outcome, code) => settleInto(dir, run, outcome, code), ...over,
  };
}

test('two landings close together run one git half: the newer tier ends the older one\'s and runs the union on its commit', async () => {
  const dir = join('_fixtures', 'heavy-run-stack-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(join(dir, 'local'), { recursive: true });
  try {
    const m = machine(dir);
    const older = runTier(landing('heavy-a', 'c1', 'c0'), tierSeams(dir, m, 1));
    await m.gitStarted;
    const newer = runTier(landing('heavy-b', 'c2', 'c1'), tierSeams(dir, m, 2));
    const [a, b] = await Promise.all([older, newer]);
    assert.deepEqual(m.git, { started: 2, finished: 1, ended: 1, running: 0, most: 1 }, 'one git half ran to its end, and never two at once');
    assert.equal(a.run.state, 'superseded');
    assert.deepEqual(a.run.handedTo, { id: 'heavy-b', commit: 'c2' });
    assert.deepEqual(a.run.handed, ['driver-git|G1', 'driver-git|G2', 'driver-rehearsals|R1']);
    assert.equal(a.run.classes.ran, 1, 'its Process class finished before the halt, its pass kept');
    assert.equal(b.run.state, 'green');
    assert.equal(b.run.from, 'c0', 'the newer tier runs from the older base');
    assert.deepEqual(b.run.took, [{ id: 'heavy-a', commit: 'c1', branches: ['feature-heavy-a'] }]);
    assert.deepEqual([b.run.classes.ran, b.run.classes.replayed], [3, 1], 'the union ran once: its own git classes and the handed rehearsal; the Process pass replayed');
    assert.deepEqual(readStack(join(dir, HEAVY_STACK)), { holder: null, waiting: null, handed: null });
    const log = m.said.join('\n');
    assert.match(log, /heavy tier: heavy-b on c2 supersedes heavy-a on c1/);
    assert.match(log, /heavy tier: heavy-a hands 3 unrun classes to heavy-b on c2/);
    assert.match(log, /heavy tier: heavy-b carries heavy-a \(c1, 3 unrun\): it runs from c0 on c2/);
    assert.deepEqual(readHeavy(dir).map((run) => run.state), ['superseded', 'green']);
    assert.equal(askTiers(dir, { home: dir,commit: 'c1' }).code, 1, 'a superseded commit is not staged as passed');
    assert.equal(askTiers(dir, { home: dir,commit: 'c2' }).code, 0);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a tier superseded before it ran a class hands its whole range, and a wait past its bound is said', async () => {
  const dir = join('_fixtures', 'heavy-run-stack-early-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(join(dir, 'local'), { recursive: true });
  try {
    const m = machine(dir);
    let ready = () => undefined;
    const held = new Promise((done) => { ready = done; });
    let clock = Date.parse('2026-11-18T10:00:00.000Z');
    const look = 30_000;
    const older = runTier(landing('heavy-a', 'c1', 'c0'), tierSeams(dir, m, 1, { prepare: async () => { await held; return dir; } }));
    const newer = runTier(landing('heavy-b', 'c2', 'c1'), tierSeams(dir, m, 2, {
      now: () => clock, staleMs: Infinity, boundMs: 2 * look,
      sleep: async () => { clock += look; if (clock >= Date.parse('2026-11-18T10:02:00.000Z')) ready(); await delay(1); },
    }));
    const [a, b] = await Promise.all([older, newer]);
    assert.equal(a.run.state, 'superseded');
    assert.equal(a.run.handed, undefined);
    assert.equal(b.run.from, 'c0');
    assert.deepEqual([b.run.classes.ran, b.run.classes.replayed], [4, 0], 'every class the two landings reach ran once, on the newer commit');
    assert.deepEqual(m.git, { started: 1, finished: 1, ended: 0, running: 0, most: 1 });
    assert.match(m.said.join('\n'), /heavy tier: heavy-b has waited 1m 0s for heavy-a \(pid 1, on c1/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a superseded run\'s commit is not staged as passed, nor any state the stage does not know', () => {
  const dir = join('_fixtures', 'heavy-run-evidence-test');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(join(dir, 'local'), { recursive: true });
  try {
    appendHeavy(dir, { ...running, state: 'superseded', handedTo: { id: 'heavy-b', commit: 'fedcba9876543210' }, ended: '2026-11-14T10:05:00.000Z' });
    assert.match(askTiers(dir, { home: dir,commit }).out, /handed its unrun classes to heavy-b on fedcba9/);
    appendHeavy(dir, { ...running, id: 'heavy-0123456-2', state: 'unknown', at: '2026-11-14T11:00:00.000Z' });
    assert.equal(askTiers(dir, { home: dir,commit }).code, 1);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
