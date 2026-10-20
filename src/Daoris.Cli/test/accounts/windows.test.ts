import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { TOOLCHAINS, accountLines } from '../../src/agents/toolchain.ts';
import type { HarnessReport, HarnessSettings } from '../../src/agents/toolchain.ts';
import { OWN_WINDOWS, age, saidLine, saidOf } from '../../src/accounts/windows.ts';
import type { WindowSaid } from '../../src/accounts/windows.ts';
import type { CurveScope } from '../../src/accounts/usagecurves.ts';
import { driverRows as csharpRows, driverFile, driverTestFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * `windows.json` read (TOOL6c, D130 §5.2): the driver writes what each account's agent said about its windows, and
 * `daoris agent list` and `profile use` read it. The CLI's half of a TWIN with the driver's `AccountWindows.SaidOf`:
 * 🔴 `WindowsTwinTests.cs` holds the same table, row for row and in the same order, and the last test here holds it to this
 * one, cell for cell.
 *
 * What an account said is written as JSON: each window still said, in the file's order, with its use, reset (UTC, to the
 * second), standing, credits, when it was seen and on which session; `null` is nothing said.
 */

type Cell = string | null;

const moment = (text: string) => new Date(text);

function said(windows: WindowSaid[]): Record<string, unknown>[] {
  const stamp = (at: Date) => `${at.toISOString().slice(0, 19)}Z`;
  return windows.map((each) => ({
    window: each.window, used: each.used, reset: stamp(each.reset), standing: each.standing, credits: each.credits,
    seen: stamp(each.seen), session: each.session, source: each.source,
  }));
}

// ——— Reading (§5.2): missing or unreadable is nothing said; a reading is gone at its reset; a week a limit told says no
// use; names compare without case; the tool's own sign-in is its own key, `OWN_WINDOWS` (CODEXUSE3).

