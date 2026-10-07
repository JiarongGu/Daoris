import i18n from '../i18n';
import { ago, elapsed, moment } from '../format';

// How each agent's accounts are used, as the page reads it (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6): the shape
// `bridge/accounts.ts` answers, and the words each account's row and each scope say, in the reader's language. Pure: the
// molecules read these, and so do the tests, with no bridge.

/** An account's cool-off, as `cooling.json` keeps it (D125 §2.3): until when, and why it lasts until then. */
export type AccountCooling = {
  until: string;
  /** Whether the agent named that time; false is Daoris's default standing in. */
  stated: boolean;
  /** The window the reset named (`session`, `weekly`), or null. */
  window?: string | null;
  seen: string;
  /** Whether this machine's zone stood in for one the agent named. */
  assumedZone: boolean;
  /** Whether the agent named a date more than 8 days off, so the default stood in. */
  notBelieved: boolean;
};

/** One window as an account's agent last said it (TOOL6c, D130 §5.2): a floor, as of when it was seen. */
export type WindowSaid = {
  /** Daoris's name for it: `session` (the five-hour window) or `weekly`. */
  window: string;
  /** How much of it is used, from 0 to 1; null where the agent gave no number. */
  used?: number | null;
  reset: string;
  /** `clear`, `near` or `refused`, by the agent's own word, on the window its frame named; else null. */
  standing?: string | null;
  /** Whether it said the request drew on usage credits. */
  credits: boolean;
  seen: string;
};

/** What an account's agent last said: each window whose reset has not passed. Absent is nothing said, never zero. */
export type AccountSaid = { seen: string; windows: WindowSaid[] };

/** One account's facts (the account's own row): never a key, never who signed in, never the agent's words. */
export type AccountFacts = {
  /** The directory's name, as a terminal types it. */
  name: string;
  cooling?: AccountCooling | null;
  said?: AccountSaid | null;
  /** The weekly reset known for it, told by a limit or by the agent's word (TOOL6b, TOOL6c); null where none is known. */
  week?: string | null;
  /** Daoris's sessions running on it now; null where they could not be counted — unknown, never zero. */
  running?: number | null;
  /** When its cool-off ended, within the last day, while `cooling.json` still holds it (TOOL6e); null otherwise. */
  offered?: string | null;
};

/**
 * Why the next start takes its account (TOOL6e, D130 §3–§4 as §16.3 amends them): the driver's `NextReason`, a closed set.
 * `own` and `named` are a scope's one account, chosen by no step; `waits` is no account ready.
 */
export type NextReason =
  | 'own' | 'named' | 'onlyReady' | 'kept' | 'near' | 'fewest' | 'lapsing' | 'pace' | 'leastRecent' | 'notStarted' | 'list'
  | 'waits';

/** What keeps the next start off an account (TOOL6e), or `ready` where nothing does and it is merely ranked after. */
export type NextHold = 'ready' | 'near' | 'cooling' | 'refused' | 'signedOut' | 'kept' | 'missing' | 'outside';

/** One account the next start does not take: null is the tool's own sign-in. */
export type AccountHeld = { account?: string | null; hold: NextHold; until?: string | null };

/**
 * Which account a scope's next start would take, why, and what holds the others (TOOL6e): the driver's own walk, asked
 * without starting, counting or probing anything.
 */
export type NextStart = {
  /** The account it takes; null for the tool's own sign-in, and while it waits. */
  account?: string | null;
  reason: NextReason;
  /** The account the reason weighed it against: where the list begins, or else the next ready account. */
  over?: string | null;
  /** Its week's reset (`lapsing`), or when the first account is offered again (`waits`). */
  when?: string | null;
  /** Every other account, in the order the walk tried them, then kept, missing and unused ones. */
  others: AccountHeld[];
};

/** How one scope's list is used (D130 §16.6). */
export type ScopeUse = { use: 'goal' | 'order'; keep?: string | null; early: boolean; near: number };

/** Why an account of a list is near its limit (D130 §6 as TOOL6c reads it). */
export type NearBy = 'refused' | 'word' | 'credits' | 'number';

