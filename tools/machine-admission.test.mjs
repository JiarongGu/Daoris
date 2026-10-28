import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  BEAT_MS, INSIDE, STALE_MS, admissionFile, capacityOf, createAdmission, holding, judge, live, lookAt, pruned, readState, waitingSentence,
} from './machine-admission.mjs';

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..');
const TABLE = JSON.parse(readFileSync(join(REPO, 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/machine-admission.json'), 'utf8'));
const NOW = Date.parse(TABLE.now);
mkdirSync(join(REPO, '_fixtures'), { recursive: true });
const SCRATCH = mkdtempSync(join(REPO, '_fixtures', 'admission-test-'));
after(() => rmSync(SCRATCH, { recursive: true, force: true }));

let homes = 0;
const newHome = () => {
  const home = join(SCRATCH, `home-${(homes += 1)}`);
  mkdirSync(home, { recursive: true });
  return home;
};

const at = (seconds) => new Date(NOW + seconds * 1000).toISOString();
/** A table row's entry; a `dead` one has pid 0, which the table's `alive` reads as a process that is gone. */
const entry = (row) => ({ id: row.id ?? 'x', kind: 'session', pid: row.dead ? 0 : 4242, what: 'a test run', since: at(row.since ?? -10), beat: at(-(row.beatAgo ?? 5)) });
const seam = { alive: (pid) => pid !== 0, now: NOW };

describe('the table both twins read (machine-admission.json)', () => {
  for (const row of TABLE.capacity) {
    test(`capacity: ${row.name}`, () => {
      assert.equal(capacityOf(row.cpus, row.slots === null ? {} : { DAORIS_ADMISSION_SLOTS: row.slots }), row.expect);
    });
  }
  for (const row of TABLE.live) {
    test(`live: ${row.name}`, () => {
      assert.equal(live(entry(row), seam), row.expect);
    });
  }
  for (const row of TABLE.judge) {
    test(`judge: ${row.name}`, () => {
      const state = pruned({ holds: row.holds.map(entry), waits: row.waits.map(entry) }, seam);
      const got = judge(state, row.id, row.capacity);
      assert.deepEqual({ admitted: got.admitted, ahead: got.ahead, holds: got.holds.length }, row.expect);
    });
  }
});

/** An admission over a home with a clock held still and a sleep that yields to the event loop only. */
function admission(home, extra = {}) {
  const said = [];
  const made = createAdmission({
    daorisHome: home, kind: 'queue', capacity: 1, say: (line) => said.push(line), pollMs: 5, sayMs: 60_000, beatMs: 1_000_000,
    sleep: (ms) => new Promise((done) => { setTimeout(done, ms); }), env: {}, ...extra,
  });
  return { ...made, said };
}

describe('two holders', () => {
  test('the second waits, saying what for, and starts when the first ends', async () => {
    const home = newHome();
    const first = admission(home);
    const second = admission(home, { kind: 'session' });
    const held = await first.take({ what: 'driver gate' });
    assert.ok(held && !held.inside);
    let started = false;
    const waiting = second.take({ what: 'a dotnet test run', session: 's-1' }).then((hold) => { started = true; return hold; });
    await new Promise((done) => { setTimeout(done, 80); });
    assert.equal(started, false, 'it must not start while the first holds the only place');
    assert.match(second.said[0], /waiting for the machine's heavy-run admission before a dotnet test run/);
    assert.match(second.said[0], /the merge queue's driver gate/);
    assert.match(second.said[0], /1 of 1 heavy runs/);
    assert.match(second.said[0], /never times out/);
    const state = readState(admissionFile(home));
    assert.deepEqual(state.holds.map((each) => each.what), ['driver gate']);
    assert.deepEqual(state.waits.map((each) => [each.what, each.session, each.kind]), [['a dotnet test run', 's-1', 'session']]);
    held.release();
    const next = await waiting;
    assert.ok(next);
    assert.equal(started, true);
    assert.match(second.said.at(-1), /has its place after waiting/);
    assert.deepEqual(readState(admissionFile(home)).holds.map((each) => each.what), ['a dotnet test run']);
    next.release();
    assert.deepEqual(readState(admissionFile(home)), { holds: [], waits: [] });
  });

  test('the capacity is the number of holders that run at once', async () => {
    const home = newHome();
    const made = admission(home, { capacity: 2 });
    const one = await made.take({ what: 'one' });
    const two = await made.take({ what: 'two' });
    assert.ok(one && two);
    let third = false;
    const waiting = made.take({ what: 'three' }).then((hold) => { third = true; return hold; });
    await new Promise((done) => { setTimeout(done, 60); });
    assert.equal(third, false);
    two.release();
    const hold = await waiting;
    assert.equal(lookAt(home, { alive: () => true, now: Date.now(), capacity: 2 }).holds.length, 2);
    one.release();
    hold.release();
  });

  test('a release twice lets go once', async () => {
    const home = newHome();
    const made = admission(home);
    const hold = await made.take({ what: 'one' });
    hold.release();
    const other = await made.take({ what: 'two' });
    hold.release();
    assert.deepEqual(readState(admissionFile(home)).holds.map((each) => each.what), ['two']);
    other.release();
  });

  test('a wait the run no longer needs ends with nothing held and nothing left waiting', async () => {
    const home = newHome();
    const first = admission(home);
    const second = admission(home);
    const held = await first.take({ what: 'one' });
    let stop = false;
    const waiting = second.take({ what: 'two', halted: () => stop });
    await new Promise((done) => { setTimeout(done, 30); });
    stop = true;
    assert.equal(await waiting, null);
    assert.deepEqual(readState(admissionFile(home)).waits, []);
    held.release();
  });
});

describe('a holder that dies releases', () => {
  const holderScript = (home) => `
    import { createAdmission } from ${JSON.stringify(new URL('./machine-admission.mjs', import.meta.url).href)};
    const made = createAdmission({ daorisHome: ${JSON.stringify(home)}, kind: 'session', capacity: 1, env: {}, say: () => {} });
    const hold = await made.take({ what: 'a dotnet test run', session: 's-dies' });
    console.log(hold ? 'held' : 'not held');
    setInterval(() => {}, 1000);
  `;

  test('a process killed outright stops counting at once', async () => {
    const home = newHome();
    const child = spawn(process.execPath, ['--input-type=module', '-e', holderScript(home)], { stdio: ['ignore', 'pipe', 'inherit'] });
    await new Promise((done, fail) => {
      child.stdout.on('data', (data) => (String(data).includes('held') ? done() : fail(new Error(String(data)))));
      child.on('error', fail);
    });
    assert.equal(readState(admissionFile(home)).holds.length, 1);
    const made = admission(home);
    let started = false;
    const waiting = made.take({ what: 'driver gate' }).then((hold) => { started = true; return hold; });
    await new Promise((done) => { setTimeout(done, 80); });
    assert.equal(started, false, 'the live holder keeps the only place');
    const ended = new Promise((done) => { child.on('exit', done); });
    child.kill('SIGKILL');
    await ended;
    const hold = await waiting;
    assert.ok(hold);
    assert.deepEqual(readState(admissionFile(home)).holds.map((each) => each.what), ['driver gate']);
    hold.release();
  });

  test('a record whose process is gone, or that stopped beating, holds nothing', async () => {
    const home = newHome();
    const gone = spawnSync(process.execPath, ['-e', '']).pid ?? 99_999_999;
    const file = admissionFile(home);
    mkdirSync(dirname(file), { recursive: true });
    const record = (id, pid, beatAgoMs) => ({ id, kind: 'session', pid, what: 'a dotnet test run', since: new Date(Date.now() - 600_000).toISOString(), beat: new Date(Date.now() - beatAgoMs).toISOString() });
    writeFileSync(file, JSON.stringify({ schema: 1, holds: [record('dead', gone, 1000), record('silent', process.pid, STALE_MS + 1000)], waits: [] }));
    const made = admission(home);
    const hold = await made.take({ what: 'driver gate' });
    assert.ok(hold);
    assert.deepEqual(readState(file).holds.map((each) => each.what), ['driver gate']);
    hold.release();
  });
});

describe('the edges', () => {
  test('with no Daoris home nothing is shared and nothing waits', async () => {
    const made = createAdmission({ daorisHome: null, say: () => {}, env: {} });
    assert.equal(await made.take({ what: 'driver gate' }), null);
  });

  test('a run started inside a hold takes no second place', async () => {
    const home = newHome();
    const outer = admission(home);
    const held = await outer.take({ what: 'a dotnet test run' });
    const inner = admission(home, { env: { [INSIDE]: held.id } });
    const inside = await inner.take({ what: 'a build it started' });
    assert.equal(inside.inside, true);
    inside.release();
    assert.equal(readState(admissionFile(home)).holds.length, 1, 'releasing the inside one lets nothing go');
    held.release();
    const after = await admission(home, { env: { [INSIDE]: held.id } }).take({ what: 'a run after it ended' });
    assert.equal(after.inside, false, 'a hold that has ended is not one to be inside');
    after.release();
  });

  test('a file that does not read is an empty machine', async () => {
    const home = newHome();
    const file = admissionFile(home);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, '{ not json');
    const hold = await admission(home).take({ what: 'driver gate' });
    assert.ok(hold);
    hold.release();
  });

  test('an entry that does not read as one counts for nothing', () => {
    const file = join(newHome(), 'a.json');
    writeFileSync(file, JSON.stringify({ holds: [{ id: 'a' }, null, 7, { id: 'b', kind: 'queue', pid: 1, what: 'x', since: 'never', beat: 'never' }], waits: 'no' }));
    assert.deepEqual(readState(file), { holds: [], waits: [] });
  });

  test('a lock left by a process that is gone is taken over', () => {
    const home = newHome();
    const file = admissionFile(home);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(`${file}.lock`, JSON.stringify({ pid: 0, since: new Date().toISOString() }));
    let ran = false;
    holding(file, () => { ran = true; }, { alive: (pid) => pid !== 0 });
    assert.equal(ran, true);
  });

  test('the sentence names each hold and the runs before', () => {
    const holds = [{ kind: 'session', session: 's-9', what: 'a dotnet test run', since: at(-125) }, { kind: 'queue', what: 'driver gate', since: at(-30) }];
    assert.equal(
      waitingSentence({ holds, ahead: 2 }, { capacity: 2, now: NOW }),
      "the machine runs 2 of 2 heavy runs: session s-9's a dotnet test run (2m 5s); the merge queue's driver gate (30s); 2 runs asked before this one",
    );
  });

  test('the beat is slower than a poll and far inside the stale bound', () => {
    assert.ok(BEAT_MS * 3 < STALE_MS);
  });
});
