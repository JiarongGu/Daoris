import { describe, expect, it } from 'vitest';
import type { Quest, Session } from '../api';
import { readEvidence, sessionTimeline } from './timeline';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'queued',
  kind: 'driven',
  created: '2026-09-21T09:00:00Z',
  updated: '2026-09-21T09:00:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Taken',
  filed: '2026-09-14T09:00:00Z',
  updated: '2026-09-21T09:30:00Z',
  ...over,
});

/**
 * The evidence bundle is the driver's own string, and this reads its SHAPE rather than its words —
 * so a reworded header is harmless and neither catalogue has to agree with a C# literal.
 */
describe('reading an evidence bundle', () => {
  it('separates the driver\'s sentence from the commits inside it', () => {
    const { text, commits } = readEvidence(
      'commits landed:\nc0ffee1 cap hydration per frame\ndeadbee1 add the budget to the chunk API',
    );

    expect(text).toBe('commits landed:');
    expect(commits).toEqual([
      { sha: 'c0ffee1', subject: 'cap hydration per frame' },
      { sha: 'deadbee1', subject: 'add the budget to the chunk API' },
    ]);
  });

  it('keeps a bundle with no commits as a sentence, not as an empty mystery', () => {
    expect(readEvidence('no commits landed')).toEqual({ text: 'no commits landed', commits: [] });
  });

  it('survives a header nobody here has ever seen', () => {
    const { text, commits } = readEvidence('the gates went green\nc0ffee1 cap hydration per frame');
    expect(text).toBe('the gates went green');
    expect(commits).toHaveLength(1);
  });

  it('keeps prose out of the commit list unless it is genuinely commit-shaped', () => {
    expect(readEvidence('1234 too short to be a sha').commits).toHaveLength(0);
    expect(readEvidence('zzzzzzz not hexadecimal at all').commits).toHaveLength(0);
    // And the accepted cost of reading shape rather than words, stated rather than hidden: a
    // sentence that opens with seven hex characters and a space IS a commit line to this reader.
    // Harmless — it renders as one row of the evidence it came from — and the alternative is
    // matching an English literal that a reworded header would silently empty.
    expect(readEvidence('deadbeef is the sha we started from').commits).toHaveLength(1);
  });
});

describe('a session timeline', () => {
  it('has exactly one entry for a session that has only just been queued', () => {
    expect(sessionTimeline(session())).toEqual([{ kind: 'opened', at: '2026-09-21T09:00:00Z' }]);
  });

  it('records the state it reached, with what the driver observed', () => {
    const events = sessionTimeline(session({
      state: 'awaiting-person', updated: '2026-09-21T09:20:00Z', note: 'two ways forward; I recommend the second.',
    }));

    expect(events).toHaveLength(2);
    expect(events[1]).toEqual({
      kind: 'state',
      at: '2026-09-21T09:20:00Z',
      state: 'awaiting-person',
      note: 'two ways forward; I recommend the second.',
    });
  });

  /** LANG1b: the note's parts ride with its English, for the entry to word in the reader's language. */
  it('carries the note’s parts beside its English', () => {
    const noteParts = [{ code: 'ended.done', values: {}, text: 'The quest reached done.' }];
    const events = sessionTimeline(session({
      state: 'completed', updated: '2026-09-21T09:20:00Z', note: 'The quest reached done.', noteParts,
    }));
    expect(events[1]).toEqual({
      kind: 'state', at: '2026-09-21T09:20:00Z', state: 'completed', note: 'The quest reached done.', noteParts,
    });
  });

  it('records the quest moving, once the move is news the session could have caused', () => {
    const events = sessionTimeline(session({ quest: '7a82cc' }), quest({ status: 'Done' }));
    expect(events.map((event) => event.kind)).toEqual(['opened', 'quest']);
  });

  /**
   * A quest that last moved before the session opened is the quest's own history, which the quest
   * view already holds — starting every timeline with news that predates it would be noise.
   */
  it('leaves a quest that moved before the session out of it', () => {
    const events = sessionTimeline(
      session({ quest: '7a82cc' }), quest({ updated: '2026-09-20T09:00:00Z' }),
    );
    expect(events.map((event) => event.kind)).toEqual(['opened']);
  });

  it('carries the evidence, commits split out of the sentence around them', () => {
    const events = sessionTimeline(session({
      state: 'completed',
      updated: '2026-09-21T10:00:00Z',
      evidence: 'commits landed:\nc0ffee1 cap hydration per frame',
    }));

    const landed = events.find((event) => event.kind === 'evidence');
    expect(landed).toMatchObject({
      at: '2026-09-21T10:00:00Z',
      text: 'commits landed:',
      commits: [{ sha: 'c0ffee1', subject: 'cap hydration per frame' }],
    });
  });

  it('runs oldest first, and keeps the state move ahead of what that move carried', () => {
    const events = sessionTimeline(
      session({
        state: 'completed',
        updated: '2026-09-21T10:00:00Z',
        evidence: 'commits landed:\nc0ffee1 cap hydration per frame',
      }),
      quest({ status: 'Done', updated: '2026-09-21T09:30:00Z' }),
    );

    expect(events.map((event) => event.kind)).toEqual(['opened', 'quest', 'state', 'evidence']);
  });
});