/** A scope (D130 §2 rule 1): the machine's, or one workspace that names a default or a list of its own. */
export type AccountScope = {
  /** The workspace, or null for this machine's scope. */
  workspace?: string | null;
  /** Its own default, or null. */
  default?: string | null;
  /** Its list: every account its starts may run on, in the person's order; empty is none. */
  list: string[];
  /** Where its starts begin: its default where its list holds it, else its list's first; with no list, its default. */
  begins?: string | null;
  use: ScopeUse;
  /** Settings it holds that this build does not know, by name. */
  unknown: string[];
  /** A file edited by hand that breaks the rule, read with the list winning (D130 §3.1); null where none. */
  problem?: { kind: 'default' | 'keep' | 'alone'; account: string } | null;
  /** The accounts of its list near their limit by its own `near` (D130 §6): by the agent's word, credits or a number. */
  near: { account: string; window: string; by: NearBy }[];
  /** Which account its next start would take, and why (TOOL6e); absent from a shell older than that. */
  next?: NextStart | null;
};

/** One agent's accounts, by the accounts' owner (AGT7): a door's accounts are its owner's. */
export type AgentAccounts = {
  agent: string;
  /** Whether its sessions say how near their limits are (TOOL6c): what switching before the limit waits for. */
  speaks: boolean;
  /** The tool's own sign-in's cool-off (D125 §3.7), or when it ended (TOOL6e): never one of the accounts, never in a list. */
  own: { cooling?: AccountCooling | null; offered?: string | null };
  accounts: AccountFacts[];
  /** The machine's scope first, then each workspace's own. */
  scopes: AccountScope[];
};

export type AccountsAnswer = { agents: AgentAccounts[] };

/** A change to how a scope's list is used: a field left out is no change; `noKeep` keeps none; `clear` is today's defaults. */
export type AccountUseChange = {
  use?: 'goal' | 'order';
  keep?: string;
  noKeep?: boolean;
  early?: boolean;
  near?: number;
  clear?: boolean;
};

/** Today's defaults (D130 §16.6), the CLI's `USE_DEFAULTS` and the driver's `RotationUse.Default`, for a scope with no list. */
export const USE_DEFAULTS: ScopeUse = { use: 'goal', keep: null, early: true, near: 90 };

/** *Near*'s range (D130 §14): a whole percent from the one to the other. */
export const NEAR_RANGE = { lowest: 50, highest: 99 } as const;

/**
 * An agent's accounts as the answer holds them, read defensively: a shell older than TOOL4g answers no such route, and a
 * field the bridge left out (a null) reads as absent.
 */
export function agentOf(answer: AccountsAnswer | undefined | null, agent: string): AgentAccounts | null {
  const agents = Array.isArray(answer?.agents) ? answer.agents : [];
  return agents.find((each) => each.agent === agent) ?? null;
}

/** The machine's scope, which every agent answers. */
export function machineScope(agent: AgentAccounts): AccountScope {
  return agent.scopes.find((scope) => !scope.workspace) ?? {
    workspace: null, default: null, list: [], begins: null, use: USE_DEFAULTS, unknown: [], problem: null, near: [],
  };
}

/** A workspace's own scope, or null where it names neither a default nor a list of its own: it runs on this machine's. */
export function workspaceScope(agent: AgentAccounts, workspace: string): AccountScope | null {
  return agent.scopes.find((scope) => scope.workspace === workspace) ?? null;
}

/** The workspaces whose own list holds an account (D130 §3.2: each account's row names the workspaces that may run on it). */
export function listedIn(agent: AgentAccounts, account: string): string[] {
  return agent.scopes.filter((scope) => scope.workspace && scope.list.includes(account)).map((scope) => scope.workspace!);
}

/** A list with an account moved one place, up (-1) or down (+1); the list as it was at either end. */
export function moved(list: readonly string[], account: string, by: -1 | 1): string[] {
  const at = list.indexOf(account);
  const to = at + by;
  if (at < 0 || to < 0 || to >= list.length) return [...list];
  const next = [...list];
  [next[at], next[to]] = [next[to]!, next[at]!];
  return next;
}

/**
 * A list with an account's *Use* turned on or off (D130 §9): on appends it, last, as the terminal names a list in order;
 * off takes it out where it was. Nothing counts accounts: a list of one, or of many, is a list.
 */
