import { describe, expect, it } from 'vitest';
import '../i18n';
import type { Quest, Session } from '../api';
import { browserDrivers } from './browserDrivers';

/**
 * BRW8: who is driving Daoris's browser, named the way the rail names a session — its repository and
 * what it is for — from the ids the driver answers and the records the page already holds.
 */
const session = (over: Partial<Session>): Session => ({
  id: 's1a2b3c4', repository: 'engine', adapter: 'claude-code', state: 'working',
  created: '2026-09-30T00:00:00Z', updated: '2026-09-30T00:01:00Z', ...over,
});

const QUEST = { id: 'q1', title: 'Read the ticket', status: 'Taken' } as Quest;

describe('browserDrivers', () => {
  it('names nobody when no session was handed the browser', () => {
    expect(browserDrivers([], [session({})], [])).toEqual([]);
    expect(browserDrivers(undefined, [session({})], [])).toEqual([]);
  });

  it("names a session by its repository and its quest's title", () => {
    expect(browserDrivers(['s1a2b3c4'], [session({ quest: 'q1' })], [QUEST]))
      .toEqual([{ id: 's1a2b3c4', name: 'engine · Read the ticket' }]);
  });

  it('names a conversation by its kind, and each of two in the order the driver gave', () => {
    const chat = session({ id: 'c0ffee00', repository: 'game', kind: 'chat' });
    expect(browserDrivers(['c0ffee00', 's1a2b3c4'], [session({ quest: 'q1' }), chat], [QUEST]).map((d) => d.name))
      .toEqual(['game · conversation', 'engine · Read the ticket']);
  });

  it('names a session the page has no record of yet by its id, rather than leaving it out', () => {
    expect(browserDrivers(['d00dfeed'], [], [])).toEqual([{ id: 'd00dfeed', name: 'session d00dfeed' }]);
  });
});
