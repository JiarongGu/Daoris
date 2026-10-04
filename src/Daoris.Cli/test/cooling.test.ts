import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { COOLING_FILE, coolingLine, coolingOf, coolingWhen, coolingWhy, endCooling, readCooling } from '../src/cooling.ts';
import type { CoolingEntry } from '../src/cooling.ts';
import {
  TOOLCHAINS, accountLines, addKeyAccount, commandHarness, profileHome, removeProfile, signInNew, signInTo,
} from '../src/toolchain.ts';
import type { HarnessReport, HarnessSettings } from '../src/toolchain.ts';
import { driverRows as csharpRows } from './_csharp.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * `cooling.json` read and ended (TOOL4e, D125 §2.3, §2.4): the driver writes an account's cool-off when a limit is read,
 * and both twins read it and end one early — the screen's *Try now* and `daoris agent profile ready`. The CLI's half of
 * a TWIN with the driver's `AccountCooling.cs`: 🔴 `CoolingTwinTests.cs` holds the same tables, row for row and in the
 * same order, and the last test here holds each to this one, cell for cell, as it does the words from
 * `AccountCoolingTests.cs`.
 *
 * An entry is said as JSON: whose, until when and seen when (UTC, to the second), and what said so; `null` is no account
 * cooling. An `after` is the file's JSON once the end is done, or `unchanged` where nothing was written.
 */

type Cell = string | null;

const moment = (text: string) => new Date(text);

function said(entry: CoolingEntry): Record<string, unknown> {
  const stamp = (at: Date) => `${at.toISOString().slice(0, 19)}Z`;
  return {
    agent: entry.agent, account: entry.account, until: stamp(entry.until), stated: entry.stated, window: entry.window,
    seen: stamp(entry.seen), session: entry.session, assumedZone: entry.assumedZone, notBelieved: entry.notBelieved,
  };
}

// ——— Reading (§2.3): missing or unreadable is none cooling; a passed `until` is ready; names compare without case.

