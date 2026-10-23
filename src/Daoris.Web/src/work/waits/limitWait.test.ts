import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { moment } from '../../locales/format';
import type { AccountsAnswer, AgentAccounts } from '../../settings/accounts/accounts';
import { USE_DEFAULTS } from '../../settings/accounts/accounts';
import { limitedLine, limitWaitOf, limitWaitSaid, notLiftedOf } from './limitWait';

// LIMITRESUME1d (D206 §3.7): what a line an account's limit cut off says while it waits, read from the accounts the page holds.

const NOW = new Date('2026-10-24T09:00:00Z');
const SOON = '2026-10-24T11:02:00Z';
const LATER = '2026-10-27T11:02:00Z';
const cooling = (until: string, stated = true) =>
  ({ until, stated, window: stated ? 'weekly' : null, seen: NOW.toISOString(), assumedZone: false, notBelieved: false });

function answer({ reason = 'waits', a = SOON, b = LATER, stated = true }: { reason?: string; a?: string; b?: string; stated?: boolean } = {}):
AccountsAnswer {
  const agent: AgentAccounts = {
    agent: 'claude-code', speaks: true, own: {},
    accounts: [{ name: 'a', cooling: cooling(a, stated) }, { name: 'b', cooling: cooling(b) }],
    scopes: [{
      workspace: null, list: ['a', 'b'], use: USE_DEFAULTS, unknown: [], near: [],
      next: { reason: reason as 'waits', others: [{ account: 'b', hold: 'cooling', until: b }, { account: 'a', hold: 'cooling', until: a }] },
    }],
  };
  return { agents: [agent] };
}

const doors = [{ harness: 'claude-code-acp', accountOf: 'claude-code' }] as never[];
const session = (profile: string | null, noteParts: unknown[] = []) =>
  ({ adapter: 'claude-code-acp', profile, noteParts }) as never;
const t = i18n.t.bind(i18n) as (key: string, options?: Record<string, unknown>) => string;
const label = (account: string | null) => account ?? i18n.t('agents.account.own');

describe('a limited line\'s wait', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('waits for the earliest account the walk passed cooling, its own where the record ran on it', () => {
    const wait = limitWaitOf({ session: session('a'), doors, answer: answer(), now: NOW });
    expect(wait).toEqual({ until: SOON, account: 'a', own: true, stated: true, notLifted: null });
    // Another account frees first: a briefed resume, on that account.
    expect(limitWaitOf({ session: session('b'), doors, answer: answer(), now: NOW })).toMatchObject({ account: 'a', own: false });
  });

  it('says nothing where an account is ready, the walk is missing, or the page holds no accounts', () => {
    expect(limitWaitOf({ session: session('a'), doors, answer: answer({ reason: 'onlyReady' }), now: NOW })).toBeNull();
    expect(limitWaitOf({ session: session('a'), doors, answer: { agents: [] }, now: NOW })).toBeNull();
    expect(limitWaitOf({ session: session('a'), doors, answer: null, now: NOW })).toBeNull();
  });

  it('reads the reset that did not lift the limit from its record', () => {
    const parts = [{ code: 'limit.not-lifted', values: { hit: 'session' } }];
    expect(notLiftedOf(parts as never)).toBe('session');
    expect(limitWaitOf({ session: session('a', parts), doors, answer: answer(), now: NOW })?.notLifted).toBe('session');
  });

  it('is a driven line a limit failed, its quest still open or taken, and not paused', () => {
    const record = { state: 'failed', limit: true, kind: 'driven', quest: 'q1' } as const;
    expect(limitedLine(record, 'Taken')).toBe(true);
    expect(limitedLine(record, 'Open')).toBe(true);
    expect(limitedLine(record, 'Done')).toBe(false);
    expect(limitedLine(record, 'Taken', true)).toBe(false);
    expect(limitedLine({ ...record, limit: false }, 'Taken')).toBe(false);
    expect(limitedLine({ ...record, kind: 'chat' }, 'Taken')).toBe(false);
  });

  it('says the sentence, own or briefed, in en and zh', async () => {
    const own = limitWaitOf({ session: session('a'), doors, answer: answer(), now: NOW })!;
    expect(limitWaitSaid(t, own, label)).toEqual([
      `Waiting for a limit to reset: it resumes by itself at ${moment(SOON)}, on a.`,
      'It goes on in its own conversation.',
    ]);
    const briefed = limitWaitOf({ session: session('b', [{ code: 'limit.not-lifted', values: { hit: 'weekly' } }]), doors, answer: answer(), now: NOW })!;
    await i18n.changeLanguage('zh');
    expect(limitWaitSaid(t, briefed, label)).toEqual([
      `正在等待额度重置：将于 ${moment(SOON)} 在 a 上自动继续。`,
      '它将在新会话中继续，并附上 Daoris 的简报。',
      '上次重置未解除额度限制：weekly。',
    ]);
  });

  it('says a reset nobody said as a try again, never as a reset', () => {
    const wait = limitWaitOf({ session: session('a'), doors, answer: answer({ stated: false }), now: NOW })!;
    expect(limitWaitSaid(t, wait, label)[0]).toBe(`No reset was said: it tries again at ${moment(SOON)}.`);
  });
});
