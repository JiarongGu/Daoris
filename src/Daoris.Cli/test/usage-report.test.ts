import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  main, median, parseArguments, parseLine, readLogs, render, summarise,
  // @ts-expect-error — untyped workspace tooling; the same seam desktop-tool.test.ts documents
} from '../../../tools/usage-report.mjs';

/**
 * The usage report (LOG1d, D94): an install's machine log, summarised for a development session — what
 * was used most, how the conversations went and how long they took, what was refused, what failed, and
 * the lifecycle. Workspace tooling, tested from here for the reason `desktop-tool.test.ts` states.
 *
 * 🔴 **The parse table is a twin's** (`docs/2026-09-30-machine-log-design.md` §3): the driver's
 * `MachineLogReader` reads the same lines with its own code, and `MachineLogReaderTests.Parsing` holds
 * the same rows with the same answers. Change one table and the other changes with it.
 */

/** The report's own shape, as far as these tests read it: the tool is untyped. */
type Counted = { name: string; count: number };
type Timing = { count: number; medianMs: number | null; slowestMs: number | null; slowestSession?: string | null };
type Failure = { event: string; source: string; what: string; count: number; sample: string | null; slowestMs: number | null };
type Report = {
  period: { from: string; to: string; days: number };
  lines: number;
  skipped: number;
  lifecycle: { source: string; starts: number; stops: number; uptimeSeconds: number; versions: string[] }[];
  used: { views: Counted[]; commands: Counted[]; panels: Counted[] };
  sessions: {
    started: number; byKind: Counted[]; byAdapter: Counted[];
    opened: Timing; answered: Timing;
    turns: Timing & { ended: number; byStopReason: Counted[] };
    ended: Counted[];
    messages: { sent: number; byKind: Counted[] };
    proposals: { applied: number; declined: number };
  };
  refused: { code: string; count: number; requests: Counted[] }[];
  failed: Failure[];
};

const NOW = new Date('2026-09-30T12:00:00.000Z');

const line = (time: string, source: string, level: string, event: string, data: Record<string, unknown> = {}) =>
  JSON.stringify({ time, source, level, event, data });

/**
 * The twin's table: each line, and whether a reader keeps it — the rows `MachineLogReaderTests.Parsing`
 * holds. A blank line is neither kept nor counted: it is the file's last newline, not a line.
 */
const PARSING: [why: string, text: string, kept: boolean][] = [
  ['a whole line', '{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","event":"session.opened","data":{"session":"76cdd5db","openMs":5840}}', true],
  ['no data', '{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"info","event":"app.started"}', true],
  ['data that is no object', '{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"info","event":"app.started","data":"x"}', true],
  ['a field nobody knows', '{"time":"2026-09-30T07:10:00.123Z","source":"mcp","level":"warn","event":"log","data":{},"extra":1}', true],
  ['a time with no milliseconds', '{"time":"2026-09-30T07:10:00Z","source":"desktop","level":"info","event":"app.started","data":{}}', true],
  ['not JSON', 'not json at all', false],
  ['a torn line', '{"time":"2026-09-30T07:10:00.123Z","source":"desk', false],
  ['an array', '["time","source"]', false],
  ['no time', '{"source":"desktop","level":"info","event":"app.started","data":{}}', false],
  ['a time that is no time', '{"time":"yesterday","source":"desktop","level":"info","event":"app.started","data":{}}', false],
  ['a time with no zone', '{"time":"2026-09-30T07:10:00.123","source":"desktop","level":"info","event":"app.started","data":{}}', false],
  ['no source', '{"time":"2026-09-30T07:10:00.123Z","level":"info","event":"app.started","data":{}}', false],
  ['no level', '{"time":"2026-09-30T07:10:00.123Z","source":"desktop","event":"app.started","data":{}}', false],
  ['no event', '{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","data":{}}', false],
  ['an event that is no string', '{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","event":5,"data":{}}', false],
];

