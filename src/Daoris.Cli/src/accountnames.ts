// An account's name and its stable id (ACCT2, D125's ACCT2 note). `accounts.json` under the home holds the name a person
// gave each account, by agent (the accounts' owner, AGT7) and id. The id is the account's folder's name, so an `account-N`
// made before this keeps its name as its id, and an account made now takes a fresh one (`newAccountId`). Everything else is
// keyed by the id — the wiring's defaults and lists, `reads.json`, `cooling.json`, `windows.json`, `keys.json`, the probe
// locks, session records and the usage ledger — so a rename touches this file alone and every one of them keeps working.
//
// It is the CLI's twin of the driver's `AccountNames`; `accountnames.test.ts` reads `AccountNamesTwinTests.cs`'s rows,
// cell for cell. The rules both keep:
//
//   1. Beside the account, never inside it: the folder is the agent's (D49 §4), and moving it would move a home a harness
//      may have keyed its credential to (D66 §3's rejected rename).
//   2. Written only by a person's rename: who signed in is offered as the name at a sign-in's end, and written only where
//      the person keeps it (D66 §3). A read writes nothing.
//   3. A name is one word a terminal can type — no space, control character or backtick, no leading dash, at most 64
//      characters — and never another account's id or name, in any case. The account's own id, or none, clears it.
//   4. The account a person means is the one whose id they named, exactly, as the wiring compares an id; else the one here
//      whose name they named, in any case.
//   5. Each writer keeps what it has no field for; agents and ids compare without case and are written as found, a name
//      first in its entry, LF with a final newline — the same bytes for the same names. "In any case" and "without case"
//      are the driver's `OrdinalIgnoreCase` (`casefold.ts`, CASEFOLD1): `straße` is not `STRASSE`.
//
// It spawns nothing and opens no socket.

import { randomBytes } from 'node:crypto';
import { join } from 'node:path';
import { findName, foldName, sameName } from './casefold.ts';
import { DaorisError } from './errors.ts';
import { readJsonObject, writeTextAtomic } from './fsx.ts';

export const ACCOUNTS_FILE = 'accounts.json';

/** The longest name: one a row and a command line both hold. */
export const NAME_LONGEST = 64;

/** What a fresh id begins with, so a log line or a record says it is an account's. */
export const ID_PREFIX = 'acct-';

/** Why a name cannot be given to an account. */
export type NameProblemKind = 'missing' | 'blank' | 'characters' | 'long' | 'taken' | 'unreadable';

export interface NameProblem {
  kind: NameProblemKind;
  /** The other account a `taken` name collides with. */
  other: string | null;
}

/** A name refused: the sentence, and the problem a twin table reads. */
export class AccountNameError extends DaorisError {
  readonly problem: NameProblem;

  constructor(problem: NameProblem, message: string) {
    super(message);
    this.problem = problem;
  }
}

/** Where the file is: beside `reads.json`, under the home. */
export function accountsPath(home: string): string {
  return join(home, ACCOUNTS_FILE);
}

type Root = Record<string, unknown>;

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** A key in an object, compared without case, as the readings compare names (rule 5; `casefold.ts`). */
function keyOf(held: Record<string, unknown>, name: string): string | undefined {
  return findName(Object.keys(held), name) ?? undefined;
}

function child(parent: Record<string, unknown> | undefined, name: string): Record<string, unknown> | undefined {
  if (!parent) return undefined;
  const key = keyOf(parent, name);
  const value = key === undefined ? undefined : parent[key];
  return isObject(value) ? value : undefined;
}

/** The name an entry holds, trimmed, or null where it holds none. */
function nameIn(entry: Record<string, unknown>): string | null {
  return typeof entry.name === 'string' && entry.name.trim().length > 0 ? entry.name.trim() : null;
}

function load(home: string): { root: Root; unreadable: boolean } {
  const { value, problem } = readJsonObject(accountsPath(home));
  return { root: value ?? {}, unreadable: problem !== null };
}