export function used(list: readonly string[], account: string, on: boolean): string[] {
  if (on) return list.includes(account) ? [...list] : [...list, account];
  return list.filter((name) => name !== account);
}

/**
 * What *Use* off would break (D130 §3.1, §4.6), before the press: the scope's default or its kept account taken out of the
 * list, or a kept account left alone in it. The terminal refuses each, so the screen offers the press only where it would
 * not be refused, and says why where it would. Taking the last account out clears the list, which is never refused.
 */
export function cannotLeave(scope: AccountScope, account: string): 'default' | 'keep' | 'alone' | null {
  const rest = scope.list.filter((name) => name !== account);
  if (rest.length === 0) return scope.use.keep ? 'alone' : null;
  if (scope.default === account) return 'default';
  if (scope.use.keep === account) return 'keep';
  if (scope.use.keep && rest.length === 1 && rest[0] === scope.use.keep) return 'alone';
  return null;
}

/** Whether no account of a list has said what it has left (D130 §16.4): the walk then needs none of their word. */
export function nothingSaid(agent: AgentAccounts, scope: AccountScope): boolean {
  return scope.list.every((name) => !agent.accounts.find((account) => account.name === name)?.said);
}

/** Why a cool-off lasts until then, in the reader's language (D125 §2.4): the agent said so, or Daoris's default stood in. */
export function coolingWhy(cooling: AccountCooling): string {
  if (cooling.stated) return i18n.t(cooling.assumedZone ? 'harness.cooling.why.assumed' : 'harness.cooling.why.stated');
  return i18n.t(cooling.notBelieved ? 'harness.cooling.why.notBelieved' : 'harness.cooling.why.default');
}

/** An account's cool-off as its row says it: until when, in this machine's zone and named, how long from now, and why. */
export function coolingLine(cooling: AccountCooling, now: Date = new Date()): string {
  return i18n.t('harness.cooling.until', {
    when: moment(cooling.until), span: elapsed(now.toISOString(), cooling.until), why: coolingWhy(cooling),
  });
}

/** A window by its name, in the reader's language; a name this build does not know is said as it is. */
export function windowName(window: string): string {
  return i18n.t(`harness.window.${window}`, { defaultValue: window });
}

/** A share as a whole percent, in the reader's own figures. */
export function percent(share: number): string {
  return `${Math.round(share * 100)}%`;
}

/** The session window, then the week, then any other, as a person reads them; an account's row reads them so too (ACCTUX1). */
export function orderedWindows(windows: readonly WindowSaid[]): WindowSaid[] {
  const rank = (window: string) => ({ session: 0, weekly: 1 } as Record<string, number>)[window.toLowerCase()] ?? 2;
  return [...windows].sort((a, b) => rank(a.window) - rank(b.window));
}

/**
 * What an account's agent last said, as its row says it (TOOL6c, D130 §5.2): how long ago, its own word where it gave one,
 * credits, and each window's use and reset. The CLI's `saidLine` says the same in the terminal's words. Nothing said is
 * said so, and is neither spent nor fresh (D57).
 */
export function saidLine(said: AccountSaid | null | undefined): string {
  if (!said || said.windows.length === 0) return i18n.t('harness.said.nothing');
  const parts: string[] = [];
  const reached = said.windows.find((each) => each.standing === 'refused');
  const warned = said.windows.find((each) => each.standing === 'near');
  if (reached) parts.push(i18n.t('harness.said.reached', { window: windowName(reached.window) }));
  else if (warned) parts.push(i18n.t('harness.said.warned', { window: windowName(warned.window) }));
  if (said.windows.some((each) => each.credits)) parts.push(i18n.t('harness.said.credits'));
  for (const each of orderedWindows(said.windows)) {
    if (typeof each.used === 'number') {
      parts.push(i18n.t('harness.said.window', { used: percent(each.used), window: windowName(each.window), when: moment(each.reset) }));
    }
  }
  if (parts.length === 0) parts.push(i18n.t('harness.said.clear'));
  return i18n.t('harness.said.line', { age: ago(said.seen), what: parts.join(i18n.t('harness.said.join')) });
}

/**
 * Why the next start takes its account, as a clause (TOOL6e): the walk's step, naming the account it weighed it against as a
 * person calls it. The list's order says the default, the list's first, or the account it comes before.
 */