test('each line of the twin table is kept or skipped, as the driver\'s reader decides', () => {
  for (const [why, text, kept] of PARSING) {
    assert.equal(parseLine(text) !== null, kept, why);
  }
  const parsed = parseLine(PARSING[0]![1]);
  assert.equal(parsed.stamp, '2026-09-30T07:10:00.123Z');
  assert.equal(parsed.time, Date.parse('2026-09-30T07:10:00.123Z'));
  assert.deepEqual([parsed.source, parsed.level, parsed.event], ['desktop', 'info', 'session.opened']);
  assert.deepEqual(parsed.data, { session: '76cdd5db', openMs: 5840 });
  assert.deepEqual(parseLine(PARSING[2]![1]).data, {});
});

/** A week on a machine, across three sources and two days, with the mistakes a real log holds. */
function week(): string {
  const home = mkdtempSync(join(tmpdir(), 'daoris-usage-'));
  const logs = join(home, 'logs');
  mkdirSync(logs);
  const file = (name: string, lines: string[]) => writeFileSync(join(logs, name), `${lines.join('\n')}\n`);

  file('2026-09-20.desktop.jsonl', [line('2026-09-20T09:00:00.000Z', 'desktop', 'info', 'view.opened', { view: 'ancient' })]);
  file('2026-09-29.desktop.jsonl', [
    line('2026-09-29T22:00:00.000Z', 'desktop', 'info', 'app.started', { version: '0.0.1+abcdef1234567890', installed: true }),
    line('2026-09-29T22:01:00.000Z', 'desktop', 'info', 'view.opened', { view: 'overview' }),
    line('2026-09-29T22:02:00.000Z', 'desktop', 'info', 'view.opened', { view: 'sessions' }),
    line('2026-09-29T22:03:00.000Z', 'desktop', 'info', 'command.run', { command: 'go.quests' }),
    line('2026-09-29T22:04:00.000Z', 'desktop', 'info', 'session.started', { session: 's1', kind: 'chat', adapter: 'claude-code-acp', repository: 'engine' }),
    line('2026-09-29T22:04:06.000Z', 'desktop', 'info', 'session.opened', { session: 's1', adapter: 'claude-code-acp', openMs: 6000 }),
    line('2026-09-29T22:04:08.000Z', 'desktop', 'info', 'message.sent', { session: 's1', kind: 'chat', length: 42, files: 0 }),
    line('2026-09-29T22:04:10.000Z', 'desktop', 'info', 'turn.answered', { session: 's1', firstAnswerMs: 2000 }),
    line('2026-09-29T22:05:00.000Z', 'desktop', 'info', 'turn.ended', { session: 's1', stopReason: 'end_turn', turnMs: 50000 }),
    line('2026-09-29T22:06:00.000Z', 'desktop', 'info', 'refused', { code: 'DRIVER_REFUSED', request: 'DAORIS.DRIVER.START_CHAT' }),
    line('2026-09-29T23:00:00.000Z', 'desktop', 'info', 'app.stopped', { uptimeSeconds: 3600 }),
  ]);
  file('2026-09-30.desktop.jsonl', [
    line('2026-09-30T08:00:00.000Z', 'desktop', 'info', 'app.started', { version: '0.0.2+1234567890abcdef', installed: true }),
    line('2026-09-30T08:01:00.000Z', 'desktop', 'info', 'view.opened', { view: 'sessions' }),
    line('2026-09-30T08:02:00.000Z', 'desktop', 'info', 'panel.moved', { view: 'console', region: 'right' }),
    'not json',
    line('2026-09-30T08:03:00.000Z', 'desktop', 'info', 'session.started', { session: 's2', kind: 'driven', adapter: 'codex-acp', repository: 'game' }),
    line('2026-09-30T08:03:02.000Z', 'desktop', 'info', 'session.opened', { session: 's2', adapter: 'codex-acp', openMs: 2000 }),
    line('2026-09-30T08:03:05.000Z', 'desktop', 'info', 'turn.answered', { session: 's2', firstAnswerMs: 4000 }),
    line('2026-09-30T08:04:00.000Z', 'desktop', 'info', 'turn.ended', { session: 's2', stopReason: 'cancelled', turnMs: null }),
    line('2026-09-30T08:05:00.000Z', 'desktop', 'info', 'session.started', { session: 's3', kind: 'chat', adapter: 'claude-code-acp' }),
    line('2026-09-30T08:05:10.000Z', 'desktop', 'info', 'session.opened', { session: 's3', adapter: 'claude-code-acp', openMs: 10000 }),
    line('2026-09-30T08:06:00.000Z', 'desktop', 'info', 'turn.answered', { session: 's3', firstAnswerMs: 3000 }),
    line('2026-09-30T08:07:00.000Z', 'desktop', 'info', 'turn.ended', { session: 's3', stopReason: 'end_turn', turnMs: 70000 }),
    line('2026-09-30T08:08:00.000Z', 'desktop', 'info', 'session.ended', { session: 's2', state: 'stopped', seconds: 300 }),
    line('2026-09-30T08:09:00.000Z', 'desktop', 'info', 'session.ended', { session: 's1', state: 'completed', seconds: null }),
    line('2026-09-30T08:10:00.000Z', 'desktop', 'info', 'refused', { code: 'DRIVER_REFUSED', request: 'DAORIS.DRIVER.START_CHAT' }),
    line('2026-09-30T08:11:00.000Z', 'desktop', 'warn', 'refused', { code: 'NO_ROUTE', request: 'DAORIS.LOG.READ' }),
    line('2026-09-30T08:12:00.000Z', 'desktop', 'error', 'page.error', { where: 'window', message: 'a'.repeat(200) }),
    line('2026-09-30T08:13:00.000Z', 'desktop', 'error', 'log', { category: 'Daoris.Desktop.DriverLoop', message: 'the tick failed', exception: 'System.Exception: the tick failed' }),
    line('2026-09-30T08:14:00.000Z', 'desktop', 'warn', 'log', { category: 'Shenora.Core', message: 'slow to answer' }),
    line('2026-09-30T08:15:00.000Z', 'desktop', 'error', 'error', { where: 'the driver loop', type: 'System.IO.IOException', message: 'the disk is full\nand a second line', stack: 'at A\nat B' }),
    line('2026-09-30T08:16:00.000Z', 'desktop', 'error', 'error', { where: 'the driver loop', type: 'System.IO.IOException', message: 'the disk is full', stack: 'at A' }),
    line('2026-09-30T08:17:00.000Z', 'desktop', 'info', 'proposal.settled', { applied: true }),
    line('2026-09-30T08:18:00.000Z', 'desktop', 'info', 'proposal.settled', { applied: false }),
    line('2026-09-30T08:19:00.000Z', 'desktop', 'info', 'message.sent', { kind: 'help', length: 10, files: 1 }),
  ]);
  file('2026-09-30.host.jsonl', [
    line('2026-09-30T07:59:00.000Z', 'host', 'info', 'app.started', { version: '0.0.2+1234567890abcdef', mode: 'local' }),
    line('2026-09-30T08:30:00.000Z', 'host', 'warn', 'request.failed', { method: 'GET', route: '/api/search', status: 500, ms: 30 }),
    line('2026-09-30T08:31:00.000Z', 'host', 'warn', 'request.failed', { method: 'GET', route: '/api/search', status: 500, ms: 2500 }),
    line('2026-09-30T09:00:00.000Z', 'host', 'warn', 'request.failed', { method: 'POST', route: '/api/quests', status: 200, ms: 2300 }),
  ]);
  file('notes.txt', ['the person\'s own note']);
  return home;
}

