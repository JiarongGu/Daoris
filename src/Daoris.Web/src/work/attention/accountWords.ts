import i18n from '../../locales/i18n';
import type { Quest } from '../../data/api';
import { accountLabel } from '../../agents/agents';
import { clockOf, list, moment } from '../../locales/format';
import type { Tool } from '../../agents/tools';
import type { Attention } from './AttentionRow';
import type { AttentionNames, PushedAttention } from './attention';
import { questName } from '../identity';

// *What needs you*'s accounts in words. Which starts wait on their accounts, and which
// signed-out account a list holds, is judged in .NET (`AccountAttention`); the page names each account as the roster it
// holds names it, says what the start holds, and says why it waits, in the reader's language.

/**
 * An account's state as last known: out, cooling, unknown or in, and a key's, which no sign-in names: unchecked, or refused by its
 * provider, which waits on the person as a sign-out does and is repaired by a new key, never a sign-in.
 */
export type NamedState = 'out' | 'cooling' | 'unknown' | 'in' | 'keyed' | 'refused';

/** One account a row names: its id (null for the tool's own sign-in), its name, its state, and its read time. */
export type NamedAccount = {
  id: string | null;
  label: string;
  state: NamedState;
  /** When its state is read; null where it is unread. */
  read: string | null;
  /** Its cool-off's end, where it cools. */
  until: string | null;
  /**
   * Its cool-off is held in memory only, the file not having taken it (`AccountCooling.Unwritten`): the
   * machine's problem, said beside the account. Absent where it is kept.
   */
  unwritten?: boolean;
};

/** Whose an account row is, so its acts and its door know the agent. */
export type AccountRowFacts = {
  /** The agent's id: what the door opens and the terminal types. */
  agent: string;
  /** What a person calls it: the door's words. */
  product: string;
  /** The door a sign-in runs on, the account-owning one; null where the roster has not answered. */
  door: string | null;
  /** Whether Daoris can run that agent's sign-in here (installed, and its door has one). */
  signsIn: boolean;
  /** The accounts the row names, in the order a start would walk them. */
  named: NamedAccount[];
  /**
   * The start is held because no Daoris account of the agent's is signed in: the row names no account and
   * offers *Sign in for Daoris*, which runs the add flow's sign-in in place.
   */
  noAccount?: boolean;
};

/** One account a pushed row names: its id, the name the look read beside it (null where none), its state as judged. */
export type PushedNamed = Omit<NamedAccount, 'label'> & { name?: string | null };

/**
 * An account row's facts as pushed: every value of `AccountRowFacts` but the names, which the page gives. There is no account
 * outside a start's list and none to read first: every account serves every workspace, and the page reads neither.
 */
export type PushedAccountFacts = Omit<AccountRowFacts, 'named'> & { named: PushedNamed[] };

/**
 * An account's name as the row says it: the roster's, where it holds the account; the tool's own sign-in as
 * the person knows it; else the name the look read beside it, else its id.
 */
function labelOf(tool: Tool | undefined, id: string | null, name?: string | null): string {
  if (id === null) return i18n.t('agents.account.own');
  const account = tool?.accounts.find((each) => each.name === id);
  return account ? accountLabel(account, tool) : name?.trim() || id;
}

/** A pushed row's account facts, each account named as the roster names it. */
export function accountFactsWorded(facts: PushedAccountFacts, tools: readonly Tool[] = []): AccountRowFacts {
  const tool = tools.find((each) => each.name === facts.agent);
  const { agent, product, door: harness, signsIn, named, noAccount } = facts;
  return {
    agent, product, door: harness, signsIn,
    named: named.map(({ name, ...one }) => ({ ...one, label: labelOf(tool, one.id, name) })),
    ...(noAccount !== undefined ? { noAccount } : {}),
  };
}

/**
 * Why the start waits, as the accounts that would serve it stand (*one line of why*): each cooling one until its
 * hold's end, those read signed out together by when, keys their provider refused together, those no read
 * answered. One that reads signed in holds nothing and is not said. Null where none is said.
 */
