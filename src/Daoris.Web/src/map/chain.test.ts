import { describe, expect, it } from 'vitest';
import type { Quest, QuestStep, Session } from '../api';
import { buildChain } from './chain';

// MAP1 (D67 §3, `docs/2026-09-23-map-design.md` §2): how the higher loop carried one piece of work —
// the ask it came from, the quests before and after it, the steps still to come, and every session
// that ran each one. Read from records the service already keeps; no model, no machine path.

const quest = (id: string, extra: Partial<Quest> = {}): Quest => ({
  id, from: 'game', to: 'engine', title: `quest ${id}`, body: '', status: 'Open',
  filed: '2026-09-23T00:00:00Z', updated: '2026-09-23T00:00:00Z', ...extra,
});

const session = (id: string, questId: string, created: string, extra: Partial<Session> = {}): Session => ({
  id, quest: questId, repository: 'engine', adapter: 'claude-code', state: 'completed',
  created, updated: created, ...extra,
});

const step = (to: string, title: string): QuestStep => ({ to, title, body: '' });

describe('a quest chain', () => {
  it('is the quest alone when nothing came before or after it', () => {
    const chain = buildChain('q1', [quest('q1')], []);

    expect(chain.map((s) => s.kind)).toEqual(['quest']);
  });

  it('is nothing at all for a quest the page does not hold', () => {
    expect(buildChain('gone', [quest('q1')], [])).toEqual([]);
  });

  /** A step publishes the next with `parent` set and the rest of the list carried on (D65 §4). */
  it('walks up through the parents and down through the published steps, in order', () => {
    const quests = [
      quest('c', { parent: 'b', filed: '2026-09-23T03:00:00Z' }),
      quest('a', { filed: '2026-09-23T01:00:00Z' }),
      quest('b', { parent: 'a', filed: '2026-09-23T02:00:00Z' }),
    ];

    const chain = buildChain('b', quests, []);

    expect(chain.map((s) => (s.kind === 'quest' ? [s.quest.id, s.current] : s.kind))).toEqual([
      ['a', false], ['b', true], ['c', false],
    ]);
  });

  it('starts at the ask its first quest was published by', () => {
    const chain = buildChain('q1', [quest('q1', { from: 'ask #abc123' })], []);

    expect(chain[0]).toEqual({ kind: 'ask', id: 'abc123' });
    expect(chain[1]).toMatchObject({ kind: 'quest', current: true });
  });

  it('ends with the steps the last quest will still publish, as the asker wrote them', () => {
    const chain = buildChain('q1', [
      quest('q1', { then: [step('game', 'Verify {parent}'), step('engine', 'Report back')] }),
    ], []);

    expect(chain.slice(1)).toEqual([
      { kind: 'pending', step: step('game', 'Verify {parent}') },
      { kind: 'pending', step: step('engine', 'Report back') },
    ]);
  });

  /** Once a step is published, what is still to come rides on IT, not on the quest before. */
  it('takes what is still to come from the last published step, never twice', () => {
    const chain = buildChain('q1', [
      quest('q1', { then: [step('game', 'Verify'), step('engine', 'Report')] }),
      quest('q2', { parent: 'q1', to: 'game', title: 'Verify', then: [step('engine', 'Report')] }),
    ], []);

    expect(chain.map((s) => (s.kind === 'pending' ? `pending ${s.step.title}` : s.kind))).toEqual([
      'quest', 'quest', 'pending Report',
    ]);
  });

  it('hangs every session that ran a quest on that quest, oldest attempt first', () => {
    const chain = buildChain('q1', [quest('q1')], [
      session('s2', 'q1', '2026-09-23T02:00:00Z', { state: 'failed' }),
      session('s1', 'q1', '2026-09-23T01:00:00Z', { state: 'failed' }),
      session('s3', 'q1', '2026-09-23T03:00:00Z'),
      session('other', 'q9', '2026-09-23T01:00:00Z'),
    ]);

    expect(chain[0]!.kind === 'quest' && chain[0]!.sessions.map((s) => s.id)).toEqual(['s1', 's2', 's3']);
  });

  /** Records are data someone else wrote: a loop in them must not hang the page. */
  it('stops at a parent loop instead of walking it forever', () => {
    const chain = buildChain('a', [quest('a', { parent: 'b' }), quest('b', { parent: 'a' })], []);

    expect(chain.filter((s) => s.kind === 'quest')).toHaveLength(2);
  });

  /** A parent this page does not hold (closed out of view, another circle) is named, not invented. */
  it('names a parent it cannot see rather than dropping the link', () => {
    const chain = buildChain('q2', [quest('q2', { parent: 'q1' })], []);

    expect(chain[0]).toEqual({ kind: 'unseen', id: 'q1' });
  });
});