function withWeek(body: (home: string) => void) {
  const home = week();
  try {
    body(home);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
}

const since = new Date(NOW.getTime() - 7 * 86_400_000);

test('the logs are every source\'s lines in the period, merged by time, with what could not be read counted', () => {
  withWeek((home) => {
    const read = readLogs(join(home, 'logs'), { since });

    assert.equal(read.skipped, 1);
    assert.equal(read.lines.length, 38);
    const times = read.lines.map((each: { time: number }) => each.time);
    assert.deepEqual(times, [...times].sort((a, b) => a - b));
    // The host's start lands between the two desktop days, and the week-old file is never read.
    assert.equal(read.lines[11].source, 'host');
    assert.equal(read.lines.some((each: { data: { view?: string } }) => each.data.view === 'ancient'), false);
  });
});

test('a folder with no log is no lines, not a failure', () => {
  const read = readLogs(join(tmpdir(), 'daoris-usage-nowhere-at-all'), { since });
  assert.deepEqual([read.lines, read.skipped], [[], 0]);
});

test('the median is the middle, or the two middles\' mean, and nothing is no median', () => {
  assert.equal(median([6000, 2000, 10000]), 6000);
  assert.equal(median([50000, 70000]), 60000);
  assert.equal(median([]), null);
});

function reportOf(home: string): Report {
  const read = readLogs(join(home, 'logs'), { since });
  return summarise(read.lines, { from: since, to: NOW, skipped: read.skipped });
}

test('what was used most: views, commands and panel moves, most first', () => {
  withWeek((home) => {
    const { used } = reportOf(home);

    assert.deepEqual(used.views, [{ name: 'sessions', count: 2 }, { name: 'overview', count: 1 }]);
    assert.deepEqual(used.commands, [{ name: 'go.quests', count: 1 }]);
    assert.deepEqual(used.panels, [{ name: 'console → right', count: 1 }]);
  });
});

test('conversations and sessions: how many, by kind and adapter, how long each part took, and how they ended', () => {
  withWeek((home) => {
    const { sessions } = reportOf(home);

    assert.equal(sessions.started, 3);
    assert.deepEqual(sessions.byKind, [{ name: 'chat', count: 2 }, { name: 'driven', count: 1 }]);
    assert.deepEqual(sessions.byAdapter, [{ name: 'claude-code-acp', count: 2 }, { name: 'codex-acp', count: 1 }]);
    assert.deepEqual(sessions.opened, { count: 3, medianMs: 6000, slowestMs: 10000, slowestSession: 's3' });
    assert.deepEqual(sessions.answered, { count: 3, medianMs: 3000, slowestMs: 4000, slowestSession: 's2' });
    // A turn whose time could not be known is counted as ended, and never timed as zero.
    assert.equal(sessions.turns.ended, 3);
    assert.deepEqual(
      [sessions.turns.count, sessions.turns.medianMs, sessions.turns.slowestMs, sessions.turns.slowestSession],
      [2, 60000, 70000, 's3']);
    assert.deepEqual(sessions.turns.byStopReason, [{ name: 'end_turn', count: 2 }, { name: 'cancelled', count: 1 }]);
    assert.deepEqual(sessions.ended, [{ name: 'completed', count: 1 }, { name: 'stopped', count: 1 }]);
    assert.deepEqual(sessions.messages, { sent: 2, byKind: [{ name: 'chat', count: 1 }, { name: 'help', count: 1 }] });
    assert.deepEqual(sessions.proposals, { applied: 1, declined: 1 });
  });
});

test('what was refused, by code, most first, with the requests that met it', () => {
  withWeek((home) => {
    assert.deepEqual(reportOf(home).refused, [
      { code: 'DRIVER_REFUSED', count: 2, requests: [{ name: 'DAORIS.DRIVER.START_CHAT', count: 2 }] },
      { code: 'NO_ROUTE', count: 1, requests: [{ name: 'DAORIS.LOG.READ', count: 1 }] },
    ]);
  });
});

test('what failed, grouped, most first: exceptions, the page\'s errors, error-level log lines and failed requests', () => {
  withWeek((home) => {
    const { failed } = reportOf(home);

    assert.deepEqual(failed.map((each) => [each.event, each.source, each.what, each.count]), [
      ['error', 'desktop', 'System.IO.IOException in the driver loop', 2],
      ['request.failed', 'host', 'GET /api/search 500', 2],
      ['page.error', 'desktop', 'the page (window)', 1],
      ['log', 'desktop', 'Daoris.Desktop.DriverLoop', 1],
      ['request.failed', 'host', 'POST /api/quests 200', 1],
    ]);
    // A warning is not a failure, and a message is its first line, cut at the log's own cap.
    assert.equal(failed.some((each) => each.what === 'Shenora.Core'), false);
    assert.equal(failed[0]!.sample, 'the disk is full');
    assert.equal(failed[1]!.slowestMs, 2500);
    assert.equal(failed[2]!.sample, `${'a'.repeat(120)}…`);
  });
});

test('the lifecycle: starts, stops, uptime and the versions each process ran', () => {
  withWeek((home) => {
    const { lifecycle, lines, skipped, period } = reportOf(home);

    assert.deepEqual(lifecycle, [
      { source: 'desktop', starts: 2, stops: 1, uptimeSeconds: 3600, versions: ['0.0.1+abcdef12', '0.0.2+12345678'] },
      { source: 'host', starts: 1, stops: 0, uptimeSeconds: 0, versions: ['0.0.2+12345678'] },
    ]);
    assert.deepEqual([lines, skipped], [38, 1]);
    assert.deepEqual(period, { from: '2026-09-23T12:00:00.000Z', to: '2026-09-30T12:00:00.000Z', days: 7 });
  });
});

test('the text report says each section, and never prints a message beyond the log\'s own cap', () => {
  withWeek((home) => {
    const text: string = render(reportOf(home), join(home, 'logs'));

    for (const heading of ['Lifecycle', 'Used most', 'Conversations and sessions', 'Refused', 'Failed']) {
      assert.match(text, new RegExp(`^${heading}$`, 'm'), heading);
    }
    assert.match(text, /views\s+sessions 2 · overview 1/);
    assert.match(text, /opening\s+median 6\.0 s · slowest 10\.0 s \(s3\) · 3 timed/);
    assert.match(text, /turns\s+3 ended — end_turn 2 · cancelled 1; median 1m 0s · slowest 1m 10s \(s3\) · 2 timed/);
    assert.match(text, /DRIVER_REFUSED\s+2\s+DAORIS\.DRIVER\.START_CHAT/);
    assert.match(text, /2× error\s+desktop\s+System\.IO\.IOException in the driver loop — the disk is full/);
    assert.match(text, /desktop\s+2 starts · 1 stop · 1h 0m up/);
    assert.equal(text.includes('a'.repeat(121)), false);
    assert.equal(text.includes('and a second line'), false);
  });
});

test('an empty period says so in every section rather than printing nothing', () => {
  const text: string = render(summarise([], { from: since, to: NOW, skipped: 0 }), 'logs');
  assert.match(text, /No lines in this period\./);
  assert.match(text, /^Refused\n {2}none$/m);
});

test('the arguments: a home, or an install\'s data folder, a number of days, and JSON', () => {
  assert.deepEqual(parseArguments(['--home', 'h']), { home: 'h', days: 7, json: false });
  assert.deepEqual(parseArguments(['--install', 'i', '--days', '30', '--json']), { home: join('i', 'data'), days: 30, json: true });
  for (const [args, problem] of [
    [[], /--home <dir> or --install <dir>/],
    [['--home', 'h', '--install', 'i'], /one of --home and --install/],
    [['--home', 'h', '--days', '0'], /--days takes a whole number of days/],
    [['--home', 'h', '--days', 'week'], /--days takes a whole number of days/],
    [['--home'], /--home takes a folder/],
    [['--home', 'h', '--tail'], /`--tail` is not an option/],
  ] as const) {
    assert.throws(() => parseArguments([...args]), problem);
  }
});

test('the runner prints the text, or the same as JSON, and says when a home holds no log', () => {
  withWeek((home) => {
    const out: string[] = [];
    const err: string[] = [];
    const io = { now: NOW, out: (text: string) => out.push(text), err: (text: string) => err.push(text) };

    assert.equal(main(['--home', home], io), 0);
    assert.match(out.join('\n'), /^Daoris usage, 2026-09-23 12:00 → 2026-09-30 12:00 UTC \(7 days\)/);

    out.length = 0;
    assert.equal(main(['--home', home, '--json'], io), 0);
    assert.equal((JSON.parse(out.join('\n')) as Report).sessions.started, 3);

    assert.equal(main(['--home', join(home, 'nowhere')], io), 2);
    assert.match(err.join('\n'), /no machine log in/);
    assert.equal(main(['--days', '3'], io), 2);
  });
});
