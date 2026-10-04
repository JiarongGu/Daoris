import i18n from '../i18n';
import { clockOf, moment } from '../format';
import type { Account, Tool } from '../tools';
import {
  type AccountCooling, type AccountFacts, type AccountScope, type AccountsAnswer, type AgentAccounts, agentOf, machineScope,
  workspaceScope,
} from '../settings/accounts';
import type { AgentRulesState } from '../settings/AgentRules';
import { OPEN_STATES } from '../settings/proposals';

// The Agents place's facts and words (UX6e, D150 §5), as the list, the page and the activity bar read them. Pure: the
// molecules and the tests read these, with no bridge. An account's state is what was last known, with when it was read;
// nothing here asks anything (§5.3).

/** A part of an agent's page a door names (D150 §2.4): its accounts, what it may do, or its usage. */
export type AgentPart = 'accounts' | 'rules' | 'usage';

/** What one account has carried (TOOL3, D57 §4), as the usage answer counts it per door; null is the tool's own sign-in. */
export type AgentUsage = { harness: string; profile: string | null; sessions: number; used: number };

/**
 * An account's state as last known (D150 §5.3): signed in, signed out, cooling until a time, or unknown. An API key says
 * its handle and has no sign-in (`keyed`): the tool says signed in for any key, so that word would claim what nobody read.
 */
export type AccountStateName = 'in' | 'out' | 'cooling' | 'unknown' | 'keyed';

export type AccountState = {
  state: AccountStateName;
  /** When its state was read; null where it never was. */
  read: string | null;
  /** Its cool-off, where it cools. */
  cooling: AccountCooling | null;
  /** A list or a default holds it, or the starts run on it: a signed-out one then holds work, and wears open's hue. */
  holdsWork: boolean;
};

/**
 * One account's state from what the roster and the accounts' files last said: signed out first, since a sign-in is the
 * person's and a reset frees nothing; then a cool-off; then a key, which no sign-in names; then the agent's word.
 */
export function accountState(
  account: Pick<Account, 'login' | 'read' | 'key'>, facts: Pick<AccountFacts, 'cooling'> | null, holdsWork: boolean,
): AccountState {
  const cooling = facts?.cooling ?? null;
  const read = account.read ?? null;
  const state: AccountStateName = account.login === 'out' && !account.key
    ? 'out'
    : cooling ? 'cooling' : account.key ? 'keyed' : account.login === 'in' ? 'in' : 'unknown';
  return { state, read, cooling, holdsWork };
}

/** Whether a default or a list names the account: the machine's or a workspace's (D130 §3.1). */
export function heldBy(tool: Tool, use: AgentAccounts | null | undefined, name: string): boolean {
  if (tool.machineDefault === name) return true;
  if (tool.workspaceDefaults.some((circle) => circle.profile === name)) return true;
  return (use?.scopes ?? []).some((scope) => scope.default === name || scope.list.includes(name));
}

/** Whether the starts run on the tool's own sign-in: no machine default and no list name an account (D125 §3.7). */
export function sharesOwn(tool: Tool, use: AgentAccounts | null | undefined): boolean {
  return tool.present && tool.machineDefault === null && (use ? machineScope(use).list.length === 0 : true);
}

/** The tool's own sign-in's state: its word only at a person's press (TOOL6g), its cool-off where it cools. */
export function ownState(tool: Tool, use: AgentAccounts | null | undefined): AccountState {
  const login = tool.ownLogin === 'in' || tool.ownLogin === 'out' ? tool.ownLogin : 'unknown';
  return accountState({ login, read: tool.ownRead }, { cooling: use?.own.cooling ?? null }, sharesOwn(tool, use));
}

/** Each named account's state on this agent, by its directory. */
export function accountStates(tool: Tool, use: AgentAccounts | null | undefined): Map<string, AccountState> {
  return new Map(tool.accounts.map((account) => [
    account.name,
    accountState(account, use?.accounts.find((facts) => facts.name === account.name) ?? null, heldBy(tool, use, account.name)),
  ]));
}

/**
 * The accounts the person must act on (D150 §2.1, the activity bar's badge): each signed out where it holds work, the
 * tool's own sign-in among them while the starts run on it.
 */
export function signedOutHeld(tool: Tool, use: AgentAccounts | null | undefined): number {
  const named = [...accountStates(tool, use).values()].filter((state) => state.state === 'out' && state.holdsWork).length;
  const own = ownState(tool, use);
  return named + (own.state === 'out' && own.holdsWork ? 1 : 0);
}

/** A state's word and its pill's tone (§5.3): open's hue only for a signed-out account that holds work. */
export function stateWord(state: AccountState): { label: string; tone: 'open' | 'neutral' } {
  switch (state.state) {
    case 'out': return { label: i18n.t('harness.login.out'), tone: state.holdsWork ? 'open' : 'neutral' };
    case 'cooling': return { label: i18n.t('agents.state.cooling', { when: moment(state.cooling!.until) }), tone: 'neutral' };
    case 'in': return { label: i18n.t('harness.login.in'), tone: 'neutral' };
    case 'keyed': return { label: i18n.t('harness.login.keyed'), tone: 'neutral' };
    default: return { label: i18n.t('harness.login.unknown'), tone: 'neutral' };
  }
}

/**
 * When the state was read, as its row says it (§5.3): *read 10:42*, *never read*, and for an unknown that was read, that
 * the last read failed. A cool-off says its own time, the reset its agent named.
 */
