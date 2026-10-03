import { type AccountScope, type AgentAccounts, USE_DEFAULTS } from './accounts';
import type { AccountChoice } from './AccountUse';

// How an agent's accounts are used, as the stories and the molecules' tests hand it (TOOL4g): one account, three, and six,
// since nothing counts accounts; a cooling one, one whose agent said it is near, one that said nothing, and a workspace
// on its own list. Names are neutral, as every fixture's are.

const minutesFrom = (minutes: number) => new Date(Date.now() + minutes * 60_000).toISOString();

export const SIX = ['account-1', 'account-2', 'account-3', 'account-4', 'account-5', 'account-6'];

/** What a person calls each account: who signed in, else a key's handle, else its directory. */
export const CHOICES: AccountChoice[] = [
  { name: 'account-1', label: 'work@example.invalid', login: 'in' },
  { name: 'account-2', label: 'home@example.invalid', login: 'in' },
  { name: 'account-3', label: 'spare@example.invalid', login: 'out' },
];

export const SIX_CHOICES: AccountChoice[] = SIX.map((name, at) => ({ name, label: `seat-${at + 1}@example.invalid`, login: 'in' }));

export const scopeOf = (over: Partial<AccountScope> = {}): AccountScope => ({
  workspace: null, default: null, list: [], begins: null, use: USE_DEFAULTS, unknown: [], problem: null, near: [], ...over,
});

/** account-1 cooling until a stated reset; account-2 said 88% of its five-hour window; account-3 said nothing yet. */
export const THREE: AgentAccounts = {
  agent: 'claude-code',
  speaks: true,
  own: {},
  accounts: [
    {
      name: 'account-1', running: 0, week: minutesFrom(60 * 30),
      cooling: { until: minutesFrom(190), stated: true, window: 'weekly', seen: minutesFrom(-10), assumedZone: false, notBelieved: false },
    },
    {
      name: 'account-2', running: 2,
      said: {
        seen: minutesFrom(-180),
        windows: [
          { window: 'session', used: 0.88, reset: minutesFrom(120), standing: 'clear', credits: false, seen: minutesFrom(-180) },
          { window: 'weekly', used: 0.14, reset: minutesFrom(60 * 24 * 4), credits: false, seen: minutesFrom(-180) },
        ],
      },
    },
    { name: 'account-3', running: 1 },
  ],
  scopes: [
    // Its default cooling: of the rest, the one running fewer of Daoris's sessions (TOOL6e).
    scopeOf({
      default: 'account-1', list: ['account-1', 'account-2', 'account-3'], begins: 'account-1', near: [],
      next: {
        account: 'account-3', reason: 'fewest', over: 'account-2',
        others: [{ account: 'account-1', hold: 'cooling', until: minutesFrom(190) }, { account: 'account-2', hold: 'ready' }],
      },
    }),
    scopeOf({
      workspace: 'work', list: ['account-2', 'account-1'], begins: 'account-2', use: { ...USE_DEFAULTS, use: 'order' },
      next: {
        account: 'account-2', reason: 'onlyReady',
        others: [{ account: 'account-1', hold: 'cooling', until: minutesFrom(190) }, { account: 'account-3', hold: 'outside' }],
      },
    }),
  ],
};

/** The machine's list near by its own near of 85: account-2's five-hour window at 88%. */
export const MACHINE_NEAR = scopeOf({
  default: 'account-1', list: ['account-1', 'account-2', 'account-3'], begins: 'account-1',
  use: { ...USE_DEFAULTS, keep: 'account-3', near: 85 },
  near: [{ account: 'account-2', window: 'session', by: 'number' }],
});

/** Six accounts, none of which has said anything, one kept for conversations. */
export const SIX_AGENT: AgentAccounts = {
  agent: 'claude-code',
  speaks: true,
  own: {},
  accounts: SIX.map((name) => ({ name, running: 0 })),
  scopes: [scopeOf({ list: SIX, begins: 'account-1', use: { ...USE_DEFAULTS, keep: 'account-6' } })],
};

/** An agent whose sessions say nothing of their limits: switching before the limit waits for that word. */
export const SILENT: AgentAccounts = {
  agent: 'codex',
  speaks: false,
  own: {},
  accounts: [{ name: 'account-1' }, { name: 'account-2' }],
  scopes: [scopeOf({ list: ['account-1', 'account-2'], begins: 'account-1' })],
};

/**
 * TOOL6e, the owner's case: the work workspace's default came out of its cool-off 58 minutes ago, nothing runs, and the
 * others started since, so the next start takes it; account-3 cools until a stated reset. This machine's list begins at
 * account-1 too, by the list's order.
 */
export const OFFERED_AGAIN: AgentAccounts = {
  agent: 'claude-code',
  speaks: true,
  own: {},
  accounts: [
    { name: 'account-1', running: 0, offered: minutesFrom(-58) },
    { name: 'account-2', running: 0 },
    {
      name: 'account-3', running: 0,
      cooling: { until: minutesFrom(190), stated: true, window: 'session', seen: minutesFrom(-60), assumedZone: false, notBelieved: false },
    },
  ],
  scopes: [
    scopeOf({
      list: ['account-1', 'account-2', 'account-3'], begins: 'account-1',
      next: {
        account: 'account-1', reason: 'list', over: 'account-2',
        others: [{ account: 'account-2', hold: 'ready' }, { account: 'account-3', hold: 'cooling', until: minutesFrom(190) }],
      },
    }),
    scopeOf({
      workspace: 'work', default: 'account-1', list: ['account-1', 'account-2', 'account-3'], begins: 'account-1',
      next: {
        account: 'account-1', reason: 'leastRecent', over: 'account-2',
        others: [{ account: 'account-2', hold: 'ready' }, { account: 'account-3', hold: 'cooling', until: minutesFrom(190) }],
      },
    }),
  ],
};

/** The tool's own sign-in cooling on the agent's default, with no reset named. */
export const OWN_COOLING = {
  until: minutesFrom(45), stated: false, window: null, seen: minutesFrom(-15), assumedZone: false, notBelieved: false,
};