const READ_ROWS: [name: string, file: Cell, now: string, agent: string, account: Cell, entry: Cell][] = [
  ['missing is no account cooling', null, '2026-10-01T08:15:00Z', 'claude-code', 'account-1', null],
  ['not JSON is none', 'not json', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', null],
  ['JSON that is not an object is none', '[1, 2]', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', null],
  ['an entry with no until is none', '{"claude-code":{"account-1":{"stated":true}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', null],
  ['an until that is not ISO 8601 is none', '{"claude-code":{"account-1":{"until":"Oct 3"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', null],
  ['a date that does not exist is none', '{"claude-code":{"account-1":{"until":"2026-11-31T10:17:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', null],
  ['an agent that is not an object is none', '{"claude-code":[1]}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', null],
  ['a named account cooling, read whole', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"window":"weekly","seen":"2026-10-01T08:15:00Z","session":"3f9c2a71"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', '{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":true,"window":"weekly","seen":"2026-10-01T08:15:00Z","session":"3f9c2a71","assumedZone":false,"notBelieved":false}'],
  ['the tool\'s own home is the empty name', '{"claude-code":{"":{"until":"2026-10-03T10:17:00Z","stated":false,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', null, '{"agent":"claude-code","account":null,"until":"2026-10-03T10:17:00Z","stated":false,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":false,"notBelieved":false}'],
  ['an until at the moment asked is ready', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-03T10:17:00Z', 'claude-code', 'account-1', null],
  ['names compare without case, and are said as written', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'Claude-Code', 'ACCOUNT-1', '{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":true,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":false,"notBelieved":false}'],
  ['another account is not this one', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-2', null],
  ['the own home is not a named account', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', null, null],
  ['a seen that is missing reads as the until', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', '{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":false,"window":null,"seen":"2026-10-03T10:17:00Z","session":null,"assumedZone":false,"notBelieved":false}'],
  ['a flag that is not true is false, and a word that is not text is none', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":"yes","window":7,"seen":"2026-10-01T08:15:00Z","session":"","assumedZone":1}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', '{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":false,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":false,"notBelieved":false}'],
  ['a moment with an offset or a fraction is read in UTC, to the second', '{"claude-code":{"account-1":{"until":"2026-10-03T16:02:00+05:45","stated":true,"seen":"2026-10-01T08:15:00.5Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', '{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":true,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":false,"notBelieved":false}'],
  ['the zone assumed and the date not believed are read where true', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":false,"seen":"2026-10-01T08:15:00Z","assumedZone":true,"notBelieved":true}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', '{"agent":"claude-code","account":"account-1","until":"2026-10-03T10:17:00Z","stated":false,"window":null,"seen":"2026-10-01T08:15:00Z","session":null,"assumedZone":true,"notBelieved":true}'],
];

test('an entry reads as the driver reads it (the twin\'s table)', () => {
  for (const [index, [name, file, now, agent, account, entry]] of READ_ROWS.entries()) {
    const fx = makeFixture(`cooling-read-${index}`);
    if (file !== null) writeFileSync(join(fx.root, COOLING_FILE), file, 'utf8');

    const read = coolingOf(fx.root, agent, account, moment(now));

    assert.deepEqual(read === null ? null : said(read), entry === null ? null : JSON.parse(entry), name);
    fx.cleanup();
  }
});

test('every account cooling, by agent and then account, and none that has passed', () => {
  const fx = makeFixture('cooling-all');
  writeFileSync(join(fx.root, COOLING_FILE), JSON.stringify({
    codex: { '': { until: '2026-10-03T10:17:00Z' } },
    'claude-code': { 'account-2': { until: '2026-10-03T10:17:00Z' }, 'account-1': { until: '2026-10-01T08:00:00Z' }, '': { until: '2026-10-02T00:00:00Z' } },
  }), 'utf8');

  const all = readCooling(fx.root, moment('2026-10-01T08:15:00Z'));

  assert.deepEqual(all.map((entry) => [entry.agent, entry.account]), [['claude-code', null], ['claude-code', 'account-2'], ['codex', null]]);
  fx.cleanup();
});

// ——— Ending one early (§2.3, §6): that account and no other; a passed or unreadable entry goes with the write; what
// has no field is kept; nothing is written where nothing was cooling under that name.

const END_ROWS: [name: string, file: Cell, now: string, agent: string, account: Cell, ended: boolean, after: string][] = [
  ['ending a cooling account takes it and no other', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"},"account-2":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', true, '{"claude-code":{"account-2":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}'],
  ['an account with no entry ends nothing and nothing is written', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-3', false, 'unchanged'],
  ['an agent with no entries ends nothing', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'codex', null, false, 'unchanged'],
  ['the last account of an agent takes the agent with it', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', true, '{}'],
  ['an entry whose until has passed was not cooling, and goes', '{"claude-code":{"account-1":{"until":"2026-10-01T08:00:00Z","stated":true,"seen":"2026-09-30T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', false, '{}'],
  ['a passed or unreadable entry elsewhere goes with the write', '{"codex":{"":{"until":"2026-10-01T08:00:00Z"},"account-2":{"until":"Oct 3"}},"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"},"account-2":{"until":"2026-10-03T10:17:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', true, '{"claude-code":{"account-2":{"until":"2026-10-03T10:17:00Z"}}}'],
  ['what has no field is kept', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"},"account-2":{"until":"2026-10-03T10:17:00Z","later":"kept"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', true, '{"claude-code":{"account-2":{"until":"2026-10-03T10:17:00Z","later":"kept"}}}'],
  ['names compare without case', '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"},"account-2":{"until":"2026-10-03T10:17:00Z"}}}', '2026-10-01T08:15:00Z', 'Claude-Code', 'ACCOUNT-1', true, '{"claude-code":{"account-2":{"until":"2026-10-03T10:17:00Z"}}}'],
  ['the own home is the empty name', '{"claude-code":{"":{"until":"2026-10-03T10:17:00Z"},"account-1":{"until":"2026-10-03T10:17:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', null, true, '{"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"}}}'],
  ['an agent that is not an object is kept as written', '{"codex":[1],"claude-code":{"account-1":{"until":"2026-10-03T10:17:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', true, '{"codex":[1]}'],
  ['a missing file ends nothing and makes none', null, '2026-10-01T08:15:00Z', 'claude-code', 'account-1', false, 'unchanged'],
  ['a file that does not read ends nothing and is kept', 'not json', '2026-10-01T08:15:00Z', 'claude-code', 'account-1', false, 'unchanged'],
];

test('an entry ends as the driver ends it (the twin\'s table)', () => {
  for (const [index, [name, file, now, agent, account, ended, after]] of END_ROWS.entries()) {
    const fx = makeFixture(`cooling-end-${index}`);
    const path = join(fx.root, COOLING_FILE);
    if (file !== null) writeFileSync(path, file, 'utf8');

    assert.equal(endCooling(fx.root, agent, account, moment(now)), ended, `${name}: ended`);

    if (after === 'unchanged') {
      assert.equal(existsSync(path) ? readFileSync(path, 'utf8') : null, file, `${name}: written`);
    } else {
      const written = readFileSync(path, 'utf8');
      assert.deepEqual(JSON.parse(written), JSON.parse(after), name);
      assert.ok(written.endsWith('}\n') && !written.includes('\r'), `${name}: LF and a final newline`);
    }
    fx.cleanup();
  }
});

// ——— What a cool-off says (§2.4), in the driver's words.

const WHEN_ROWS: [moment: string, zone: string, said: string][] = [
  ['2026-10-03T10:17:00Z', 'Asia/Kathmandu', 'Oct 3, 16:02 (Asia/Kathmandu)'],
  ['2026-10-02T18:15:00Z', 'Asia/Kathmandu', 'Oct 3, 00:00 (Asia/Kathmandu)'],
  ['2026-01-01T23:30:00Z', 'America/New_York', 'Jan 1, 18:30 (America/New_York)'],
  ['2026-09-29T21:52:00Z', 'Europe/London', 'Sep 29, 22:52 (Europe/London)'],
];

test('a moment is said in the machine\'s zone with the zone named, as the driver says it', () => {
  for (const [at, zone, words] of WHEN_ROWS) assert.equal(coolingWhen(moment(at), zone), words, `${at} in ${zone}`);
});

const WHY_ROWS: [stated: boolean, assumed: boolean, notBelieved: boolean, why: string][] = [
  [true, false, false, 'as the agent said'],
  [false, false, false, 'Daoris\'s default: the agent named no time'],
  [false, false, true, 'Daoris\'s default: the agent named a date more than 8 days off'],
  [true, true, false, 'as the agent said, in this machine\'s zone'],
];

test('why a cool-off lasts as long as it does is said in the driver\'s words', () => {
  for (const [stated, assumedZone, notBelieved, why] of WHY_ROWS) {
    assert.equal(coolingWhy({ stated, assumedZone, notBelieved }), why);
  }
});

test('a cooling account\'s line says until when, how long that is, why, and how to try it now', () => {
  const entry: CoolingEntry = {
    agent: 'claude-code', account: 'account-1', until: moment('2026-10-03T10:17:00Z'), stated: true, window: 'weekly',
    seen: moment('2026-10-01T08:15:00Z'), session: '3f9c2a71', assumedZone: false, notBelieved: false,
  };

  assert.equal(
    coolingLine(entry, moment('2026-10-01T08:15:00Z'), 'Asia/Kathmandu'),
    'cooling until Oct 3, 16:02 (Asia/Kathmandu), in 2 d 2 h — as the agent said; '
      + '`daoris agent profile ready claude-code account-1` tries it now');
  assert.match(coolingLine(entry, moment('2026-10-03T07:07:00Z'), 'UTC'), /, in 3 h 10 min —/);
  assert.match(coolingLine(entry, moment('2026-10-03T10:16:30Z'), 'UTC'), /, in 1 min —/);
  assert.match(coolingLine({ ...entry, account: null }, moment('2026-10-01T08:15:00Z'), 'UTC'),
    /`daoris agent profile ready claude-code --own` tries it now/);
});

// ——— The verb (§6): `daoris agent profile ready <agent> <profile>|--own`, the terminal's *Try now*.

function run(argv: string[], path: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HARNESS_CONFIG;
  process.env.DAORIS_HARNESS_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = commandHarness({ root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd() }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HARNESS_CONFIG;
    else process.env.DAORIS_HARNESS_CONFIG = saved;
  }
}

/** A cool-off a day ahead of now, written as the driver writes one. */
function cooling(fx: { root: string }, held: Record<string, Record<string, unknown>>): void {
  writeFileSync(join(fx.root, COOLING_FILE), `${JSON.stringify(held, null, 2)}\n`, 'utf8');
}

const AHEAD = () => new Date(Date.now() + 86_400_000).toISOString().replace(/\.\d+Z$/, 'Z');

test('`profile ready` ends an account\'s cool-off, and says so', () => {
  const fx = makeFixture('cooling-ready');
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-1'), { recursive: true });
  cooling(fx, { 'claude-code': { 'account-1': { until: AHEAD(), stated: true }, 'account-2': { until: AHEAD(), stated: true } } });

  const ready = run(['profile', 'ready', 'claude-code', 'account-1'], join(fx.root, 'harnesses.json'));

  assert.equal(ready.code, 0);
  assert.match(ready.out, /the `claude-code` account `account-1` is offered again: no start waits on its limit now/);
  assert.equal(coolingOf(fx.root, 'claude-code', 'account-1', new Date()), null);
  assert.notEqual(coolingOf(fx.root, 'claude-code', 'account-2', new Date()), null);
  fx.cleanup();
});

test('`profile ready` on an account that is not cooling says so and changes nothing', () => {
  const fx = makeFixture('cooling-ready-not');
  cooling(fx, { 'claude-code': { 'account-2': { until: AHEAD(), stated: true } } });
  const before = readFileSync(join(fx.root, COOLING_FILE), 'utf8');

  const ready = run(['profile', 'ready', 'claude-code', 'account-1'], join(fx.root, 'harnesses.json'));

  assert.equal(ready.code, 0);
  assert.match(ready.out, /the `claude-code` account `account-1` is not cooling, so nothing changed — cooling now: account-2/);
  assert.equal(readFileSync(join(fx.root, COOLING_FILE), 'utf8'), before);
  fx.cleanup();
});

test('`profile ready --own` ends the tool\'s own sign-in\'s cool-off, and a door ends its owner\'s', () => {
  const fx = makeFixture('cooling-ready-own');
  cooling(fx, { 'claude-code': { '': { until: AHEAD(), stated: false }, 'account-1': { until: AHEAD(), stated: true } } });

  const own = run(['profile', 'ready', 'claude-code', '--own'], join(fx.root, 'harnesses.json'));
  const door = run(['profile', 'ready', 'claude-code-acp', 'account-1'], join(fx.root, 'harnesses.json'));

  assert.match(own.out, /`claude-code`'s own sign-in is offered again/);
  assert.match(door.out, /runs as `claude-code`/);
  assert.deepEqual(readCooling(fx.root, new Date()), []);
  fx.cleanup();
});

test('`profile ready` needs an account or --own, never both', () => {
  const fx = makeFixture('cooling-ready-args');

  assert.match(captureError(() => run(['profile', 'ready', 'claude-code'], join(fx.root, 'harnesses.json'))).message,
    /needs <agent> <profile>\|--own/);
  assert.match(captureError(() => run(['profile', 'ready', 'claude-code', 'account-1', '--own'], join(fx.root, 'harnesses.json'))).message,
    /`--own` names no account/);
  fx.cleanup();
});

// ——— What else ends one (§2.3): a sign-in or a key into the account, as the driver's `LoginAsync` and `HarnessKeys.Add`
// end one; and an account removed takes its cool-off with it, as the driver's `RemoveProfile` does.

test('a finished sign-in into an account ends its cool-off; one that did not finish ends nothing', () => {
  const fx = makeFixture('cooling-sign-in');
  const script = join(fx.root, 'signed.mjs');
  writeFileSync(script, "console.log(JSON.stringify({ loggedIn: true, email: 'someone@example.invalid' }));\n", 'utf8');
  const toolchain = { ...TOOLCHAINS['claude-code']!, binary: [process.execPath, script] };
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-1'), { recursive: true });
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-2'), { recursive: true });
  cooling(fx, { 'claude-code': { 'account-1': { until: AHEAD(), stated: true }, 'account-2': { until: AHEAD(), stated: true } } });

  assert.equal(signInTo('claude-code', toolchain, fx.root, 'account-2', () => 2, () => {}), 2);
  assert.notEqual(coolingOf(fx.root, 'claude-code', 'account-2', new Date()), null);
  assert.equal(signInTo('claude-code', toolchain, fx.root, 'account-1', () => 0, () => {}), 0);
  assert.equal(coolingOf(fx.root, 'claude-code', 'account-1', new Date()), null);
  assert.notEqual(coolingOf(fx.root, 'claude-code', 'account-2', new Date()), null);
  // A new account takes a fresh id (ACCT2), so it carries no cool-off of another's, and ends none.
  assert.equal(signInNew('claude-code', toolchain, fx.root, () => 0, () => {}), 0);
  assert.notEqual(coolingOf(fx.root, 'claude-code', 'account-2', new Date()), null);
  fx.cleanup();
});

test('a key made into an account takes an id no cool-off names, and leaves the others\'', () => {
  const fx = makeFixture('cooling-key');
  cooling(fx, { 'claude-code': { 'account-1': { until: AHEAD(), stated: true } } });

  const account = addKeyAccount(fx.root, 'claude-code', 'sk-test-0000-wxyz', () => {});
  assert.notEqual(account, 'account-1');
  assert.equal(coolingOf(fx.root, 'claude-code', account, new Date()), null);
  assert.notEqual(coolingOf(fx.root, 'claude-code', 'account-1', new Date()), null);
  fx.cleanup();
});

test('an account removed takes its cool-off with it, whether its directory was there or not', () => {
  const fx = makeFixture('cooling-remove');
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-1'), { recursive: true });
  cooling(fx, {
    'claude-code': { 'account-1': { until: AHEAD() }, 'account-2': { until: AHEAD() }, 'account-3': { until: AHEAD() } },
  });

  assert.equal(removeProfile(fx.root, 'claude-code', 'account-1'), true);
  assert.equal(removeProfile(fx.root, 'claude-code', 'account-3'), false);
  assert.deepEqual(readCooling(fx.root, new Date()).map((entry) => entry.account), ['account-2']);
  fx.cleanup();
});

// ——— `daoris agent list` (§2.4, §3.7): each account's cool-off, the order, and whether starts run on the person's own
// sign-in. Every agent is pinned to a version nothing installed, so the listing asks no tool anything.

function listed(fx: { root: string }, wiring: Record<string, unknown>): string {
  const path = join(fx.root, 'harnesses.json');
  const nowhere = { 'claude-code': '0.0.0-none', 'claude-code-acp': '0.0.0-none', codex: '0.0.0-none', 'codex-acp': '0.0.0-none', dsh: '0.0.0-none' };
  writeFileSync(path, JSON.stringify({ versions: nowhere, ...wiring }), 'utf8');
  return run(['list'], path).out;
}

test('`agent list` says each account\'s cool-off under it, and the tool\'s own sign-in\'s under the agent', () => {
  const fx = makeFixture('cooling-list');
  for (const name of ['account-1', 'account-2']) mkdirSync(profileHome(fx.root, 'claude-code', name), { recursive: true });
  cooling(fx, { 'claude-code': { 'account-1': { until: AHEAD(), stated: true }, '': { until: AHEAD(), stated: false } } });

  const out = listed(fx, { defaults: { 'claude-code': 'account-2' }, rotation: { 'claude-code': ['account-1', 'account-2'] } });

  assert.match(out, /account-1 .*\n\s+cooling until [A-Z][a-z]{2} \d{1,2}, \d{2}:\d{2} \(.+\), in [^—]+ — as the agent said; `daoris agent profile ready claude-code account-1` tries it now/);
  assert.match(out, /its own sign-in: cooling until .*Daoris's default: the agent named no time; `daoris agent profile ready claude-code --own` tries it now/);
  assert.match(out, /rotation\s+account-1, then account-2/);
  assert.doesNotMatch(out, /ready claude-code account-2/);
  fx.cleanup();
});

test('`agent list` says when starts run on the person\'s own sign-in, and how to give Daoris an account of its own', () => {
  const fx = makeFixture('cooling-list-own');
  const report = (machineDefault: string | null): HarnessReport => ({
    harness: 'claude-code', present: true, version: '2.1.0', problem: null, machineDefault,
    profiles: [{ name: 'account-1', home: '', login: 'in', account: null, key: null }],
  });
  const settings: HarnessSettings = {
    defaults: {}, workspaces: { work: { 'claude-code': 'account-1' } }, versions: {}, workspaceVersions: {},
    rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {},
  };
  const said = (agent: string, at: HarnessReport) =>
    accountLines(agent, TOOLCHAINS[agent]!, at, settings, fx.root, new Date(), 'UTC').join('\n');

  assert.match(said('claude-code', report(null)), /starts run on your own sign-in, the account `claude-code` uses at your own terminal \(outside work\):/);
  assert.match(said('claude-code', report(null)), /`daoris agent login claude-code --new` gives Daoris an account of its own/);
  // A default named, an agent not here, and a door onto another agent's accounts say nothing of it.
  assert.doesNotMatch(said('claude-code', report('account-1')), /own sign-in/);
  assert.doesNotMatch(said('claude-code', { ...report(null), present: false }), /own sign-in/);
  assert.doesNotMatch(said('claude-code-acp', report(null)), /own sign-in/);
  fx.cleanup();
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TESTS = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const twin = readFileSync(join(DRIVER_TESTS, 'CoolingTwinTests.cs'), 'utf8').replace(/\r\n/g, '\n');
  const words = readFileSync(join(DRIVER_TESTS, 'AccountCoolingTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(twin, 'An_entry_reads_as_the_cli_reads_it', {}, 'CoolingTwinTests'), READ_ROWS);
  assert.deepEqual(csharpRows(twin, 'An_entry_ends_as_the_cli_ends_it', {}, 'CoolingTwinTests'), END_ROWS);
  assert.deepEqual(csharpRows(twin, 'A_moment_is_said_in_the_machine_s_zone_with_the_zone_named', {}, 'CoolingTwinTests'), WHEN_ROWS);
  assert.deepEqual(csharpRows(words, 'Why_says_whether_the_agent_named_the_time', {}, 'AccountCoolingTests'), WHY_ROWS);
});
