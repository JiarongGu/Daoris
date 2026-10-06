import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ACCOUNTS_OWN, failuresOf } from '../src/strikes.ts';
import { sessionRecords } from '../src/service.ts';
import { driverRows as csharpRows } from './_csharp.ts';

/**
 * A quest's strikes, counted from this machine's session records (RETRY1b; D58 as D104, D125 and ROSTER1b amend it): what
 * `daoris driver retry <quest>` marks the quest at, so the terminal's retry starts it where the planner stands. The CLI's
 * half of a TWIN with the driver's `ServiceClient.ReadStrikes`: 🔴 `StrikeTests.cs` holds the same table, row for row and in
 * the same order, and a test here holds it to this one, cell for cell.
 *
 * Each row is one record as `/api/sessions?includeClosed=true` answers it, and whether it is a strike against `q1`.
 */
const ROWS: [string, boolean][] = [
  ['{ "id": "s1", "quest": "q1", "state": "failed" }', true],
  ['{ "id": "s1", "quest": "Q1", "state": "FAILED" }', true],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "limit": true }', false],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "limit": false }', true],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "limit": "true" }', true],
  ['{ "id": "s1", "quest": "q1", "state": "stopped" }', false],
  ['{ "id": "s1", "quest": "q1", "state": "stopped", "interrupted": true }', true],
  ['{ "id": "s1", "quest": "q1", "state": "stopped", "interrupted": false }', false],
  ['{ "id": "s1", "quest": "q1", "state": "completed", "interrupted": true }', false],
  ['{ "id": "s1", "quest": "q1", "state": "stood-down" }', false],
  ['{ "id": "s1", "quest": "q1", "state": "declined" }', false],
  ['{ "id": "s1", "quest": "q1", "state": "working" }', false],
  ['{ "id": "s1", "quest": "q1" }', false],
  ['{ "id": "origin/s1", "quest": "q1", "state": "failed" }', false],
  ['{ "id": 7, "quest": "q1", "state": "failed" }', true],
  ['{ "id": "s1", "quest": "q2", "state": "failed" }', false],
  ['{ "id": "s1", "quest": "", "state": "failed" }', false],
  ['{ "id": "s1", "state": "failed" }', false],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "noteParts": [{ "code": "account.refused", "values": { "owner": "claude-code" }, "text": "x" }] }', false],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "noteParts": [{ "code": "account.refused-own", "values": { "owner": "claude-code" }, "text": "x" }] }', false],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "noteParts": [{ "code": "account.signed-out", "values": { "owner": "claude-code" }, "text": "x" }] }', false],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "noteParts": [{ "code": "account.signed-out-own", "values": { "owner": "claude-code" }, "text": "x" }] }', false],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "noteParts": [{ "code": "ended.untouched-exit", "values": { "exit": 1 }, "text": "x" }] }', true],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "noteParts": [{ "code": "ACCOUNT.REFUSED" }] }', true],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "noteParts": [{ "words": "account.refused", "by": "agent" }] }', true],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "noteParts": "account.refused" }', true],
  ['{ "id": "s1", "quest": "q1", "state": "failed", "note": "The agent refused the account for its sign-in." }', true],
  ['{ "id": "s1", "quest": "q1", "state": "stopped", "interrupted": true, "noteParts": [{ "code": "account.refused" }] }', true],
  ['42', false],
];

test('each record strikes as the driver counts it', () => {
  for (const [record, strikes] of ROWS) {
    assert.equal(failuresOf(JSON.parse(`[${record}]`), 'q1'), strikes ? 1 : 0, record);
  }
});

test('a quest’s failures are every strike against it, its id in any case, and records that are not a list are none', () => {
  const records = JSON.parse(`[${ROWS.map(([record]) => record).join(',')}]`);
  const strikes = ROWS.filter(([, strikes]) => strikes).length;

  assert.equal(failuresOf(records, 'q1'), strikes);
  assert.equal(failuresOf(records, 'Q1'), strikes);
  assert.equal(failuresOf(records, 'q2'), 1);
  assert.equal(failuresOf(records, 'q9'), 0);
  assert.equal(failuresOf([], 'q1'), 0);
  assert.equal(failuresOf({ sessions: [] }, 'q1'), null);
  assert.equal(failuresOf(null, 'q1'), null);
});

