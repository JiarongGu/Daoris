// `cooling.json` under the home (TOOL4d, TOOL4e, D125 §2.3): each account cooling after its agent said it hit a limit,
// keyed by the account's owner and profile name, `""` for the tool's own configuration home:
//
//   { "claude-code": { "account-1": { "until": "2026-09-29T21:52:00Z", "stated": true, "window": "session",
//                                      "seen": "2026-09-29T14:02:11Z", "session": "3f9c2a71" } } }
//
// The driver WRITES it when a limit is read (`AccountCooling.Cool`); this module only reads it and ends an entry early,
// the terminal's *Try now* (`daoris agent profile ready`) and a sign-in or a key into the account. It is the CLI's twin
// of the driver's `AccountCooling.Read`, `Of` and `End`, and of `CoolingWords.When` and the reason a cool-off gives;
// `cooling.test.ts` holds the tables `CoolingTwinTests.cs` and `AccountCoolingTests.cs` hold, cell for cell. The rules
// both keep:
//
//   1. Missing or unreadable is no account cooling (D21's reading): an observation lost costs at most one start.
//   2. An entry is an object with an `until` written as ISO 8601 and nothing else — a lenient parse would read *Oct 3*
//      as this year's. `seen` is the `until` when it does not read; a flag is true only as JSON `true`; a word is text.
//   3. An entry whose `until` is not after now is ready. Names compare and order without case as the driver's
//      `OrdinalIgnoreCase` does (`casefold.ts`, CASEFOLD1), so `straße` is not `STRASSE`, and are said as written.
//   4. Ending writes the file whole only where an entry by that name was there: every passed or unreadable entry goes
//      with the write, an agent left with none goes, and anything it has no field for stays as written.
//
// It holds no words of the agent's, no key and nobody's name: the session id points at the record, which holds them.
// No HTTP route reaches it (D47 §4), and this module opens no socket and spawns nothing.

import { join } from 'node:path';
import { compareNames, findName, sameName } from './casefold.ts';
import { readJsonObject, writeJsonAtomic } from './fsx.ts';
import { shellWord } from './shellword.ts';

export const COOLING_FILE = 'cooling.json';

/** One account cooling: whose, until when, and what said so. */
export interface CoolingEntry {
  /** The account's owner (AGT7), so a door and its tool share one cool-off. */
  agent: string;
  /** The profile's name, or null for the tool's own configuration home. */
  account: string | null;
  until: Date;
  /** Whether the agent named that time; false is Daoris's default standing in. */
  stated: boolean;
  window: string | null;
  seen: Date;
  session: string | null;
  /** Whether the machine's zone stood in for the one the agent named. */
  assumedZone: boolean;
  /** Whether the agent named a date more than 8 days off, so the default stood in for it. */
  notBelieved: boolean;
}

/** Where the file is: beside `harnesses.json` and the accounts, under the home. */
export function coolingPath(home: string): string {
  return join(home, COOLING_FILE);
}

/** Every account cooling at `now`, by agent and then account, as the driver orders them. */
export function readCooling(home: string, now: Date): CoolingEntry[] {
  return entries(load(home))
    .filter((entry) => entry.until.getTime() > now.getTime())
    .sort((a, b) => compareNames(a.agent, b.agent) || compareNames(a.account ?? '', b.account ?? ''));
}

/** One account's cool-off at `now`, or null when it is ready. */
export function coolingOf(home: string, agent: string, account: string | null, now: Date): CoolingEntry | null {
  return readCooling(home, now)
    .find((entry) => sameName(entry.agent, agent) && sameName(entry.account ?? '', account ?? '')) ?? null;
}

/** End one account's cool-off early (rule 4). True when it was cooling. */
export function endCooling(home: string, agent: string, account: string | null, now: Date): boolean {
  const root = load(home);
  const agentKey = key(root, agent);
  const held = agentKey === null ? null : asEntry(root[agentKey]);
  const accountKey = held === null ? null : key(held, account ?? '');
  if (held === null || accountKey === null) return false;

  const entry = asEntry(held[accountKey]);
  const until = entry === null ? null : isoMoment(entry['until']);
  const ended = until !== null && until.getTime() > now.getTime();
  delete held[accountKey];

  for (const [name, accounts] of Object.entries(root)) {
    const kept = asEntry(accounts);
    if (kept === null) continue;
    for (const [owned, node] of Object.entries(kept)) {
      const read = asEntry(node);
      const at = read === null ? null : isoMoment(read['until']);
      if (at === null || at.getTime() <= now.getTime()) delete kept[owned];
    }
    if (Object.keys(kept).length === 0) delete root[name];
  }

  writeJsonAtomic(coolingPath(home), root);
  return ended;
}

