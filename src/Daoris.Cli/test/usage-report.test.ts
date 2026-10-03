import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, rmSync, utimesSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  RECORD_ROUTES, askOf, endingOf, main, median, outcomesOf, parseArguments, parseLine, readLogs, readProposals, readRecords,
  render, summarise,
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
  used: { views: Counted[]; commands: Counted[]; panels: Counted[]; previews: { opened: number; sessions: number; byKind: Counted[] } };
  sessions: {
    started: number; byKind: Counted[]; byAdapter: Counted[];
    opened: Timing; answered: Timing;
    turns: Timing & { ended: number; byStopReason: Counted[] };
    ended: Counted[];
    messages: { sent: number; byKind: Counted[] };
    proposals: { applied: number; declined: number };
  };
  asks: Spread & {
    refused: number;
    byAdapter: (Spread & { name: string })[];
    byRepository: (Spread & { name: string })[];
    byKind: Counted[]; byTool: Counted[]; by: Counted[];
    unstarted: number;
  };
  proposals: { waiting: number; byWeek: { week: string; made: number; byState: Counted[] }[] };
  setups: { started: number; byState: Counted[]; sessions: SetupCost[]; total: Omit<SetupCost, 'session' | 'repository' | 'workspace' | 'state' | 'parked'> & { sessions: number } };
  parks: { parked: number; byKind: Counted[]; byWeek: { week: string; parked: number; started: number; byWorkspace: { name: string; parked: number; started: number }[] }[] };
  refused: { code: string; count: number; requests: Counted[] }[];
  failed: Failure[];
};

/** One set-up session's cost, as the report reads it from the log: absent is null, never zero. */
type SetupCost = {
  session: string; repository: string; workspace: string; state: string; parked: number;
  seconds: number | null; calls: number | null;
  input: number | null; cacheRead: number | null; cacheWrite: number | null; output: number | null;
  used: number | null; size: number | null;
};

/** Asks per session, spread: the sessions counted, their asks, and the statistics over them. */
type Spread = { sessions: number; asks: number; mean: number | null; median: number | null; p90: number | null; none: number | null };

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
    // LEFT2's preview.opened: a file read for its preview in the side bar, by session and path within the tree.
    line('2026-09-30T08:20:00.000Z', 'desktop', 'info', 'preview.opened', { session: 's3', path: 'src/chunk.ts' }),
    line('2026-09-30T08:21:00.000Z', 'desktop', 'info', 'preview.opened', { session: 's3', path: 'README.MD' }),
    line('2026-09-30T08:22:00.000Z', 'desktop', 'info', 'preview.opened', { session: 's2', path: 'src/api/routes.ts' }),
    line('2026-09-30T08:23:00.000Z', 'desktop', 'info', 'preview.opened', { session: 's2', path: 'Makefile' }),
    line('2026-09-30T08:24:00.000Z', 'desktop', 'info', 'preview.opened', { session: 's2', path: 'docs/guide.md' }),
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
    assert.equal(read.lines.length, 43);
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

/**
 * LEFT3 f: whether the side bar's reading room is used (LEFT2, D111): how many previews opened, in how many sessions,
 * and what kinds of file, by extension. Never which file: a kind says what is read, and a path says nothing more a
 * development session needs.
 */
