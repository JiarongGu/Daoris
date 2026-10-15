import i18n from '../locales/i18n';
import { ago, elapsed, moment } from '../locales/format';
import { type CURVE_WINDOWS, type CurvePoint, type CurveScope, type Switch, tightestSaid, wholePoints } from './usageCurves';

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
  /**
   * `clear`, `warned` or `refused`, by the agent's own word, on the window its frame named; else null. `warned` is Claude Code's
   * pace warning, said as *ahead of its pace* and never as near (USAGEREAD3b, D165 point 2).
   */
  standing?: string | null;
  /** Whether it said the request drew on usage credits. */
  credits: boolean;
  seen: string;
  /**
   * Where its reading came from (USAGEREAD2a, SWITCH1d): `read` from its agent's own answer, asked with no prompt, or `said` by
   * a session's frame. A shell older than SWITCH1d answers none, read as `said`.
   */
  source?: 'read' | 'said' | null;
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

/**
 * How one scope's list is used (D130 §16.6, D165 §6): `switch` and `curve` (SWITCH1d), which a shell older than that answers
 * neither of, read as the smart switch with no curve; `early` and `near`, the old names, derived from them for a page that still
 * reads them.
 */
export type ScopeUse = { use: 'goal' | 'order'; keep?: string | null; early: boolean; near: number } & CurveScope;

/**
 * Why an account of a list is passed at a start (D130 §6 as D165 point 2 amends it): a limit reached, usage credits, or its
 * number over the scope's curve on the window named (`number`, SWITCH1b-ii). The agent's pace warning is none of them
 * (USAGEREAD3a).
 */
export type NearBy = 'refused' | 'credits' | 'number';

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
  /** The accounts of its list a start passes (D130 §6, D165 point 2): a limit reached, credits, or a number over its curve. */
  near: { account: string; window: string; by: NearBy }[];
  /** Which account its next start would take, and why (TOOL6e); absent from a shell older than that. */
  next?: NextStart | null;
};

/** One agent's accounts, by the accounts' owner (AGT7): a door's accounts are its owner's. */
export type AgentAccounts = {
  agent: string;
  /** Whether its sessions say how near their limits are (TOOL6c): what switching before the limit waits for. */
  speaks: boolean;
  /**
   * Whether its accounts' use is read by asking its agent, with no prompt (USAGEREAD2a, CODEXUSE1): what *Read now* and the read
   * as its page opens ask (SWITCH1d). A shell older than that answers none, and neither is offered.
   */
  reads?: boolean;
  /**
   * The tool's own sign-in's cool-off (D125 §3.7), or when it ended (TOOL6e): never one of the accounts, never in a list. What
   * its agent last said of its windows where a press read them (CODEXUSE3); a shell older than that answers none.
   */
  own: { cooling?: AccountCooling | null; offered?: string | null; said?: AccountSaid | null };
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
  /** The old names (D165 §6), which the modules still take, and which this page no longer sends. */
  early?: boolean;
  near?: number;
  /** When to switch (SWITCH1d, `--switch`): Daoris's curve, the person's own, or no switch while a session runs. */
  switch?: Switch;
  /** Each window of the person's curve set, the others kept (`--curve`); one that breaks a rule is `ACCOUNT_USE_CURVE`. */
  curve?: Partial<Record<(typeof CURVE_WINDOWS)[number], CurvePoint[]>>;
  /** The person's curve taken out, the switch kept (`--no-curve`). */
  noCurve?: boolean;
  clear?: boolean;
};

/** Today's defaults (D130 §16.6), the CLI's `USE_DEFAULTS` and the driver's `RotationUse.Default`, for a scope with no list. */
export const USE_DEFAULTS: ScopeUse = { use: 'goal', keep: null, early: true, near: 90 };

/**
 * When a scope switches (SWITCH1d, D165 §6): its `switch`; from a shell older than that, which answers only the old names,
 * `off` where `early` is false and else the smart switch, its `near` alone naming no curve the page could draw.
 */
export function switchOf(use: ScopeUse): Switch {
  return use.switch ?? (use.early === false ? 'off' : 'smart');
}

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