export function whyLine(named: readonly NamedAccount[], now: Date): string | null {
  const clauses: string[] = [];
  for (const account of named.filter((one) => one.state === 'cooling' && one.until)) {
    clauses.push(i18n.t('work.attention.accounts.cooling', { account: account.label, when: moment(account.until!) }));
    if (account.unwritten) clauses.push(i18n.t('work.attention.accounts.unkept', { account: account.label }));
  }
  const byWhen = new Map<string, string[]>();
  const together = (key: string, label: string) => byWhen.set(key, [...(byWhen.get(key) ?? []), label]);
  for (const account of named.filter((one) => one.state === 'out')) together(`out\n${account.read ?? ''}`, account.label);
  for (const account of named.filter((one) => one.state === 'refused')) together('refused\n', account.label);
  for (const account of named.filter((one) => one.state === 'unknown')) together(`unknown\n${account.read ?? ''}`, account.label);
  for (const [key, labels] of byWhen) {
    const [state, read] = key.split('\n') as [string, string];
    const accounts = list(labels);
    if (state === 'refused') {
      clauses.push(i18n.t('work.attention.accounts.refused', { accounts }));
    } else if (state === 'out') {
      clauses.push(read
        ? i18n.t('work.attention.accounts.out', { accounts, when: clockOf(read, now) })
        : i18n.t('work.attention.accounts.outUnread', { accounts }));
    } else {
      clauses.push(read
        ? i18n.t('work.attention.accounts.failed', { accounts, when: clockOf(read, now) })
        : i18n.t('work.attention.accounts.never', { accounts }));
    }
  }
  return clauses.length > 0
    ? i18n.t('work.attention.accounts.line', { clauses: clauses.join(i18n.t('work.attention.accounts.join')) })
    : null;
}

/** What a held start holds, as its row's title: one quest by its name, one intake as Sessions names it, else counted. */
export function heldTitle(holding: { quests: readonly string[]; asks: readonly string[] }, quests: readonly Quest[] = []): string {
  if (holding.quests.length === 1 && holding.asks.length === 0) {
    const quest = quests.find((one) => one.id === holding.quests[0]);
    return quest ? questName(quest) : `#${holding.quests[0]}`;
  }
  if (holding.quests.length === 0 && holding.asks.length === 1) return i18n.t('work.intake.title', { ask: holding.asks[0] });
  const counted = i18n.t('work.attention.held.quests', { count: holding.quests.length });
  if (holding.asks.length === 0) return counted;
  return holding.quests.length === 0
    ? i18n.t('work.attention.held.intakes', { count: holding.asks.length })
    : i18n.t('work.attention.held.both', { quests: counted, count: holding.asks.length });
}

/** Whether the agent is held by an API key alone, so what its accounts lack is a key, never a sign-in. */
function heldByKey(tools: readonly Tool[] | undefined, agent: string): boolean {
  const acts = tools?.find((each) => each.name === agent)?.acts;
  return acts?.takesKey === true && acts.signsIn !== true;
}

/**
 * An account's row worded: a start waiting for accounts, named by what it holds, its line saying the accounts
 * that would serve it as last known with each one's read time, or that no Daoris account is signed in; a signed-out
 * account, named as the roster names it, its line saying a key refused. Where it runs is not said: every account of Daoris's
 * serves every workspace. A signed-out account's row has no wait: it says its read time.
 */
export function accountRowWorded(row: PushedAttention, names: AttentionNames, now: Date): Attention {
  const pushed = row.account ?? { agent: '', product: '', door: null, signsIn: false, named: [] };
  const account = accountFactsWorded(pushed, names.tools);
  const base = { id: row.id, kind: row.kind, since: row.since, where: row.where ?? '', account };
  if (row.kind === 'account-wait') {
    return {
      ...base,
      title: heldTitle(row.holding ?? { quests: [], asks: [] }, names.quests),
      circle: row.wholeWorkspace === true,
      // No account to name where none is signed in: the hold says so itself, once, as the driver does.
      detail: account.noAccount && account.named.length === 0
        ? i18n.t(heldByKey(names.tools, account.agent) ? 'work.attention.accounts.noKey' : 'work.attention.accounts.noAccount', { product: account.product })
        : whyLine(account.named, now),
    };
  }
  const [one] = account.named;
  // A key its provider refused says so, and where it is repaired: its agent's page, where a new key is added.
  const detail = one?.state === 'refused' ? i18n.t('work.attention.signedOut.refused', { product: account.product }) : null;
  return { ...base, title: one?.label ?? row.id, read: row.read ?? null, detail };
}