test('what was previewed: how many files, in how many sessions, and of what kind', () => {
  withWeek((home) => {
    const { used } = reportOf(home);

    // A kind is the extension, whatever its case, and a file with none is `(none)`.
    assert.deepEqual(used.previews, {
      opened: 5,
      sessions: 2,
      byKind: [{ name: '.md', count: 2 }, { name: '.ts', count: 2 }, { name: '(none)', count: 1 }],
    });
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
    assert.deepEqual([lines, skipped], [43, 1]);
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
    assert.match(text, /previews\s+5 opened in 2 sessions — \.md 2 · \.ts 2 · \(none\) 1/);
    assert.equal(text.includes('src/chunk.ts'), false);
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
  assert.match(text, /^Used most\n {2}none$/m);
  // OUTCOME1: with no host named, the outcomes are not read, and the section says how to read them.
  assert.match(text, /^Outcomes\n {2}not read — no host was named: --service <url> reads its quests and sessions$/m);
  const quiet: string = render(summarise([], {
    from: since, to: NOW, skipped: 0,
    records: { quests: [], questsUnread: null, sessions: [], sessionsUnread: null, asks: [], asksUnread: null },
  }), 'logs');
  assert.match(quiet, /^Outcomes\n {2}none$/m);
});

/**
 * UNBLOCK5 (D122 §3.10): a week of asks. Four sessions started in the period and one before it; the
 * driver's `permission.refused` lines, one per refused call; and the home's rule proposals, one file each.
 */
function asksWeek(): string {
  const home = mkdtempSync(join(tmpdir(), 'daoris-asks-'));
  const logs = join(home, 'logs');
  mkdirSync(logs);
  const refused = (time: string, data: Record<string, unknown>) => line(time, 'desktop', 'info', 'permission.refused', data);
  writeFileSync(join(logs, '2026-09-29.desktop.jsonl'), `${[
    line('2026-09-29T09:00:00.000Z', 'desktop', 'info', 'session.started', { session: 's1', kind: 'driven', adapter: 'claude-code', repository: 'engine' }),
    line('2026-09-29T09:01:00.000Z', 'desktop', 'info', 'session.started', { session: 's2', kind: 'chat', adapter: 'claude-code-acp', repository: 'engine' }),
    line('2026-09-29T09:02:00.000Z', 'desktop', 'info', 'session.started', { session: 's3', kind: 'driven', adapter: 'codex-acp', repository: 'game' }),
    line('2026-09-29T09:03:00.000Z', 'desktop', 'info', 'session.started', { session: 's4', kind: 'driven', adapter: 'claude-code', repository: 'game' }),
    refused('2026-09-29T09:10:00.000Z', { session: 's1', adapter: 'claude-code', tool: 'Bash', kind: 'execute', by: 'rule' }),
    refused('2026-09-29T09:11:00.000Z', { session: 's1', adapter: 'claude-code', tool: 'Bash', kind: 'execute', by: 'rule' }),
    refused('2026-09-29T09:12:00.000Z', { session: 's1', adapter: 'claude-code', tool: 'WebFetch', kind: 'fetch', by: null }),
    refused('2026-09-29T09:13:00.000Z', { session: 's2', adapter: 'claude-code-acp', tool: null, kind: 'execute', by: null }),
    // A session whose start is not in the period: its ask is counted, and no session's.
    refused('2026-09-29T09:14:00.000Z', { session: 's9', adapter: 'claude-code', tool: 'Edit', kind: 'edit', by: 'mode' }),
  ].join('\n')}\n`);

  const proposals = join(home, 'proposals');
  mkdirSync(proposals);
  const proposal = (name: string, body: Record<string, unknown>) =>
    writeFileSync(join(proposals, `${name}.json`), `${JSON.stringify(body)}\n`);
  const change = { action: 'add', scope: 'repository', name: 'engine', list: 'allow', rule: 'Bash(npm run secret-script)' };
  proposal('p1', { id: 'p1', proposed: '2026-09-24T10:00:00Z', by: { session: 's1' }, change, why: 'the words of a proposal', state: 'accepted' });
  proposal('p2', { id: 'p2', proposed: '2026-09-29T08:00:00Z', change, why: 'the words of a proposal', state: 'waiting' });
  proposal('p3', { id: 'p3', proposed: '2026-09-30T09:00:00Z', change, why: 'the words of a proposal', state: 'waiting' });
  proposal('p4', { id: 'p4', proposed: '2026-09-29T09:00:00Z', change, why: 'the words of a proposal' });
  // Made before the period, and still waiting for the person: it is waiting now, and made in no week shown.
  proposal('p5', { id: 'p5', proposed: '2026-09-10T00:00:00Z', change, why: 'the words of a proposal', state: 'waiting' });
  // No time of its own: the file's is the proposal's, as both of the file's readers take it.
  proposal('p6', { id: 'p6', change, why: 'the words of a proposal', state: 'declined' });
  utimesSync(join(proposals, 'p6.json'), new Date('2026-09-25T00:00:00Z'), new Date('2026-09-25T00:00:00Z'));
  writeFileSync(join(proposals, 'torn.json'), '{"id":"p7","chan');
  writeFileSync(join(proposals, 'notes.txt'), 'the person\'s own note');
  return home;
}

function withAsks(body: (home: string) => void) {
  const home = asksWeek();
  try {
    body(home);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
}

function asksOf(home: string): Report {
  const read = readLogs(join(home, 'logs'), { since });
  return summarise(read.lines, { from: since, to: NOW, skipped: read.skipped, proposals: readProposals(join(home, 'proposals')) });
}

test('asks per session: the mean, the median, the 90th percentile and the share with none, by adapter and repository', () => {
  withAsks((home) => {
    const { asks } = asksOf(home);

    // s1 asked 3, s2 1, s3 and s4 none: the median is the two middles' mean, and the 90th the nearest rank.
    assert.deepEqual(
      [asks.refused, asks.sessions, asks.asks, asks.mean, asks.median, asks.p90, asks.none],
      [5, 4, 4, 1, 0.5, 3, 0.5]);
    assert.deepEqual(asks.byAdapter, [
      { name: 'claude-code', sessions: 2, asks: 3, mean: 1.5, median: 1.5, p90: 3, none: 0.5 },
      { name: 'claude-code-acp', sessions: 1, asks: 1, mean: 1, median: 1, p90: 1, none: 0 },
      { name: 'codex-acp', sessions: 1, asks: 0, mean: 0, median: 0, p90: 0, none: 1 },
    ]);
    assert.deepEqual(asks.byRepository, [
      { name: 'engine', sessions: 2, asks: 4, mean: 2, median: 2, p90: 3, none: 0 },
      { name: 'game', sessions: 2, asks: 0, mean: 0, median: 0, p90: 0, none: 1 },
    ]);
    // What was refused, over every ask in the period, and what the wire did not say is `(unsaid)`.
    assert.deepEqual(asks.byKind, [{ name: 'execute', count: 3 }, { name: 'edit', count: 1 }, { name: 'fetch', count: 1 }]);
    assert.deepEqual(asks.by, [{ name: '(unsaid)', count: 2 }, { name: 'rule', count: 2 }, { name: 'mode', count: 1 }]);
    assert.equal(asks.byTool.find((tool: Counted) => tool.name === 'Bash')?.count, 2);
    assert.equal(asks.unstarted, 1);
  });
});

/**
 * The asks line's parse table: what each shape of a `permission.refused` line counts as. A line is a
 * session's ask only when it names a session that started in the period; a name that is not a string is
 * unsaid, never a word from somewhere else.
 */
const ASK_LINES: [why: string, data: Record<string, unknown>, s1: number, unstarted: number, kind: string][] = [
  ['a whole line', { session: 's1', adapter: 'claude-code', tool: 'Bash', kind: 'execute', by: 'rule' }, 1, 0, 'execute'],
  ['a session nobody started in the period', { session: 's7', kind: 'execute' }, 0, 1, 'execute'],
  ['no session', { kind: 'edit' }, 0, 1, 'edit'],
  ['a session that is no string', { session: 1, kind: 'edit' }, 0, 1, 'edit'],
  ['a kind that is no string', { session: 's1', kind: 7 }, 1, 0, '(unsaid)'],
  ['no data at all', {}, 0, 1, '(unsaid)'],
];

test('each shape of an asks line counts as the parse table says', () => {
  const started = parseLine(line('2026-09-29T09:00:00.000Z', 'desktop', 'info', 'session.started', { session: 's1', adapter: 'claude-code', repository: 'engine' }));
  for (const [why, data, s1, unstarted, kind] of ASK_LINES) {
    const ask = parseLine(line('2026-09-29T09:10:00.000Z', 'desktop', 'info', 'permission.refused', data));
    const { asks } = summarise([started, ask], { from: since, to: NOW }) as Report;
    assert.deepEqual([asks.asks, asks.unstarted, asks.byKind], [s1, unstarted, [{ name: kind, count: 1 }]], why);
  }
});

test('a period with no session started has no statistics, and says none rather than zero', () => {
  const { asks } = summarise([], { from: since, to: NOW }) as Report;
  assert.deepEqual([asks.sessions, asks.mean, asks.median, asks.p90, asks.none], [0, null, null, null, null]);
});

test('the rule proposals: what waits for the person now, and those made in the period by week and state', () => {
  withAsks((home) => {
    const { proposals } = asksOf(home);

    assert.equal(proposals.waiting, 3);
    // Weeks begin on Monday, in UTC. A file that is not a proposal is left out, and one with no state
    // is `proposed`, as the file's other readers read it.
    assert.deepEqual(proposals.byWeek, [
      { week: '2026-09-21', made: 2, byState: [{ name: 'accepted', count: 1 }, { name: 'declined', count: 1 }] },
      { week: '2026-09-28', made: 3, byState: [{ name: 'waiting', count: 2 }, { name: 'proposed', count: 1 }] },
    ]);
  });
});

/**
 * The proposals' parse table: the file is the contract (`RuleProposals.cs`, `ruleproposals.ts`), and the
 * report reads only its id, its time and its state, by the same rules: a file with no id or no change is
 * not a proposal, and a state nobody knows is `proposed`.
 */
const PROPOSAL_FILES: [why: string, text: string, state: string | null][] = [
  ['a whole proposal', '{"id":"a1","proposed":"2026-09-29T08:00:00Z","change":{"action":"add"},"state":"waiting"}', 'waiting'],
  ['no state', '{"id":"a1","proposed":"2026-09-29T08:00:00Z","change":{"action":"add"}}', 'proposed'],
  ['a state nobody knows', '{"id":"a1","proposed":"2026-09-29T08:00:00Z","change":{},"state":"pending-review"}', 'proposed'],
  ['a state that is no string', '{"id":"a1","proposed":"2026-09-29T08:00:00Z","change":{},"state":3}', 'proposed'],
  ['no id', '{"proposed":"2026-09-29T08:00:00Z","change":{},"state":"waiting"}', null],
  ['an id of blanks', '{"id":"  ","change":{},"state":"waiting"}', null],
  ['no change', '{"id":"a1","proposed":"2026-09-29T08:00:00Z","state":"waiting"}', null],
  ['a change that is no object', '{"id":"a1","change":["add"],"state":"waiting"}', null],
  ['an array', '[{"id":"a1"}]', null],
  ['not JSON', 'not json at all', null],
];

test('each proposal file is read or left out as the file\'s other readers decide', () => {
  const folder = mkdtempSync(join(tmpdir(), 'daoris-proposals-'));
  try {
    for (const [why, text, state] of PROPOSAL_FILES) {
      writeFileSync(join(folder, 'one.json'), text);
      const read: { state: string; proposed: number }[] = readProposals(folder);
      assert.deepEqual(read.map((each) => each.state), state === null ? [] : [state], why);
    }
    assert.deepEqual(readProposals(join(folder, 'nowhere')), []);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
});

test('the text report says the asks and the proposals, and never a proposal\'s words', () => {
  withAsks((home) => {
    const report = asksOf(home);
    const text: string = render(report, join(home, 'logs'));

    assert.match(text, /^Asks$/m);
    assert.match(text, /sessions\s+4 started · 4 asks · mean 1 · median 0\.5 · 90th 3 · 50% with none/);
    assert.match(text, /by adapter\s+claude-code 2 sessions: mean 1\.5 · median 1\.5 · 90th 3 · 50% with none/);
    assert.match(text, /^\s+codex-acp 1 session: mean 0 · median 0 · 90th 0 · 100% with none$/m);
    assert.match(text, /by repository\s+engine 2 sessions: mean 2 · median 2 · 90th 3 · 0% with none/);
    assert.match(text, /decided by\s+\(unsaid\) 2 · rule 2 · mode 1/);
    assert.match(text, /1 ask from a session that did not start in this period/);
    assert.match(text, /^Rule proposals$/m);
    assert.match(text, /waiting now\s+3/);
    assert.match(text, /week of 2026-09-28\s+3 made — waiting 2 · proposed 1/);
    for (const words of ['the words of a proposal', 'secret-script', 'Bash(npm']) {
      assert.equal(text.includes(words), false, words);
      assert.equal(JSON.stringify(report).includes(words), false, words);
    }
  });
});

test('a period with no asks and no proposals says none in both sections', () => {
  const text: string = render(summarise([], { from: since, to: NOW, skipped: 0 }), 'logs');
  assert.match(text, /^Asks\n {2}none$/m);
  assert.match(text, /^Rule proposals\n {2}none$/m);
});

test('the runner reads the home\'s proposals beside its log', async () => {
  const home = asksWeek();
  try {
    const out: string[] = [];
    const io = { now: NOW, out: (text: string) => out.push(text), err: () => {} };

    assert.equal(await main(['--home', home, '--json'], io), 0);
    const report = JSON.parse(out.join('\n')) as Report;
    assert.equal(report.proposals.waiting, 3);
    assert.equal(report.asks.refused, 5);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
});

/**
 * WSSETUP11 (D124 §7.3): two weeks of set-ups and parks. Three set-up sessions in two repositories (one parks,
 * is answered and carried on by a second; one has not ended, and its turn is the older shape with no counts),
 * and the parks of the period: driven sessions, an intake, and one whose open its process never saw.
 */
function setupWeeks(): string {
  const home = mkdtempSync(join(tmpdir(), 'daoris-setups-'));
  const logs = join(home, 'logs');
  mkdirSync(logs);
  const at = (time: string, event: string, data: Record<string, unknown>) => line(time, 'desktop', 'info', event, data);
  const started = (time: string, session: string, kind: string, repository: string, workspace: string, setup?: boolean) =>
    at(time, 'session.started', { session, kind, adapter: 'claude-code-acp', repository, workspace, ...(setup ? { setup } : {}) });
  const parked = (time: string, session: string, kind: string | null, repository: string | null, workspace: string | null) =>
    at(time, 'session.parked', { session, kind, repository, workspace });
  writeFileSync(join(logs, '2026-09-24.desktop.jsonl'), `${[
    started('2026-09-24T09:00:00.000Z', 's7', 'driven', 'reports', 'work'),
    parked('2026-09-24T09:30:00.000Z', 's7', 'driven', 'reports', 'work'),
    parked('2026-09-25T10:00:00.000Z', 's9', null, null, null),
  ].join('\n')}\n`);
  writeFileSync(join(logs, '2026-09-29.desktop.jsonl'), `${[
    started('2026-09-29T09:00:00.000Z', 's1', 'driven', 'billing', 'work', true),
    at('2026-09-29T09:05:00.000Z', 'turn.ended', {
      session: 's1', stopReason: 'end_turn', turnMs: 300000,
      input: 12, cacheRead: 51100, cacheWrite: 16700, output: 80, calls: 3, used: 48000, size: 1000000,
    }),
    at('2026-09-29T09:06:00.000Z', 'turn.ended', {
      session: 's1', stopReason: 'end_turn', turnMs: 60000,
      input: 5, cacheRead: null, cacheWrite: null, output: 7, calls: 0, used: null, size: null,
    }),
    parked('2026-09-29T09:07:00.000Z', 's1', 'driven', 'billing', 'work'),
    at('2026-09-29T09:10:00.000Z', 'session.ended', { session: 's1', state: 'completed', seconds: 600 }),
    started('2026-09-29T09:11:00.000Z', 's2', 'driven', 'billing', 'work', true),
    at('2026-09-29T09:30:00.000Z', 'turn.ended', {
      session: 's2', stopReason: 'end_turn', turnMs: 1140000,
      input: 100, cacheRead: 1000, cacheWrite: 200, output: 50, calls: 10, used: 60000, size: 1000000,
    }),
    at('2026-09-29T09:31:00.000Z', 'session.ended', { session: 's2', state: 'completed', seconds: 1200 }),
    started('2026-09-29T10:00:00.000Z', 's3', 'driven', 'reports', 'work', true),
    at('2026-09-29T10:05:00.000Z', 'turn.ended', { session: 's3', stopReason: 'end_turn', turnMs: 300000 }),
  ].join('\n')}\n`);
  writeFileSync(join(logs, '2026-09-30.desktop.jsonl'), `${[
    started('2026-09-30T08:00:00.000Z', 's4', 'driven', 'reports', 'work'),
    parked('2026-09-30T08:10:00.000Z', 's4', 'driven', 'reports', 'work'),
    started('2026-09-30T08:20:00.000Z', 's5', 'intake', 'ask #a1', 'home'),
    parked('2026-09-30T08:30:00.000Z', 's5', 'intake', 'ask #a1', 'home'),
    // A conversation never parks, so it is no session a park is counted against.
    started('2026-09-30T08:40:00.000Z', 's6', 'chat', 'engine', 'work'),
  ].join('\n')}\n`);
  return home;
}

function withSetups(body: (report: Report, home: string) => void) {
  const home = setupWeeks();
  try {
    body(reportOf(home), home);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
}

test('each set-up\'s cost: how it stands, its minutes, its tool calls, its tokens as METER1 splits them, and its context', () => {
  withSetups(({ setups }) => {
    assert.equal(setups.started, 3);
    assert.deepEqual(setups.byState, [{ name: 'completed', count: 2 }, { name: 'not ended', count: 1 }]);
    assert.deepEqual(setups.sessions, [
      {
        session: 's1', repository: 'billing', workspace: 'work', state: 'completed', parked: 1, seconds: 600, calls: 3,
        input: 17, cacheRead: 51100, cacheWrite: 16700, output: 87, used: 48000, size: 1000000,
      },
      {
        session: 's2', repository: 'billing', workspace: 'work', state: 'completed', parked: 0, seconds: 1200, calls: 10,
        input: 100, cacheRead: 1000, cacheWrite: 200, output: 50, used: 60000, size: 1000000,
      },
      // A turn of the older shape says nothing it cannot know: every count is null, never zero.
      {
        session: 's3', repository: 'reports', workspace: 'work', state: 'not ended', parked: 0, seconds: null, calls: null,
        input: null, cacheRead: null, cacheWrite: null, output: null, used: null, size: null,
      },
    ]);
    // The total sums what was measured, and its context is the largest any set-up held.
    assert.deepEqual(setups.total, {
      sessions: 3, seconds: 1800, calls: 13, input: 117, cacheRead: 52100, cacheWrite: 16900, output: 137, used: 60000, size: 1000000,
    });
  });
});

test('parks per week by workspace, held against the sessions started there that can park', () => {
  withSetups(({ parks }) => {
    assert.equal(parks.parked, 5);
    assert.deepEqual(parks.byKind, [{ name: 'driven', count: 3 }, { name: '(unsaid)', count: 1 }, { name: 'intake', count: 1 }]);
    // Weeks begin on Monday, in UTC. A conversation's start is not counted, and a park whose workspace the
    // line does not say is `(unnamed)`.
    assert.deepEqual(parks.byWeek, [
      {
        week: '2026-09-21', parked: 2, started: 1,
        byWorkspace: [{ name: 'work', parked: 1, started: 1 }, { name: '(unnamed)', parked: 1, started: 0 }],
      },
      {
        week: '2026-09-28', parked: 3, started: 5,
        byWorkspace: [{ name: 'work', parked: 2, started: 4 }, { name: 'home', parked: 1, started: 1 }],
      },
    ]);
  });
});

/**
 * The set-up lines' parse table: a session is a set-up only when its start says `setup` as the boolean true,
 * and a turn's count is a count only when it is a number at or above zero. Anything else is unsaid, never a
 * zero and never a guess.
 */
const SETUP_LINES: [why: string, started: Record<string, unknown>, turn: Record<string, unknown>, setups: number, input: number | null][] = [
  ['a whole set-up', { session: 's1', kind: 'driven', setup: true }, { session: 's1', input: 5 }, 1, 5],
  ['no setup field', { session: 's1', kind: 'driven' }, { session: 's1', input: 5 }, 0, null],
  ['setup false', { session: 's1', kind: 'driven', setup: false }, { session: 's1', input: 5 }, 0, null],
  ['setup as a word', { session: 's1', kind: 'driven', setup: 'true' }, { session: 's1', input: 5 }, 0, null],
  ['setup as a number', { session: 's1', kind: 'driven', setup: 1 }, { session: 's1', input: 5 }, 0, null],
  ['a start with no session', { kind: 'driven', setup: true }, { session: 's1', input: 5 }, 0, null],
  ['a count that is a word', { session: 's1', setup: true }, { session: 's1', input: '5' }, 1, null],
  ['a count below zero', { session: 's1', setup: true }, { session: 's1', input: -1 }, 1, null],
  ['a count of zero', { session: 's1', setup: true }, { session: 's1', input: 0 }, 1, 0],
  ['another session\'s turn', { session: 's1', setup: true }, { session: 's2', input: 5 }, 1, null],
];

test('each shape of a set-up line counts as its parse table says', () => {
  for (const [why, started, turn, setups, input] of SETUP_LINES) {
    const lines = [
      parseLine(line('2026-09-29T09:00:00.000Z', 'desktop', 'info', 'session.started', started)),
      parseLine(line('2026-09-29T09:01:00.000Z', 'desktop', 'info', 'turn.ended', turn)),
    ];
    const report = summarise(lines, { from: since, to: NOW }) as Report;
    assert.equal(report.setups.started, setups, why);
    assert.equal(report.setups.sessions[0]?.input ?? null, input, why);
  }
});

test('the text report says each set-up\'s cost and the parks by week, and none where there are none', () => {
  withSetups((report, home) => {
    const text: string = render(report, join(home, 'logs'));

    assert.match(text, /^Set-ups$/m);
    assert.match(text, /started\s+3 — completed 2 · not ended 1/);
    // Anew is what was read fresh, written to the cache included; the rest was read from it.
    assert.match(text, /s1\s+billing \(work\)\s+completed · parked 1 · 10m 0s · 3 calls · 16\.7K anew · 51\.1K from cache · 87 out · context 48K of 1M/);
    assert.match(text, /s3\s+reports \(work\)\s+not ended · not measured/);
    assert.match(text, /total\s+3 sessions · 30m 0s · 13 calls · 17K anew · 52\.1K from cache · 137 out · largest context 60K of 1M/);
    assert.match(text, /^Parks$/m);
    assert.match(text, /kinds\s+driven 3 · \(unsaid\) 1 · intake 1/);
    assert.match(text, /week of 2026-09-28\s+3 parked · 5 started\n\s+work\s+2 parked · 4 started\n\s+home\s+1 parked · 1 started/);
  });

  const empty: string = render(summarise([], { from: since, to: NOW, skipped: 0 }), 'logs');
  assert.match(empty, /^Set-ups\n {2}none$/m);
  assert.match(empty, /^Parks\n {2}none$/m);
});

test('the arguments: a home, or an install\'s data folder, a number of days, JSON, and the local host', () => {
  assert.deepEqual(parseArguments(['--home', 'h']), { home: 'h', days: 7, json: false, service: null });
  assert.deepEqual(
    parseArguments(['--install', 'i', '--days', '30', '--json']),
    { home: join('i', 'data'), days: 30, json: true, service: null });
  // OUTCOME1: the host is named by its origin, and only this machine's own: the report reads the local host alone.
  for (const [given, origin] of [
    ['http://localhost:5177', 'http://localhost:5177'],
    ['http://localhost:5177/', 'http://localhost:5177'],
    ['http://127.0.0.1:5177', 'http://127.0.0.1:5177'],
    ['http://[::1]:5177', 'http://[::1]:5177'],
  ] as const) {
    assert.equal(parseArguments(['--home', 'h', '--service', given]).service, origin, given);
  }
  for (const [args, problem] of [
    [[], /--home <dir> or --install <dir>/],
    [['--home', 'h', '--install', 'i'], /one of --home and --install/],
    [['--home', 'h', '--days', '0'], /--days takes a whole number of days/],
    [['--home', 'h', '--days', 'week'], /--days takes a whole number of days/],
    [['--home'], /--home takes a folder/],
    [['--home', 'h', '--tail'], /`--tail` is not an option/],
    [['--home', 'h', '--service'], /--service takes the local host's address/],
    [['--home', 'h', '--service', 'localhost:5177'], /--service takes the local host's address/],
    [['--home', 'h', '--service', 'ftp://localhost:5177'], /--service takes the local host's address/],
    [['--home', 'h', '--service', 'https://daoris.example.org'], /only this machine's own host/],
    [['--home', 'h', '--service', 'http://192.168.1.4:5177'], /only this machine's own host/],
  ] as const) {
    assert.throws(() => parseArguments([...args]), problem);
  }
});

test('the runner prints the text, or the same as JSON, and says when a home holds no log', async () => {
  const home = week();
  try {
    const out: string[] = [];
    const err: string[] = [];
    const io = { now: NOW, out: (text: string) => out.push(text), err: (text: string) => err.push(text) };

    assert.equal(await main(['--home', home], io), 0);
    assert.match(out.join('\n'), /^Daoris usage, 2026-09-23 12:00 → 2026-09-30 12:00 UTC \(7 days\)/);

    out.length = 0;
    assert.equal(await main(['--home', home, '--json'], io), 0);
    assert.equal((JSON.parse(out.join('\n')) as Report).sessions.started, 3);

    assert.equal(await main(['--home', join(home, 'nowhere')], io), 2);
    assert.match(err.join('\n'), /no machine log in/);
    assert.equal(await main(['--days', '3'], io), 2);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
});

/**
 * OUTCOME1 (the future-directions review's §3, row 4): an outcome per quest, from the records. The quests and the session
 * records are the local host's (`/api/quests` and `/api/sessions`, closed ones included), the person's words are each
 * ask's (D133, `/api/asks`), and the parks are the machine log's. A report, never a gate (D54); a fact no store keeps is
 * said missing, never zero (D57, D143 point 3).
 */
type Group = {
  quests: number; done: number; declined: number;
  firstPass: { yes: number; of: number; unknown: number };
  carryOn: { quests: number; of: number; carryOns: number };
  answers: { total: number; over: number; unknown: number };
};
type OutcomeSession = {
  session: string; state: string; ending: string; adapter: string | null; account: string; teammate: boolean;
  created: string | null; parked: number | null;
};
type Outcome = {
  quest: string; repository: string; workspace: string | null; status: string; waiting: boolean;
  filed: string | null; closed: { how: string; at: string | null; afterMs: number | null; bySession?: boolean } | null;
  sessions: OutcomeSession[]; teammates: number; carryOns: number; strikes: number; limits: number;
  parks: number | null; answers: number | null; added: number | null;
  requirements: number; departures: number; held: boolean; accepted: string | null;
  firstPass: boolean | null; missing: { fact: string; why: string }[];
};
type Outcomes = {
  read: boolean; unread: string | null; quests: Outcome[];
  byRepository: (Group & { repository: string })[];
  byAgent: (Group & { agent: string; account: string | null })[];
};

/** Every word a person or an agent wrote on these records: none of it may reach the report, as text or as data. */
const WORDS = ['TITLE-WORDS', 'BODY-WORDS', 'NOTE-WORDS', 'QUOTE-WORDS', 'CHECK-WORDS', 'MET-WORDS', 'DEPARTED-WORDS',
  'ASKED-WORDS', 'ANSWER-WORDS', 'ADDED-WORDS', 'SESSION-NOTE-WORDS', 'SAID-WORDS'];

const quest = (id: string, from: string, to: string, status: string, filed: string, updated: string, more: Record<string, unknown> = {}) => ({
  id, from, to, title: `TITLE-WORDS ${id}`, body: `BODY-WORDS ${id}`, status, note: `NOTE-WORDS ${id}`,
  filed: `${filed}+00:00`, updated: `${updated}+00:00`, workspace: 'work', links: [], attachments: [], then: [], parent: null,
  conflicts: [], requirements: [], answers: [], held: false, accepted: null, ...more,
});
const requirement = { quote: 'QUOTE-WORDS', check: 'CHECK-WORDS' };

/** The host's quests: one per shape an outcome takes, and one wholly before the period. */
const QUESTS = [
  // Done by its one session, with nothing asked of the person: a first pass.
  quest('q-first', 'ask #a1', 'engine', 'Done', '2026-09-29T08:00:00', '2026-09-29T10:00:00', {
    requirements: [requirement], answers: [{ requirement: 1, met: 'MET-WORDS', departed: null, quote: null }],
  }),
  // Cut off by a limit, answered, carried on on another account, and done with a departure the person accepted.
  quest('q-carry', 'ask #a1', 'engine', 'Done', '2026-09-29T08:30:00', '2026-09-29T15:00:00', {
    requirements: [requirement], answers: [{ requirement: 1, met: null, departed: 'DEPARTED-WORDS', quote: 'QUOTE-WORDS' }],
    accepted: '2026-09-29T15:00:00+00:00',
  }),
  // Asked by a repository, filed before the period and still worked in it: a failure, an interrupted stop, a third run.
  quest('q-strikes', 'engine', 'game', 'Taken', '2026-09-20T08:00:00', '2026-09-21T08:00:00'),
  // Declined by its session; its ask keeps the person's words only from after it was published.
  quest('q-declined', 'ask #a2', 'game', 'Declined', '2026-09-29T09:00:00', '2026-09-29T09:45:00'),
  // Done with a departure still held for the person's yes; its ask is not on the host; a teammate's record stood down.
  quest('q-held', 'ask #gone', 'engine', 'Done', '2026-09-29T11:00:00', '2026-09-29T13:00:00', {
    requirements: [requirement], answers: [{ requirement: 1, met: null, departed: 'DEPARTED-WORDS', quote: 'QUOTE-WORDS' }],
    held: true,
  }),
  // Published and not yet taken: no session.
  quest('q-quiet', 'ask #a1', 'game', 'Open', '2026-09-30T09:00:00', '2026-09-30T09:00:00'),
  // Wholly before the period.
  quest('q-old', 'engine', 'game', 'Done', '2026-09-01T09:00:00', '2026-09-02T09:00:00'),
];

const record = (id: string, questId: string | null, adapter: string, state: string, created: string, updated: string, more: Record<string, unknown> = {}) => ({
  id, quest: questId, repository: 'engine', adapter, state, note: 'SESSION-NOTE-WORDS', evidence: null, transcript: null,
  created: `${created}+00:00`, updated: `${updated}+00:00`, workspace: 'work', kind: 'driven', harnessVersion: '2.1.0',
  profile: null, tree: null, took: false, answer: null, interrupted: false, limit: false, said: [{ id: 'w1', text: 'SAID-WORDS' }],
  ...more,
});

/** The host's session records, closed ones included, as `/api/sessions?includeClosed=true` answers this machine. */
const SESSIONS = [
  record('s1', 'q-first', 'claude-code', 'completed', '2026-09-29T08:05:00', '2026-09-29T09:55:00', { profile: 'work', took: true }),
  record('s2', 'q-carry', 'claude-code', 'failed', '2026-09-29T08:35:00', '2026-09-29T09:00:00', { profile: 'work', took: true, limit: true }),
  record('s3', 'q-carry', 'claude-code', 'completed', '2026-09-29T10:00:00', '2026-09-29T14:50:00', { profile: 'personal' }),
  record('s4', 'q-strikes', 'codex-acp', 'failed', '2026-09-20T08:10:00', '2026-09-20T09:00:00', { took: true }),
  record('s5', 'q-strikes', 'codex-acp', 'stopped', '2026-09-29T12:00:00', '2026-09-29T12:30:00', { interrupted: true }),
  record('s6', 'q-strikes', 'codex-acp', 'working', '2026-09-30T08:00:00', '2026-09-30T08:00:00'),
  record('s7', 'q-declined', 'claude-code-acp', 'declined', '2026-09-29T09:05:00', '2026-09-29T09:40:00', { profile: 'work' }),
  record('s8', 'q-held', 'claude-code', 'completed', '2026-09-29T11:05:00', '2026-09-29T12:55:00', { profile: 'personal', took: true }),
  // A teammate's record, synced down keyed `origin/id`: its account and its log are on its machine.
  record('m2/s9', 'q-held', 'claude-code', 'stood-down', '2026-09-29T11:06:00', '2026-09-29T11:07:00'),
  // A conversation and an intake serve no quest, and are no quest's sessions.
  record('s10', null, 'claude-code', 'completed', '2026-09-29T08:00:00', '2026-09-29T08:10:00', { kind: 'chat' }),
  record('s11', null, 'claude-code', 'completed', '2026-09-29T07:50:00', '2026-09-29T07:59:00', { kind: 'chat', ask: 'a1', repository: 'ask #a1' }),
  record('s12', 'q-old', 'claude-code', 'completed', '2026-09-01T09:05:00', '2026-09-01T10:00:00'),
];

const word = (kind: string, at: string, session: string | null, questId: string | null, text: string) =>
  ({ kind, text, at: `${at}+00:00`, session, quest: questId });

/** The host's asks, closed ones included, each with the person's words (DRIFT1a). */
const ASKS = [
  {
    id: 'a1', workspace: 'work', sentence: 'ASKED-WORDS', state: 'Open', tier: 'intake', quests: ['q-first', 'q-carry', 'q-quiet'],
    words: [
      word('asked', '2026-09-29T07:45:00', null, null, 'ASKED-WORDS'),
      word('answered', '2026-09-29T08:55:00', 's2', 'q-carry', 'ANSWER-WORDS'),
      word('added', '2026-09-29T11:00:00', 's3', 'q-carry', 'ADDED-WORDS'),
      // A word on another quest is not this one's.
      word('answered', '2026-09-29T12:00:00', 'sx', 'q-elsewhere', 'ANSWER-WORDS'),
    ],
  },
  {
    id: 'a2', workspace: 'work', sentence: 'ASKED-WORDS', state: 'Done', tier: 'intake', quests: ['q-declined'],
    words: [word('asked', '2026-09-29T08:50:00', null, null, 'ASKED-WORDS')], wordsKeptFrom: '2026-09-29T12:00:00+00:00',
  },
];

/** The machine log beside the records: the starts this period holds (not s4's, before it) and the parks. */
function outcomeHome(): string {
  const home = mkdtempSync(join(tmpdir(), 'daoris-outcomes-'));
  const logs = join(home, 'logs');
  mkdirSync(logs);
  const at = (time: string, event: string, data: Record<string, unknown>) => line(time, 'desktop', 'info', event, data);
  const started = (time: string, session: string, adapter: string) =>
    at(time, 'session.started', { session, kind: 'driven', adapter, repository: 'engine', workspace: 'work' });
  writeFileSync(join(logs, '2026-09-29.desktop.jsonl'), `${[
    started('2026-09-29T08:05:00.000Z', 's1', 'claude-code'),
    started('2026-09-29T08:35:00.000Z', 's2', 'claude-code'),
    at('2026-09-29T08:50:00.000Z', 'session.parked', { session: 's2', kind: 'driven', repository: 'engine', workspace: 'work' }),
    started('2026-09-29T09:05:00.000Z', 's7', 'claude-code-acp'),
    started('2026-09-29T10:00:00.000Z', 's3', 'claude-code'),
    started('2026-09-29T11:05:00.000Z', 's8', 'claude-code'),
    at('2026-09-29T11:30:00.000Z', 'session.parked', { session: 's8', kind: 'driven', repository: 'engine', workspace: 'work' }),
    started('2026-09-29T12:00:00.000Z', 's5', 'codex-acp'),
  ].join('\n')}\n`);
  writeFileSync(join(logs, '2026-09-30.desktop.jsonl'), `${started('2026-09-30T08:00:00.000Z', 's6', 'codex-acp')}\n`);
  return home;
}

const RECORDS = { quests: QUESTS, questsUnread: null, sessions: SESSIONS, sessionsUnread: null, asks: ASKS, asksUnread: null };

function outcomesFor(records: unknown = RECORDS): Outcomes {
  const home = outcomeHome();
  try {
    const read = readLogs(join(home, 'logs'), { since });
    return outcomesOf(records, read.lines, { from: since, to: NOW });
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
}

const byId = (outcomes: Outcomes) => new Map(outcomes.quests.map((each) => [each.quest, each]));

test('the outcomes hold every quest the period touched, by publish, close or a session, oldest first, and none other', () => {
  const outcomes = outcomesFor();
  assert.equal(outcomes.read, true);
  assert.equal(outcomes.unread, null);
  // q-strikes was published before the period and is in it by its sessions; q-old is wholly before it.
  assert.deepEqual(outcomes.quests.map((each) => each.quest), ['q-strikes', 'q-first', 'q-carry', 'q-declined', 'q-held', 'q-quiet']);
});

test('each quest\'s sessions, oldest first, and how each ended: a limit and an interrupted stop said apart from a failure', () => {
  const quests = byId(outcomesFor());
  const ended = (id: string) => quests.get(id)!.sessions.map((each) => `${each.session} ${each.ending} ${each.adapter} ${each.account}`);

  assert.deepEqual(ended('q-carry'), ['s2 limit claude-code work', 's3 completed claude-code personal']);
  assert.deepEqual(ended('q-strikes'), [
    's4 failed codex-acp (own sign-in)', 's5 interrupted codex-acp (own sign-in)', 's6 working codex-acp (own sign-in)',
  ]);
  // A teammate's record is listed and counted apart: its account is on its machine.
  assert.deepEqual(ended('q-held'), ['s8 completed claude-code personal', 'm2/s9 stood-down claude-code (a teammate\'s)']);
  assert.equal(quests.get('q-held')!.teammates, 1);
  assert.deepEqual(quests.get('q-quiet')!.sessions, []);
});

test('carry-ons, strikes and limits: a run after a cut-off is a carry-on, a limit is never a strike, and a teammate\'s is no strike here', () => {
  const quests = byId(outcomesFor());
  const counts = (id: string) => {
    const each = quests.get(id)!;
    return [each.carryOns, each.strikes, each.limits];
  };

  assert.deepEqual(counts('q-first'), [0, 0, 0]);
  assert.deepEqual(counts('q-carry'), [1, 0, 1]);
  // A failure, then an interrupted stop (D104), then a third run: two carry-ons and two strikes (D58 as amended).
  assert.deepEqual(counts('q-strikes'), [2, 2, 0]);
  assert.deepEqual(counts('q-held'), [0, 0, 0]);
});

test('how and when each closed, and the time from publish to close; a yes that replaced the done\'s moment is said missing', () => {
  const quests = byId(outcomesFor());

  assert.deepEqual(quests.get('q-first')!.closed, { how: 'done', at: '2026-09-29T10:00:00.000Z', afterMs: 2 * 3_600_000 });
  assert.deepEqual(quests.get('q-declined')!.closed, {
    how: 'declined', at: '2026-09-29T09:45:00.000Z', afterMs: 45 * 60_000, bySession: true,
  });
  assert.equal(quests.get('q-strikes')!.closed, null);
  assert.equal(quests.get('q-quiet')!.closed, null);

  const carried = quests.get('q-carry')!;
  assert.deepEqual(carried.closed, { how: 'done', at: null, afterMs: null });
  assert.equal(carried.accepted, '2026-09-29T15:00:00.000Z');
  assert.match(carried.missing.find((each) => each.fact === 'closed')?.why ?? '', /yes/);
});

test('departures and the person\'s yes (D133): accepted, or still held', () => {
  const quests = byId(outcomesFor());
  const said = (id: string) => {
    const each = quests.get(id)!;
    return [each.requirements, each.departures, each.held, each.accepted];
  };

  assert.deepEqual(said('q-first'), [1, 0, false, null]);
  assert.deepEqual(said('q-carry'), [1, 1, false, '2026-09-29T15:00:00.000Z']);
  assert.deepEqual(said('q-held'), [1, 1, true, null]);
});

test('answers are the person\'s words on this quest from its ask, and where no ask keeps them they are missing, never zero', () => {
  const quests = byId(outcomesFor());
  const words = (id: string) => [quests.get(id)!.answers, quests.get(id)!.added];
  const why = (id: string) => quests.get(id)!.missing.find((each) => each.fact === 'answers')?.why ?? '';

  assert.deepEqual(words('q-first'), [0, 0]);
  assert.deepEqual(words('q-carry'), [1, 1]);
  assert.deepEqual(words('q-quiet'), [0, 0]);
  assert.deepEqual(words('q-strikes'), [null, null]);
  assert.match(why('q-strikes'), /asked by a repository/);
  assert.deepEqual(words('q-declined'), [null, null]);
  assert.match(why('q-declined'), /from 2026-09-29 12:00 UTC, after the quest was published/);
  assert.deepEqual(words('q-held'), [null, null]);
  assert.match(why('q-held'), /ask #gone is not on the host/);
});

test('parks are the machine log\'s, for this machine\'s sessions whose start the period holds, and missing where one\'s does not', () => {
  const quests = byId(outcomesFor());

  assert.equal(quests.get('q-first')!.parks, 0);
  assert.equal(quests.get('q-carry')!.parks, 1);
  // The teammate's record is on its own machine's log, and does not make this machine's count unknown.
  assert.equal(quests.get('q-held')!.parks, 1);
  assert.deepEqual(quests.get('q-carry')!.sessions.map((each) => each.parked), [1, 0]);
  // s4 started before the period, so whether it parked is not known.
  assert.equal(quests.get('q-strikes')!.parks, null);
  assert.deepEqual(quests.get('q-strikes')!.sessions.map((each) => each.parked), [null, 0, 0]);
  assert.match(quests.get('q-strikes')!.missing.find((each) => each.fact === 'parks')?.why ?? '', /before the period/);
});

test('a first pass is done by its one working session, with no departure and nothing asked of the person; not done is not judged', () => {
  const quests = byId(outcomesFor());

  assert.equal(quests.get('q-first')!.firstPass, true);
  assert.equal(quests.get('q-carry')!.firstPass, false);
  // One working session (the teammate's stood down), but a departure and a park.
  assert.equal(quests.get('q-held')!.firstPass, false);
  for (const id of ['q-strikes', 'q-declined', 'q-quiet']) assert.equal(quests.get(id)!.firstPass, null, id);
});

/**
 * The first-pass table: a done quest with one working session, and what is known of whether the person was asked. Either
 * store saying the person was asked makes it no first pass; one saying nothing was asked makes it one; neither knowing is
 * not known, and is said missing.
 */
const FIRST_PASS: [why: string, answered: boolean | null, parked: boolean | null, firstPass: boolean | null][] = [
  ['both say nothing was asked', false, false, true],
  ['the ask says nothing was answered, the log does not know', false, null, true],
  ['the log says it never parked, the ask keeps no words', null, false, true],
  ['the ask holds an answer', true, null, false],
  ['the log holds a park', null, true, false],
  ['neither store knows', null, null, null],
];

test('each row of the first-pass table judges as it says', () => {
  for (const [why, answered, parked, firstPass] of FIRST_PASS) {
    const one = quest('q1', answered === null ? 'engine' : 'ask #a9', 'engine', 'Done', '2026-09-29T08:00:00', '2026-09-29T09:00:00');
    const run = record('r1', 'q1', 'claude-code', 'completed', '2026-09-29T08:01:00', '2026-09-29T08:59:00');
    const ask = { id: 'a9', words: answered ? [word('answered', '2026-09-29T08:30:00', 'r1', 'q1', 'ANSWER-WORDS')] : [] };
    const lines = parked === null ? [] : [
      parseLine(line('2026-09-29T08:01:00.000Z', 'desktop', 'info', 'session.started', { session: 'r1', kind: 'driven' })),
      ...(parked ? [parseLine(line('2026-09-29T08:20:00.000Z', 'desktop', 'info', 'session.parked', { session: 'r1' }))] : []),
    ];
    const records = { quests: [one], questsUnread: null, sessions: [run], sessionsUnread: null, asks: [ask], asksUnread: null };
    const [outcome] = (outcomesOf(records, lines, { from: since, to: NOW }) as Outcomes).quests;
    assert.equal(outcome!.firstPass, firstPass, why);
    assert.equal(outcome!.missing.some((each) => each.fact === 'firstPass'), firstPass === null, why);
  }
});

test('the sender names the ask, as the service\'s AskDesk.AskOf reads it, and a session\'s ending as the strikes read it', () => {
  for (const [sender, ask] of [
    ['ask #a1', 'a1'], ['ask #7f3c', '7f3c'], ['ask #', null], ['Ask #a1', null], ['engine', null], [null, null], [3, null],
  ] as const) {
    assert.equal(askOf(sender), ask, String(sender));
  }
  for (const [given, ending] of [
    [{ state: 'failed' }, 'failed'],
    [{ state: 'failed', limit: true }, 'limit'],
    [{ state: 'failed', limit: 'true' }, 'failed'],
    [{ state: 'stopped' }, 'stopped'],
    [{ state: 'stopped', interrupted: true }, 'interrupted'],
    [{ state: 'completed', limit: true }, 'completed'],
    [{ state: 'awaiting-person' }, 'awaiting-person'],
    [{}, '(unsaid)'],
  ] as const) {
    assert.equal(endingOf(given), ending, JSON.stringify(given));
  }
});

test('the roll-up by repository: quests, first passes of those done, carry-ons and answers, each with its count', () => {
  const { byRepository } = outcomesFor();

  assert.deepEqual(byRepository, [
    {
      repository: 'engine', quests: 3, done: 3, declined: 0,
      firstPass: { yes: 1, of: 3, unknown: 0 }, carryOn: { quests: 1, of: 3, carryOns: 1 }, answers: { total: 1, over: 2, unknown: 1 },
    },
    {
      repository: 'game', quests: 3, done: 0, declined: 1,
      firstPass: { yes: 0, of: 0, unknown: 0 }, carryOn: { quests: 1, of: 2, carryOns: 2 }, answers: { total: 0, over: 1, unknown: 2 },
    },
  ]);
});

test('the roll-up by agent and account, each quest under its first working session, and a quest with none last', () => {
  const { byAgent } = outcomesFor();

  assert.deepEqual(byAgent.map((each) => [each.agent, each.account, each.quests, each.done, each.firstPass, each.carryOn, each.answers]), [
    ['claude-code', 'work', 2, 2, { yes: 1, of: 2, unknown: 0 }, { quests: 1, of: 2, carryOns: 1 }, { total: 1, over: 2, unknown: 0 }],
    ['claude-code', 'personal', 1, 1, { yes: 0, of: 1, unknown: 0 }, { quests: 0, of: 1, carryOns: 0 }, { total: 0, over: 0, unknown: 1 }],
    ['claude-code-acp', 'work', 1, 0, { yes: 0, of: 0, unknown: 0 }, { quests: 0, of: 1, carryOns: 0 }, { total: 0, over: 0, unknown: 1 }],
    ['codex-acp', '(own sign-in)', 1, 0, { yes: 0, of: 0, unknown: 0 }, { quests: 1, of: 1, carryOns: 2 }, { total: 0, over: 0, unknown: 1 }],
    ['(no session)', null, 1, 0, { yes: 0, of: 0, unknown: 0 }, { quests: 0, of: 0, carryOns: 0 }, { total: 0, over: 1, unknown: 0 }],
  ]);
});

test('no score: each roll-up row carries its counts and nothing that folds them into one number', () => {
  const { byRepository, byAgent } = outcomesFor();
  for (const row of [...byRepository, ...byAgent]) {
    for (const key of Object.keys(row)) assert.equal(/score|rate|grade|rank/i.test(key), false, key);
  }
});

test('records the host did not answer are said, and the log\'s report still stands', () => {
  const unread = outcomesFor({
    quests: null, questsUnread: '/api/quests?includeClosed=true answered 500', sessions: [], sessionsUnread: null, asks: [], asksUnread: null,
  });
  assert.deepEqual([unread.read, unread.unread, unread.quests], [false, '/api/quests?includeClosed=true answered 500', []]);

  // Asks unread: every quest an ask asked has its answers missing, saying why, and the rest stands.
  const asksUnread = byId(outcomesFor({ ...RECORDS, asks: null, asksUnread: '/api/asks?includeClosed=true answered 404' }));
  assert.equal(asksUnread.get('q-first')!.answers, null);
  assert.match(asksUnread.get('q-first')!.missing.find((each) => each.fact === 'answers')?.why ?? '', /answered 404/);
  assert.equal(asksUnread.get('q-first')!.parks, 0);

  // An ask from a host before DRIFT1a answers no words.
  const wordless = byId(outcomesFor({ ...RECORDS, asks: [{ id: 'a1' }] }));
  assert.match(wordless.get('q-first')!.missing.find((each) => each.fact === 'answers')?.why ?? '', /without the person's words/);

  // No host named at all.
  const none = outcomesOf(null, [], { from: since, to: NOW }) as Outcomes;
  assert.deepEqual([none.read, none.quests, none.byRepository, none.byAgent], [false, [], [], []]);
  assert.match(none.unread ?? '', /--service/);
});

/** A host that answers the three routes from the fixture, and remembers what it was asked. */
function fakeHost(answers: Record<string, { status: number; body: unknown } | Error> = {}) {
  const asked: { url: string; method: string }[] = [];
  const fetch = async (url: string, init: { method?: string } = {}) => {
    asked.push({ url, method: init.method ?? 'GET' });
    const route = url.replace('http://localhost:5177', '');
    const fixture: Record<string, unknown> = {
      [RECORD_ROUTES.quests]: QUESTS, [RECORD_ROUTES.sessions]: SESSIONS, [RECORD_ROUTES.asks]: ASKS,
    };
    const answer = answers[route] ?? { status: 200, body: fixture[route] };
    if (answer instanceof Error) throw answer;
    return {
      ok: answer.status >= 200 && answer.status < 300,
      status: answer.status,
      text: async () => (typeof answer.body === 'string' ? answer.body : JSON.stringify(answer.body)),
    };
  };
  return { fetch, asked };
}

test('the host is read by three GETs of its routes, closed records included, and nothing else is asked of it', async () => {
  const host = fakeHost();
  const records = await readRecords('http://localhost:5177', { fetch: host.fetch });

  assert.deepEqual(RECORD_ROUTES, {
    quests: '/api/quests?includeClosed=true', sessions: '/api/sessions?includeClosed=true', asks: '/api/asks?includeClosed=true',
  });
  assert.deepEqual(host.asked.map((each) => each.method), ['GET', 'GET', 'GET']);
  assert.deepEqual(
    host.asked.map((each) => each.url).sort(),
    Object.values(RECORD_ROUTES).map((route) => `http://localhost:5177${route}`).sort());
  assert.equal(records.quests.length, QUESTS.length);
  assert.deepEqual([records.questsUnread, records.sessionsUnread, records.asksUnread], [null, null, null]);
});

test('a route that fails, answers no list or is unreachable is said by its route, never read as no records', async () => {
  const records = await readRecords('http://localhost:5177', {
    fetch: fakeHost({
      [RECORD_ROUTES.quests]: { status: 200, body: 'not json' },
      [RECORD_ROUTES.sessions]: { status: 200, body: { sessions: [] } },
      [RECORD_ROUTES.asks]: { status: 404, body: { error: 'no route' } },
    }).fetch,
  });
  assert.deepEqual([records.quests, records.sessions, records.asks], [null, null, null]);
  assert.match(records.questsUnread, /^\/api\/quests\?includeClosed=true answered something that is not JSON$/);
  assert.match(records.sessionsUnread, /^\/api\/sessions\?includeClosed=true answered no list$/);
  assert.match(records.asksUnread, /^\/api\/asks\?includeClosed=true answered 404$/);

  const refused = Object.assign(new Error('fetch failed'), { cause: { code: 'ECONNREFUSED' } });
  const down = await readRecords('http://localhost:5177', { fetch: fakeHost({ [RECORD_ROUTES.quests]: refused }).fetch });
  assert.match(down.questsUnread, /^\/api\/quests\?includeClosed=true did not answer: ECONNREFUSED$/);
});

test('the runner reads the host it is named and reports the outcomes beside the log, as text or JSON', async () => {
  const home = outcomeHome();
  try {
    const host = fakeHost();
    const out: string[] = [];
    const io = { now: NOW, out: (text: string) => out.push(text), err: () => {}, fetch: host.fetch };

    assert.equal(await main(['--home', home, '--service', 'http://localhost:5177', '--json'], io), 0);
    const report = JSON.parse(out.join('\n')) as { outcomes: Outcomes };
    assert.equal(report.outcomes.read, true);
    assert.equal(report.outcomes.quests.length, 6);

    // Without a host the outcomes are not read, and nothing is asked of any host.
    out.length = 0;
    const idle = fakeHost();
    assert.equal(await main(['--home', home, '--json'], { ...io, fetch: idle.fetch }), 0);
    assert.equal((JSON.parse(out.join('\n')) as { outcomes: Outcomes }).outcomes.read, false);
    assert.deepEqual(idle.asked, []);

    // A host that does not answer is said, and the log's report still prints: exit 0.
    out.length = 0;
    const down = fakeHost({ [RECORD_ROUTES.quests]: new Error('fetch failed') });
    assert.equal(await main(['--home', home, '--service', 'http://localhost:5177'], { ...io, fetch: down.fetch }), 0);
    assert.match(out.join('\n'), /^Outcomes\n {2}not read — \/api\/quests\?includeClosed=true did not answer: fetch failed$/m);
    assert.match(out.join('\n'), /^Lifecycle$/m);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
});

test('the text report says each quest\'s outcome, its sessions and what is not known, then the roll-ups with their counts', () => {
  const home = outcomeHome();
  try {
    const read = readLogs(join(home, 'logs'), { since });
    const text: string = render(summarise(read.lines, { from: since, to: NOW, skipped: read.skipped, records: RECORDS }), join(home, 'logs'));

    assert.match(text, /^Outcomes\n {2}quests {9}6 — done 3 · declined 1 · open 1 · taken 1$/m);
    assert.match(text, /^ {2}q-first {5}engine {2}done in 2h 0m · first pass · 1 session · answers 0\n {22}s1 completed \[claude-code · work\]$/m);
    assert.match(text, new RegExp([
      '^  q-carry     engine  done · not first pass · 1 departure, accepted 6h 30m after publish · 2 sessions · 1 carry-on',
      ' · 1 limit · parked 1 · answers 1 · added 1$',
    ].join(''), 'm'));
    assert.match(text, /^ {22}s2 limit \[claude-code · work\] → s3 completed \[claude-code · personal\]$/m);
    assert.match(text, /^ {22}not known: closed — the person's yes is its last moment/m);
    assert.match(text, /^ {2}q-strikes {3}game {4}taken · 3 sessions · 2 carry-ons · 2 strikes$/m);
    assert.match(text, /^ {22}not known: parks — the period read holds no start for s4 .*; answers — asked by a repository/m);
    assert.match(text, /^ {2}q-declined {2}game {4}declined in 45m 0s by its session · 1 session$/m);
    assert.match(text, /q-held {6}engine {2}done in 2h 0m · not first pass · 1 departure, held for the person's yes · 2 sessions \(1 a teammate's\) · parked 1$/m);
    assert.match(text, /^ {2}q-quiet {5}game {4}open · 0 sessions · answers 0$/m);
    assert.match(text, new RegExp([
      '^  by repository\\n',
      '    engine  3 quests · done 3 · first pass 1 of 3 done · carried on 1 of 3 run here \\(1 carry-on\\)',
      ' · answers 1 over 2 quests \\(0\\.5 a quest\\), 1 not kept\\n',
      '    game    3 quests · done 0 · declined 1 · first pass: none done · carried on 1 of 2 run here \\(2 carry-ons\\)',
      ' · answers 0 over 1 quest \\(0 a quest\\), 2 not kept$',
    ].join(''), 'm'));
    assert.match(text, /^ {2}by agent and account, each quest under its first session\n {4}claude-code · work {9}2 quests · done 2 · first pass 1 of 2 done/m);
    assert.match(text, /^ {4}codex-acp · \(own sign-in\) {2}1 quest · done 0 · first pass: none done · carried on 1 of 1 run here \(2 carry-ons\) · answers not kept \(1\)$/m);
    assert.match(text, /^ {4}\(no session\) {15}1 quest · done 0 · first pass: none done · carried on: no session here · answers 0 over 1 quest \(0 a quest\)$/m);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
});

test('the outcomes never carry anyone\'s words: no title, body, note, requirement, answer or word of the ask', async () => {
  const home = outcomeHome();
  try {
    const out: string[] = [];
    const io = { now: NOW, out: (text: string) => out.push(text), err: () => {}, fetch: fakeHost().fetch };
    await main(['--home', home, '--service', 'http://localhost:5177'], io);
    await main(['--home', home, '--service', 'http://localhost:5177', '--json'], io);
    const printed = out.join('\n');
    assert.match(printed, /q-carry/);
    for (const words of WORDS) assert.equal(printed.includes(words), false, words);
  } finally {
    rmSync(home, { recursive: true, force: true });
  }
});