export function readLine(state: AccountState, now: Date = new Date()): string | null {
  if (state.state === 'cooling') return null;
  if (!state.read) return i18n.t('agents.read.never');
  const when = clockOf(state.read, now);
  return state.state === 'unknown' ? i18n.t('agents.read.failed', { when }) : i18n.t('agents.read.at', { when });
}

/** The latest moment any of an agent's accounts was read, for its Accounts section's head; null where none was. */
export function latestRead(states: readonly AccountState[]): string | null {
  return states.map((state) => state.read).filter((read): read is string => Boolean(read)).sort().at(-1) ?? null;
}

/** One agent as the list shows it (§5.1). */
export type AgentRow = {
  /** The agent's id, a terminal's word and the list's item. */
  name: string;
  /** What a person calls it, and whose it is. */
  product: string;
  maker: string | null;
  installed: boolean;
  /** Its accounts in a phrase: *3 accounts · 2 signed out*, *1 account*, *not installed*. */
  phrase: string;
  /** The accounts the person must act on, which wear the waiting mark. */
  waiting: number;
};

/** Each agent once, whatever doors reach it, in the roster's order (§5.1). */
export function agentRows(tools: readonly Tool[], answer: AccountsAnswer | null | undefined): AgentRow[] {
  return tools.map((tool) => {
    const use = agentOf(answer, tool.name);
    const states = [...accountStates(tool, use).values()];
    const signedOut = states.filter((state) => state.state === 'out').length;
    // An agent with no account of Daoris's own runs on its own sign-in, where it has one; one with no sign-in at all (a
    // plugin's agent) is said to be installed and nothing more.
    const phrase = !tool.present
      ? i18n.t('harness.absent')
      : tool.accounts.length === 0
        ? i18n.t(tool.doors[0]?.signsIn === false ? 'harness.installed' : 'agents.row.own')
        : [
          i18n.t('agents.row.accounts', { count: tool.accounts.length }),
          ...(signedOut > 0 ? [i18n.t('agents.row.signedOut', { count: signedOut })] : []),
        ].join(' · ');
    return {
      name: tool.name,
      product: tool.product ?? tool.name,
      maker: tool.maker,
      installed: tool.present,
      phrase,
      waiting: tool.present ? signedOutHeld(tool, use) : 0,
    };
  });
}

// ---------------------------------------------------------------- each folded section's line (D150 §1 rule 4, §5.2)

/** How accounts are used, folded: the list's mode and switching early, or where the starts run with no list. */
export function useSummary(machine: AccountScope, labelOf: (name: string) => string): string {
  if (machine.list.length === 0) {
    return i18n.t('agents.use.summary.runs', {
      account: machine.default ? labelOf(machine.default) : i18n.t('harness.use.emptyOwn'),
    });
  }
  const mode = i18n.t(machine.use.use === 'order' ? 'harness.use.mode.order' : 'harness.use.mode.goal');
  return machine.use.early ? `${mode} · ${i18n.t('agents.use.summary.early')}` : mode;
}

/** Each workspace, folded: on its own accounts with its list, or on this machine's. */
export function workspacesSummary(use: AgentAccounts | null | undefined, workspaces: readonly string[], labelOf: (name: string) => string): string {
  if (workspaces.length === 0) return i18n.t('agents.workspaces.none');
  return workspaces.map((workspace) => {
    const own = use ? workspaceScope(use, workspace) : null;
    if (!own) return i18n.t('agents.workspaces.machine', { workspace });
    const named = own.list.length > 0 ? own.list : own.default ? [own.default] : [];
    return i18n.t('agents.workspaces.own', { workspace, accounts: named.map(labelOf).join(', ') });
  }).join(' · ');
}

/** The ways in, folded: each door's word and version, and the one driven work starts on (`driver.json`'s adapter). */
export function doorsSummary(tool: Tool, adapter: string | undefined): string {
  return tool.doors.map((door) => {
    const word = i18n.t(door.wire === 'acp' ? 'harness.wire.acp' : 'harness.wire.pipe');
    if (!door.present) return i18n.t('agents.doors.absent', { door: word });
    const version = door.version ?? '';
    return i18n.t(door.harness === adapter ? 'agents.doors.driven' : 'agents.doors.door', { door: word, version });
  }).join(' · ');
}

/** What it may do, folded: Daoris's defaults on, the person's rules for every session, and the proposals waiting. */
export function rulesSummary(rules: AgentRulesState): string {
  const machine = rules.scopes.find((scope) => scope.scope === 'machine');
  const mine = machine ? machine.allow.length + machine.ask.length + machine.deny.length : 0;
  const waiting = (rules.proposals ?? []).filter((proposal) => OPEN_STATES.has(proposal.state)).length;
  return [
    i18n.t('agents.rules.summary.defaults', { count: rules.defaults.filter((shipped) => shipped.on).length }),
    i18n.t('agents.rules.summary.mine', { count: mine }),
    ...(waiting > 0 ? [i18n.t('agents.rules.summary.proposals', { count: waiting })] : []),
  ].join(' · ');
}

/** The model and effort, folded: the tool's own defaults, or how many accounts set their own (AGT6). */
export function settingsSummary(tool: Tool): string {
  const set = tool.accounts.filter((account) => account.settings
    && (account.settings.model !== null || account.settings.effort !== null || account.settings.perModel.length > 0)).length;
  return set === 0 ? i18n.t('agents.settings.summary.default') : i18n.t('agents.settings.summary.set', { count: set });
}

/** Usage, folded: the sessions measured, and nothing measured said so, never zero (D57). */
export function usageSummary(sessions: number): string {
  return sessions > 0 ? i18n.t('agents.usage.summary', { count: sessions }) : i18n.t('agents.usage.none');
}
