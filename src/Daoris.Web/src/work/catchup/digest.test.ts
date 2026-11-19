import { describe, expect, it } from 'vitest';
import { toTurns } from '../conversation/conversation';
import type { SessionEvent } from '../conversation/conversationTypes';
import { lifetimesIn } from '../beside/besides';
import { type DigestLine, digestOf } from './digest';
import { isAfter, isForward, type ReadMark, seqOf } from './readMark';
import { CHAT, CHAT_MARK, DRIVEN, DRIVEN_MARK, MOVED, MOVED_MARK } from './catchup.fixtures';

// *Since you left*: each line of the digest of three recorded sessions, checked against what their records hold after the mark.

const digest = (events: SessionEvent[], part: string, mark: ReadMark, more: Partial<Parameters<typeof digestOf>[0]> = {}) =>
  digestOf({ turns: toTurns(events).turns, part, mark, lifetimes: lifetimesIn(events), ...more });

/** What the record holds after the mark, read from its events alone, never from the digest's own reading. */
function fromRecord(events: SessionEvent[], part: string, mark: ReadMark) {
  const after = events.filter((event) => isAfter(mark, part, event));
  const calls = [...new Map(after.filter((event) => event.kind === 'tool' && event.title).map((event) => [event.id, event])).values()];
  const commands = calls.filter((event) => event.toolKind === 'execute');
  const ends = (kind: string) => after.filter((event) => event.kind === kind && event.end);
  return {
    turns: after.filter((event) => event.kind === 'turn').length,
    files: new Set(calls.filter((event) => event.toolKind === 'edit').flatMap((event) => event.locations ?? [])).size,
    commands: commands.length,
    failed: commands.filter((event) => event.status === 'failed').map((event) => event.title),
    children: [after.filter((event) => event.kind === 'child' && !event.end).length, ends('child').length],
    tasks: [after.filter((event) => event.kind === 'task' && !event.end).length, ends('task').length],
    words: after.filter((event) => event.kind === 'message').at(-1)?.text?.split('\n')[0],
  };
}

/** Every line's jump lands on a row the conversation holds, and on one after the mark. */
function landsAfter(lines: readonly DigestLine[], events: SessionEvent[], part: string, mark: ReadMark) {
  const { turns } = toTurns(events);
  const keys = new Set(turns.flatMap((turn) => [turn.ask?.key, ...turn.items.map((item) => item.key)]));
  for (const line of lines) {
    if (!('to' in line) || !line.to) continue;
    expect(keys.has(line.to), `${line.kind} jumps to ${line.to}`).toBe(true);
    const seq = seqOf(line.to)!;
    expect(isAfter(mark, part, { seq, at: events.find((event) => event.seq === seq)!.at }), `${line.kind} lands after the mark`).toBe(true);
  }
}

const line = <K extends DigestLine['kind']>(lines: readonly DigestLine[], kind: K) =>
  lines.find((each) => each.kind === kind) as Extract<DigestLine, { kind: K }> | undefined;

