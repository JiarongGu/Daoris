import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../i18n';
import {
  type AccountScope, type AgentAccounts, agentOf, cannotLeave, coolingLine, heldLine, listedIn, machineScope, moved, nextLine,
  type NextStart, nothingSaid, offeredLine, ownLine, saidLine, used, USE_DEFAULTS, workspaceScope,
} from './accounts';

// How each agent's accounts are used, read and said (TOOL4g; D125 §2.4, §3.7; D130 §3.1, §4.6, §5.2, §16.4): the pure half
// the molecules and the Agents domain read. With one account and with six: nothing counts accounts.

const scope = (over: Partial<AccountScope> = {}): AccountScope => ({
  workspace: null, default: null, list: [], begins: null, use: USE_DEFAULTS, unknown: [], problem: null, near: [], ...over,
});

const SIX = ['account-1', 'account-2', 'account-3', 'account-4', 'account-5', 'account-6'];

const agent = (over: Partial<AgentAccounts> = {}): AgentAccounts => ({
  agent: 'claude-code', speaks: true, own: {}, accounts: SIX.map((name) => ({ name })), scopes: [scope()], ...over,
});

describe('the answer, read', () => {
  it('finds an agent by its accounts\' owner, and nothing in an answer an older shell gave', () => {
    expect(agentOf({ agents: [agent()] }, 'claude-code')?.agent).toBe('claude-code');
    expect(agentOf({ agents: [agent()] }, 'codex')).toBeNull();
    expect(agentOf(undefined, 'claude-code')).toBeNull();
    expect(agentOf({} as never, 'claude-code')).toBeNull();
  });

  it('reads the machine\'s scope, and a workspace\'s only where it names one of its own', () => {
    const work = scope({ workspace: 'work', list: ['account-2'] });
    const held = agent({ scopes: [scope({ list: SIX }), work] });

    expect(machineScope(held).list).toEqual(SIX);
    expect(workspaceScope(held, 'work')).toBe(work);
    expect(workspaceScope(held, 'lab')).toBeNull();
    expect(machineScope(agent({ scopes: [] })).use).toEqual(USE_DEFAULTS);
    expect(listedIn(held, 'account-2')).toEqual(['work']);
    expect(listedIn(held, 'account-6')).toEqual([]);
  });
});

describe('a list, edited', () => {
  it('moves an account one place, and keeps the list at either end', () => {
    expect(moved(SIX, 'account-3', -1)).toEqual(['account-1', 'account-3', 'account-2', 'account-4', 'account-5', 'account-6']);
    expect(moved(SIX, 'account-6', 1)).toEqual(SIX);
    expect(moved(['account-1'], 'account-1', -1)).toEqual(['account-1']);
  });

  it('turns Use on last, and off where it was', () => {
    expect(used(['account-2'], 'account-1', true)).toEqual(['account-2', 'account-1']);
    expect(used(SIX, 'account-4', false)).toEqual(['account-1', 'account-2', 'account-3', 'account-5', 'account-6']);
    expect(used(['account-1'], 'account-1', false)).toEqual([]);
  });

  /** What the terminal refuses, the screen does not offer (D130 §3.1, §4.6). */
  it('says what Use off would break: the default, the kept account, or a kept account left alone', () => {
    const kept = (keep: string, list: string[]) => scope({ list, use: { ...USE_DEFAULTS, keep } });

    expect(cannotLeave(scope({ list: SIX, default: 'account-2' }), 'account-2')).toBe('default');
    expect(cannotLeave(kept('account-3', SIX), 'account-3')).toBe('keep');
    expect(cannotLeave(kept('account-2', ['account-1', 'account-2']), 'account-1')).toBe('alone');
    expect(cannotLeave(scope({ list: SIX, default: 'account-2' }), 'account-5')).toBeNull();
    // The last account out clears the list, its settings with it, which is never refused.
    expect(cannotLeave(scope({ list: ['account-1'], default: 'account-1' }), 'account-1')).toBeNull();
  });
});

