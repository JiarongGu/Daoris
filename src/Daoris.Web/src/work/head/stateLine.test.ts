import { describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import type { SessionGrouping } from '../groups';
import type { DockLine } from '../waits/dock';
import { stateLineOf, stateLineSaid } from './stateLine';

// The head's state line: the driver's reader's group and reason with its cause and age, never the record's state word; an open
// wait the person settles reads Waiting on you whatever the record says, and a disagreement between the dock and the reader is told.

const t = i18n.t.bind(i18n);
const NOW = new Date('2026-11-18T10:00:00Z');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', door: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-11-18T08:00:00Z', updated: '2026-11-18T09:00:00Z', ...over,
});

const row = (over: Partial<SessionGrouping> = {}): SessionGrouping => ({
  session: 's1a2b3c4', group: 'done', shown: 'completed', archived: false, teammate: false, ...over,
});

const accept: DockLine = {
  key: 'step:r1/accept', since: '2026-11-18T09:22:00Z',
  wait: { kind: 'step', run: 'r1', node: 'accept', why: 'accept', role: 'reviewer', since: '2026-11-18T09:22:00Z' },
};
const rule: DockLine = { key: 'rule:p1', since: '2026-11-18T09:40:00Z', wait: { kind: 'rule', id: 'p1' } };

describe('stateLineOf', () => {
  it('reads Waiting on you from the reader, never the record: a completed record whose run waits on its Accept', () => {
    const grouping = row({
      group: 'needs-you', state: 'needs-you', reason: 'review',
      waits: [{ run: 'r1', node: 'accept', why: 'accept', role: 'reviewer', since: '2026-11-18T09:22:00Z' }],
    });
    const line = stateLineOf({ session: session(), grouping, docked: [accept, rule] });
    expect(line).toMatchObject({ kind: 'needs-you', since: '2026-11-18T09:22:00Z', more: 1, disagrees: null });
    const said = stateLineSaid(t, line, NOW);
    expect(said.word).toBe('Waiting on you');
    expect(said.rest).toContain('38m');
    expect(said.rest).toContain('Accept, and 1 more');
    expect(`${said.word} ${said.rest}`).not.toMatch(/completed/i);
  });

  it('reads Waiting on you where the dock holds a wait the reader does not place, and names the disagreement', () => {
    const line = stateLineOf({ session: session({ state: 'working' }), grouping: row({ group: 'working', shown: 'working' }), docked: [rule] });
    expect(line.kind).toBe('needs-you');
    expect(line.disagrees).toEqual({ group: 'working', reason: '', docked: 'rule' });
  });

  it('names the disagreement where the reader places a docked kind under Needs you and nothing is docked', () => {
    const line = stateLineOf({ session: session(), grouping: row({ group: 'needs-you', reason: 'question' }), docked: [] });
    expect(line.kind).toBe('needs-you');
    expect(line.disagrees).toEqual({ group: 'needs-you', reason: 'question', docked: '' });
    // A reason no dock kind answers yet (its press is the head's) is no disagreement.
    expect(stateLineOf({ session: session(), grouping: row({ group: 'needs-you', reason: 'policy-held' }), docked: [] }).disagrees).toBeNull();
  });

  it('reads its line\'s standing where another part decides it (`LineStandings`)', () => {
    const grouping = row({ group: 'done', line: { state: 'working', group: 'working', reason: 'working', by: 'other', role: 'part' } });
    expect(stateLineOf({ session: session(), grouping, docked: [] }).kind).toBe('working');
  });

  it('says working with its current act, done with how it ended and when, failed with why, and a step it waits on', () => {
    const working = stateLineOf({
      session: session({ state: 'working' }),
      grouping: row({ group: 'working', shown: 'working', calling: { title: 'npm test', since: '2026-11-18T09:58:00Z' } }),
      docked: [],
    });
    expect(stateLineSaid(t, working, NOW)).toEqual({ word: 'Working', rest: '2m · npm test' });
    const done = stateLineOf({ session: session(), grouping: row(), docked: [] });
    expect(stateLineSaid(t, done, NOW).word).toBe('Done');
    expect(stateLineSaid(t, done, NOW).rest).toMatch(/^completed · /);
    const failed = stateLineOf({ session: session({ state: 'failed' }), grouping: row({ group: 'failed', reason: 'transient', shown: 'failed' }), docked: [] });
    expect(stateLineSaid(t, failed, NOW)).toMatchObject({ word: 'Failed', rest: expect.stringContaining('ended past its retries') });
    const step = stateLineOf({
      session: session({ state: 'awaiting-step' }), grouping: row({ group: 'working', shown: 'awaiting-step', awaits: 'checks' }), docked: [],
    });
    expect(stateLineSaid(t, step, NOW)).toMatchObject({ word: 'Waiting on a step', rest: expect.stringContaining('checks') });
  });

  it('says it is reading before the reader answers, never the record\'s word', () => {
    const line = stateLineOf({ session: session(), grouping: null, docked: [] });
    expect(stateLineSaid(t, line, NOW).word).toBe('Reading its state');
  });
});
