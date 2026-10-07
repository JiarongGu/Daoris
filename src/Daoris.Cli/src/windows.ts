// `windows.json` under the home (TOOL6c, D130 §5.2): what each account's agent last said about its windows, keyed by the
// account's owner and profile name, then by Daoris's name for the window:
//
//   { "claude-code": { "account-1": {
//       "session": { "reset": "2026-10-02T14:00:00Z", "used": 0.88, "standing": "clear", "seen": "2026-10-02T11:40:00Z", "session": "s1" },
//       "weekly":  { "reset": "2026-10-06T21:18:00Z", "used": 0.14, "seen": "2026-10-02T11:40:00Z", "session": "s1" } } } }
//
// The driver WRITES it as a session's door carries the agent's frame, or as the agent's own server answers where no door
// carries one (CODEXUSE1; `AccountWindows.Said` either way, a server's answer with no `session`), and its weekly limits'
// resets (`AccountWindows.Told`); this module only reads it, for `daoris agent list` and `profile use`. It is the CLI's
// twin of the driver's `AccountWindows.SaidOf`: `windows.test.ts` holds the table `WindowsTwinTests.cs` holds, cell for
// cell. The rules both keep:
//
//   1. Missing or unreadable is nothing said (D21's reading), and nothing said is unknown: never spent, never fresh (D57).
//   2. A window is an object whose `reset` and `seen` are ISO 8601 and nothing lenient; it says something only where `used`
//      is a number from nothing up, `standing` is text, or `credits` is JSON `true`. A week a limit told says no use.
//   3. A window whose `reset` is not after now is gone: a reading is a floor as of when it was said, and lasts to its reset.
//   4. Names compare without case as the driver's `OrdinalIgnoreCase` does (`casefold.ts`, CASEFOLD1), so `straße` is not
//      `STRASSE`, and are said as written; windows keep the file's order.
//
// It holds no words of the agent's, no key and nobody's name. No HTTP route reaches it (D47 §4), and this module opens no
// socket and spawns nothing.

import { join } from 'node:path';
import { findName } from './casefold.ts';
import { coolingWhen, isoMoment } from './cooling.ts';
import { readJsonObject } from './fsx.ts';

export const WINDOWS_FILE = 'windows.json';

/**
 * The tool's own sign-in's key (CODEXUSE3): `""`, as `cooling.json` keys its cool-off, since no account's directory is named
 * nothing. The driver keeps its reading there when a person's press asks its agent's server for it (`AccountWindows.Own`,
 * held by `windows.test.ts`); it is read as an account's is, and is none of them.
 */
export const OWN_WINDOWS = '';

/** One window as the driver keeps it: its use where said, its reset, its standing and credits, when, and on which session. */
export interface WindowSaid {
  window: string;
  /** How much of it is used, from 0 to 1; null where the agent gave no number. */
  used: number | null;
  reset: Date;
  /** `clear`, `near` or `refused`, on the window the frame named; null otherwise. */
  standing: string | null;
  credits: boolean;
  seen: Date;
  session: string | null;
}

/** Where the file is: beside `cooling.json`, under the home. */
export function windowsPath(home: string): string {
  return join(home, WINDOWS_FILE);
}

/** What an account's agent last said about its windows, as of `now`: each window still said, or null where none is. */
export function saidOf(home: string, agent: string, account: string, now: Date): WindowSaid[] | null {
  const { value } = readJsonObject(windowsPath(home));
  const owner = value === null ? null : child(value, agent);
  const held = owner === null ? null : child(owner, account);
  if (held === null) return null;

  const windows: WindowSaid[] = [];
  for (const [window, node] of Object.entries(held)) {
    const entry = asEntry(node);
    const reset = entry === null ? null : isoMoment(entry['reset']);
    const seen = entry === null ? null : isoMoment(entry['seen']);
    if (entry === null || reset === null || seen === null || reset.getTime() <= now.getTime()) continue;

    const raw = entry['used'];
    const used = typeof raw === 'number' && Number.isFinite(raw) && raw >= 0 ? raw : null;
    const standing = text(entry['standing']);
    const credits = entry['credits'] === true;
    if (used === null && standing === null && !credits) continue;
    windows.push({ window, used, reset, standing, credits, seen, session: text(entry['session']) });
  }
  return windows.length === 0 ? null : windows;
}

/**
 * What an account last said, as one line a person reads (TOOL6c, D130 §3.2, §9): how long ago, its own word where it gave
 * one, credits, each window's use and reset in the machine's zone, and — where a scope's *near* is given — whether a window
 * reached it. Never a judgement the agent did not make beyond that number.
 */
export function saidLine(windows: WindowSaid[], now: Date, zone: string, near: number | null = null): string {
  const parts: string[] = [];
  const reached = windows.find((each) => each.standing === 'refused');
  const warned = windows.find((each) => each.standing === 'near');
  if (reached) parts.push(`its ${reached.window} limit reached, by its own word`);
  else if (warned) parts.push(`near its ${warned.window} limit, by its own word`);
  if (windows.some((each) => each.credits)) parts.push('drawing on usage credits');
  for (const each of ordered(windows)) {
    if (each.used !== null) parts.push(`${percent(each.used)} of its ${each.window} limit used, resetting ${coolingWhen(each.reset, zone)}`);
  }
  if (near !== null && !reached && !warned && !windows.some((each) => each.credits)
    && windows.some((each) => each.used !== null && each.used * 100 >= near - 1e-9)) {
    parts.push(`near at ${near}%`);
  }
  // A reading with no number, no warning and no credits is its agent's clear word alone.
  if (parts.length === 0) parts.push('clear, by its own word');
  const seen = new Date(Math.max(...windows.map((each) => each.seen.getTime())));
  return `said ${age(seen, now)}: ${parts.join('; ')}`;
}

/** How long ago, as the driver's start line says it: `just now`, `20 min ago`, `3 h ago`, `2 d ago`. */
export function age(seen: Date, now: Date): string {
  const minutes = Math.floor((now.getTime() - seen.getTime()) / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  if (minutes < 1440) return `${Math.floor(minutes / 60)} h ago`;
  return `${Math.floor(minutes / 1440)} d ago`;
}

/** A share as a whole percent: `88%`. */
function percent(share: number): string {
  return `${Math.round(share * 100)}%`;
}

/** The session window, then the week, then any other, as a person reads them. */
function ordered(windows: WindowSaid[]): WindowSaid[] {
  const rank = (window: string) => ({ session: 0, weekly: 1 } as Record<string, number>)[window.toLowerCase()] ?? 2;
  return [...windows].sort((a, b) => rank(a.window) - rank(b.window));
}

type Node = Record<string, unknown>;

function asEntry(value: unknown): Node | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Node : null;
}

/** The value under the first key equal to `name` without case, where it is an object, as the driver finds one (rule 4). */
function child(held: Node, name: string): Node | null {
  const found = findName(Object.keys(held), name);
  return found === null ? null : asEntry(held[found]);
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}