/**
 * How the list an account is measured by is used, for its windows against the curve (SWITCH1d): this machine's where its list
 * holds the account or none does, else the first workspace's whose own list holds it. An account in two lists is drawn against
 * this machine's, the scope its row's next start says first.
 */
export function useOf(agent: AgentAccounts, account: string): ScopeUse {
  const machine = machineScope(agent);
  if (machine.list.includes(account)) return machine.use;
  return agent.scopes.find((scope) => scope.workspace && scope.list.includes(account))?.use ?? machine.use;
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

/**
 * A window by its name, in the reader's language; a name this build does not know is said as it is. The driver names a Codex
 * window of any other length `<n>-minute` (D125's CODEXUSE1 note), worded here with its digits (WINDOWNAME1). The cooling note's
 * lines hand in their own `t`.
 */
export function windowName(
  window: string,
  t: (key: string, options?: Record<string, unknown>) => string = (key, options) => i18n.t(key, options),
): string {
  const minutes = /^(\d+)-minute$/.exec(window);
  if (minutes) return t('harness.window.minutes', { n: minutes[1], defaultValue: window });
  return t(`harness.window.${window}`, { defaultValue: window });
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
 * said so, and is neither spent nor fresh (D57). Its pace warning is *ahead of its pace*, as the start's note says it, and
 * never near its limit (USAGEREAD3b). Where a scope is handed, its tightest window over the scope's curve is said with the
 * curve's value now, and the line is led by its newest reading's source, *read* or *said* (SWITCH1d).
 */
export function saidLine(
  said: AccountSaid | null | undefined,
  { use, now = new Date() }: {
    /** The scope's switch and curve: its tightest window over it is said with the curve's value now (SWITCH1d). */
    use?: CurveScope;
    now?: Date;
  } = {},
): string {
  if (!said || said.windows.length === 0) return i18n.t('harness.said.nothing');
  const parts: string[] = [];
  const reached = said.windows.find((each) => each.standing === 'refused');
  const warned = said.windows.find((each) => each.standing === 'warned');
  if (reached) parts.push(i18n.t('harness.said.reached', { window: windowName(reached.window) }));
  else if (warned) parts.push(i18n.t('harness.said.warned', { window: windowName(warned.window) }));
  if (said.windows.some((each) => each.credits)) parts.push(i18n.t('harness.said.credits'));
  for (const each of orderedWindows(said.windows)) {
    if (typeof each.used === 'number') {
      parts.push(i18n.t('harness.said.window', { used: percent(each.used), window: windowName(each.window), when: moment(each.reset) }));
    }
  }
  // Over its curve on its tightest window, as the terminal's said line and the start's line add it (SWITCH1d, the twins'
  // `tightest` rows); on its curve is not over it.
  const over = use ? tightestSaid(use, said.windows, now) : null;
  if (over && over.room < 0) {
    parts.push(i18n.t('harness.said.over', { window: windowName(over.window.window), curve: `${wholePoints(over.curve)}%` }));
  }
  if (parts.length === 0) parts.push(i18n.t('harness.said.clear'));
  // Led by where its newest reading came from (USAGEREAD2b's lead, the terminal's): read from its agent, or said by a session.
  const newest = said.windows.reduce((best, each) => (
    each.seen > best.seen || (each.seen === best.seen && each.source === 'read') ? each : best));
  return i18n.t(newest.source === 'read' ? 'harness.said.lineRead' : 'harness.said.line', {
    age: ago(said.seen), what: parts.join(i18n.t('harness.said.join')),
  });
}

/**
 * Why an account passed is passed, as the key's last word (SWITCH1b-iii, D165 point 2): its limit reached, usage credits, or
 * its use over its curve, by the scope's own `near` entry for it, with the window; the three named together where the scope
 * names no entry for it.
 */
function passedBy(near: AccountScope['near'], account: string | null | undefined): { suffix: string; window?: string } {
  const entry = account ? near.find((each) => each.account === account) : undefined;
  if (!entry) return { suffix: '' };
  const suffix = { refused: 'Reached', credits: 'Credits', number: 'Over' }[entry.by];
  return { suffix, window: windowName(entry.window) };
}

/**
 * Why the next start takes its account, as a clause (TOOL6e): the walk's step, naming the account it weighed it against as a
 * person calls it. The list's order says the default, the list's first, or the account it comes before. An account passed
 * says what passed it (SWITCH1b-iii); step 5 is the most room under the curve, and the week's pace only with the switch off
 * (the switch design §3), where the page's `early` is false.
 */
export function nextWhy(next: NextStart, scope: AccountScope, labelOf: (name: string) => string): string {
  const over = next.over ? labelOf(next.over) : '';
  switch (next.reason) {
    case 'near': {
      const by = passedBy(scope.near, next.over);
      return i18n.t(`harness.next.why.near${by.suffix}`, { over, window: by.window });
    }
    case 'pace':
      return i18n.t(switchOf(scope.use) !== 'off' ? 'harness.next.why.room' : 'harness.next.why.pace', { over });
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
export function nextLine(
  next: NextStart, scope: AccountScope, labelOf: (name: string) => string, isKey: (name: string) => boolean = () => false,
): string {
  if (next.reason === 'own') return i18n.t('harness.next.own');
  if (next.reason === 'waits' || !next.account) {
    const waits = next.when ? i18n.t('harness.next.waitsUntil', { when: moment(next.when) }) : i18n.t('harness.next.waits');
    // TOOL6g: an account not signed in waits for a person, never for the reset, so a wait says a sign-in frees it.
    // ACCTUX1b: a key read signed out is a key refused; only a new key repairs it, so it is never said as a sign-in.
    const out = next.others.filter((held) => (held.hold === 'signedOut' || held.hold === 'refused') && held.account);
    const join = (names: string[]) => names.join(i18n.t('harness.next.comma'));
    const signIn = out.filter((held) => !isKey(held.account!) && held.hold === 'signedOut').map((held) => labelOf(held.account!));
    const keys = out.filter((held) => isKey(held.account!)).map((held) => labelOf(held.account!));
    const sooner = next.when ? 'Sooner' : '';
    const sentences = [
      signIn.length > 0 ? i18n.t(`harness.next.signIn${sooner}`, { accounts: join(signIn) }) : null,
      keys.length > 0 ? i18n.t(`harness.next.newKey${sooner}`, { accounts: join(keys) }) : null,
    ].filter((sentence): sentence is string => sentence !== null);
    return [waits, ...sentences].join(i18n.t('harness.said.sentences'));
  }
  return i18n.t('harness.next.takes', { account: labelOf(next.account), why: nextWhy(next, scope, labelOf) });
}

/**
 * What holds the accounts the next start does not take, as one or two sentences (TOOL6e): each held account and why, then
 * the accounts the scope does not use together; an account merely ranked after is not said. Null where nothing holds any.
 * An account passed says what passed it by the scope's `near` entry for it (SWITCH1b-iii).
 */
export function heldLine(
  next: NextStart, labelOf: (name: string) => string, isKey: (name: string) => boolean = () => false,
  near: AccountScope['near'] = [],
): string | null {
  const name = (held: AccountHeld) => (held.account ? labelOf(held.account) : i18n.t('harness.use.emptyOwn'));
  const clauses = next.others.flatMap((held) => {
    if (held.hold === 'ready' || held.hold === 'outside') return [];
    // ACCTUX1b: a key held signed out or refused is one refused key, repaired by a new key (the walk says `signedOut` after a restart).
    if (held.account && isKey(held.account) && (held.hold === 'signedOut' || held.hold === 'refused')) {
      return [i18n.t('harness.next.held.keyRefused', { account: name(held) })];
    }
    if (held.hold === 'cooling') return held.until ? [i18n.t('harness.next.held.cooling', { account: name(held), when: moment(held.until) })] : [];
    if (held.hold === 'near') {
      const by = passedBy(near, held.account);
      return [i18n.t(`harness.next.held.near${by.suffix}`, { account: name(held), window: by.window })];
    }
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