function nextWhy(next: NextStart, scope: AccountScope, labelOf: (name: string) => string): string {
  const over = next.over ? labelOf(next.over) : '';
  switch (next.reason) {
    case 'list':
      if (next.account === scope.default) return i18n.t('harness.next.why.listDefault');
      if (next.account === scope.begins || !next.over) return i18n.t('harness.next.why.listFirst');
      return i18n.t('harness.next.why.listNext', { over });
    case 'kept':
      return next.over ? i18n.t('harness.next.why.keptOver', { over }) : i18n.t('harness.next.why.kept');
    case 'lapsing':
      return next.when ? i18n.t('harness.next.why.lapsing', { when: moment(next.when) }) : i18n.t('harness.next.why.lapsingSoon');
    default:
      return i18n.t(`harness.next.why.${next.reason}`, { over });
  }
}

/**
 * The next start, as a sentence (TOOL6e, D130 §3–§4): which account it takes and the walk's step that chose it; the tool's
 * own sign-in where nothing names an account (D125 §3.7); or that it waits, until when where an account is cooling.
 */
export function nextLine(next: NextStart, scope: AccountScope, labelOf: (name: string) => string): string {
  if (next.reason === 'own') return i18n.t('harness.next.own');
  if (next.reason === 'waits' || !next.account) {
    const waits = next.when ? i18n.t('harness.next.waitsUntil', { when: moment(next.when) }) : i18n.t('harness.next.waits');
    // TOOL6g: an account not signed in waits for a person, never for the reset, so a wait says a sign-in frees it.
    const signedOut = next.others.filter((held) => held.hold === 'signedOut' && held.account).map((held) => labelOf(held.account!));
    if (signedOut.length === 0) return waits;
    const accounts = signedOut.join(i18n.t('harness.next.comma'));
    return `${waits}${i18n.t('harness.said.sentences')}${i18n.t(next.when ? 'harness.next.signInSooner' : 'harness.next.signIn', { accounts })}`;
  }
  return i18n.t('harness.next.takes', { account: labelOf(next.account), why: nextWhy(next, scope, labelOf) });
}

/**
 * What holds the accounts the next start does not take, as one or two sentences (TOOL6e): each held account and why, then
 * the accounts the scope does not use together; an account merely ranked after is not said. Null where nothing holds any.
 */
export function heldLine(next: NextStart, labelOf: (name: string) => string): string | null {
  const name = (held: AccountHeld) => (held.account ? labelOf(held.account) : i18n.t('harness.use.emptyOwn'));
  const clauses = next.others.flatMap((held) => {
    if (held.hold === 'ready' || held.hold === 'outside') return [];
    if (held.hold === 'cooling') return held.until ? [i18n.t('harness.next.held.cooling', { account: name(held), when: moment(held.until) })] : [];
    return [i18n.t(`harness.next.held.${held.hold}`, { account: name(held) })];
  });
  const outside = next.others.filter((held) => held.hold === 'outside').map(name);
  const sentences = [
    clauses.length > 0 ? i18n.t('harness.next.held.line', { clauses: clauses.join(i18n.t('harness.said.join')) }) : null,
    outside.length > 0 ? i18n.t('harness.next.outside', { accounts: outside.join(i18n.t('harness.next.comma')) }) : null,
  ].filter((sentence): sentence is string => sentence !== null);
  return sentences.length > 0 ? sentences.join(i18n.t('harness.said.sentences')) : null;
}

/** Since when an account is offered again, its cool-off having ended (TOOL6e), as its row says it. */
export function offeredLine(offered: string): string {
  return i18n.t('harness.next.offered', { when: moment(offered) });
}

/** The tool's own sign-in's line (D125 §3.7), when a start would run on it, with its cool-off where it cools. */
export function ownLine(who: string | null | undefined, cooling: AccountCooling | null | undefined): string {
  const shared = who ? i18n.t('harness.own.shared', { who }) : i18n.t('harness.own.sharedUnknown');
  return cooling ? `${shared}${i18n.t('harness.said.sentences')}${i18n.t('harness.own.cooling', { when: moment(cooling.until) })}` : shared;
}
