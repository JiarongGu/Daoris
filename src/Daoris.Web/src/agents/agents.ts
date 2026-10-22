import i18n from '../locales/i18n';
import { ago, clockOf, moment } from '../locales/format';
import type { Account, Tool } from './tools';
import {
  type AccountCooling, type AccountFacts, type AccountScope, type AccountsAnswer, type AgentAccounts, agentOf, coolingLine,
  heldLine, machineScope, nextLine, offeredLine, orderedWindows, percent, type PoolReceipt, type WindowSaid, windowName,
} from '../settings/accounts/accounts';
import type { AgentRulesState } from '../settings/repository/AgentRules';
import { OPEN_STATES } from '../settings/repository/proposals';

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
 * A key read signed out is one its provider refused (`refused`, ACCTUX1): only a refusal reads a key out (AGT3b, ROSTER1b),
 * since a read hands the key and the tool says yes for any.
 */
export type AccountStateName = 'in' | 'out' | 'cooling' | 'unknown' | 'keyed' | 'refused';

export type AccountState = {
  state: AccountStateName;
  /** When its state was read; null where it never was. */
  read: string | null;
  /** Its cool-off, where it cools. */
  cooling: AccountCooling | null;
  /** The starts run on it, as on every account of Daoris's (D197): a signed-out one then holds work, and wears open's hue. */
  holdsWork: boolean;
};

/**
 * One account's state from what the roster and the accounts' files last said: signed out first, or for a key refused, since
 * a sign-in or a new key is the person's and a reset frees neither (ACCTUX1: the refusal is kept whatever the account is);
 * then a cool-off; then a key, which no sign-in names; then the agent's word.
 */
export function accountState(
  account: Pick<Account, 'login' | 'read' | 'key'>, facts: Pick<AccountFacts, 'cooling'> | null, holdsWork: boolean,
): AccountState {
  const cooling = facts?.cooling ?? null;
  const read = account.read ?? null;
  const state: AccountStateName = account.login === 'out'
    ? (account.key ? 'refused' : 'out')
    : cooling ? 'cooling' : account.key ? 'keyed' : account.login === 'in' ? 'in' : 'unknown';
  return { state, read, cooling, holdsWork };
}

/** Whether a state waits on the person as a sign-out does (ACCTUX1): signed out, or a key its provider refused. */
export function waitsOnPerson(state: Pick<AccountState, 'state'>): boolean {
  return state.state === 'out' || state.state === 'refused';
}

/**
 * Whether the starts run on the account (POOL1c, D197 point 1): every account of Daoris's does, one set serving every workspace,
 * whatever its order names, so a signed-out one holds work wherever it is. The tool's own sign-in is none of them (point 3).
 */
export function heldBy(tool: Tool, name: string): boolean {
  return tool.accounts.some((account) => account.name === name);
}

/** Each named account's state on this agent, by its directory. */
export function accountStates(tool: Tool, use: AgentAccounts | null | undefined): Map<string, AccountState> {
  return new Map(tool.accounts.map((account) => [
    account.name,
    accountState(account, use?.accounts.find((facts) => facts.name === account.name) ?? null, heldBy(tool, account.name)),
  ]));
}

/**
 * The accounts the person must act on (D150 §2.1, the activity bar's badge): each signed out or with its key refused, since
 * each holds work (D197). The tool's own sign-in is never among them: no start runs on it (D175 point 1).
 */
export function signedOutHeld(tool: Tool, use: AgentAccounts | null | undefined): number {
  return [...accountStates(tool, use).values()].filter((state) => waitsOnPerson(state) && state.holdsWork).length;
}