describe('what is said', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const now = new Date('2026-10-03T12:50:00Z');
  const cooling = { until: '2026-10-03T16:02:00Z', stated: true, window: 'weekly', seen: '2026-10-03T12:00:00Z', assumedZone: false, notBelieved: false };

  /** D125 §2.4: until when, in this machine's zone and named, how long from now, and why. */
  it('says a cool-off with how long it lasts and why, in either language', async () => {
    expect(coolingLine(cooling, now)).toMatch(/^Cooling until .+ · in 3h 12m · the agent said so$/);
    expect(coolingLine({ ...cooling, stated: false }, now)).toContain("Daoris's default: the agent named no time");
    expect(coolingLine({ ...cooling, assumedZone: true }, now)).toContain("in this machine's zone");
    expect(coolingLine({ ...cooling, stated: false, notBelieved: true }, now)).toContain('more than 8 days off');

    await i18n.changeLanguage('zh');
    expect(coolingLine(cooling, now)).toMatch(/^冷却至 .+ · 还有 .+ · 智能体如此说明$/);
  });

  /** D130 §5.2, as the CLI's `saidLine` says it: its own word first, then each window's use and reset. */
  it('says what an agent last said, with how long ago, and nothing said as nothing', () => {
    const seen = new Date(Date.now() - 3 * 3_600_000).toISOString();
    const said = {
      seen,
      windows: [
        { window: 'weekly', used: 0.14, reset: '2026-10-07T10:00:00Z', credits: false, seen },
        { window: 'session', used: 0.88, reset: '2026-10-03T16:00:00Z', standing: 'near', credits: false, seen },
      ],
    };

    const line = saidLine(said);
    expect(line).toMatch(/^said 3h ago: near its five-hour limit, by its own word; 88% of its five-hour limit used, resets .+; 14% of its weekly limit used, resets .+$/);
    expect(saidLine(null)).toBe('nothing said yet');
    expect(saidLine({ seen, windows: [{ window: 'session', reset: '2026-10-03T16:00:00Z', standing: 'clear', credits: false, seen }] }))
      .toBe('said 3h ago: clear, by its own word');
    expect(saidLine({ seen, windows: [{ window: 'weekly', used: 0.5, reset: '2026-10-07T10:00:00Z', credits: true, seen }] }))
      .toContain('drawing on usage credits');
  });

  /** D130 §16.4: with nothing said by any account of the list, the walk needs none of their word, and the screen says so. */
  it('knows when no account of a list has said anything', () => {
    const list = scope({ list: ['account-1', 'account-2'] });
    const quiet = agent();
    const spoke = agent({ accounts: [{ name: 'account-1' }, { name: 'account-2', said: { seen: '2026-10-03T12:00:00Z', windows: [] } }] });

    expect(nothingSaid(quiet, list)).toBe(true);
    expect(nothingSaid(spoke, list)).toBe(false);
  });

  /**
   * TOOL6e (D130 §3–§4): which account the next start takes and the walk's step that chose it, in the reader's language,
   * the account named as a person calls it.
   */
  it('says which account the next start takes and why', async () => {
    const labelOf = (name: string) => ({ 'account-1': 'work@example.invalid' } as Record<string, string>)[name] ?? name;
    const list = scope({ default: 'account-1', list: ['account-1', 'account-2', 'account-3'], begins: 'account-1' });
    const next = (over: Partial<NextStart>): NextStart => ({ account: 'account-1', reason: 'list', over: 'account-2', others: [], ...over });

    expect(nextLine(next({ reason: 'leastRecent' }), list, labelOf))
      .toBe('The next start takes work@example.invalid: Daoris started on it less recently than account-2.');
    expect(nextLine(next({}), list, labelOf)).toBe('The next start takes work@example.invalid: it is the default here.');
    expect(nextLine(next({}), scope({ list: ['account-1', 'account-2'], begins: 'account-1' }), labelOf))
      .toBe('The next start takes work@example.invalid: it comes first in the list.');
    expect(nextLine(next({ account: 'account-3', over: 'account-2' }), list, labelOf))
      .toBe('The next start takes account-3: it comes before account-2 in the list.');
    expect(nextLine(next({ account: 'account-2', reason: 'fewest', over: 'account-1' }), list, labelOf))
      .toBe('The next start takes account-2: it runs fewer of Daoris\'s sessions than work@example.invalid.');
    expect(nextLine(next({ account: 'account-2', reason: 'near', over: 'account-1' }), list, labelOf))
      .toBe('The next start takes account-2: work@example.invalid is near its limit, so it goes last.');
    expect(nextLine(next({ reason: 'lapsing', when: '2026-10-04T10:00:00Z' }), list, labelOf))
      .toMatch(/^The next start takes work@example\.invalid: its week resets first, .+\.$/);
    expect(nextLine(next({ account: 'account-3', reason: 'notStarted' }), list, labelOf))
      .toBe('The next start takes account-3: Daoris has not started on it yet.');
    expect(nextLine(next({ account: 'account-2', reason: 'onlyReady', over: null }), list, labelOf))
      .toBe('The next start takes account-2: it is the only account here that is ready.');
    expect(nextLine(next({ account: 'account-2', reason: 'named', over: null }), list, labelOf))
      .toBe('The next start takes account-2: it is the one account used here.');
    expect(nextLine(next({ account: null, reason: 'own', over: null }), scope(), labelOf))
      .toBe('The next start runs on your own sign-in: nothing here names an account.');
    expect(nextLine(next({ account: null, reason: 'waits', over: null, when: '2026-10-03T16:02:00Z' }), list, labelOf))
      .toMatch(/^No account here is ready, so the next start waits until .+\.$/);
    expect(nextLine(next({ account: null, reason: 'waits', over: null }), list, labelOf))
      .toBe('No account here is ready, and none comes ready by itself: the next start waits for you.');

    await i18n.changeLanguage('zh');
    expect(nextLine(next({ reason: 'leastRecent' }), list, labelOf))
      .toBe('下一次启动使用 work@example.invalid：Daoris 在它上面启动的时间比 account-2 更早。');
    expect(nextLine(next({ account: null, reason: 'own', over: null }), scope(), labelOf))
      .toBe('下一次启动运行在你自己的登录上：这里没有指定任何账户。');
  });

  /**
   * TOOL6g: a next start that waits while accounts of its list are not signed in says a sign-in starts it, sooner than the
   * reset where one cools, so *waits until* never hides that a sign-in would free it.
   */
  it('says a waiting start would start sooner on a sign-in, naming the accounts not signed in', async () => {
    const labelOf = (name: string) => ({ 'account-1': 'work@example.invalid' } as Record<string, string>)[name] ?? name;
    const list = scope({ list: ['gmail', 'account-1', 'account-2'], begins: 'gmail' });
    const waits = (when: string | null, others: NextStart['others']): NextStart => ({ account: null, reason: 'waits', over: null, when, others });
    const passed: NextStart['others'] = [
      { account: 'gmail', hold: 'cooling', until: '2026-10-06T09:00:00Z' },
      { account: 'account-1', hold: 'signedOut' },
      { account: 'account-2', hold: 'signedOut' },
    ];

    expect(nextLine(waits('2026-10-06T09:00:00Z', passed), list, labelOf)).toMatch(
      /^No account here is ready, so the next start waits until .+\. Signing in to work@example\.invalid, account-2 starts it sooner\.$/);
    expect(nextLine(waits(null, passed.slice(1)), list, labelOf)).toBe(
      'No account here is ready, and none comes ready by itself: the next start waits for you. Signing in to '
      + 'work@example.invalid, account-2 starts it.');
    expect(nextLine(waits('2026-10-06T09:00:00Z', passed.slice(0, 1)), list, labelOf)).not.toMatch(/Signing in/);

    await i18n.changeLanguage('zh');
    expect(nextLine(waits(null, passed.slice(1)), list, labelOf)).toContain('登录 work@example.invalid、account-2');
    // ACCTUX1: the accounts come before the editor this is said in, so neither language points below.
    expect(nextLine(waits('2026-10-06T09:00:00Z', passed), list, labelOf)).not.toMatch(/下方|对应的行/);
    expect(nextLine(waits(null, passed.slice(1)), list, labelOf)).not.toMatch(/下方|对应的行/);
  });

  /** ACCTUX1b: a key is repaired by a new key, never a sign-in, so a wait names the key's repair apart from a login's. */
  it('says a waiting start needs a new API key for a key, and a sign-in for an account', async () => {
    const labelOf = (name: string) => name;
    const isKey = (name: string) => name === 'key-1';
    const list = scope({ list: ['key-1', 'account-2'], begins: 'key-1' });
    const others: NextStart['others'] = [{ account: 'key-1', hold: 'signedOut' }, { account: 'account-2', hold: 'signedOut' }];
    const waits = (when: string | null, held: NextStart['others']): NextStart => ({ account: null, reason: 'waits', over: null, when, others: held });

    expect(nextLine(waits(null, others), list, labelOf, isKey)).toBe(
      'No account here is ready, and none comes ready by itself: the next start waits for you. '
      + 'Signing in to account-2 starts it. A new API key in place of key-1 starts it.');
    expect(nextLine(waits(null, others.slice(0, 1)), list, labelOf, isKey)).not.toMatch(/Signing in/);
    expect(nextLine(waits('2026-10-06T09:00:00Z', others.slice(0, 1)), list, labelOf, isKey)).toMatch(
      /A new API key in place of key-1 starts it sooner\.$/);
    expect(heldLine({ account: 'account-3', reason: 'fewest', over: null, others }, labelOf, isKey))
      .toBe('key-1 was refused, so only a new API key repairs it; account-2 is not signed in.');

    await i18n.changeLanguage('zh');
    expect(nextLine(waits(null, others.slice(0, 1)), list, labelOf, isKey)).toContain('用新的 API 密钥替换 key-1');
    expect(heldLine({ account: 'account-3', reason: 'fewest', over: null, others: others.slice(0, 1) }, labelOf, isKey))
      .toBe('key-1 的密钥被拒绝，只有新的 API 密钥能修复它。');
  });

  /** TOOL6e: what holds every other account, cooling with until when, and the accounts a scope does not use, together. */
  it('says what holds the other accounts, and nothing for an account merely ranked after', async () => {
    const labelOf = (name: string) => name;
    const next: NextStart = {
      account: 'account-2', reason: 'fewest', over: 'account-1',
      others: [
        { account: 'account-1', hold: 'cooling', until: '2026-10-03T16:02:00Z' },
        { account: 'account-3', hold: 'ready' },
        { account: 'account-4', hold: 'near' },
        { account: 'account-5', hold: 'refused' },
        { account: 'account-6', hold: 'kept' },
        { account: 'account-7', hold: 'missing' },
        { account: 'account-8', hold: 'outside' },
        { account: 'account-9', hold: 'outside' },
      ],
    };

    expect(heldLine(next, labelOf)).toMatch(new RegExp(
      '^account-1 is cooling until .+; account-4 is near its limit; account-5 was refused by its provider; '
      + 'account-6 is kept for conversations; account-7 is not on this machine\\. Not used here: account-8, account-9\\.$'));
    expect(heldLine({ ...next, others: [{ account: 'account-3', hold: 'ready' }] }, labelOf)).toBeNull();
    expect(heldLine({ ...next, others: [{ account: null, hold: 'cooling', until: '2026-10-03T16:02:00Z' }] }, labelOf))
      .toMatch(/^the tool's own sign-in is cooling until .+\.$/);

    await i18n.changeLanguage('zh');
    expect(heldLine({ ...next, others: [{ account: 'account-5', hold: 'signedOut' }, { account: 'account-8', hold: 'outside' }, { account: 'account-9', hold: 'outside' }] }, labelOf))
      .toBe('account-5 未登录。这里不使用：account-8、account-9。');
  });

  /** TOOL6e: when a cool-off ended, the account offered again since then. */
  it('says since when an account is offered again', async () => {
    expect(offeredLine('2026-10-03T10:17:00Z')).toMatch(/^offered again since .+$/);
    await i18n.changeLanguage('zh');
    expect(offeredLine('2026-10-03T10:17:00Z')).toMatch(/^自 .+ 起重新可用$/);
  });

  /** D125 §3.7: who the tool's own sign-in is where it says, and its cool-off where it cools. */
  it('says the tool\'s own sign-in, who it is, and its cool-off', () => {
    expect(ownLine('someone@example.invalid', null)).toMatch(/^Sessions run on your own sign-in, someone@example\.invalid: /);
    expect(ownLine(null, null)).toMatch(/^Sessions run on your own sign-in: /);
    expect(ownLine(null, cooling)).toMatch(/It is cooling until .+\. If you have signed in to another account since, look again\.$/);
  });
});
