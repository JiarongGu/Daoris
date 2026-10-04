import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import type { Quest, Session } from '../api';
import type { SessionGrouping } from './groups';
import { questFacts, sessionFacts } from './headFacts';

// The facts line under a head's title (UX7c, D152 §7; the UX7 design §5.2, §5.3): the facts a record's state makes
// matter, each said once. The clock is held, so every span reads the same on every run.

const NOW = new Date('2026-10-05T10:00:00Z');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-05T09:58:00Z', updated: '2026-10-05T09:59:00Z', ...over,
});

const grouping = (over: Partial<SessionGrouping> = {}): SessionGrouping => ({
  session: 's1a2b3c4', group: 'working', shown: 'working', archived: false, teammate: false, ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: 'e67690366b56', from: 'ask #f6d947', to: 'engine', title: 'Continue the production half', body: 'b',
  status: 'Open', filed: '2026-10-05T06:00:00Z', updated: '2026-10-05T06:00:00Z', ...over,
});

const t = i18n.t.bind(i18n);

describe('a session’s facts line', () => {
  beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(NOW); });
  afterEach(async () => { vi.useRealTimers(); await i18n.changeLanguage('en'); });

  it('says a working session’s repository, account and how long it has run', () => {
    expect(sessionFacts(t, { session: session({ profile: 'account-1' }), shown: 'working' }))
      .toEqual(['engine', 'account-1', '2m']);
  });

  /** The install's fourth try, three failed before it, was the fourth line of a strip in mono; the head says it. */
  it('says a working session is another try, and how many failed before it', () => {
    expect(sessionFacts(t, { session: session({ profile: 'account-1' }), shown: 'working', grouping: grouping({ strikes: 3 }) }))
      .toEqual(['engine', 'account-1', '2m', 'attempt 4: the 3 before it failed']);
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: grouping({ strikes: 1 }) }))
      .toEqual(['engine', '2m', 'attempt 2: the one before it failed']);
  });

  it('names the tool’s own sign-in where the record names no account and its agent has accounts', () => {
    expect(sessionFacts(t, { session: session(), shown: 'working', ownSignIn: true })).toEqual(['engine', 'on your own sign-in', '2m']);
  });

  it('says how long a session has waited on the person', () => {
    expect(sessionFacts(t, { session: session({ state: 'awaiting-person', updated: '2026-10-05T09:48:00Z' }), shown: 'awaiting-person' }))
      .toEqual(['engine', 'waiting 12m']);
  });

  it('says a parked session’s failures and an ended one’s span, in the row’s words', () => {
    expect(sessionFacts(t, {
      session: session({ state: 'failed', created: '2026-10-05T09:40:00Z', updated: '2026-10-05T09:54:00Z' }),
      shown: 'parked', grouping: grouping({ group: 'you', shown: 'parked', strikes: 3 }),
    })).toEqual(['engine', 'after 3 failed sessions', 'ran 14m']);
  });

  it('says where an ended session’s work landed, and how long it ran', () => {
    expect(sessionFacts(t, {
      session: session({ state: 'completed', created: '2026-10-05T09:40:00Z', updated: '2026-10-05T09:54:00Z' }),
      shown: 'completed', branch: { kind: 'landed', where: 'feature/x' } as never,
    })).toEqual(['engine', 'landed on feature/x', 'ran 14m']);
  });

  it('says an intake’s ask and what it is', () => {
    expect(sessionFacts(t, { session: session({ kind: 'chat', ask: 'a1b2c3', quest: null, repository: 'ask #a1b2c3' }), shown: 'working' }))
      .toEqual(['ask #a1b2c3', 'intake']);
  });

  it('is said in 中文', async () => {
    await i18n.changeLanguage('zh');
    expect(sessionFacts(t, { session: session({ profile: 'account-1' }), shown: 'working', grouping: grouping({ strikes: 3 }) }))
      .toEqual(['engine', 'account-1', '2 分钟', '第 4 次尝试：之前 3 次失败']);
  });
});

describe('a quest’s facts line', () => {
  beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(NOW); });
  afterEach(async () => { vi.useRealTimers(); await i18n.changeLanguage('en'); });

  it('says whom it asks, who asked and when, and that a session is starting for it here', () => {
    expect(questFacts(t, { quest: quest(), session: session({ state: 'starting' }) }))
      .toEqual(['to engine', 'from ask #f6d947', 'filed 4h ago', 'a session is starting here']);
  });

  it('says its session working, its park, a take with nothing running, and when it closed', () => {
    expect(questFacts(t, { quest: quest({ status: 'Taken' }), session: session({ state: 'working', created: '2026-10-05T09:48:00Z' }) }).at(-1))
      .toBe('its session working 12m');
    expect(questFacts(t, { quest: quest(), sitting: { quest: 'e67690366b56', repository: 'engine', verdict: 'Exhausted', reason: 'r', strikes: 3 } }).at(-1))
      .toBe('parked after 3 failed sessions');
    expect(questFacts(t, { quest: quest({ status: 'Taken', updated: '2026-10-05T08:00:00Z' }) }).at(-1)).toBe('taken 2h ago');
    expect(questFacts(t, { quest: quest({ status: 'Done', updated: '2026-10-05T08:00:00Z' }) }).at(-1)).toBe('done 2h ago');
  });

  it('says nothing more of an open quest nothing is running for', () => {
    expect(questFacts(t, { quest: quest() })).toEqual(['to engine', 'from ask #f6d947', 'filed 4h ago']);
  });
});
