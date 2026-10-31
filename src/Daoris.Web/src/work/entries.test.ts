import { describe, expect, it } from 'vitest';
import {
  askLines, askOfEntry, entryFacts, entryOfSession, listedSessions, nameOfEntry, opensOf, partIds, placeOf, sessionsByAsk, type SessionEntry,
} from './entries';
import { EVIDENCE_ASKS, EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from './entries.fixtures';

// SESSONE1c (D167 point 5): the list's entries as the driver's reader answers them, drawn without a rule of the page's own.
// The owner's evidence: two asks, one of them over two repositories, eleven records, three lines of work.

// LINESTOGETHER1a: a row opens on the driver's `opens`: the head, or the part that waits on the person or works.
describe('where a row opens', () => {
  const [APP] = EVIDENCE_ENTRIES as [SessionEntry];
  const ended = { ...APP, group: 'ended' as const, head: 'ap000002', opens: 'ap000002' };

  it('opens on the head of an ended session, not its newest part', () => {
    expect(opensOf([ended], 'ap000003')).toBe('ap000002');
  });

  it('opens on a part that waits on the person', () => {
    const waiting = { ...APP, group: 'you' as const, head: 'ap000002', opens: 'ap000001' };
    expect(opensOf([waiting], 'ap000003')).toBe('ap000001');
  });

  it('opens on a part that is working', () => {
    expect(opensOf(EVIDENCE_ENTRIES, 'ap000003')).toBe('ap000003');
  });

  it('opens on the leading part of an ask\'s siblings, which have no head', () => {
    const siblings = { ...APP, head: null, opens: 'ap000003' };
    expect(opensOf([siblings], 'ap000003')).toBe('ap000003');
  });

  it('opens on the leading part where the host is older and says no `opens`', () => {
    const { opens: _o, head: _h, ...older } = APP;
    expect(opensOf([older as SessionEntry], 'ap000003')).toBe('ap000003');
    expect(opensOf(undefined, 'x')).toBe('x');
  });
});

describe('the session list by entry', () => {
  it('lists the owner\'s two asks as three sessions, not eleven records', () => {
    expect(EVIDENCE_SESSIONS).toHaveLength(11);
    expect(listedSessions(EVIDENCE_SESSIONS, EVIDENCE_ENTRIES).map((row) => row.id)).toEqual(['ap000003', 'db000003', 'ap000013']);
  });

  it('lists every record where no host answered entries, as it always did', () => {
    expect(listedSessions(EVIDENCE_SESSIONS, undefined)).toHaveLength(11);
  });

  it('draws only what the page holds: an entry whose leading record is not here has no row', () => {
    expect(listedSessions(EVIDENCE_SESSIONS.filter((row) => row.id !== 'db000003'), EVIDENCE_ENTRIES).map((row) => row.id))
      .toEqual(['ap000003', 'ap000013']);
  });

  it('finds the entry any of its parts belongs to, and every part\'s id for an archive', () => {
    expect(entryOfSession(EVIDENCE_ENTRIES, 'ap000001')?.entry).toBe('ask:c98a7e/app');
    expect(entryOfSession(EVIDENCE_ENTRIES, 'nobody')).toBeNull();
    expect(partIds(EVIDENCE_ENTRIES, 'ap000012')).toEqual(['ap000011', 'ap000012', 'ap000013']);
    // A record in no entry is its own, so an archive of it is what it always was.
    expect(partIds(EVIDENCE_ENTRIES, 'lonely')).toEqual(['lonely']);
    expect(partIds(undefined, 'ap000012')).toEqual(['ap000012']);
  });

  it('counts the parts of each entry on its leading row', () => {
    const facts = entryFacts(EVIDENCE_ENTRIES);
    expect(facts.get('ap000003')).toEqual({ parts: 3, ids: ['ap000001', 'ap000002', 'ap000003'] });
    expect(facts.get('ap000001')).toBeUndefined();
  });

  it('names an entry by its ask, and an intake or an ask this page does not hold by its record', () => {
    const [app] = EVIDENCE_ENTRIES;
    expect(nameOfEntry(app, EVIDENCE_ASKS)).toBe('Add the export feed to the app, backed by the database');
    expect(nameOfEntry(app, {})).toBeNull();
    expect(nameOfEntry({ ...app!, repository: 'intake' }, EVIDENCE_ASKS)).toBeNull();
    expect(nameOfEntry({ ...app!, origin: 'quest:q0000a' }, EVIDENCE_ASKS)).toBeNull();
    expect(nameOfEntry(undefined, EVIDENCE_ASKS)).toBeNull();
    expect(askOfEntry({ origin: 'session:ch1' })).toBeNull();
  });

  describe('by ask', () => {
    it('groups each ask its entries in the reader\'s order, what no ask began last', () => {
      const chat: SessionEntry = {
        entry: 'session:ch000001/app', origin: 'session:ch000001', repository: 'app', group: 'ended', leading: 'ch000001',
        parts: [{ session: 'ch000001', quest: null, shown: 'completed', group: 'ended', began: 'opened', after: null, askedBy: null }],
      };
      const sessions = [...EVIDENCE_SESSIONS, { ...EVIDENCE_SESSIONS[0]!, id: 'ch000001' }];
      const groups = sessionsByAsk(sessions, [chat, ...EVIDENCE_ENTRIES], { selected: null, archived: false });

      expect(groups.map((group) => [group.ask, group.sessions.map((row) => row.id)])).toEqual([
        ['c98a7e', ['ap000003', 'db000003']],
        ['841c38', ['ap000013']],
        [null, ['ch000001']],
      ]);
    });

    it('hides an archived entry unless shown, and never one of its parts that is attended', () => {
      const archived = EVIDENCE_ENTRIES.map((entry) => (entry.leading === 'ap000013' ? { ...entry, group: 'archived' as const } : entry));
      const ids = (selected: string | null, show: boolean) => sessionsByAsk(EVIDENCE_SESSIONS, archived, { selected, archived: show })
        .flatMap((group) => group.sessions.map((row) => row.id));

      expect(ids(null, false)).toEqual(['ap000003', 'db000003']);
      expect(ids(null, true)).toEqual(['ap000003', 'db000003', 'ap000013']);
      expect(ids('ap000011', false)).toEqual(['ap000003', 'db000003', 'ap000013']);
    });
  });

  describe('on the ask\'s page', () => {
    it('lists the repositories\' lines of work the ask began, with the leading part\'s word', () => {
      expect(askLines(EVIDENCE_ENTRIES, 'c98a7e')).toEqual([
        { entry: 'ask:c98a7e/app', repository: 'app', parts: 3, leading: 'ap000003', opens: 'ap000003', shown: 'working' },
        { entry: 'ask:c98a7e/db', repository: 'db', parts: 3, leading: 'db000003', opens: 'db000003', shown: 'working' },
      ]);
      expect(askLines(EVIDENCE_ENTRIES, '841c38').map((line) => line.leading)).toEqual(['ap000013']);
      expect(askLines(undefined, 'c98a7e')).toEqual([]);
    });
  });
});

// LINESTOGETHER1b: where a part stands against its line's head, for the box's first line.
describe('where a part stands by the head', () => {
  const part = (session: string, began: string) =>
    ({ session, quest: 'q', shown: 'completed', group: 'review', began, after: null, askedBy: null }) as SessionEntry['parts'][number];
  const entry = (head: string | null | undefined): SessionEntry => ({
    entry: 'e', origin: 'quest:q', repository: 'engine', group: 'review', leading: 'p3', head,
    parts: [part('p1', 'opened'), part('p2', 'resumed'), part('p3', 'asked')],
  });

  it('numbers a part, and says whether it is the head, beside it, or earlier in its line', () => {
    expect(placeOf(entry('p2'), 'p2')).toEqual({ part: 2, place: 'head' });
    expect(placeOf(entry('p2'), 'p3')).toEqual({ part: 3, place: 'beside' });
    expect(placeOf(entry('p2'), 'p1')).toEqual({ part: 1, place: 'line' });
  });

  it('says nothing where the entry names no head, or does not hold the part', () => {
    expect(placeOf(entry(null), 'p1')).toBeNull();
    expect(placeOf(entry(undefined), 'p1')).toBeNull();
    expect(placeOf(entry('p2'), 'zz')).toBeNull();
    expect(placeOf(null, 'p1')).toBeNull();
  });
});
