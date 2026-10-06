// `reads.json` under the home (ROSTER1, D150 §5.3; AGENTREAD1): what was last read of each account's sign-in, and of the
// tool's own, per agent (the accounts' owner, AGT7), with when:
//
//   { "claude-code": { "own": { "login": "in", "read": "2026-10-04T10:01:00Z" },
//                      "accounts": { "account-1": { "login": "out", "read": "2026-10-04T10:00:00Z" } } } }
//
// The driver's roster reports from it, so a look, a view opening and a restart start from what was last read and ask
// nothing. A reading the terminal makes is kept here too, so the screen and the driver loop learn from `daoris agent list`
// and a sign-in's end at a terminal as they learn from their own (D50): before this, the terminal read accounts and kept
// nothing, and the screen learned nothing from its press (D125's ROSTER1 note).
//
// It is the CLI's twin of the driver's `AccountReads` (`Of`, `Keep`, `Forget`). The table both keep is one file,
// `test/fixtures/account-reads.json`: `accountreads.test.ts` holds this module to it row for row, and the driver's own rows
// of what does not read to it cell for cell, and the driver's `AccountReadsTests` holds `AccountReads` to it row for row
// (AGENTREAD1b). The rules:
//
//   1. Missing or unreadable is nothing known: never read, and never a guess of signed in or out (D57). An entry is an
//      object whose `login` is exactly `in`, `out` or `unknown` and whose `read` is ISO 8601 and nothing lenient; anything
//      else is no reading. Agents, accounts and the file's own keys compare without case as the driver's ordinal
//      comparison does, code point by code point (AGENTREAD1c): each to its one capital and never a wider one, so `straße`
//      is not `STRASSE`, and a dotless i is not an I. An account written twice in any case is the last that reads.
//   2. Written only by a reading: a question that was never asked (an agent with no login question, a lock another holder
//      kept, a pin with nothing installed) writes nothing, so the last word stands with its own time.
//   3. An older reading never replaces a newer one; one at the same moment does, and an entry that does not read is
//      replaced whatever its time. A reading replaces its entry whole, written last under the name given, its moment in
//      UTC to the second.
//   4. A word and a time, never who: who signed in is read fresh and written nowhere (D66 §3).
//   5. Each writer keeps what it has no field for, LF with a final newline: for the same readings both write the same
//      bytes, a name in any script and HTML's marks as they are, since the driver writes with the relaxed encoder
//      (AGENTREAD1b); the few characters that encoder still escapes are listed in D125's AGENTREAD1b note. An unreadable
//      file is nothing known, so a reading replaces it, as the driver's does.
//   6. An account removed from this machine is forgotten, so one made later under its name starts never read; nothing to
//      forget writes nothing.
//
// No HTTP route reaches it (D47 §4), and this module opens no socket and spawns nothing.

import { join } from 'node:path';
import { findName } from './casefold.ts';
import { isoMoment } from './cooling.ts';
import { readJsonObject, writeTextAtomic } from './fsx.ts';

/** The file's name, as the driver's `AccountReads.FileName` spells it. */
export const READS_FILE = 'reads.json';

// The file's own keys, as the driver's `AccountReads` spells them.
const OWN = 'own';
const ACCOUNTS = 'accounts';

/** The agent's word on a sign-in: unknown where its answer did not read or the question could not be put to it. */
export type Login = 'in' | 'out' | 'unknown';

const WORDS: readonly Login[] = ['in', 'out', 'unknown'];

/** What one reading of a sign-in said, and when it was made. */
export interface AccountRead {
  login: Login;
  at: Date;
}

/** An agent's readings: its own sign-in's, and each account's by its name as first written; none where nothing was read. */
export interface AgentReads {
  own: AccountRead | null;
  accounts: Record<string, AccountRead>;
}

type Node = Record<string, unknown>;

/** Where the file is: beside `cooling.json` and `windows.json`, under the home. */
export function readsPath(home: string): string {
  return join(home, READS_FILE);
}