const READ_ROWS: [name: string, file: Cell, now: string, agent: string, account: string, said: Cell][] = [
  ['missing is nothing said', null, '2026-10-02T12:00:00Z', 'claude-code', 'account-1', null],
  ['not JSON is nothing said', 'not json', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', null],
  ['JSON that is not an object is nothing said', '[1, 2]', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', null],
  ['a reading from Daoris\'s own read says so; any other source says said', '{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z","source":"read"},"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.2,"seen":"2026-10-02T11:00:00Z","source":"Read"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', '[{"window":"session","used":0.5,"reset":"2026-10-02T14:00:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:00:00Z","session":null,"source":"read"},{"window":"weekly","used":0.2,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:00:00Z","session":null,"source":"said"}]'],
  ['a reading read whole, in the file\'s order','{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.88,"standing":"clear","seen":"2026-10-02T11:40:00Z","session":"s1"},"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.14,"seen":"2026-10-02T11:40:00Z","session":"s1"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', '[{"window":"session","used":0.88,"reset":"2026-10-02T14:00:00Z","standing":"clear","credits":false,"seen":"2026-10-02T11:40:00Z","session":"s1","source":"said"},{"window":"weekly","used":0.14,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:40:00Z","session":"s1","source":"said"}]'],
  ['a window whose reset has passed is gone', '{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T12:00:00Z","used":0.88,"seen":"2026-10-02T09:00:00Z"},"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.14,"seen":"2026-10-02T09:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', '[{"window":"weekly","used":0.14,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T09:00:00Z","session":null,"source":"said"}]'],
  ['every window passed is nothing said', '{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T11:00:00Z","used":0.88,"seen":"2026-10-02T09:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', null],
  ['a week a limit told says no use', '{"claude-code":{"account-1":{"weekly":{"reset":"2026-10-03T10:17:00Z","seen":"2026-10-01T08:15:00Z","session":"s7"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', null],
  ['names compare without case, and are said as written', '{"claude-code":{"account-1":{"Session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'CLAUDE-CODE', 'Account-1', '[{"window":"Session","used":0.5,"reset":"2026-10-02T14:00:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:00:00Z","session":null,"source":"said"}]'],
  ['another account is not this one', '{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-2', null],
  ['a standing alone is a reading; credits are only JSON true', '{"claude-code":{"account-1":{"weekly":{"reset":"2026-10-06T21:18:00Z","standing":"warned","credits":"yes","seen":"2026-10-02T11:00:00Z"},"session":{"reset":"2026-10-02T14:00:00Z","used":0.2,"credits":true,"seen":"2026-10-02T11:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', '[{"window":"weekly","used":null,"reset":"2026-10-06T21:18:00Z","standing":"warned","credits":false,"seen":"2026-10-02T11:00:00Z","session":null,"source":"said"},{"window":"session","used":0.2,"reset":"2026-10-02T14:00:00Z","standing":null,"credits":true,"seen":"2026-10-02T11:00:00Z","session":null,"source":"said"}]'],
  ['a near written before USAGEREAD3a was only ever the agent pace warning, and reads as warned', '{"claude-code":{"account-1":{"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.6,"standing":"near","seen":"2026-10-02T11:00:00Z"},"session":{"reset":"2026-10-02T14:00:00Z","used":0.2,"standing":"Near","seen":"2026-10-02T11:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', '[{"window":"weekly","used":0.6,"reset":"2026-10-06T21:18:00Z","standing":"warned","credits":false,"seen":"2026-10-02T11:00:00Z","session":null,"source":"said"},{"window":"session","used":0.2,"reset":"2026-10-02T14:00:00Z","standing":"Near","credits":false,"seen":"2026-10-02T11:00:00Z","session":null,"source":"said"}]'],
  ['a use that is not a number, a standing that is not text and a session that is empty say nothing', '{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":"lots","standing":7,"seen":"2026-10-02T11:00:00Z","session":""},"weekly":{"reset":"2026-10-06T21:18:00Z","used":-0.2,"seen":"2026-10-02T11:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', null],
  ['a reset or a seen that is not ISO 8601 is nothing said', '{"claude-code":{"account-1":{"session":{"reset":"Oct 3","used":0.5,"seen":"2026-10-02T11:00:00Z"},"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.5,"seen":"today"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', null],
  ['a window that is not an object, and an account that is not one, say nothing', '{"claude-code":{"account-1":{"session":"0.5"},"account-2":[1]}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', null],
  ['a moment with an offset or a fraction is read in UTC, to the second', '{"claude-code":{"account-1":{"weekly":{"reset":"2026-10-07T03:03:00+05:45","used":0.25,"seen":"2026-10-02T11:00:00.5Z","session":"s2"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'account-1', '[{"window":"weekly","used":0.25,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:00:00Z","session":"s2","source":"said"}]'],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"claude-code":{"straße":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'STRASSE', null],
  ['a dotless i is not an I', '{"claude-code":{"ışık":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}}}}', '2026-10-02T12:00:00Z', 'claude-code', 'IŞIK', null],
  ['the tool\'s own sign-in reads under its own key, beside the accounts and none of them', '{"codex":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"}},"":{"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.4,"seen":"2026-10-02T11:30:00Z"}}}}', '2026-10-02T12:00:00Z', 'codex', '', '[{"window":"weekly","used":0.4,"reset":"2026-10-06T21:18:00Z","standing":null,"credits":false,"seen":"2026-10-02T11:30:00Z","session":null,"source":"said"}]'],
  ['an account is not the tool\'s own sign-in', '{"codex":{"":{"weekly":{"reset":"2026-10-06T21:18:00Z","used":0.4,"seen":"2026-10-02T11:30:00Z"}}}}', '2026-10-02T12:00:00Z', 'codex', 'account-1', null],
];

test('an account\'s windows read as the driver reads them (the twin\'s table)', () => {
  for (const [index, [name, file, now, agent, account, expected]] of READ_ROWS.entries()) {
    const fx = makeFixture(`windows-read-${index}`);
    if (file !== null) writeFileSync(fx.entry('windows'), file, 'utf8');

    const read = saidOf(fx.root, agent, account, moment(now));

    assert.deepEqual(read === null ? null : said(read), expected === null ? null : JSON.parse(expected), name);
    fx.cleanup();
  }
});

// ——— What a person reads (D130 §3.2, §9): the reading and its age; a limit reached where the agent said it, its pace
// warning in its own words and never as near (USAGEREAD3a), and, where a scope is given, its tightest window over its curve
// with the curve's value now, as the start's line says it (SWITCH1b-iii; `usagecurves.test.ts` holds the curve's rows).

const NOW = moment('2026-10-02T12:00:00Z');

function window(name: string, used: number | null, extra: Partial<WindowSaid> = {}): WindowSaid {
  return {
    window: name, used, reset: moment(name === 'session' ? '2026-10-02T14:00:00Z' : '2026-10-06T21:18:00Z'), standing: null,
    credits: false, seen: moment('2026-10-02T11:40:00Z'), session: 's1', source: 'said', ...extra,
  };
}

test('a reading is said with its age, each window\'s use and reset in the machine\'s zone, and over its curve only as measured', () => {
  const clear = [window('session', 0.88, { standing: 'clear' }), window('weekly', 0.14)];
  const smart: CurveScope = { switch: 'smart', curve: {} };
  const own = (session: number): CurveScope => ({ switch: 'curve', curve: { session: [[0, session]] } });

  assert.equal(
    saidLine(clear, NOW, 'UTC'),
    'said 20 min ago: 88% of its session limit used, resetting Oct 2, 14:00 (UTC); 14% of its weekly limit used, resetting Oct 6, 21:18 (UTC)');
  // The scope's own curve, its five-hour window flat at 85: 88% is over it; the smart curve's 90 is not.
  assert.match(saidLine(clear, NOW, 'UTC', own(85)), /; over its session curve's 85%$/);
  assert.doesNotMatch(saidLine(clear, NOW, 'UTC', smart), /over its|near/);
  assert.match(saidLine([window('session', 0.1, { standing: 'warned' })], NOW, 'UTC', smart), /^said 20 min ago: ahead of its pace on its session window, by its own word; 10% of its session limit used, resetting [^;]*$/);
  // A week 37% gone: the smart curve allows 57% now, so a warned 95% is over it by its number.
  assert.match(saidLine([window('weekly', 0.95, { standing: 'warned' })], NOW, 'UTC', smart), /^said 20 min ago: ahead of its pace on its weekly window, by its own word; 95% .*; over its weekly curve's 57%$/);
  assert.match(saidLine([window('weekly', null, { standing: 'refused' })], NOW, 'UTC', smart), /^said 20 min ago: its weekly limit reached, by its own word$/);
  assert.match(saidLine([window('session', 0.2, { credits: true })], NOW, 'UTC', own(50)), /^said 20 min ago: drawing on usage credits; 20% of its session limit used, resetting .*\(UTC\)$/);
  // Credits and a window over its curve are each said, as the start's line says both.
  assert.match(saidLine([window('session', 0.95, { credits: true })], NOW, 'UTC', smart), /^said 20 min ago: drawing on usage credits; 95% of its session limit used, resetting [^;]*; over its session curve's 90%$/);
  assert.equal(saidLine([window('session', null, { standing: 'clear' })], NOW, 'UTC', smart), 'said 20 min ago: clear, by its own word');
});

test('a line leads with where the reading came from: read for Daoris\'s own, said for a frame (USAGEREAD2b)', () => {
  const read = (name: string, used: number | null, extra: Partial<WindowSaid> = {}) => window(name, used, { source: 'read', ...extra });
  const late = { seen: moment('2026-10-02T11:58:00Z') };
  const early = { seen: moment('2026-10-02T09:00:00Z') };

  assert.equal(
    saidLine([read('session', 0.88, late), read('weekly', 0.14, late)], NOW, 'UTC'),
    'read 2 min ago: 88% of its session limit used, resetting Oct 2, 14:00 (UTC); 14% of its weekly limit used, resetting Oct 6, 21:18 (UTC)');
  // The lead is the newest reading's source; a window from the other source says so, with its own age.
  assert.equal(
    saidLine([read('session', 0.88, late), window('weekly', 0.14, early)], NOW, 'UTC'),
    'read 2 min ago: 88% of its session limit used, resetting Oct 2, 14:00 (UTC); 14% of its weekly limit used (said 3 h ago), resetting Oct 6, 21:18 (UTC)');
  assert.equal(
    saidLine([window('session', 0.88, late), read('weekly', 0.14, early)], NOW, 'UTC'),
    'said 2 min ago: 88% of its session limit used, resetting Oct 2, 14:00 (UTC); 14% of its weekly limit used (read 3 h ago), resetting Oct 6, 21:18 (UTC)');
  // A tie goes to the read, as the driver keeps it.
  assert.match(saidLine([window('session', 0.5, late), read('weekly', 0.1, late)], NOW, 'UTC'), /^read 2 min ago: 50% of its session limit used \(said 2 min ago\), resetting/);
});

test('a source that is not `read` is said, and absent is said (USAGEREAD2b)', () => {
  const fx = makeFixture('windows-source');
  const file = (source: string) => `{"claude-code":{"account-1":{"session":{"reset":"2026-10-02T14:00:00Z","used":0.5,"seen":"2026-10-02T11:00:00Z"${source}}}}}`;
  const sources = (source: string) => {
    writeFileSync(fx.entry('windows'), file(source), 'utf8');
    return saidOf(fx.root, 'claude-code', 'account-1', moment('2026-10-02T12:00:00Z'))?.map((each) => each.source);
  };

  assert.deepEqual(sources(',"source":"read"'), ['read']);
  assert.deepEqual(sources(''), ['said']);
  assert.deepEqual(sources(',"source":"Read"'), ['said']);
  assert.deepEqual(sources(',"source":7'), ['said']);
  fx.cleanup();
});

test('an age is said as the driver says it', () => {
  assert.equal(age(moment('2026-10-02T11:59:31Z'), NOW), 'just now');
  assert.equal(age(moment('2026-10-02T11:01:00Z'), NOW), '59 min ago');
  assert.equal(age(moment('2026-10-02T09:00:00Z'), NOW), '3 h ago');
  assert.equal(age(moment('2026-09-29T12:00:00Z'), NOW), '3 d ago');
});

test('`agent list` says beneath each account what it last said, and nothing for one that said nothing', () => {
  const fx = makeFixture('windows-list');
  const ahead = (hours: number) => new Date(Date.now() + hours * 3_600_000).toISOString().replace(/\.\d+Z$/, 'Z');
  const seen = new Date(Date.now() - 3 * 3_600_000).toISOString().replace(/\.\d+Z$/, 'Z');
  writeFileSync(fx.entry('windows'), JSON.stringify({
    'claude-code': { 'account-1': { session: { reset: ahead(2), used: 0.88, standing: 'clear', seen } } },
  }), 'utf8');
  const report: HarnessReport = {
    harness: 'claude-code', present: true, version: '2.1.0', problem: null, machineDefault: 'account-1',
    profiles: [
      { name: 'account-1', home: '', login: 'in', account: null, key: null },
      { name: 'account-2', home: '', login: 'in', account: null, key: null },
    ],
  };
  const settings: HarnessSettings = {
    defaults: { 'claude-code': 'account-1' }, workspaces: {}, versions: {}, workspaceVersions: {},
    rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {},
  };

  const lines = accountLines('claude-code', TOOLCHAINS['claude-code']!, report, settings, fx.root, new Date(), 'UTC');
  // A door onto Claude Code reads its owner's readings (twin rule 7).
  const door = accountLines('claude-code-acp', TOOLCHAINS['claude-code-acp']!, report, settings, fx.root, new Date(), 'UTC');

  const at = lines.findIndex((line) => /account-1 /.test(line));
  assert.match(lines[at + 1]!, /^\s+said 3 h ago: 88% of its session limit used, resetting .* \(UTC\)$/);
  assert.equal(lines.filter((line) => /said .* ago:/.test(line)).length, 1, lines.join('\n'));
  assert.equal(door.filter((line) => /said 3 h ago:/.test(line)).length, 1);
  fx.cleanup();
});

/**
 * CODEXUSE3: the driver reads the tool's own sign-in's windows at a person's press and keeps them under its own key, so
 * `agent list` says them beneath the agent, as it says its cool-off, and never as one of its accounts.
 */
test('`agent list` says what the tool\'s own sign-in last said beneath its agent, never as an account (CODEXUSE3)', () => {
  const fx = makeFixture('windows-own');
  const ahead = (hours: number) => new Date(Date.now() + hours * 3_600_000).toISOString().replace(/\.\d+Z$/, 'Z');
  const seen = new Date(Date.now() - 3 * 3_600_000).toISOString().replace(/\.\d+Z$/, 'Z');
  writeFileSync(fx.entry('windows'), JSON.stringify({
    codex: { [OWN_WINDOWS]: { session: { reset: ahead(2), used: 0.01, seen }, weekly: { reset: ahead(100), used: 0.15, seen } } },
  }), 'utf8');
  const report: HarnessReport = {
    harness: 'codex', present: true, version: 'codex-cli 0.160.0', problem: null, machineDefault: null, profiles: [],
  };
  const settings: HarnessSettings = {
    defaults: {}, workspaces: {}, versions: {}, workspaceVersions: {},
    rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {},
  };

  const lines = accountLines('codex', TOOLCHAINS['codex']!, report, settings, fx.root, new Date(), 'UTC');

  const own = lines.filter((line) => /said .* ago:/.test(line));
  assert.equal(own.length, 1, lines.join('\n'));
  assert.match(own[0]!, /^\s+its own sign-in: said 3 h ago: 1% of its session limit used, resetting .* \(UTC\); 15% of its weekly limit used, resetting .* \(UTC\)$/);
  // It is no account: the agent still has none, and its starts still run on the own sign-in by no list.
  assert.ok(lines.some((line) => /no accounts — Daoris's sessions wait until you sign in: `daoris agent login codex --new`/.test(line)), lines.join('\n'));
  assert.ok(!lines.some((line) => /rotation/.test(line)), lines.join('\n'));
  fx.cleanup();
});

// ——— The twin, held: the driver's table is this table, row for row and in this order.

test('the driver’s table is this table, row for row and in this order', () => {
  const twin = readFileSync(driverTestFile('WindowsTwinTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(twin, 'An_account_s_windows_read_as_the_cli_reads_them', {}, 'WindowsTwinTests'), READ_ROWS);
});

test('the tool’s own sign-in’s key is the driver’s, and no account’s (CODEXUSE3)', () => {
  const source = readFileSync(driverFile('AccountWindows.cs'), 'utf8');

  assert.equal(OWN_WINDOWS, '');
  assert.match(source, /public const string Own = "";/);
});