describe('the digest at the read mark', () => {
  it('counts a conversation read through its first turn: two turns, two files, three commands with the one that failed, its last words', () => {
    const read = digest(CHAT, 's-chat', CHAT_MARK)!;
    const record = fromRecord(CHAT, 's-chat', CHAT_MARK);

    expect(read.lines).toEqual([
      { kind: 'turns', count: 2, to: 'e6' },
      { kind: 'files', count: 2, to: 'e8' },
      { kind: 'commands', count: 3, failed: ['npm test'], to: 'e7' },
      { kind: 'words', text: 'Pushed fix-login.', to: 'e16' },
    ]);
    expect([line(read.lines, 'turns')!.count, line(read.lines, 'files')!.count]).toEqual([record.turns, record.files]);
    expect([line(read.lines, 'commands')!.count, line(read.lines, 'commands')!.failed]).toEqual([record.commands, record.failed]);
    expect(line(read.lines, 'words')!.text).toBe(record.words);
    expect(read.turn).toBe(toTurns(CHAT).turns.find((turn) => turn.ask?.key === 'e6')!.key);
    landsAfter(read.lines, CHAT, 's-chat', CHAT_MARK);
  });

  it('counts a driven session read as far as its ask: its subagents and task and how they ended, the steps passed and what waits', () => {
    const read = digest(DRIVEN, 's-driven', DRIVEN_MARK, { steps: { passed: 2, failed: [] }, waiting: 1 })!;
    const record = fromRecord(DRIVEN, 's-driven', DRIVEN_MARK);

    expect(read.lines).toEqual([
      { kind: 'turns', count: 1, to: 'e2' },
      { kind: 'commands', count: 1, failed: [], to: 'e4' },
      { kind: 'children', started: 2, ended: 2, failed: 0, to: 'e2' },
      { kind: 'tasks', started: 1, ended: 1, failed: 0, to: 'e4' },
      { kind: 'steps', passed: 1, failed: [] },
      { kind: 'waits', open: 1, asked: 0, answered: 0 },
      { kind: 'words', text: 'The refresh now checks expiry first.', to: 'e10' },
    ]);
    expect(line(read.lines, 'turns')!.count).toBe(record.turns);
    expect(line(read.lines, 'commands')!.count).toBe(record.commands);
    expect([line(read.lines, 'children')!.started, line(read.lines, 'children')!.ended]).toEqual(record.children);
    expect([line(read.lines, 'tasks')!.started, line(read.lines, 'tasks')!.ended]).toEqual(record.tasks);
    expect(line(read.lines, 'words')!.text).toBe(record.words);
    landsAfter(read.lines, DRIVEN, 's-driven', DRIVEN_MARK);
  });

  it('counts the whole of a newer part when the mark is in the part before it, from the part boundary on', () => {
    const read = digest(MOVED, 's-moved', MOVED_MARK)!;
    const record = fromRecord(MOVED, 's-moved', MOVED_MARK);

    expect(read.lines).toEqual([
      { kind: 'turns', count: 2, to: 'e1' },
      { kind: 'commands', count: 2, failed: ['npm run lint'], to: 'e4' },
      { kind: 'words', text: 'Opened the pull request.', to: 'e9' },
    ]);
    expect([line(read.lines, 'turns')!.count, line(read.lines, 'commands')!.count]).toEqual([record.turns, record.commands]);
    expect(line(read.lines, 'commands')!.failed).toEqual(record.failed);
    expect(line(read.lines, 'words')!.text).toBe(record.words);
    expect(read.turn).toBe(toTurns(MOVED).turns[0]!.key);
    landsAfter(read.lines, MOVED, 's-moved', MOVED_MARK);
  });

  it('is none where nothing came after the mark, or the mark is a later part\'s', () => {
    const end = CHAT[CHAT.length - 1]!;
    expect(digest(CHAT, 's-chat', { ...CHAT_MARK, seq: end.seq, at: end.at })).toBeNull();
    expect(digest(MOVED, 's-moved', { part: 's-after', seq: 1, at: '2026-11-19T12:00:00Z', read: '2026-11-19T12:00:00Z' })).toBeNull();
  });
});

describe('the read mark', () => {
  it('moves only forward: a later event of its part, or another part\'s later event, never back and never the same', () => {
    const was = { part: 's1', seq: 12, at: '2026-11-19T10:00:00Z' };
    expect(isForward(null, was)).toBe(true);
    expect(isForward(was, { ...was, seq: 13 })).toBe(true);
    expect(isForward(was, { ...was, seq: 12 })).toBe(false);
    expect(isForward(was, { ...was, seq: 9, at: '2026-11-19T11:00:00Z' })).toBe(false);
    expect(isForward(was, { part: 's2', seq: 1, at: '2026-11-19T10:00:01Z' })).toBe(true);
    expect(isForward(was, { part: 's0', seq: 900, at: '2026-11-19T09:59:59Z' })).toBe(false);
  });
});