/** A moment as a person reads it here, `Oct 3, 16:02 (Asia/Kathmandu)`: in the machine's zone, named. */
export function coolingWhen(at: Date, zone: string): string {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: zone, month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).formatToParts(at);
  const part = (type: string) => parts.find((each) => each.type === type)?.value ?? '';
  return `${part('month')} ${part('day')}, ${part('hour')}:${part('minute')} (${zone})`;
}

/** Why a cool-off lasts as long as it does, in the driver's words. */
export function coolingWhy(entry: Pick<CoolingEntry, 'stated' | 'assumedZone' | 'notBelieved'>): string {
  if (entry.stated) return entry.assumedZone ? 'as the agent said, in this machine\'s zone' : 'as the agent said';
  return entry.notBelieved
    ? 'Daoris\'s default: the agent named a date more than 8 days off'
    : 'Daoris\'s default: the agent named no time';
}

/** The line `agent list` gives a cooling account (D125 §2.4): until when, how long that is, why, and how to try it now. */
export function coolingLine(entry: CoolingEntry, now: Date, zone: string): string {
  const ready = `\`daoris agent profile ready ${entry.agent} ${entry.account === null ? '--own' : shellWord(entry.account, '<account>')}\``;
  return `cooling until ${coolingWhen(entry.until, zone)}, in ${span(entry.until.getTime() - now.getTime())} — `
    + `${coolingWhy(entry)}; ${ready} tries it now`;
}

/** This machine's zone, as the platform names it. */
export function machineZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

/** How long until then, to the minute and rounded up: `2 d 2 h`, `3 h 10 min`, `1 min`. */
function span(milliseconds: number): string {
  const minutes = Math.max(1, Math.ceil(milliseconds / 60_000));
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  if (days > 0) return hours > 0 ? `${days} d ${hours} h` : `${days} d`;
  return hours > 0 ? `${hours} h ${minutes % 60} min` : `${minutes} min`;
}

type Node = Record<string, unknown>;

/** The file's root, or an empty one when it is missing or does not read (rule 1). */
function load(home: string): Node {
  const { value } = readJsonObject(coolingPath(home));
  return value ?? {};
}

/** Every entry that reads (rule 2), whatever its `until`. */
function entries(root: Node): CoolingEntry[] {
  const found: CoolingEntry[] = [];
  for (const [agent, accounts] of Object.entries(root)) {
    const held = asEntry(accounts);
    if (held === null) continue;
    for (const [account, node] of Object.entries(held)) {
      const entry = asEntry(node);
      const until = entry === null ? null : isoMoment(entry['until']);
      if (entry === null || until === null) continue;
      found.push({
        agent, account: account.length === 0 ? null : account, until, stated: entry['stated'] === true,
        window: text(entry['window']), seen: isoMoment(entry['seen']) ?? until, session: text(entry['session']),
        assumedZone: entry['assumedZone'] === true, notBelieved: entry['notBelieved'] === true,
      });
    }
  }
  return found;
}

function asEntry(value: unknown): Node | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Node : null;
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

/** The first key equal to `name` without case, as the driver finds one (rule 3; `casefold.ts`). */
function key(held: Node, name: string): string | null {
  return findName(Object.keys(held), name);
}

/** The ISO 8601 forms the driver reads: to the second, with a fraction or not, in UTC or at an offset. */
const ISO_MOMENT = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?(Z|[+-]\d{2}:\d{2})$/;

/**
 * A moment as ISO 8601 writes it, and only so, as the driver reads one; `windows.ts` reads its moments with it too, and a
 * pause's time in `driver.json` is read by the same forms (PAUSE1a, `driverconfig.ts`).
 */
export function isoMoment(value: unknown): Date | null {
  return moment(value);
}

/** A moment as ISO 8601 writes it, and only so (rule 2); a date that does not exist is none. */
function moment(value: unknown): Date | null {
  if (typeof value !== 'string') return null;
  const match = ISO_MOMENT.exec(value);
  if (!match) return null;

  const [, year, month, day, hour, minute, second, fraction, zone] = match;
  const y = Number(year), mo = Number(month), d = Number(day), h = Number(hour), mi = Number(minute), s = Number(second);
  const local = Date.UTC(y, mo - 1, d, h, mi, s);
  const check = new Date(local);
  if (check.getUTCFullYear() !== y || check.getUTCMonth() !== mo - 1 || check.getUTCDate() !== d
    || check.getUTCHours() !== h || check.getUTCMinutes() !== mi || check.getUTCSeconds() !== s) {
    return null;
  }

  const offset = zone === 'Z' ? 0 : (zone!.startsWith('-') ? -1 : 1) * (Number(zone!.slice(1, 3)) * 60 + Number(zone!.slice(4, 6)));
  const milliseconds = fraction ? Math.floor(Number(`0.${fraction}`) * 1000) : 0;
  return new Date(local - offset * 60_000 + milliseconds);
}