/** A state's word and its pill's tone (§5.3): open's hue only for an account that waits on the person and holds work. */
export function stateWord(state: AccountState): { label: string; tone: 'open' | 'neutral' } {
  switch (state.state) {
    case 'out': return { label: i18n.t('harness.login.out'), tone: state.holdsWork ? 'open' : 'neutral' };
    case 'refused': return { label: i18n.t('harness.login.refused'), tone: state.holdsWork ? 'open' : 'neutral' };
    case 'cooling': return { label: i18n.t('harness.cooling.pill'), tone: 'neutral' };
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
  if (state.state === 'cooling') return coolingLine(state.cooling!, now);
  if (!state.read) return i18n.t('agents.read.never');
  const when = clockOf(state.read, now);
  return state.state === 'unknown' ? i18n.t('agents.read.failed', { when }) : i18n.t('agents.read.at', { when });
}

/**
 * When, beside the state's word in its column (D152 §4.2): *signed in · 01:31*, *unknown · never read*, *cooling · until 6
 * Oct 22:02*. The time alone, since the column and the list's head say what it is; its tip says the whole (`readLine`). A
 * cool-off is a hold, said until its end whoever chose the time (ACCTUX1): *resets …* claimed a reset where Daoris chose the
 * wait because the agent named none, and a reported reset is said in its window's cell (`usageCells`).
 */
export function stateWhen(state: AccountState, now: Date = new Date()): string {
  if (state.state === 'cooling') return i18n.t('agents.state.until', { when: moment(state.cooling!.until) });
  if (!state.read) return i18n.t('agents.read.never');
  const when = clockOf(state.read, now);
  return state.state === 'unknown' ? i18n.t('agents.read.failed', { when }) : when;
}

/** The windows every row draws a cell for, for both makers (ACCTUX4): Claude Code's and Codex's five hours and week. */
export const FIXED_WINDOWS = ['session', 'weekly'] as const;

/**
 * One window on an account's row (ACCTUX4, the second opinion's account section): a cell in the same place on every row, so a
 * person compares a window down the rows and two makers by the same cells. It says its exact share, a thin bar of that
 * window's own allowance, the agent's own word, its reset and how long ago its own reading was (each window keeps its newest
 * reading, D130 §5.2). One its agent did not report is unknown, said in words with no bar and never 0%: absent is nothing
 * said, never zero.
 */
export type WindowCell = {
  /** Daoris's name for it: `session`, `weekly`, or another a reading named (`<n>-minute`, CODEXUSE1). */
  window: string;
  /** Its name in the reader's language: *five-hour*, *weekly*; one this build does not know as it is. */
  name: string;
  /** Whether its agent said anything of it; false is unknown. */
  known: boolean;
  /** Its share used, 0 and up, where its agent gave one: the bar's fill. Null draws no bar. */
  used: number | null;
  /**
   * Its agent's own word that it is reached or ahead of its pace: the bar wears the warning hue. A pace warning is still a
   * warning, and is never said as near (USAGEREAD3b, D165 point 2).
   */
  warned: boolean;
  /** What a person acts on, in the ink: its share, its agent's own word, that it draws on credits; or *unknown*. */
  reading: string;
  /**
   * Its share against its scope's curve now (SWITCH1d; the switch design §5), as .NET judged it (PUSH1e): *its curve allows 45%
   * now*, or *over its curve's 45% now*. Absent where it was not measured (your own sign-in, which no list holds, a window its agent
   * gave no share of, one this build has no curve for, a shell older than PUSH1e).
   */
  curve?: string;
  /**
   * Its use projected to now, said as projected (USAGEPROBE1b): *about 23% by now, projected from Daoris's own turns since*
   * where it differs from the reading by a whole percent, or *not projected* where Daoris spent on the account since and cannot
   * weigh it. Absent where it is the reading itself, or none was sent.
   */
  projected?: string;
  /** Its share is over its curve now: said so, and the bar wears the warning hue. Absent where `curve` is. */
  over?: boolean;
  /**
   * Its times, in the soft ink: when it resets, and how long ago its reading was, read from its agent or said by a session
   * (SWITCH1d). None where it is unknown.
   */
  times: string[];
};

/** What an account's agent last said, as its row draws it: a cell a window, and what is the account's and no window's. */
export type AccountUsage = { cells: WindowCell[]; notes: string[] };

/** A window as its agent last said it, as its cell says it: against its scope's curve where .NET measured it (SWITCH1d, PUSH1e). */
function windowCell(said: WindowSaid, now: Date, product?: string): WindowCell {
  const used = typeof said.used === 'number' ? said.used : null;
  // Claude Code's pace warning (`warned`) in its own words, *ahead of its pace*: near is the scope's, never this (USAGEREAD3b).
  const word = said.standing === 'refused' ? 'agents.window.reached' : said.standing === 'warned' ? 'agents.window.warned' : null;
  const readings = [
    ...(used !== null ? [i18n.t('agents.window.used', { used: percent(used) })] : []),
    ...(word ? [i18n.t(word)] : []),
    ...(said.credits ? [i18n.t('harness.said.credits')] : []),
  ];
  // A window with no share and no word of trouble is one its agent said was clear (the driver keeps none with neither).
  if (readings.length === 0) readings.push(i18n.t('harness.said.clear'));
  // Its share against the curve now, as .NET judged it beside the reading (PUSH1e): the curve's value a whole percent as the
  // terminal says it, and over it where its room is below zero, so a share exactly on its curve is not over it.
  const against = typeof said.curve !== 'number' || used === null ? {} : {
    curve: i18n.t(said.over ? 'agents.window.overCurve' : 'agents.window.curve', { curve: `${said.curve}%` }),
    over: said.over === true,
  };
  // Where its reading came from (USAGEREAD2a): read from its agent, asked with no prompt, or said by a session's frame.
  const age = said.source !== 'read'
    ? i18n.t('agents.said.bySession', { age: ago(said.seen) })
    : product ? i18n.t('agents.said.read', { product, age: ago(said.seen) }) : i18n.t('agents.said.readAt', { age: ago(said.seen) });
  // The projection to now beside the reading (USAGEPROBE1b): the same one a start reads, said only where it adds something.
  const projected = said.projectedBy === 'unweighed'
    ? i18n.t('agents.window.unweighed')
    : said.projectedBy === 'measured' && typeof said.projected === 'number' && used !== null && percent(said.projected) !== percent(used)
      ? i18n.t('agents.window.projected', { projected: percent(said.projected) })
      : null;
  return {
    window: said.window, name: windowName(said.window), known: true, used, warned: word !== null,
    reading: readings.join(i18n.t('agents.said.comma')),
    ...against,
    ...(projected ? { projected } : {}),
    times: [i18n.t('agents.said.resets', { when: clockOf(said.reset, now) }), age],
  };
}

/**
 * What an account's agent last said, as its row draws it under its line (ACCTUX4; TOOL6c, D130 §5.2; ACCTUX1): a cell for
 * the session and the week, each unknown where its agent did not report it, then a cell for any other window a reading named.
 * The two are drawn where something was said, and where `fixed` asks for them though nothing was: a sign-in whose agent
 * speaks, which a session's frame or a press reads. A key, an agent that does not speak and a sign-in nothing reads say
 * nothing where nothing was said, as *nothing said yet* is not said on a row (D152 §4.2).
 *
 * Its notes are the account's and no window's: since when it is offered again, and for a cool-off whose length Daoris chose
 * because the agent named no reset, that the reset is unknown, where no window reported one (the state's column says the
 * hold's end, never a reset). Null says nothing.
 */
export function usageCells(
  facts: Pick<AccountFacts, 'said' | 'offered'> | null | undefined, state: AccountState,
  { fixed = false, now = new Date(), product }: {
    fixed?: boolean;
    now?: Date;
    /** What a person calls the agent, for a reading read from it: *read from Claude Code 2 min ago*. */
    product?: string;
  } = {},
): AccountUsage | null {
  const windows = facts?.said?.windows ?? [];
  const named = (window: string) => windows.find((each) => each.window.toLowerCase() === window);
  const cells: WindowCell[] = windows.length > 0 || fixed
    ? [
      ...FIXED_WINDOWS.map((window) => {
        const said = named(window);
        return said
          ? windowCell(said, now, product)
          : { window, name: windowName(window), known: false, used: null, warned: false, reading: i18n.t('agents.window.unknown'), times: [] };
      }),
      ...orderedWindows(windows.filter((each) => !(FIXED_WINDOWS as readonly string[]).includes(each.window.toLowerCase())))
        .map((each) => windowCell(each, now, product)),
    ]
    : [];
  const resetSaid = cells.some((cell) => cell.known);
  const notes = [
    ...(state.state === 'cooling' && !state.cooling!.stated && !resetSaid ? [i18n.t('agents.said.resetUnknown')] : []),
    ...(facts?.offered && state.state !== 'cooling' ? [offeredLine(facts.offered)] : []),
  ];
  return cells.length > 0 || notes.length > 0 ? { cells, notes } : null;
}

/** The latest moment any of an agent's accounts was read, for its Accounts section's head; null where none was. */
export function latestRead(states: readonly AccountState[]): string | null {
  return states.map((state) => state.read).filter((read): read is string => Boolean(read)).sort().at(-1) ?? null;
}

// ---------------------------------------------------------------- an account's row (UX7b, D152 §4)

/** A fresh id (ACCT2): `acct-` and eight hex characters, which the person never chose and which says nothing. */
const FRESH = /^acct-[0-9a-f]{8}$/i;

type Named = Pick<Account, 'name'> & Partial<Pick<Account, 'displayName' | 'account' | 'key' | 'org' | 'plan'>>;

/** One agent's sign-ins, an account told apart among (ACCOUNTID1b): its accounts, and its own sign-in where it is known. */
export type Among = { accounts: readonly Named[] } & Partial<Pick<Tool, 'ownAccount' | 'ownOrg' | 'ownPlan'>>;

/**
 * An account's name, as its row leads with it (D152 §4.2): the person's own (ACCT2), else a key's handle, else, for a fresh
 * id the person never chose, who signed in, else its id, an old `account-N` being the word every list and session head says.
 */
export function accountName(account: Named): string {
  const own = account.displayName?.trim();
  if (own) return own;
  if (account.key) return i18n.t('harness.profile.keyName', { handle: account.key });
  if (FRESH.test(account.name) && account.account?.trim()) return account.account.trim();
  return account.name;
}

/**
 * A name as the driver compares one, for the page's own judgement of whether two are the same words: each code point to its
 * one capital, never a wider one (D125's AGENTREAD1c and CASEFOLD1 notes). The page decides only what to show by it, so the
 * driver's exceptions (`ı`, `ſ`, and the Greek letters with a subscript iota) are left to the driver.
 */
function folded(name: string): string {
  return Array.from(name.trim(), (letter) => {
    const capital = letter.toUpperCase();
    return Array.from(capital).length === 1 ? capital : letter;
  }).join('');
}

/**
 * Who signed in, said beside the name in the same place on every row where the two differ; null where they are one. A name
 * that is its identity in another case is one (ACCTNAME1's page half: the owner named each account by its email), since
 * every name here is compared without case.
 */
export function accountWho(account: Named, among?: Among | null): string | null {
  if (account.key) return null;
  const who = account.account?.trim();
  if (!who) return null;
  const name = accountName(account);
  const apart = apartOf(account, among);
  const said = [says(name, who) ? null : who, apart && !says(name, apart) ? apart : null].filter((part) => part !== null);
  return said.length > 0 ? said.join(' · ') : null;
}

/**
 * An account's name on one line, where a pill, a chip, a note or a header has room for one (ACCOUNTID1b): its name, and what
 * tells it apart after it where the name is who signed in and another sign-in of its agent is that email too. A name the
 * person chose is one no other account has (ACCT2), so it stands alone.
 */
export function accountLabel(account: Named, among?: Among | null): string {
  const name = accountName(account);
  const who = account.account?.trim();
  const apart = apartOf(account, among);
  return apart && who && says(name, who) && !says(name, apart) ? `${name} · ${apart}` : name;
}

/**
 * Who signed in, with what tells the sign-in apart from another of its agent of the same email after it (ACCOUNTID1b), for a
 * line that says who and no name: *Signed in as …*, the agent's own sign-in. Null for a key, or where the agent said nobody.
 */
export function whoApart(account: Named, among?: Among | null): string | null {
  const who = account.key ? null : account.account?.trim();
  if (!who) return null;
  const apart = apartOf(account, among);
  return apart ? `${who} · ${apart}` : who;
}

/** Who is signed in to the agent's own home, told apart from an account of the same email (ACCOUNTID1b). */
export function ownWho(tool: Among): string | null {
  return whoApart({ name: '', account: tool.ownAccount, org: tool.ownOrg, plan: tool.ownPlan }, tool);
}

/**
 * What tells a sign-in apart from another of its agent signed in as the same email (ACCOUNTID1b, D125's note of 2026-10-19):
 * the organisation its agent named beside who, where no such other sign-in has it, else the plan likewise; null where no
 * other sign-in of the agent has that email, in any case, or neither part tells them apart. The rule `agent list` says it by
 * (the CLI's `apartBy`), with the agent's own sign-in one of them on the page. A key names no one.
 */
function apartOf(account: Named, among: Among | null | undefined): string | null {
  const who = account.key ? null : account.account?.trim();
  if (!who || !among) return null;
  const same = (one: string | null | undefined, other: string) => Boolean(one?.trim()) && folded(one!) === folded(other);
  // Every sign-in of the email, itself among them: an account is in the list, the agent's own sign-in is its `ownAccount`.
  const signedIn = [
    ...among.accounts.filter((each) => !each.key && same(each.account, who)).map((each) => ({ org: each.org, plan: each.plan })),
    ...(same(among.ownAccount, who) ? [{ org: among.ownOrg, plan: among.ownPlan }] : []),
  ];
  if (signedIn.length < 2) return null;
  for (const part of ['org', 'plan'] as const) {
    const mine = account[part]?.trim();
    // Itself is the one sign-in holding it; another with the same part leaves it telling nothing apart.
    if (mine && signedIn.filter((each) => same(each[part], mine)).length === 1) return mine;
  }
  return null;
}

/**
 * Whether a name already says a part (ACCTNAME1's rule, said once): the part is the name, or one of its words joined by `·`,
 * as the sign-in's end offers a second sign-in's name (`email·Org-B`, each run of space a dash), compared without case.
 */
function says(name: string, part: string): boolean {
  const word = (text: string) => folded(text.trim().split(/[\s`\p{Cc}]+/u).filter(Boolean).join('-'));
  const wanted = word(part);
  return folded(name) === folded(part) || name.split('·').some((each) => word(each) === wanted);
}

/** A version as its number alone (D152 §4.1): the product name a binary prints after it, in brackets, is dropped. */
export function versionOnly(version: string): string {
  return version.match(/^(.+?)\s+\([^)]*\)$/)?.[1] ?? version;
}

/** The set's next start as the page says it above the accounts (ACCTUX4b): the sentence, then what holds the others. */
export type NextStartSaid = { takes: string; held: string | null };

/**
 * Which account the set's next start takes and why, in any workspace, above the account list (ACCTUX4b; the second opinion's
 * lines 128-133, D152 §4.2; POOL1c: one set, so no workspace is named): the fold's words (`nextLine`, `heldLine`, TOOL6e), so
 * the two never say it differently. Nothing where the answer names no next start, a shell older than TOOL6e, rather than a
 * guess from the order's first.
 *
 * Of the holds, only the one on the account the list begins on: it is the account a person expects the start to take, and
 * the second opinion's line 125 is that the row said it did. Every other hold is on its own row, and the fold keeps the
 * whole walk. None where it takes that account, or where the reason names it already (`over`), and none for a wait, whose
 * own sentence names until when and the sign-in or key that frees it.
 */
export function nextStartSaid(
  scope: AccountScope | null | undefined, labelOf: (name: string) => string, isKey: (name: string) => boolean = () => false,
): NextStartSaid | null {
  const next = scope?.next;
  if (!scope || !next || next.reason === 'own') return null;
  const passed = next.reason !== 'waits' && next.account && scope.begins && next.account !== scope.begins
    && next.over !== scope.begins;
  const head = { ...next, others: next.others.filter((held) => held.account === scope.begins) };
  return { takes: nextLine(next, scope, labelOf, isKey), held: passed ? heldLine(head, labelOf, isKey, scope.near) : null };
}

/** The one act a row's state asks for (D152 §4.2): its name, and whether it is loud, which is where it holds work. */
export type AccountAct = { act: 'signIn' | 'read' | 'tryNow' | 'newKey'; loud: boolean };

/**
 * An account's one act by its state (D152 §4.2): *Sign in* when signed out, *Read* (that account alone) when unknown, *Try
 * now* when cooling; none when signed in, since every account serves every workspace (POOL1c, the account-pool design §4.2:
 * *Use in a workspace…* went). Unknown gets an act: on the install the account a list held read unknown, offered nothing, and
 * the person reached for the header instead. A key its provider refused is repaired by a key (ACCTUX1): *New API key*, where
 * the agent takes one, which puts it into the same account (`key-replace`, KEYREPLACE1b).
 */
export function accountAct(
  state: AccountState, agent: { signsIn: boolean; present: boolean; takesKey?: boolean },
): AccountAct | null {
  switch (state.state) {
    case 'out': return agent.signsIn && agent.present ? { act: 'signIn', loud: state.holdsWork } : null;
    case 'refused': return agent.takesKey && agent.present ? { act: 'newKey', loud: state.holdsWork } : null;
    case 'unknown': return agent.present ? { act: 'read', loud: state.holdsWork } : null;
    case 'cooling': return { act: 'tryNow', loud: false };
    default: return null;
  }
}

/**
 * How a setting the fold did not keep is named in its line (POOL1c; the account-pool design §4.2's `agents.pooled.notKept`):
 * by what it does, and its value as the page names that value (a way by its word, an account by its name, a curve by its
 * windows); a setting this build does not know is said as written.
 */
function settingSaid(setting: string, raw: string, labelOf: (name: string) => string): { setting: string; value: string } {
  let value: unknown = raw;
  try {
    value = JSON.parse(raw);
  } catch {
    // Kept as written: the driver hands each value's JSON whole.
  }
  const named = i18n.t(`agents.pooled.setting.${setting}`, { defaultValue: setting });
  if (typeof value === 'string') {
    if (setting === 'use') return { setting: named, value: i18n.t(`harness.use.mode.${value}`, { defaultValue: value }) };
    if (setting === 'switch') return { setting: named, value: i18n.t(`harness.use.switch.${value}`, { defaultValue: value }) };
    if (setting === 'keep') return { setting: named, value: labelOf(value) };
    return { setting: named, value };
  }
  if (setting === 'curve' && value && typeof value === 'object') {
    return { setting: named, value: Object.keys(value).map((window) => windowName(window)).join(i18n.t('harness.next.comma')) };
  }
  return { setting: named, value: raw };
}

/**
 * What the fold into one set changed (POOL1c, D197 point 7; the account-pool design §4.2, §6.3), one receipt's lines in the
 * reader's language from its kinds and values, in the order the terminal's `agent list` says them
 * (`AgentListCommand.PoolLines`): each scope that named fewer accounts than the set, the machine's as the workspaces that named
 * none, never *this machine*; that their starts still begin where they did; each first moved; each account placed; each
 * setting not kept; each name gone.
 */
export function pooledLines(receipt: PoolReceipt, labelOf: (name: string) => string): string[] {
  const count = receipt.order.length;
  const lines: string[] = [];
  for (const scope of receipt.scopes) {
    const had = scope.list.length > 0 ? scope.list : scope.default ? [scope.default] : [];
    if (had.length === 0 || had.length >= count) continue;
    const one = had.length === 1;
    const accounts = had.map(labelOf).join(i18n.t('harness.next.comma'));
    lines.push(scope.workspace
      ? i18n.t(one ? 'agents.pooled.oneAccount' : 'agents.pooled.ownList', { workspace: scope.workspace, account: accounts, accounts, count })
      : i18n.t(one ? 'agents.pooled.unnamedOne' : 'agents.pooled.unnamed', { account: accounts, accounts, count }));
  }
  if (receipt.inOrder) lines.push(i18n.t('agents.pooled.inOrder', { account: labelOf(receipt.inOrder) }));
  for (const moved of receipt.moved) {
    lines.push(moved.workspace
      ? i18n.t('agents.pooled.moved', { workspace: moved.workspace, account: labelOf(moved.account), place: moved.place })
      : i18n.t('agents.pooled.unnamedMoved', { account: labelOf(moved.account), place: moved.place }));
  }
  for (const joined of receipt.joined) lines.push(i18n.t('agents.pooled.joined', { account: labelOf(joined.account), place: joined.place }));
  for (const kept of receipt.notKept) {
    const said = settingSaid(kept.setting, kept.value, labelOf);
    lines.push(kept.workspace
      ? i18n.t('agents.pooled.notKept', { workspace: kept.workspace, ...said })
      : i18n.t('agents.pooled.notKeptUnnamed', said));
  }
  for (const gone of receipt.gone) lines.push(i18n.t('agents.pooled.gone', { account: gone }));
  return lines;
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
  /** Its mark on the list's strip, a letter or two no agent above it wears (ACCTUX2): *CC*, *Co*. */
  mark: string;
  /**
   * Whether Daoris has an installer for it (INSTALLDOOR1): false offers *How to install*, never Install. Read from the door the
   * install runs on, and true where an older shell sends none.
   */
  installs: boolean;
};

/** A strip mark's most letters: two fit the strip's 40 px at its size, a 中文 name's two characters included. */
const MARK_LETTERS = 2;

/**
 * The mark an agent's declaration gives (ACCTUX2, the UX7 design §4.6's *CC*, *Cx*), as written and cut to a mark's letters;
 * null where none does (a shell older than AGENTMARK1 sends none, and the first letters stand).
 */
function declaredMark(tool: Tool): string | null {
  for (const door of tool.doors) {
    const letters = typeof door.mark === 'string' ? Array.from(door.mark.trim()).slice(0, MARK_LETTERS).join('') : '';
    if (letters) return letters;
  }
  return null;
}

/**
 * An agent's marks on the strip, each its own (ACCTUX2): the strip drew a first character, so Claude Code and Codex were both
 * `C`. A declared mark leads; else each word's initial (*CC*, *DH*) or a lone word's first two letters (*Co*), the first a
 * capital; and one a row above already wears gives way to the first letter and a later one of its name (*Cody* beside *Codex*
 * is *Cd*), so no two rows look alike.
 */
function agentMarks(tools: readonly Tool[]): string[] {
  const worn = new Set<string>();
  return tools.map((tool) => {
    const name = (tool.product ?? tool.name).trim();
    const words = name.split(/\s+/).filter(Boolean).map((word) => Array.from(word));
    const first = words[0]?.[0] ?? '?';
    const capital = first.toUpperCase();
    const lead = Array.from(capital).length === 1 ? capital : first;
    const letters = words.length > 1
      ? [lead, ...words.slice(1, MARK_LETTERS).map((word) => word[0]!)]
      : [lead, ...(words[0] ?? []).slice(1, MARK_LETTERS)];
    const later = words.flat().slice(1).map((letter) => `${lead}${letter}`);
    const mark = [declaredMark(tool) ?? letters.join(''), ...later].find((each) => !worn.has(each)) ?? letters.join('');
    worn.add(mark);
    return mark;
  });
}

/** Each agent once, whatever doors reach it, in the roster's order (§5.1). */
export function agentRows(tools: readonly Tool[], answer: AccountsAnswer | null | undefined): AgentRow[] {
  const marks = agentMarks(tools);
  return tools.map((tool, at) => {
    const use = agentOf(answer, tool.name);
    const states = [...accountStates(tool, use).values()];
    const signedOut = states.filter((state) => state.state === 'out').length;
    const refused = states.filter((state) => state.state === 'refused').length;
    // An agent with no account of Daoris's own runs on its own sign-in, where it has one; one with no sign-in at all (a
    // plugin's agent) is said to be installed and nothing more.
    const phrase = !tool.present
      ? i18n.t('harness.absent')
      : tool.accounts.length === 0
        ? i18n.t(tool.doors[0]?.signsIn === false ? 'harness.installed' : 'agents.row.own')
        : [
          i18n.t('agents.row.accounts', { count: tool.accounts.length }),
          ...(signedOut > 0 ? [i18n.t('agents.row.signedOut', { count: signedOut })] : []),
          ...(refused > 0 ? [i18n.t('agents.row.refused', { count: refused })] : []),
        ].join(' · ');
    return {
      name: tool.name,
      product: tool.product ?? tool.name,
      maker: tool.maker,
      installed: tool.present,
      phrase,
      waiting: tool.present ? signedOutHeld(tool, use) : 0,
      mark: marks[at]!,
      installs: tool.doors[0]?.installs !== false,
    };
  });
}

// ---------------------------------------------------------------- each folded section's line (D150 §1 rule 4, §5.2)

/**
 * How accounts are used, folded (D152 §4.4; POOL1c, the account-pool design §4.2's `agents.use.summary.all`): the one set's,
 * every account for every workspace, and the way the set is used: *All 3 accounts, for every workspace · Make the most of them*.
 */
export function useSummary(use: AgentAccounts, count: number): string {
  const mode = i18n.t(machineScope(use).use.use === 'order' ? 'harness.use.mode.order' : 'harness.use.mode.goal');
  return i18n.t('agents.use.summary.all', { count, mode });
}

/** The ways in, folded: each door's word and version, and the one driven work starts on (`driver.json`'s adapter). */
export function doorsSummary(tool: Tool, adapter: string | undefined): string {
  return tool.doors.map((door) => {
    const word = i18n.t(door.wire === 'acp' ? 'harness.wire.acp' : 'harness.wire.pipe');
    if (!door.present) return i18n.t('agents.doors.absent', { door: word });
    const version = versionOnly(door.version ?? '');
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
