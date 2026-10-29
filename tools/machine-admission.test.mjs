import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  BEAT_MS, INSIDE, STALE_MS, admissionFile, admissionLines, capacityOf, createAdmission, judge, live, lookAt, pruned, readState, readStrict, waitingSentence,
  wholeDoubts,
} from './machine-admission.mjs';
import { writeAtomic } from './fsx.mjs';

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

  test('the sentence names each hold and the runs before', () => {
    const holds = [{ kind: 'session', session: 's-9', what: 'a dotnet test run', since: at(-125) }, { kind: 'queue', what: 'driver gate', since: at(-30) }];
    assert.equal(
      waitingSentence({ holds, ahead: 2 }, { capacity: 2, now: NOW }),
      "the machine runs 2 of 2 heavy runs: session s-9's a dotnet test run (2m 5s); the merge queue's driver gate (30s); 2 runs asked before this one",
    );
  });

  test('the queue status says the places held, whose and for how long, and how many ask', () => {
    const holds = [{ kind: 'queue', what: 'driver gate', since: at(-600) }, { kind: 'session', session: 's-3', what: 'dotnet test', since: at(-30) }];
    const waits = [{ kind: 'session', what: 'vitest', since: at(-5) }];
    assert.deepEqual(admissionLines({ capacity: 3, holds, waits }, NOW), [
      'machine heavy-run admission (the Daoris home): 2 of 3 places held, 1 asking',
      `  holds: the merge queue's driver gate, since ${at(-600)} (10 min)`,
      `  holds: session s-3's dotnet test, since ${at(-30)} (1 min)`,
      `  asks: a driven run's vitest, since ${at(-5)}`,
    ]);
  });

  test('the beat is slower than a poll and far inside the stale bound', () => {
    assert.ok(BEAT_MS * 3 < STALE_MS);
  });
});

/**
 * INTEGRATE1b (design §4.5, C7, F34): the confirm run takes the whole admission as the capacity's worth of ordinary entries in one
 * write, beats each, lets them go in one write, and reads the record at its start, at each beat and at its end; anything there that
 * does not show the machine held for it alone makes it inconclusive.
 */