/**
 * A quest's id compares as `ReadStrikes` compares it, by `OrdinalIgnoreCase` (CASEFOLD1, `casefold.ts`): an id full case
 * mapping would widen or lower to the same letters is another quest's, and its failures are none of this one's.
 */
test('a quest’s id in any case is one only as the driver finds it: straße is not STRASSE, İzmir not i̇zmir', () => {
  const failed = (quest: string) => ({ id: 's1', quest, state: 'failed' });

  assert.equal(failuresOf([failed('straße'), failed('STRASSE')], 'STRASSE'), 1);
  assert.equal(failuresOf([failed('İzmir'), failed('i\u{307}zmir')], 'i\u{307}zmir'), 1);
  assert.equal(failuresOf([failed('Νίκος')], 'ΝΊΚΟΣ'), 1);
});

// ——— The twin, held: the driver's table is this table, row for row and in this order, and its codes are these codes.

const DRIVER = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop');

test('the driver’s table is this table, row for row and in this order', () => {
  const twin = readFileSync(join(DRIVER, 'Daoris.Desktop.Driver.Tests', 'StrikeTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(twin, 'A_record_strikes_as_the_cli_counts_it', {}, 'StrikeTests'), ROWS);
});

test('the account’s own codes are the driver’s, code for code and in its order', () => {
  const source = readFileSync(join(DRIVER, 'Daoris.Desktop.Driver', 'NoteCodes.cs'), 'utf8');
  const names = /AccountsOwn \{ get; \} = \[([^\]]+)\];/.exec(source)?.[1]?.split(',').map((name) => name.trim());
  assert.ok(names && names.length > 0, 'NoteCodes.cs declares no AccountsOwn');
  const codes = names.map((name) => new RegExp(`NoteCode ${name} = new\\("([^"]+)"`).exec(source)?.[1]);

  assert.deepEqual(codes, ACCOUNTS_OWN);
});

// ——— Reading them: the service client asks this machine's host, and says why when it cannot.

/** A host that answers one GET, as the test says, and records what it was asked. */
function host(answer: () => Response | Promise<Response>): { asked: { url: string; init: RequestInit }[]; get: (url: string, init: RequestInit) => Promise<Response> } {
  const asked: { url: string; init: RequestInit }[] = [];
  return { asked, get: async (url, init) => { asked.push({ url, init }); return answer(); } };
}

test('the records are read from this machine’s host, closed ones included, with its key', async () => {
  const records = [{ id: 's1', quest: 'q1', state: 'failed' }];
  const local = host(() => Response.json(records));

  const read = await sessionRecords({ DAORIS_SERVICE_URL: 'http://localhost:5177/', DAORIS_SERVICE_KEY: 'k1' }, local.get);

  assert.deepEqual(read, { records });
  assert.equal(local.asked[0]?.url, 'http://localhost:5177/api/sessions?includeClosed=true');
  assert.deepEqual(local.asked[0]?.init.headers, { authorization: 'Bearer k1' });
});

test('records that cannot be read say why, and nothing is asked of a host that is not this machine', async () => {
  const never = host(() => { throw new Error('asked'); });

  assert.match(
    ((await sessionRecords({}, never.get)) as { unread: string }).unread, /^no DAORIS_SERVICE_URL is set/);
  assert.match(
    ((await sessionRecords({ DAORIS_SERVICE_URL: 'https://team.example' }, never.get)) as { unread: string }).unread,
    /^DAORIS_SERVICE_URL names https:\/\/team\.example, which is not this machine/);
  assert.equal(never.asked.length, 0);

  const down = host(() => { throw new TypeError('fetch failed'); });
  assert.equal(
    ((await sessionRecords({ DAORIS_SERVICE_URL: 'http://localhost:5177' }, down.get)) as { unread: string }).unread,
    'the service at http://localhost:5177 did not answer: fetch failed');

  const refused = host(() => Response.json({ error: 'a key is needed' }, { status: 401 }));
  assert.equal(
    ((await sessionRecords({ DAORIS_SERVICE_URL: 'http://localhost:5177' }, refused.get)) as { unread: string }).unread,
    'the service at http://localhost:5177 answered 401: a key is needed');

  const odd = host(() => Response.json({ sessions: [] }));
  assert.equal(
    ((await sessionRecords({ DAORIS_SERVICE_URL: 'http://localhost:5177' }, odd.get)) as { unread: string }).unread,
    'the service at http://localhost:5177 answered no list of records');
});
