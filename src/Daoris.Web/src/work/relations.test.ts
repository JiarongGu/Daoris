import { describe, expect, it } from 'vitest';
import type { Quest, Session } from '../api';
import { relationsOf } from './relations';

// SESS1: where a session's work came from and what it caused, beyond the chain — from what the
// records declare (`publishedBy`, `awaits`), never guessed from the times.

const quest = (over: Partial<Quest> & { id: string }): Quest => ({
  from: 'engine', to: 'game', title: over.id, body: '', status: 'Open', filed: '2026-09-29T01:00:00Z', updated: '2026-09-29T01:00:00Z',
  ...over,
});
const session = (over: Partial<Session> & { id: string }): Session => ({
  quest: null, repository: 'engine', adapter: 'claude-code-acp', state: 'completed', kind: 'driven',
  created: '2026-09-29T01:00:00Z', updated: '2026-09-29T02:00:00Z', ...over,
});

describe('who a session worked with', () => {
  it('names the session that asked for its quest, found or not', () => {
    const asker = session({ id: 'a1', repository: 'game' });
    const mine = quest({ id: 'q1', publishedBy: 'a1' });
    expect(relationsOf(session({ id: 's1', quest: 'q1' }), mine, [mine], [asker]).askedBy)
      .toEqual({ id: 'a1', session: asker });
    // Another machine's session this one holds no record of is still named.
    expect(relationsOf(session({ id: 's1', quest: 'q1' }), mine, [mine], []).askedBy).toEqual({ id: 'a1', session: null });
    // A quest a person published, or one it published itself, has no asker to name.
    expect(relationsOf(session({ id: 's1' }), quest({ id: 'q2' }), [], []).askedBy).toBeNull();
    expect(relationsOf(session({ id: 's1' }), quest({ id: 'q3', publishedBy: 's1' }), [], []).askedBy).toBeNull();
  });

  it('lists the quests it published, oldest first, and marks the one its quest waited on', () => {
    const question = quest({ id: 'q9', to: 'iothub', publishedBy: 's1', status: 'Done', note: 'the id is 5fc1', filed: '2026-09-29T01:30:00Z' });
    const other = quest({ id: 'q8', to: 'report-db', publishedBy: 's1', filed: '2026-09-29T01:10:00Z' });
    const someoneElse = quest({ id: 'q7', publishedBy: 's2' });
    const mine = quest({ id: 'q1', status: 'Taken', awaits: 'q9' });

    const found = relationsOf(session({ id: 's1', quest: 'q1' }), mine, [mine, question, other, someoneElse], []);
    expect(found.asked.map((row) => [row.quest.id, row.question])).toEqual([['q8', false], ['q9', true]]);
    expect(found.resumedAfter).toBeNull();
  });

  it('says it carried on once a question an earlier session asked was answered', () => {
    const question = quest({ id: 'q9', publishedBy: 's0', status: 'Done', note: 'use the conveyor container' });
    const mine = quest({ id: 'q1', status: 'Taken', awaits: 'q9' });

    const found = relationsOf(session({ id: 's1', quest: 'q1' }), mine, [mine, question], []);
    expect(found.resumedAfter).toBe(question);
    expect(found.asked).toEqual([]);
    // Still open, nothing has carried on from it.
    const open = { ...question, status: 'Open' as const };
    expect(relationsOf(session({ id: 's1', quest: 'q1' }), mine, [mine, open], []).resumedAfter).toBeNull();
  });

  it('has nothing to say for a session that asked and was asked nothing', () => {
    const found = relationsOf(session({ id: 's1' }), null, [quest({ id: 'q1' })], []);
    expect(found).toEqual({ askedBy: null, resumedAfter: null, asked: [] });
  });
});
