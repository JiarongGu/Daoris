import i18n from '../i18n';
import type { Ask, Quest, Registration } from '../api';
import { accountName, accountState, accountStates, joinChoices, ownState, runsFor } from '../agents/agents';
import { clockOf, list, moment } from '../format';
import {
  type AccountCooling, type AccountScope, type AccountsAnswer, type AgentAccounts, agentOf, machineScope, workspaceScope,
} from '../settings/accounts';
import type { AccountWaitTick, Consideration } from '../signals';
import type { Tool } from '../tools';
import { workspaceOf } from '../workspaces';
import type { Attention } from './AttentionRow';
import { questName } from './identity';

// *What needs you*'s accounts (UX6d, D150 搂6.2鈥撀?.3; TOOL4m's row): a start waiting for accounts, and a signed-out account a
// list or a default holds that no waiting start names. Pure: every fact is one the page already holds (the tick's waits and
// considerations, the roster's last readings, the accounts' files), so nothing here asks the bridge, and no row starts a
// process to find out (搂6.3).

/** What the page knows of the accounts, each answer as it holds it: none of them in a browser. */
export type AccountsKnown = {
  /** The roster, by agent (`byTool`): each account's last reading and when (ROSTER1), its doors. */
  tools: readonly Tool[];
  /** The accounts' files (`ACCOUNTS`): each scope's list and default, each account's cool-off. */
  use?: AccountsAnswer | null;
  /** The starts the last tick held on cooling accounts, an intake's among them (the tick's `waits`). */
  waits?: readonly AccountWaitTick[] | null;
};

/** An account's state as last known, the four of D150 搂5.3 and a key's, which no sign-in names. */
export type NamedState = 'out' | 'cooling' | 'unknown' | 'in' | 'keyed';

/** One account a row names (D150 搂5.3): its id (null for the tool's own sign-in), its name, its state, and when it was read. */
export type NamedAccount = {
  id: string | null;
  label: string;
  state: NamedState;
  /** When its state was read; null where it never was. */
  read: string | null;
  /** Its cool-off's end, where it cools. */
  until: string | null;
};

/** Whose an account row is, so its acts and its door know the agent (UX6d). */
export type AccountRowFacts = {
  /** The agent's id: what the door opens and the terminal types. */
  agent: string;
  /** What a person calls it: the door's words. */
  product: string;
  /** The door a sign-in runs on, the account-owning one; null where the roster has not answered. */
  harness: string | null;
  /** Whether Daoris can run that agent's sign-in here (installed, and its door has one). */
  signsIn: boolean;
  /** The accounts the row names, in the order a start would walk them. */
  named: NamedAccount[];
  /**
   * A ready account outside the list the waiting start reads, which the person may let run the work (D130 搂3.3): the list
   * the join adds it to (a workspace's, or null for this machine's) and the workspace that waits. Null where none is ready.
   */
  outside?: { id: string; label: string; list: string | null; workspace: string } | null;
};

/** One start the driver held on its accounts, gathered from the tick: what it holds, whose accounts, and why. */
type Held = {
  key: string;
  agent: string;
  /** The account whose cool-off ends first, as the tick named it; null for the tool's own sign-in. */
  cooling: { account: string | null; name: string | null; until: string; stated: boolean } | null;
  /** The accounts its start passed signed out (TOOL6g), with the names the tick read beside them. */
  signedOut: { id: string; name: string | null }[];
  quests: string[];
  asks: string[];
  workspace: string | null;
};

/**
 * The starts held on their accounts: each of the tick's waits (one per account, an intake's among them), then each quest a
 * consideration says waits that no wait holds, which is a start held on signed-out accounts alone (TOOL6g) or a shell whose
 * tick carries no waits. Quests held alike, in one workspace, are one start's wait.
 */