describe('the whole admission, for a confirm run', () => {
  /** Today's schema: an entry carries these and the optional `session`, `repository` and `tree`, nothing else. */
  const SCHEMA = ['beat', 'id', 'kind', 'pid', 'since', 'what'];
  /** A foreign process the seam calls alive. */
  const OTHER = 4242;
  const alive = (pid) => pid === process.pid || pid === OTHER;
  const foreign = (id, what = 'a dotnet test run') => ({ id, kind: 'session', pid: OTHER, what, since: new Date().toISOString(), beat: new Date().toISOString() });
  const write = (home, state) => {
    const file = admissionFile(home);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, JSON.stringify({ schema: 1, holds: [], waits: [], ...state }));
  };
  /** An admission whose writes are kept, as the text each wrote, and can be made to fail. */
  function whole(home, extra = {}) {
    const writes = [];
    const failing = { write: false, read: false };
    const made = admission(home, {
      alive, capacity: 3,
      writeText: (file, text) => {
        if (failing.write) throw new Error('the disk refused the write');
        writes.push(JSON.parse(text));
        writeAtomic(file, text);
      },
      readText: (file) => {
        if (failing.read) throw new Error('the disk refused the read');
        return readFileSync(file, 'utf8');
      },
      ...extra,
    });
    return { ...made, writes, failing };
  }
  const idsIn = (state, ids) => [...state.holds, ...state.waits].filter((each) => ids.includes(each.id)).length;

  test('the take of N: the capacity\'s worth of entries of today\'s schema in one write, each held, then let go in one write', async () => {
    const home = newHome();
    const made = whole(home);
    const hold = await made.takeWhole({ what: 'confirm run of driver' });
    assert.equal(hold.held, true, hold.why);
    assert.equal(hold.ids.length, 3);
    assert.equal(new Set(hold.ids).size, 3, 'each its own id');
    const state = readState(admissionFile(home));
    assert.deepEqual(state.holds.map((each) => each.id), hold.ids);
    for (const entry of state.holds) {
      assert.deepEqual(Object.keys(entry).sort(), SCHEMA, 'no field is added to an entry');
      assert.equal(entry.kind, 'queue');
      assert.equal(entry.pid, process.pid, 'under the same process');
    }
    assert.equal(new Set(state.holds.map((each) => each.since)).size, 1, 'they asked together');
    assert.deepEqual(state.holds.map((each) => each.what), ['confirm run of driver (place 1 of 3)', 'confirm run of driver (place 2 of 3)', 'confirm run of driver (place 3 of 3)']);
    const first = made.writes.findIndex((written) => idsIn(written, hold.ids) > 0);
    assert.equal(idsIn(made.writes[first], hold.ids), 3, 'the first write that holds any of them holds all of them');
    assert.deepEqual(made.writes[first].waits.map((each) => each.id), hold.ids, 'they stand together in first-come order');
    const evidence = hold.end();
    assert.equal(made.writes.at(-1).holds.length + made.writes.at(-1).waits.length, 0);
    assert.equal(idsIn(made.writes.at(-2), hold.ids), 3, 'one write lets all of them go');
    assert.deepEqual(readState(admissionFile(home)), { holds: [], waits: [] });
    assert.equal(evidence.conclusive, true, JSON.stringify(evidence.doubts));
    assert.deepEqual(evidence.doubts, []);
    assert.deepEqual(evidence.ids, hold.ids);
    assert.equal(evidence.capacity, 3);
    assert.ok(evidence.asked && evidence.started && evidence.ended);
    assert.equal(typeof evidence.waitedMs, 'number');
    assert.equal(hold.end(), evidence, 'an end twice is the one end');
  });

  test('each entry is beaten in one write', async () => {
    const home = newHome();
    let clock = Date.now();
    const made = whole(home, { now: () => clock });
    const hold = await made.takeWhole({ what: 'confirm run of driver' });
    clock += 30_000;
    const writes = made.writes.length;
    hold.beat();
    assert.equal(made.writes.length, writes + 1);
    const beaten = new Date(clock).toISOString();
    assert.deepEqual(readState(admissionFile(home)).holds.map((each) => each.beat), [beaten, beaten, beaten]);
    const evidence = hold.end();
    assert.equal(evidence.beats, 1);
    assert.equal(evidence.conclusive, true, JSON.stringify(evidence.doubts));
  });

  test('the capacity is capacityOf\'s, the count the driver reads', async () => {
    const home = newHome();
    const made = whole(home, { capacity: null, env: { DAORIS_ADMISSION_SLOTS: '4' } });
    const hold = await made.takeWhole({ what: 'confirm run' });
    assert.equal(hold.ids.length, 4);
    assert.equal(hold.end().capacity, 4);
  });

  test('it waits for the runs it waits for, never doubting them, and no later ask falls between its entries', async () => {
    const home = newHome();
    write(home, { holds: [foreign('a-session-run')] });
    const made = whole(home, { capacity: 2 });
    let started = false;
    const taking = made.takeWhole({ what: 'confirm run of driver' }).then((hold) => { started = true; return hold; });
    await new Promise((done) => { setTimeout(done, 80); });
    assert.equal(started, false, 'it starts once every entry holds');
    const waiting = readState(admissionFile(home));
    assert.equal(waiting.holds.length, 2, 'its first place is held, beside the run it waits for');
    assert.equal(waiting.waits.length, 1);
    assert.match(made.said.join('\n'), /confirm run of driver/);
    // A session asks after it, and the run it waited for ends.
    write(home, { holds: waiting.holds.filter((each) => each.pid !== OTHER), waits: [...waiting.waits, foreign('a-later-ask')] });
    const hold = await taking;
    assert.equal(hold.held, true, hold.why);
    const state = readState(admissionFile(home));
    assert.deepEqual(state.holds.map((each) => each.id), hold.ids);
    assert.deepEqual(state.waits.map((each) => each.id), ['a-later-ask'], 'the later ask waits behind every one of its places');
    const evidence = hold.end();
    assert.equal(evidence.conclusive, true, JSON.stringify(evidence.doubts));
    assert.ok(evidence.waitedMs >= 0);
    assert.deepEqual(readState(admissionFile(home)).waits.map((each) => each.id), ['a-later-ask'], 'its end lets go its own alone');
  });

  test('inconclusive: another live hold not its own at its start', async () => {
    const home = newHome();
    let inject = false;
    const made = whole(home, {
      writeText: (file, text) => {
        writeAtomic(file, text);
        const state = JSON.parse(text);
        if (state.holds.length === 3 && !state.waits.length) inject = true;
      },
      // A writer with a larger capacity takes a place as the confirm run's last entry is admitted.
      readText: (file) => {
        const state = JSON.parse(readFileSync(file, 'utf8'));
        return JSON.stringify(inject ? { ...state, holds: [...state.holds, foreign('a-larger-capacity')] } : state);
      },
    });
    const hold = await made.takeWhole({ what: 'confirm run' });
    const evidence = hold.end();
    assert.equal(evidence.conclusive, false);
    assert.equal(evidence.doubts[0].at, 'start');
    assert.match(evidence.doubts[0].why, /another live hold stands beside it: a driven run's a dotnet test run/);
  });

  test('inconclusive: another live hold not its own at a beat, and at its end', async () => {
    for (const at of ['beat', 'end']) {
      const home = newHome();
      const made = whole(home);
      const hold = await made.takeWhole({ what: 'confirm run' });
      const state = readState(admissionFile(home));
      write(home, { holds: [...state.holds, foreign('a-run-that-took-a-place')], waits: state.waits });
      if (at === 'beat') hold.beat();
      const evidence = hold.end();
      assert.equal(evidence.conclusive, false, at);
      assert.equal(evidence.doubts[0].at, at);
      assert.match(evidence.doubts[0].why, /another live hold stands beside it/);
      assert.deepEqual(readState(admissionFile(home)).holds.map((each) => each.id), ['a-run-that-took-a-place'], 'its own are let go, the other is not');
    }
  });

  test('inconclusive: a read that fails, at a beat and at its end, and a record that does not read', async () => {
    const home = newHome();
    const made = whole(home);
    const hold = await made.takeWhole({ what: 'confirm run' });
    made.failing.read = true;
    hold.beat();
    made.failing.read = false;
    writeFileSync(admissionFile(home), '{ not json');
    const evidence = hold.end();
    assert.equal(evidence.conclusive, false);
    assert.deepEqual(evidence.doubts.map((each) => each.at), ['beat', 'end']);
    assert.match(evidence.doubts[0].why, /could not be read or written .*the disk refused the read/);
    assert.match(evidence.doubts[1].why, /could not be read or written/);
  });

  test('inconclusive: a write that fails at a beat and at its end, its places still let go', async () => {
    const home = newHome();
    const made = whole(home);
    const hold = await made.takeWhole({ what: 'confirm run' });
    made.failing.write = true;
    hold.beat();
    const evidence = hold.end();
    assert.equal(evidence.conclusive, false);
    assert.deepEqual(evidence.doubts.map((each) => each.at), ['beat', 'end']);
    assert.match(evidence.doubts[0].why, /the disk refused the write/);
    assert.deepEqual(readState(admissionFile(home)), { holds: [], waits: [] }, 'a release that could not be written strictly is made as any writer makes it');
  });

  test('inconclusive at the take, nothing held: a record that cannot be written, one that keeps a hold it cannot read, no home', async () => {
    const unwritten = whole(newHome());
    unwritten.failing.write = true;
    const refused = await unwritten.takeWhole({ what: 'confirm run' });
    assert.equal(refused.held, false);
    assert.match(refused.why, /could not be written.*the disk refused the write/);
    assert.equal(refused.evidence.conclusive, false);
    assert.equal(refused.evidence.doubts[0].at, 'take');

    const home = newHome();
    write(home, { holds: [{ id: 'a newer writer', kind: 'robot', pid: OTHER }] });
    const unread = await whole(home).takeWhole({ what: 'confirm run' });
    assert.equal(unread.held, false);
    assert.match(unread.why, /1 hold that does not read as an entry/);
    assert.equal(JSON.parse(readFileSync(admissionFile(home), 'utf8')).holds.length, 1, 'nothing written over it');

    const none = await createAdmission({ daorisHome: null, say: () => {}, env: {} }).takeWhole({ what: 'confirm run' });
    assert.equal(none.held, false);
    assert.match(none.why, /no Daoris home/);
    assert.equal(none.evidence.conclusive, false);
  });

  test('its own process holding a place already: refused at once and nothing written, since the take would wait on itself', async () => {
    const home = newHome();
    const own = await admission(home, { capacity: 3 }).take({ what: 'driver gate' });
    const made = whole(home);
    const hold = await made.takeWhole({ what: 'confirm run' });
    assert.equal(hold.held, false);
    assert.match(hold.why, /its own process holds or asks a place already \(driver gate\)/);
    assert.deepEqual(readState(admissionFile(home)).holds.map((each) => each.what), ['driver gate']);
    own.release();
  });

  test('a drop while it waits lets every place go', async () => {
    const home = newHome();
    write(home, { holds: [foreign('a-session-run')] });
    let stop = false;
    const taking = whole(home).takeWhole({ what: 'confirm run', halted: () => stop });
    await new Promise((done) => { setTimeout(done, 50); });
    stop = true;
    const hold = await taking;
    assert.equal(hold.held, false);
    assert.equal(hold.dropped, true);
    assert.deepEqual(readState(admissionFile(home)).holds.map((each) => each.id), ['a-session-run']);
    assert.deepEqual(readState(admissionFile(home)).waits, []);
  });

  test('the doubts of one reading, over the record pruned', () => {
    const ids = ['m-01', 'm-02'];
    const mine = ids.map((id) => ({ ...foreign(id), kind: 'queue', pid: process.pid }));
    assert.deepEqual(wholeDoubts({ holds: mine, waits: [foreign('asks')], unread: 0 }, ids), [], 'an ask behind it is no doubt');
    assert.match(wholeDoubts({ holds: mine.slice(0, 1), waits: [], unread: 0 }, ids)[0], /1 of its 2 places is no longer held \(m-02\)/);
    assert.match(wholeDoubts({ holds: [...mine, foreign('x', 'vitest')], waits: [], unread: 0 }, ids)[0], /another live hold stands beside it: a driven run's vitest/);
    assert.match(wholeDoubts({ holds: mine, waits: [], unread: 2 }, ids)[0], /2 holds that do not read as an entry/);
  });

  test('a strict read: a missing record only where allowed, and one that does not read throws', () => {
    const home = newHome();
    const file = admissionFile(home);
    assert.deepEqual(readStrict(file, { missing: true }), { holds: [], waits: [], unread: 0 });
    assert.throws(() => readStrict(file), /ENOENT/);
    write(home, { holds: [{ id: 'x' }], waits: [] });
    assert.deepEqual(readStrict(file), { holds: [], waits: [], unread: 1 });
    writeFileSync(file, '{ "holds": "no" }');
    assert.throws(() => readStrict(file), /holds is not a list/);
  });
});
