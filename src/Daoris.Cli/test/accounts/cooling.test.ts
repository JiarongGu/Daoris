import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { coolingOf, endCooling, readCooling } from '../../src/accounts/cooling.ts';
import type { CoolingEntry } from '../../src/accounts/cooling.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { TOOLCHAINS, addKeyAccount, profileHome, removeProfile, signInNew, signInTo } from '../../src/agents/toolchain.ts';
import { driverRows as csharpRows, driverTestFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * `cooling.json` read and ended (TOOL4e, D125 §2.3): the driver writes an account's cool-off when a limit is read, and
 * both twins read it and end one early, here a sign-in or a key into the account and its removal. The CLI's half of a
 * TWIN with the driver's `AccountCooling.cs`: 🔴 `CoolingTwinTests.cs` holds the same tables, row for row and in the same
 * order, and the last test here holds each to this one, cell for cell.
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
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"claude-code":{"straße":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'STRASSE', null],
  ['a dotless i is not an I', '{"claude-code":{"ışık":{"until":"2026-10-03T10:17:00Z","stated":true,"seen":"2026-10-01T08:15:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'IŞIK', null],
];

test('an entry reads as the driver reads it (the twin\'s table)', () => {
  for (const [index, [name, file, now, agent, account, entry]] of READ_ROWS.entries()) {
    const fx = makeFixture(`cooling-read-${index}`);
    if (file !== null) writeFileSync(homeEntry(fx.root, 'cooling'), file, 'utf8');

    const read = coolingOf(fx.root, agent, account, moment(now));

    assert.deepEqual(read === null ? null : said(read), entry === null ? null : JSON.parse(entry), name);
    fx.cleanup();
  }
});

test('every account cooling, by agent and then account, and none that has passed', () => {
  const fx = makeFixture('cooling-all');
  writeFileSync(homeEntry(fx.root, 'cooling'), JSON.stringify({
    codex: { '': { until: '2026-10-03T10:17:00Z' } },
    'claude-code': { 'account-2': { until: '2026-10-03T10:17:00Z' }, 'account-1': { until: '2026-10-01T08:00:00Z' }, '': { until: '2026-10-02T00:00:00Z' } },
  }), 'utf8');

  const all = readCooling(fx.root, moment('2026-10-01T08:15:00Z'));

  assert.deepEqual(all.map((entry) => [entry.agent, entry.account]), [['claude-code', null], ['claude-code', 'account-2'], ['codex', null]]);
  fx.cleanup();
});

test('accounts order as the driver\'s OrdinalIgnoreCase orders them: STRASSE before straße, which a wider capital would tie', () => {
  const fx = makeFixture('cooling-order');
  writeFileSync(homeEntry(fx.root, 'cooling'), JSON.stringify({
    'claude-code': { 'straße': { until: '2026-10-03T10:17:00Z' }, STRASSE: { until: '2026-10-03T10:17:00Z' } },
  }), 'utf8');

  const all = readCooling(fx.root, moment('2026-10-01T08:15:00Z'));

  assert.deepEqual(all.map((entry) => entry.account), ['STRASSE', 'straße']);
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
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"claude-code":{"straße":{"until":"2026-10-03T10:17:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'STRASSE', false, 'unchanged'],
  ['a dotless i is not an I', '{"claude-code":{"ışık":{"until":"2026-10-03T10:17:00Z"}}}', '2026-10-01T08:15:00Z', 'claude-code', 'IŞIK', false, 'unchanged'],
];

test('an entry ends as the driver ends it (the twin\'s table)', () => {
  for (const [index, [name, file, now, agent, account, ended, after]] of END_ROWS.entries()) {
    const fx = makeFixture(`cooling-end-${index}`);
    const path = homeEntry(fx.root, 'cooling');
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

// A cool-off said in words (§2.4: its moment in the machine's zone, why it lasts, *Try now*) is the driver's alone since
// `agent list` went to its program (PUSH1f2e4): `CoolingWords` and `AgentsTextTests`' `list` rows hold it.

/** A cool-off a day ahead of now, written as the driver writes one. */
function cooling(fx: { root: string }, held: Record<string, Record<string, unknown>>): void {
  writeFileSync(homeEntry(fx.root, 'cooling'), `${JSON.stringify(held, null, 2)}\n`, 'utf8');
}

const AHEAD = () => new Date(Date.now() + 86_400_000).toISOString().replace(/\.\d+Z$/, 'Z');

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

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

test('the driver’s tables are these tables, row for row and in this order', () => {
  const twin = readFileSync(driverTestFile('CoolingTwinTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(twin, 'An_entry_reads_as_the_cli_reads_it', {}, 'CoolingTwinTests'), READ_ROWS);
  assert.deepEqual(csharpRows(twin, 'An_entry_ends_as_the_cli_ends_it', {}, 'CoolingTwinTests'), END_ROWS);
});