function heldStarts(
  waits: readonly AccountWaitTick[], considered: readonly Consideration[], quests: readonly Quest[],
  registry: readonly Registration[],
): Held[] {
  const held: Held[] = waits.map((wait) => ({
    key: `wait:${wait.agent}/${wait.account ?? ''}`,
    agent: wait.agent,
    cooling: { account: wait.account ?? null, name: wait.name ?? null, until: wait.until, stated: wait.stated },
    signedOut: (wait.signedOut ?? []).map((id) => ({ id, name: null })),
    quests: wait.quests ?? [],
    asks: wait.asks ?? [],
    workspace: wait.workspace ?? null,
  }));
  const inAWait = new Set(held.flatMap((wait) => wait.quests));
  const grouped = new Map<string, Held>();
  for (const consideration of considered) {
    const { waitsFor, signedOut } = consideration;
    if (consideration.verdict !== 'Blocked' || inAWait.has(consideration.quest)) continue;
    if (!waitsFor && !signedOut?.accounts?.length) continue;
    const agent = waitsFor?.agent ?? signedOut!.agent;
    const quest = quests.find((one) => one.id === consideration.quest);
    const registered = registry.find((row) => row.repository === consideration.repository);
    const workspace = quest?.workspace ?? (registered ? workspaceOf(registered) : null);
    const passed = (signedOut?.accounts ?? []).map((id, at) => ({ id, name: signedOut?.names?.[at]?.trim() || null }));
    const key = [
      'quest', agent, waitsFor ? `${waitsFor.account ?? ''}@${waitsFor.until}` : '', passed.map(({ id }) => id).sort().join(','),
      workspace ?? '',
    ].join('/');
    const seen = grouped.get(key);
    if (seen) {
      seen.quests.push(consideration.quest);
      continue;
    }
    grouped.set(key, {
      key, agent,
      cooling: waitsFor
        ? { account: waitsFor.account ?? null, name: waitsFor.name?.trim() || null, until: waitsFor.until, stated: waitsFor.stated }
        : null,
      signedOut: passed, quests: [consideration.quest], asks: [], workspace,
    });
  }
  return [...held, ...grouped.values()];
}

/**
 * The scope a start in this workspace reads (D130 搂3.1): its own where it names a default or a list, else this machine's.
 * None where the held starts span workspaces, since then no one list is theirs.
 */
function scopeOf(use: AgentAccounts | null, workspace: string | null): AccountScope | null {
  if (!use || !workspace) return null;
  return workspaceScope(use, workspace) ?? machineScope(use);
}

/**
 * The accounts that would serve a held start: its scope's list, else its default, else the tool's own sign-in (D125 搂3.7),
 * less the account kept for conversations, which a driven start drops (D130 point 6) unless it would leave none; then
 * whatever the tick named that the list no longer holds, since that is what held it.
 */
function servingIds(scope: AccountScope | null, held: Held): (string | null)[] {
  const ids: (string | null)[] = [];
  const add = (id: string | null) => { if (!ids.includes(id)) ids.push(id); };
  if (scope) {
    const listed = scope.list.length > 0 ? scope.list : scope.default ? [scope.default] : [];
    const kept = listed.filter((id) => id !== scope.use.keep);
    for (const id of kept.length > 0 ? kept : listed) add(id);
    if (listed.length === 0) add(null);
  }
  if (held.cooling) add(held.cooling.account);
  for (const { id } of held.signedOut) add(id);
  return ids;
}

/**
 * One account as last known (搂5.3): the roster's reading with when, the accounts' files' cool-off. Where the roster has no
 * reading, the tick's word stands: the start's own refusal said it signed out, or its wait said it cools. A reading the
 * roster holds is never overruled by the tick, since a sign-in since the look is newer.
 */
function namedAccount(
  id: string | null, tool: Tool | null, use: AgentAccounts | null, scope: AccountScope | null, held: Held,
): NamedAccount {
  const ticked = held.cooling && held.cooling.account === id
    ? { until: held.cooling.until, stated: held.cooling.stated, seen: '', assumedZone: false, notBelieved: false } as AccountCooling
    : null;
  const passed = id !== null && held.signedOut.some((one) => one.id === id);
  // The driver's own judgement of what holds each account, asked of the walk without probing (TOOL6e).
  const holds = scope?.next?.others.find((other) => (other.account ?? null) === id)?.hold;
  const tickedName = id === null ? null
    : held.cooling?.account === id ? held.cooling.name : held.signedOut.find((one) => one.id === id)?.name ?? null;

  if (id === null) {
    const own = tool ? ownState(tool, use) : null;
    const cooling = use?.own.cooling ?? ticked;
    const state: NamedState = own?.state === 'out' ? 'out' : cooling ? 'cooling' : own?.state === 'in' ? 'in' : 'unknown';
    return { id, label: i18n.t('agents.account.own'), state, read: own?.read ?? null, until: cooling?.until ?? null };
  }
  const account = tool?.accounts.find((each) => each.name === id) ?? null;
  const cooling = use?.accounts.find((facts) => facts.name === id)?.cooling ?? ticked;
  const known = account
    ? accountState(account, { cooling }, true).state
    : cooling ? 'cooling' : 'unknown';
  const out = known === 'unknown' && (passed || holds === 'signedOut' || holds === 'refused');
  return {
    id,
    label: account ? accountName(account) : tickedName ?? id,
    state: out ? 'out' : known,
    read: account?.read ?? null,
    until: cooling?.until ?? null,
  };
}

