import { describe, expect, it } from 'vitest';
import type { Quest, Session } from '../api';
import type { SessionGrouping } from './groups';
import { folderOf, offeredActs, primaryAct, stopAsk } from './acts';

// Which acts a session is offered, by the one rule its row's ⋯ and its page header share (SESSUX1d, D126 §3.1): each
// offered where it applies and absent where it does not, never disabled (D119 §3.2). Pure, so every session a person
// could have is an argument.

const ROOT = 'C:/somewhere/engine';
const TREE = 'C:/somewhere/data/trees/aurora/engine/s-2394e5d9';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z', tree: ROOT, ...over,
});

const placed = (over: Partial<SessionGrouping> & Pick<SessionGrouping, 'group' | 'shown'>): SessionGrouping => ({
  session: 's1a2b3c4', archived: false, teammate: false, ...over,
});

const quest = (status: Quest['status']): Quest => ({
  id: 'abc123', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: 'A body.', status,
  filed: '2026-10-01T00:00:00Z', updated: '2026-10-01T00:00:00Z',
});

describe('the acts a session is offered', () => {
  it('offers a running driven session its stop, its folder and a terminal there, in §3.1’s order', () => {
    expect(offeredActs({ session: session(), grouping: placed({ group: 'working', shown: 'working' }), root: ROOT }, 'row'))
      .toEqual(['stop', 'review', 'openFolder', 'terminal', 'detach', 'copy']);
  });

  it('offers a session waiting on you its answer on its row, and not in its header, where its card keeps it', () => {
    const waiting = { session: session({ state: 'awaiting-person' }), grouping: placed({ group: 'you', shown: 'awaiting-person' }), root: ROOT };
    expect(offeredActs(waiting, 'row')).toEqual(['answer', 'stop', 'review', 'openFolder', 'terminal', 'detach', 'copy']);
    expect(offeredActs(waiting, 'header')).not.toContain('answer');
  });

  /** ANSWER1c (D131): answered, the same session goes on at the driver's next look, and there is no box to answer in. */
  it('offers no answer to a park the person has answered, and keeps its stop', () => {
    const answered = {
      session: session({ state: 'awaiting-person', answer: 'Use the second.' }),
      grouping: placed({ group: 'working', shown: 'answered' }),
      root: ROOT,
    };
    expect(offeredActs(answered, 'row')).toEqual(['stop', 'review', 'openFolder', 'terminal', 'detach', 'copy']);
  });

  it('offers a parked quest’s last session Try again, and a stopped one Try again only where its stop holds its quest', () => {
    const parked = { session: session({ state: 'failed' }), grouping: placed({ group: 'you', shown: 'parked', strikes: 3 }), root: ROOT };
    expect(offeredActs(parked, 'row')).toContain('retry');
    expect(offeredActs(parked, 'row')).not.toContain('stop');

    const holding = { session: session({ state: 'stopped' }), grouping: placed({ group: 'ended', shown: 'stopped', holdsQuest: true }), root: ROOT };
    expect(offeredActs(holding, 'row')).toContain('retry');
    const released = { ...holding, grouping: placed({ group: 'ended', shown: 'stopped', holdsQuest: false }) };
    expect(offeredActs(released, 'row')).not.toContain('retry');
  });

  it('offers Archive on what the reader placed in Ended, and Unarchive wherever the mark stands', () => {
    const ended = { session: session({ state: 'completed' }), grouping: placed({ group: 'ended', shown: 'completed' }), root: ROOT };
    expect(offeredActs(ended, 'row')).toContain('archive');
    expect(offeredActs(ended, 'row')).not.toContain('unarchive');

    const marked = { ...ended, grouping: placed({ group: 'archived', shown: 'completed', archived: true }) };
    expect(offeredActs(marked, 'row')).toEqual(expect.arrayContaining(['unarchive']));
    expect(offeredActs(marked, 'row')).not.toContain('archive');

    // Archive never hides what needs the person, nor what runs, nor a record the reader has not placed.
    expect(offeredActs({ ...ended, grouping: placed({ group: 'review', shown: 'completed' }) }, 'row')).not.toContain('archive');
    expect(offeredActs({ ...ended, grouping: null }, 'row')).not.toContain('archive');
  });

  /** SESSUX1f (D126 §5.4): Delete… only where the reader says the delete would be taken, D95's way, at both doors. */
  it('offers Delete… only where the reader says deletable, after the archive marks and before the id', () => {
    const chat = session({ quest: null, kind: 'chat', state: 'completed', tree: null });
    const deletable = { session: chat, grouping: placed({ group: 'ended', shown: 'completed', deletable: true }), root: ROOT };
    expect(offeredActs(deletable, 'row')).toEqual(['openFolder', 'terminal', 'detach', 'archive', 'delete', 'copy']);
    expect(offeredActs(deletable, 'header')).toContain('delete');

    expect(offeredActs({ ...deletable, grouping: placed({ group: 'ended', shown: 'completed' }) }, 'row')).not.toContain('delete');
    expect(offeredActs({ ...deletable, grouping: null }, 'row')).not.toContain('delete');
    const driven = { session: session({ state: 'completed' }), grouping: placed({ group: 'ended', shown: 'completed', deletable: false }), root: ROOT };
    expect(offeredActs(driven, 'header')).not.toContain('delete');
  });

  it('offers a teammate’s record only what this machine can do of it: archive marks and its id', () => {
    const theirs = session({ id: 'person@machine-b/s1a2b3c4', state: 'completed', tree: null });
    expect(offeredActs({ session: theirs, grouping: placed({ group: 'ended', shown: 'completed', teammate: true }) }, 'header'))
      .toEqual(['archive', 'copy']);
    expect(offeredActs({ session: { ...theirs, state: 'working' }, grouping: placed({ group: 'working', shown: 'working', teammate: true }) }, 'row'))
      .toEqual(['copy']);
  });

  it('offers a folder and a terminal only where the folder is on this machine', () => {
    // Its own tree, until a tidy took it.
    const own = { session: session({ state: 'completed', tree: TREE }), root: ROOT };
    expect(folderOf(own)).toBe(TREE);
    expect(folderOf({ ...own, where: { treeGone: true } })).toBeNull();
    expect(offeredActs({ ...own, where: { treeGone: true } }, 'row')).not.toContain('openFolder');
    // The repository's checkout, named by the record or by nothing.
    expect(folderOf({ session: session(), root: ROOT })).toBe(ROOT);
    expect(folderOf({ session: session({ tree: null }), root: ROOT })).toBe(ROOT);
    expect(folderOf({ session: session({ tree: null }), root: null })).toBeNull();
    // An intake runs in Daoris's own room, and Ask Daoris in none of a repository's.
    const intake = session({ quest: null, kind: 'chat', ask: '0fda18', repository: 'ask #0fda18', tree: 'C:/somewhere/data/intake/default' });
    expect(offeredActs({ session: intake, root: null }, 'row')).toEqual(['stop', 'detach', 'copy']);
  });

  it('offers Review where there is work to read, and leads with it in To review', () => {
    const toReview = { session: session({ state: 'stopped', tree: TREE }), grouping: placed({ group: 'review', shown: 'stopped' }), root: ROOT };
    expect(offeredActs(toReview, 'header')).toContain('review');
    expect(primaryAct(offeredActs(toReview, 'header'), toReview.grouping)).toBe('review');
    expect(offeredActs({ session: session({ tree: null }), root: null }, 'row')).not.toContain('review');
  });

  it('leads with Try again where it is offered, and with nothing on a session that has no next step', () => {
    const parked = placed({ group: 'you', shown: 'parked' });
    expect(primaryAct(offeredActs({ session: session({ state: 'failed' }), grouping: parked, root: ROOT }, 'header'), parked)).toBe('retry');
    expect(primaryAct(['stop', 'copy'], placed({ group: 'working', shown: 'working' }))).toBeNull();
  });
});

describe('what a stop says before it is meant', () => {
  it('says what follows by what the session is (§3.3)', () => {
    expect(stopAsk(session(), quest('Taken'))).toEqual({ key: 'work.stop.drivenHeld', values: {} });
    expect(stopAsk(session({ state: 'queued' }), quest('Open'))).toEqual({ key: 'work.stop.drivenOpen', values: { quest: 'abc123' } });
    expect(stopAsk(session({ state: 'awaiting-person' }), quest('Taken'))).toEqual({ key: 'work.stop.parked', values: {} });
    // ANSWER1c: answered, it is not stopped unanswered; it holds its quest as a driven session at work does.
    expect(stopAsk(session({ state: 'awaiting-person', answer: 'carry on.' }), quest('Taken')))
      .toEqual({ key: 'work.stop.drivenHeld', values: {} });
    expect(stopAsk(session({ quest: null, kind: 'chat' }), null)).toEqual({ key: 'work.stop.chat', values: {} });
    expect(stopAsk(session({ quest: null, kind: 'chat', ask: '0fda18' }), null)).toEqual({ key: 'work.intake.stopMeans', values: {} });
  });
});