function namesIn(root: Root, agent: string): Record<string, string> {
  const names: Record<string, string> = {};
  const held = child(root, agent);
  if (!held) return names;
  for (const [id, entry] of Object.entries(held)) {
    const name = isObject(entry) ? nameIn(entry) : null;
    if (name !== null && keyOf(names, id) === undefined) names[id] = name;
  }
  return names;
}

/** Every name one agent's accounts were given, by id; none where nothing was. */
export function accountNames(home: string, agent: string): Record<string, string> {
  return namesIn(load(home).root, agent);
}

/** The name an id holds in `names`, compared without case, or null. */
export function nameFor(names: Record<string, string>, account: string): string | null {
  const key = keyOf(names, account);
  return key === undefined ? null : names[key]!;
}

/** The name the person gave an account, trimmed, or null where none. */
export function nameOf(home: string, agent: string, account: string): string | null {
  return nameFor(accountNames(home, agent), account);
}

/** What an account reads as: its name, else its id. */
export function shownAs(names: Record<string, string>, account: string): string {
  return nameFor(names, account) ?? account;
}

/**
 * The account a person means by `given` (rule 4): the one whose id it is, exactly; else the one here whose name it is, in
 * any case; null where neither.
 */
export function resolveAccount(accounts: string[], names: Record<string, string>, given: string): string | null {
  const wanted = given.trim();
  if (accounts.includes(wanted)) return wanted;
  return accounts.find((account) => {
    const name = nameFor(names, account);
    return name !== null && sameName(name, wanted);
  }) ?? null;
}

/**
 * Which account a sign-in into an account that is here reaches (ACCT1): the one named, by its id or its name, else the
 * machine's default. `account` when it is one here; else `missing`, the name that is none, or neither where none was named
 * and the machine names no default. Never one that is not here: a sign-in into an account never makes a folder.
 */
export function signInTarget(
  accounts: string[], names: Record<string, string>, machineDefault: string | null | undefined, given: string | null | undefined,
): { account: string | null; missing: string | null } {
  const named = given?.trim() || machineDefault?.trim() || null;
  if (named === null) return { account: null, missing: null };
  const account = resolveAccount(accounts, names, named);
  return account === null ? { account: null, missing: named } : { account, missing: null };
}

/** Each account as a person reads it in a sentence: its id, with its name before it where it has one. */
export function listedAccounts(accounts: string[], names: Record<string, string>): string {
  if (accounts.length === 0) return '(none)';
  return accounts.map((account) => {
    const name = nameFor(names, account);
    return name === null ? account : `${name} (${account})`;
  }).join(', ');
}

/** The refusal a sign-in into an account that is not here says (ACCT1), as the driver's `AccountNames.SignInRefusal` says it. */
export function signInRefusal(
  agent: string, target: { missing: string | null }, accounts: string[], names: Record<string, string>,
): string {
  return (target.missing !== null
    ? `\`${agent}\` has no account \`${target.missing}\` on this machine, so nothing was signed in and no account was made`
    : `name the \`${agent}\` account to sign in to: this machine names no default for it, and a sign-in into an account `
      + 'never makes one')
    + ` — accounts there: ${listedAccounts(accounts, names)}. \`daoris agent login ${agent} --new\` signs in to a new account.`;
}