/**
 * Why the start waits, as the accounts that would serve it stand (搂6.3's *one line of why*): each cooling one until its
 * reset, those read signed out together by when, those no read answered. One that reads signed in holds nothing and is not
 * said. Null where none is said.
 */
function whyLine(named: readonly NamedAccount[], now: Date): string | null {
  const clauses: string[] = [];
  for (const account of named.filter((one) => one.state === 'cooling' && one.until)) {
    clauses.push(i18n.t('work.attention.accounts.cooling', { account: account.label, when: moment(account.until!) }));
  }
  const byWhen = new Map<string, string[]>();
  const together = (key: string, label: string) => byWhen.set(key, [...(byWhen.get(key) ?? []), label]);
  for (const account of named.filter((one) => one.state === 'out')) together(`out\n${account.read ?? ''}`, account.label);
  for (const account of named.filter((one) => one.state === 'unknown')) together(`unknown\n${account.read ?? ''}`, account.label);
  for (const [key, labels] of byWhen) {
    const [state, read] = key.split('\n') as [string, string];
    const accounts = list(labels);
    if (state === 'out') {
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

/**
 * A ready account outside the list the start reads, which the person may let run the work (D130 搂3.3): one the join would
 * take (ACCT1's rules, `joinChoices`), neither cooling nor signed out, a reading of signed in or a key first. None where the
 * wait spans workspaces, since there is then no one list to add it to.
 */
function outsideOf(
  tool: Tool | null, use: AgentAccounts | null, scope: AccountScope | null, held: Held, named: readonly NamedAccount[],
): AccountRowFacts['outside'] {
  if (!tool || !use || !scope || !held.workspace) return null;
  const target = scope.workspace ?? null;
  const states = accountStates(tool, use);
  const candidates = tool.accounts.filter((account) => {
    if (named.some((one) => one.id === account.name)) return false;
    const state = states.get(account.name)?.state;
    if (state === 'out' || state === 'cooling') return false;
    return joinChoices(tool, use, [], (name) => name, account.name).some((choice) => choice.workspace === target);
  });
  const ready = (state: string | undefined) => state === 'in' || state === 'keyed';
  const chosen = candidates.find((account) => ready(states.get(account.name)?.state)) ?? candidates[0];
  return chosen ? { id: chosen.name, label: accountName(chosen), list: target, workspace: held.workspace } : null;
}

/** What a held start holds, as its row's title: one quest by its name, one intake as Sessions names it, else counted. */
function heldTitle(held: Held, quests: readonly Quest[]): string {
  if (held.quests.length === 1 && held.asks.length === 0) {
    const quest = quests.find((one) => one.id === held.quests[0]);
    return quest ? questName(quest) : `#${held.quests[0]}`;
  }
  if (held.quests.length === 0 && held.asks.length === 1) return i18n.t('work.intake.title', { ask: held.asks[0] });
  const counted = i18n.t('work.attention.held.quests', { count: held.quests.length });
  if (held.asks.length === 0) return counted;
  return held.quests.length === 0
    ? i18n.t('work.attention.held.intakes', { count: held.asks.length })
    : i18n.t('work.attention.held.both', { quests: counted, count: held.asks.length });
}

/**
 * Since when it has waited: since what it holds was asked for (the oldest quest filed, the oldest ask asked), or since the
 * limit that held it where that came later. A reading's time is when it was read, never when the account signed out, so
 * it moves nothing.
 */
function heldSince(
  held: Held, use: AgentAccounts | null, quests: readonly Quest[], asks: readonly Ask[], now: Date,
): string {
  const asked = [
    ...held.quests.map((id) => quests.find((one) => one.id === id)?.filed),
    ...held.asks.map((id) => asks.find((one) => one.id === id)?.asked),
  ].filter((at): at is string => Boolean(at)).sort();
  const cooled = held.cooling
    ? (held.cooling.account === null ? use?.own.cooling : use?.accounts.find((facts) => facts.name === held.cooling!.account)?.cooling)?.seen
    : undefined;
  const from = asked[0] ?? now.toISOString();
  return cooled && cooled.localeCompare(from) > 0 ? cooled : from;
}

/** Whose a row is: the agent's product, the door its acts run on, and whether Daoris runs its sign-in. */
function rowFacts(agent: string, tool: Tool | null): Omit<AccountRowFacts, 'named' | 'outside'> {
  const door = tool?.doors[0] ?? null;
  return {
    agent,
    product: tool?.product ?? agent,
    harness: door?.harness ?? null,
    signsIn: Boolean(tool?.present && door && door.signsIn !== false),
  };
}

/**
 * *What needs you*'s account rows (UX6d, D150 搂6.2), each holding work: **a start waiting for accounts**, saying the
 * accounts that would serve it as last known with when each was read, and **a signed-out account a list or a default
 * holds** that no waiting start names, which a waiting start otherwise stands for (搂6.3, *one thing is listed once*).
 *
 * @remarks
 * 馃敶 **No row starts a process to find out** (搂6.3, D150 point 5): the waits are the tick's own, read from the cool-offs
 * and the refusals its starts met (D125 搂4); an account's state is the roster's last reading with its time, which opening a
 * view never asks again (ROSTER1); its cool-off and its scope's list are the accounts' files. What no read answered is said
 * so, *unknown, never read*, and never guessed signed in or out.
 *
 * **TOOL4m's row**: the start waiting for an account, with *Let 鈥?run 鈥? (D130 搂3.3) where an account outside its list is
 * ready, which the row asks once before it adds the account to that list.
 */
export function accountAttention(
  known: AccountsKnown,
  considered: readonly Consideration[],
  quests: readonly Quest[],
  asks: readonly Ask[],
  registry: readonly Registration[],
  now: Date = new Date(),
): Attention[] {
  const toolOf = (agent: string) => known.tools.find((tool) => tool.name === agent) ?? null;
  const named = new Set<string>();

  const waiting = heldStarts(known.waits ?? [], considered, quests, registry).map((held): Attention => {
    const tool = toolOf(held.agent);
    const use = agentOf(known.use, held.agent);
    const scope = scopeOf(use, held.workspace);
    const accounts = servingIds(scope, held).map((id) => namedAccount(id, tool, use, scope, held));
    for (const account of accounts) named.add(`${held.agent}/${account.id ?? ''}`);
    const repositories = [...new Set(held.quests.map((id) => quests.find((one) => one.id === id)?.to
      ?? considered.find((one) => one.quest === id)?.repository).filter(Boolean))];
    const inOne = held.asks.length === 0 && repositories.length === 1;
    return {
      id: held.key,
      kind: 'account-wait',
      title: heldTitle(held, quests),
      where: inOne ? repositories[0]! : held.workspace ?? '',
      circle: !inOne && Boolean(held.workspace),
      since: heldSince(held, use, quests, asks, now),
      detail: whyLine(accounts, now),
      account: { ...rowFacts(held.agent, tool), named: accounts, outside: outsideOf(tool, use, scope, held, accounts) },
    };
  });

  const signedOut = known.tools.filter((tool) => tool.present).flatMap((tool): Attention[] => {
    const use = agentOf(known.use, tool.name);
    const states = accountStates(tool, use);
    const facts = rowFacts(tool.name, tool);
    const rows = tool.accounts.flatMap((account): Attention[] => {
      const state = states.get(account.name)!;
      if (state.state !== 'out' || !state.holdsWork || named.has(`${tool.name}/${account.name}`)) return [];
      const runs = runsFor(tool, use, account.name).map((place) => place.workspace ?? i18n.t('agents.runs.machine'));
      return [{
        id: `signed-out:${tool.name}/${account.name}`,
        kind: 'signed-out',
        title: accountName(account),
        where: facts.product,
        since: state.read ?? now.toISOString(),
        detail: [
          state.read ? i18n.t('work.attention.signedOut.read', { when: clockOf(state.read, now) }) : null,
          runs.length > 0 ? i18n.t('work.attention.signedOut.runs', { runs: list(runs) }) : null,
        ].filter(Boolean).join(i18n.t('harness.said.sentences')) || null,
        account: {
          ...facts,
          named: [{ id: account.name, label: accountName(account), state: 'out', read: state.read, until: null }],
          outside: null,
        },
      }];
    });
    // The tool's own sign-in, while the starts run on it (D125 搂3.7): only the person signs it in, where they use the tool.
    const own = ownState(tool, use);
    if (own.state === 'out' && own.holdsWork && !named.has(`${tool.name}/`)) {
      rows.push({
        id: `signed-out:${tool.name}/`,
        kind: 'signed-out',
        title: i18n.t('agents.account.own'),
        where: facts.product,
        since: own.read ?? now.toISOString(),
        detail: [
          own.read ? i18n.t('work.attention.signedOut.read', { when: clockOf(own.read, now) }) : null,
          i18n.t('work.attention.signedOut.own'),
        ].filter(Boolean).join(i18n.t('harness.said.sentences')),
        account: {
          ...facts,
          named: [{ id: null, label: i18n.t('agents.account.own'), state: 'out', read: own.read, until: null }],
          outside: null,
        },
      });
    }
    return rows;
  });

  return [...waiting, ...signedOut];
}
