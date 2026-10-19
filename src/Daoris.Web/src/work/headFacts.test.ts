import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import type { Quest, Session } from '../data/api';
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

  it('says a working session’s repository and how long it has run, and not its account (HEAD2c)', () => {
    expect(sessionFacts(t, { session: session({ profile: 'account-1' }), shown: 'working' }))
      .toEqual(['engine', '2m']);
  });

  /** WAITSHOWN1: a working session whose agent has not answered says so, with when it was asked, after its age and attempt. */
  it('says a working session has no answer yet, and when it was asked', () => {
    const since = '2026-10-05T09:49:00Z';
    const [repository, age, waiting] = sessionFacts(t, { session: session(), shown: 'working', waitingSince: since });
    expect([repository, age]).toEqual(['engine', '2m']);
    expect(waiting).toMatch(/^no answer yet, asked .+/);
    // Not said of a session that is not working, nor where no wait is told.
    expect(sessionFacts(t, { session: session(), shown: 'working' })).toEqual(['engine', '2m']);
    expect(sessionFacts(t, { session: session({ state: 'completed' }), shown: 'completed', waitingSince: since }).join()).not.toMatch(/no answer/);
  });

  /** WAITSAY1: a working session inside a long call names it and says for how long. */
  it('says a working session is running a call, and for how long', () => {
    const calling = { title: 'npm test', since: '2026-10-05T09:40:00Z' };
    const facts = sessionFacts(t, { session: session(), shown: 'working', calling });
    expect(facts.at(-1)).toBe('running npm test for 20m');
    expect(sessionFacts(t, { session: session(), shown: 'working' }).join()).not.toMatch(/running npm/);
    expect(sessionFacts(t, { session: session({ state: 'completed' }), shown: 'completed', calling }).join()).not.toMatch(/running npm/);
  });

  /**
   * WAITSAY1b: where the page holds none of the call's events (begun before the window opened, or past the history's page), the
   * head says the call the driver heard, from the row's reader; the record's own reading, live, goes first where it has one.
   */
  it('says the call the driver heard where the record read here names none', () => {
    const heard = grouping({ calling: { title: 'npm test', since: '2026-10-05T09:52:00Z' } });
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: heard }).at(-1)).toBe('running npm test for 8m');
    const read = { title: 'npm run build', since: '2026-10-05T09:58:00Z' };
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: heard, calling: read }).at(-1)).toBe('running npm run build for 2m');
    expect(sessionFacts(t, { session: session({ state: 'completed' }), shown: 'completed', grouping: heard }).join()).not.toMatch(/running/);
  });

  /**
   * AUTONOMY1b3b: a call the driver marks waiting on the person's answer is said as one, timed from the hold, whatever the record read
   * here says runs; only while the session works, in both languages, and never in the words a parked intake's region reads.
   */
  it('says the call the driver marks waiting for the person’s answer', async () => {
    const held = grouping({ calling: { title: 'git push', since: '2026-10-05T09:55:00Z', waiting: true } });
    const read = { title: 'git push', since: '2026-10-05T09:50:00Z' };
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: held, calling: read }).at(-1))
      .toBe('waiting for your answer to git push for 5m');
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: held }).join()).not.toMatch(/running|waiting on you/i);
    const unnamed = grouping({ calling: { title: '', since: '2026-10-05T09:55:00Z', waiting: true } });
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: unnamed }).at(-1)).toBe('waiting for your answer to a tool call for 5m');
    expect(sessionFacts(t, { session: session({ state: 'completed' }), shown: 'completed', grouping: held }).join()).not.toMatch(/your answer/);

    await i18n.changeLanguage('zh');
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: held }).at(-1)).toBe('git push 等你答复，已 5 分钟');
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: unnamed }).at(-1)).toBe('一个工具调用等你答复，已 5 分钟');
  });

  /** The install's fourth try, three failed before it, was the fourth line of a strip in mono; the head says it. */
  it('says a working session is another try, and how many failed before it', () => {
    expect(sessionFacts(t, { session: session({ profile: 'account-1' }), shown: 'working', grouping: grouping({ strikes: 3 }) }))
      .toEqual(['engine', '2m', 'attempt 4: the 3 before it failed']);
    expect(sessionFacts(t, { session: session(), shown: 'working', grouping: grouping({ strikes: 1 }) }))
      .toEqual(['engine', '2m', 'attempt 2: the one before it failed']);
  });

  /** The account is *Details*' (ACCTNAME1 names it there), whoever it is and however it signed in. */
  it('leaves the account to Details, named or on the tool’s own sign-in', () => {
    const nameOf = () => 'work';
    expect(sessionFacts(t, { session: session({ profile: 'acct-3f9c1a2b' }), shown: 'working', nameOf })).toEqual(['engine', '2m']);
    expect(sessionFacts(t, { session: session(), shown: 'working', ownSignIn: true })).toEqual(['engine', '2m']);
  });

  /** Where the account explains a hold, the row's own line says it: a cooling account keeps its place in the facts. */
  it('keeps what a held session’s account explains, in the row’s words', () => {
    const facts = sessionFacts(t, {
      session: session({ state: 'completed', profile: 'account-1' }), shown: 'completed',
      grouping: grouping({ group: 'you', shown: 'completed', holds: { why: 'cooling', reason: 'cooling.', until: '2026-10-05T16:02:00Z' } } as never),
    });
    expect(facts[0]).toBe('engine');
    expect(facts).toHaveLength(3);
    expect(facts).not.toContain('account-1');
  });

  it('says a running chat how long ago it began', () => {
    expect(sessionFacts(t, { session: session({ kind: 'chat', quest: null, profile: 'account-1' }), shown: 'working' }))
      .toEqual(['engine', 'started 2m ago']);
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
      .toEqual(['engine', '2 分钟', '第 4 次尝试：之前 3 次失败']);
  });

  /**
   * MIXAGENT3a-i (the mixed-agents design §4): a session that continues another says which agent's, and why in the hand-over's
   * own words; a helper says whom it helps. Both follow the repository, whatever the state, since they say what the session is.
   */
  describe('who it continues or helps', () => {
    const CHAT = { kind: 'chat', quest: null } as const;
    const continued = (over: Partial<Session> = {}) => session({ ...CHAT, from: 'a1b2c3d4', fromWhy: 'near', fromBy: 'daoris', ...over });

    it('says the agent it continues and why it was handed over', () => {
      expect(sessionFacts(t, { session: continued(), shown: 'working', continuesAgent: 'Codex' }))
        .toEqual(['engine', 'continues from Codex', 'its account was near its limit', 'started 2m ago']);
      expect(sessionFacts(t, {
        session: continued({ fromWhy: 'press', fromBy: 'person', state: 'completed', created: '2026-10-05T09:40:00Z', updated: '2026-10-05T09:54:00Z' }),
        shown: 'completed', continuesAgent: 'Codex',
      })).toEqual(['engine', 'continues from Codex', 'you asked for it', 'ran 14m']);
      // SWITCH1c-i (D165 point 3): Daoris moved it to another account by the usage curve, its agent unchanged.
      expect(sessionFacts(t, { session: continued({ fromWhy: 'curve' }), shown: 'working', continuesAgent: 'Claude Code' }))
        .toEqual(['engine', 'continues from Claude Code', 'its account was over its usage curve', 'started 2m ago']);
    });

    /** A why this build has no words for: the line says what it knows and guesses nothing. */
    it('says no why it has no words for, and another session where the one it continues is not held', () => {
      expect(sessionFacts(t, { session: continued({ fromWhy: 'moved-on' }), shown: 'working', continuesAgent: 'Codex' }))
        .toEqual(['engine', 'continues from Codex', 'started 2m ago']);
      expect(sessionFacts(t, { session: continued({ fromWhy: 'limit' }), shown: 'working' }))
        .toEqual(['engine', 'continues another session', 'its account reached its limit', 'started 2m ago']);
    });

    it('says whom a helper helps, by that session’s title, and another session where it is not held', () => {
      const helper = session({ ...CHAT, serves: 's9f8e7d6' });
      expect(sessionFacts(t, { session: helper, shown: 'working', helping: 'Expose a streaming budget' }))
        .toEqual(['engine', 'helping Expose a streaming budget', 'started 2m ago']);
      expect(sessionFacts(t, { session: helper, shown: 'working' })).toEqual(['engine', 'helping another session', 'started 2m ago']);
    });

    it('says nothing of a link the record does not hold', () => {
      expect(sessionFacts(t, { session: session(CHAT), shown: 'working', continuesAgent: 'Codex', helping: 'Expose a streaming budget' }))
        .toEqual(['engine', 'started 2m ago']);
    });

    it('is said in 中文', async () => {
      await i18n.changeLanguage('zh');
      expect(sessionFacts(t, { session: continued(), shown: 'working', continuesAgent: 'Codex' }).slice(0, 3))
        .toEqual(['engine', '接续 Codex 的会话', '它的账户接近上限']);
      expect(sessionFacts(t, { session: session({ ...CHAT, serves: 's9f8e7d6' }), shown: 'working', helping: '流式预算' })[1])
        .toBe('正在协助「流式预算」');
    });
  });
});

describe('a quest’s facts line', () => {
  beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(NOW); });
  afterEach(async () => { vi.useRealTimers(); await i18n.changeLanguage('en'); });

  it('says where it goes, its age, and that a session is starting for it here; the requester is its Details’', () => {
    expect(questFacts(t, { quest: quest(), session: session({ state: 'starting' }) }))
      .toEqual(['to engine', 'filed 4h ago', 'a session is starting here']);
  });

  it('opens on a hold the person lifts: a departure awaiting their yes, or a review (HEAD2b)', () => {
    expect(questFacts(t, { quest: quest({ held: true }) })).toEqual(['awaits your yes', 'to engine', 'filed 4h ago']);
    expect(questFacts(t, { quest: quest({ held: true, hold: 'unreviewed' }) })).toEqual(['awaits review', 'to engine', 'filed 4h ago']);
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
    expect(questFacts(t, { quest: quest() })).toEqual(['to engine', 'filed 4h ago']);
  });
});