/** Rule 3: why `name` cannot name `account`, or null when it can. Null, or the account's own id, clears its name. */
export function nameProblem(
  accounts: string[], names: Record<string, string>, account: string, name: string | null,
): NameProblem | null {
  if (!accounts.includes(account)) return { kind: 'missing', other: null };
  if (name === null) return null;

  const wanted = name.trim();
  if (wanted.length === 0) return { kind: 'blank', other: null };
  if (wanted === account) return null;
  if (wanted.startsWith('-') || /[\s\p{Cc}`]/u.test(wanted)) return { kind: 'characters', other: null };
  if (wanted.length > NAME_LONGEST) return { kind: 'long', other: null };

  for (const other of accounts.filter((each) => each !== account)) {
    const called = nameFor(names, other);
    if (sameName(other, wanted) || (called !== null && sameName(called, wanted))) return { kind: 'taken', other };
  }
  return null;
}

function sentence(
  agent: string, problem: NameProblem, account: string, name: string | null, accounts: string[], names: Record<string, string>,
): string {
  const wanted = name?.trim() ?? '';
  switch (problem.kind) {
    case 'missing':
      return `\`${agent}\` has no account \`${account}\` on this machine — accounts there: ${listedAccounts(accounts, names)}.`;
    case 'blank':
      return `a name is not blank — name \`${account}\` by its id, \`${account}\`, to give it none.`;
    case 'characters':
      return `\`${wanted}\` is not a name a terminal can type: a name is one word, with no space or backtick, not starting with a dash.`;
    case 'long':
      return `a name is at most ${NAME_LONGEST} characters — \`${wanted}\` is ${wanted.length}.`;
    default:
      return `\`${wanted}\` already names \`${problem.other}\` — each \`${agent}\` account has a name of its own, and an id is one.`;
  }
}

/**
 * Give an account its name, or clear it with null or the account's own id (ACCT2): the person's word, both doors'. Asked
 * `nameProblem` first, and refused with its sentence, writing nothing.
 *
 * @returns The account's name now, or null where it has none.
 */
export function renameAccount(home: string, agent: string, accounts: string[], account: string, name: string | null): string | null {
  const { root, unreadable } = load(home);
  const names = namesIn(root, agent);
  const problem = nameProblem(accounts, names, account, name);
  if (problem !== null) throw new AccountNameError(problem, sentence(agent, problem, account, name, accounts, names));
  if (unreadable) {
    throw new AccountNameError({ kind: 'unreadable', other: null },
      `${ACCOUNTS_FILE} could not be read, so \`${account}\` was not renamed — writing it would lose every other account's `
      + 'name. Fix the file or remove it, then rename again.');
  }

  const wanted = name?.trim() ?? null;
  const clear = wanted === null || wanted === account;
  const agentKey = keyOf(root, agent);
  const held = child(root, agent);
  const entryKey = held ? keyOf(held, account) : undefined;
  const entry = held && entryKey !== undefined && isObject(held[entryKey]) ? held[entryKey] as Record<string, unknown> : undefined;

  if (clear) {
    if (!entry) return null;
    delete entry.name;
    if (Object.keys(entry).length === 0) delete held![entryKey!];
    if (Object.keys(held!).length === 0) delete root[agentKey!];
  } else {
    const parent = held ?? (root[agent] = {}) as Record<string, unknown>;
    const rest = Object.entries(entry ?? {}).filter(([key]) => key !== 'name');
    // The name goes first in its entry, as the driver writes it, whatever else the entry holds.
    parent[entryKey ?? account] = { name: wanted, ...Object.fromEntries(rest) };
  }

  writeTextAtomic(accountsPath(home), `${JSON.stringify(root, null, 2)}\n`);
  return clear ? null : wanted;
}

/** An account removed from this machine (D66 §3): its name goes with it. Nothing to forget writes nothing. */
export function forgetName(home: string, agent: string, account: string): void {
  const { root, unreadable } = load(home);
  const agentKey = keyOf(root, agent);
  const held = child(root, agent);
  const entryKey = held ? keyOf(held, account) : undefined;
  if (unreadable || !held || entryKey === undefined) return;
  delete held[entryKey];
  if (Object.keys(held).length === 0) delete root[agentKey!];
  writeTextAtomic(accountsPath(home), `${JSON.stringify(root, null, 2)}\n`);
}

/**
 * A new account's id (ACCT2, twin rule 5 of `toolchain.ts`): `acct-` and eight lowercase hex characters, drawn again while
 * an account of the agent already has it. Never reused, unlike the first free `account-N` it replaces, so a reading, a
 * cool-off or a usage total kept under one id never lands on another account. The person reads its name, never this.
 *
 * @param taken The agent's accounts on this machine.
 * @param draw Eight hex characters; the system's random source unless a test holds its own.
 */
export function newAccountId(taken: string[], draw: () => string = () => randomBytes(4).toString('hex')): string {
  const held = new Set(taken.map(foldName));
  for (;;) {
    const id = `${ID_PREFIX}${draw()}`;
    if (!held.has(foldName(id))) return id;
  }
}