/** What was last read of `agent`'s accounts and its own sign-in (rule 1); nothing where nothing was. */
export function readsOf(home: string, agent: string): AgentReads {
  const held = child(load(home), agent);
  if (held === null) return { own: null, accounts: {} };

  const accounts: Record<string, AccountRead> = {};
  for (const [name, node] of Object.entries(child(held, ACCOUNTS) ?? {})) {
    const read = asNode(node) === null ? null : reading(asNode(node)!);
    if (read === null) continue;
    // The last that reads, under the name first written, as a dictionary that ignores case keeps it.
    accounts[key(accounts, name) ?? name] = read;
  }

  const own = child(held, OWN);
  return { own: own === null ? null : reading(own), accounts };
}

/** One account's reading, compared without case, or with `account` null the tool's own; null where none. */
export function readOf(reads: AgentReads, account: string | null): AccountRead | null {
  if (account === null) return reads.own;
  const found = key(reads.accounts, account);
  return found === null ? null : reads.accounts[found]!;
}

/**
 * Keep a reading of one account's sign-in, or with `account` null the tool's own, unless a newer reading of it is already
 * there (rule 3). True when it was written.
 */
export function keepRead(home: string, agent: string, account: string | null, login: Login, at: Date): boolean {
  const root = load(home);
  // Made under the name given where none reads, as the driver makes one: a key holding anything else is replaced.
  const held = child(root, agent) ?? (root[agent] = {}) as Node;
  const parent = account === null ? held : child(held, ACCOUNTS) ?? (held[ACCOUNTS] = {}) as Node;
  const name = account ?? OWN;

  const was = child(parent, name);
  const before = was === null ? null : reading(was);
  if (before !== null && before.at.getTime() > at.getTime()) return false;

  const old = key(parent, name);
  if (old !== null) delete parent[old];
  parent[name] = { login, read: stamp(at) };
  save(home, root);
  return true;
}

/** An account removed from this machine (rule 6): its reading goes, and the rest stand. True when there was one. */
export function forgetRead(home: string, agent: string, account: string): boolean {
  const root = load(home);
  const named = child(child(root, agent), ACCOUNTS);
  const found = named === null ? null : key(named, account);
  if (named === null || found === null) return false;

  delete named[found];
  save(home, root);
  return true;
}

/** The file's root, or an empty one when it is missing or does not read (rule 1). */
function load(home: string): Node {
  return readJsonObject(readsPath(home)).value ?? {};
}

/** Written whole, beside then renamed, LF with a final newline (rule 5): an unreadable file is nothing kept. */
function save(home: string, root: Node): void {
  writeTextAtomic(readsPath(home), `${JSON.stringify(root, null, 2)}\n`);
}

/** One entry's reading, or null where its word or its time does not read (rule 1). */
function reading(entry: Node): AccountRead | null {
  const word = entry['login'];
  const login = typeof word === 'string' && (WORDS as readonly string[]).includes(word) ? word as Login : null;
  const at = isoMoment(entry['read']);
  return login !== null && at !== null ? { login, at } : null;
}

/** A moment as the driver's `AccountCooling.Stamp` writes it: UTC, to the second. */
function stamp(at: Date): string {
  return `${at.toISOString().slice(0, 19)}Z`;
}

function asNode(value: unknown): Node | null {
  return value !== null && typeof value === 'object' && !Array.isArray(value) ? value as Node : null;
}

/**
 * The object the first key equal to `name` without case holds, as the driver finds a child: null where there is no such
 * key, or where that key holds anything else.
 */
function child(parent: Node | null, name: string): Node | null {
  const found = parent === null ? null : key(parent, name);
  return found === null ? null : asNode(parent![found]);
}

/** The first key equal to `name` without case, as the driver finds one (rule 1; `casefold.ts`). */
function key(held: object, name: string): string | null {
  return findName(Object.keys(held), name);
}
