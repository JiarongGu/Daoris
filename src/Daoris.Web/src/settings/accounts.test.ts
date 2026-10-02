import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../i18n';
import {
  type AccountScope, type AgentAccounts, agentOf, cannotLeave, coolingLine, listedIn, machineScope, moved, nothingSaid, ownLine,
  saidLine, used, USE_DEFAULTS, workspaceScope,
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

  /** D125 §3.7: who the tool's own sign-in is where it says, and its cool-off where it cools. */
  it('says the tool\'s own sign-in, who it is, and its cool-off', () => {
    expect(ownLine('someone@example.invalid', null)).toMatch(/^Sessions run on your own sign-in, someone@example\.invalid: /);
    expect(ownLine(null, null)).toMatch(/^Sessions run on your own sign-in: /);
    expect(ownLine(null, cooling)).toMatch(/It is cooling until .+\. If you have signed in to another account since, look again\.$/);
  });
});
