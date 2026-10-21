import type { Session } from '../data/api';
import type { SessionGroupName } from './groups';

// The session list by entry (SESSONE1c, D167 point 5, the co-working design §1.6): one row per line of work in one
// repository, however many records (parts) it holds. The driver's one reader places each entry (`SESSION_GROUPS`'
// `entries`, SESSONE1b); these helpers only draw from its answer, so the page and the terminal never group by a rule of
// their own. Pure, so every list a person could have is an argument.

/** How a part began, as the driver's reader says it (D167, SESSONE1b). A word a newer host adds is read as `opened`. */
export type PartBegan = 'opened' | 'retried' | 'resumed' | 'carried-on' | 'handed-over' | 'moved' | 'asked';

/** One record of an entry, as `SESSION_GROUPS` answers it: oldest first within its entry. */
export type EntryPart = {
  session: string;
  /** The quest it served; null for a conversation. */
  quest: string | null;
  /** The row's word (`working`, `parked`, `completed`, ...). */
  shown: string;
  group: SessionGroupName;
  began: PartBegan | string;
  /** The earlier part it followed, where it followed one. */
  after: string | null;
  /** The repository whose request it answered, for `asked`. */
  askedBy: string | null;
};

/**
 * What one part spent, as `SESSION_SPENT` answers it from this machine's record (SESSCTX1d; the driver's `SessionSpent`): its
 * turns, those whose door said what they consumed, and each count summed over those, null where none said it, never zero.
 */
export type PartSpent = {
  turns: number;
  reported: number;
  input?: number | null;
  output?: number | null;
  cacheRead?: number | null;
  cacheWrite?: number | null;
};

/** One line of work in one repository: its key is opaque, its origin names the ask or quest that began it. */
export type SessionEntry = {
  entry: string;
  /** `ask:<id>`, `quest:<id>` or `session:<id>`. */
  origin: string;
  /** The repository, or `intake`. */
  repository: string;
  group: SessionGroupName;
  /** The id of the newest part in the group the entry sits in: the row's record. */
  leading: string;
  parts: EntryPart[];
};

/** The ask an entry belongs to, or null for a conversation and a line rooted in a quest alone. */
export function askOfEntry(entry: Pick<SessionEntry, 'origin'>): string | null {
  return entry.origin.startsWith('ask:') ? entry.origin.slice('ask:'.length) : null;
}

/** Whether an entry is an ask's intake (D167): its repository is the word `intake`. */
export const isIntakeEntry = (entry: Pick<SessionEntry, 'repository'>) => entry.repository === 'intake';

/**
 * The sessions the list draws, one record per entry: the leading part. Where no host answered entries, every record, as the
 * list was before. A record in no entry (an ended intake whose ask became quests belongs to its ask's page) is not listed
 * once entries are answered, and a part that does not lead is shown by its strip, not its own row.
 */
export function listedSessions(sessions: readonly Session[], entries: readonly SessionEntry[] | undefined): Session[] {
  if (!entries) return [...sessions];
  const leading = new Set(entries.map((entry) => entry.leading));
  return sessions.filter((session) => leading.has(session.id));
}

/** The entry that holds a session as one of its parts, or null where it holds none (or no host answered entries). */
export function entryOfSession(entries: readonly SessionEntry[] | undefined, id: string | null | undefined): SessionEntry | null {
  if (!entries || !id) return null;
  return entries.find((entry) => entry.parts.some((part) => part.session === id)) ?? null;
}

/** Every part's id of the entry a session belongs to: what an archive of the entry sends. A session in none is its own. */
export function partIds(entries: readonly SessionEntry[] | undefined, id: string): string[] {
  const entry = entryOfSession(entries, id);
  return entry ? entry.parts.map((part) => part.session) : [id];
}

/**
 * An entry's name where it is not its leading record's own (design §1.6): the ask's, for a line of work an ask began, named as
 * the ask's page names it. Null for an intake, whose record's own name already names its ask, and for an entry whose ask this
 * page does not hold (a teammate's), which then keeps its record's name.
 */
export function nameOfEntry(entry: SessionEntry | undefined, askNames: Readonly<Record<string, string>>): string | null {
  if (!entry || isIntakeEntry(entry)) return null;
  const ask = askOfEntry(entry);
  return ask ? askNames[ask] ?? null : null;
}

/** The listed row's facts about its entry: how many parts it holds, and all their ids. Absent where it is a record alone. */
export type EntryFacts = { parts: number; ids: readonly string[] };

/** The facts of each entry's leading row, by that row's id; only entries of more than one part carry a count worth saying. */
export function entryFacts(entries: readonly SessionEntry[] | undefined): Map<string, EntryFacts> {
  return new Map((entries ?? []).map((entry): [string, EntryFacts] => [
    entry.leading, { parts: entry.parts.length, ids: entry.parts.map((part) => part.session) },
  ]));
}

/** One line of an ask's page (design §1.6): a repository's line of work for the ask, its parts counted, its leading part's word. */
export type AskLine = { entry: string; repository: string; parts: number; leading: string; shown: string };

/**
 * An ask's work by entry, in the reader's order: each repository's line of work that began with this ask, each a door to its
 * leading part. The intake is not one of them: it has a section of its own on the page, and once its ask became quests it is
 * out of the entries altogether.
 */
export function askLines(entries: readonly SessionEntry[] | undefined, ask: string): AskLine[] {
  return (entries ?? [])
    .filter((entry) => askOfEntry(entry) === ask && !isIntakeEntry(entry))
    .map((entry) => ({
      entry: entry.entry,
      repository: entry.repository,
      parts: entry.parts.length,
      leading: entry.leading,
      shown: entry.parts.find((part) => part.session === entry.leading)?.shown ?? entry.parts.at(-1)!.shown,
    }));
}

/** One group of the list by ask: its ask's id, or null for what no ask began; its entries' leading records in the reader's order. */
export type AskGroup = { ask: string | null; sessions: Session[] };

/**
 * The list by ask (D167 point 5, design §1.6): each ask its entries, in the order the reader placed them (so the ask with
 * what needs you leads), and under *Not on an ask* the conversations and the lines rooted in a quest alone, last. Archived
 * entries are hidden unless shown; the attended one is never taken out from under its reader.
 */
export function sessionsByAsk(
  sessions: readonly Session[], entries: readonly SessionEntry[] | undefined,
  { selected, archived }: { selected: string | null | undefined; archived: boolean },
): AskGroup[] {
  const held = new Map(sessions.map((row): [string, Session] => [row.id, row]));
  const groups = new Map<string | null, Session[]>();
  for (const entry of entries ?? []) {
    const record = held.get(entry.leading);
    if (!record) continue;
    const attended = entry.parts.some((part) => part.session === selected);
    if (entry.group === 'archived' && !archived && !attended) continue;
    const ask = askOfEntry(entry);
    groups.set(ask, [...(groups.get(ask) ?? []), record]);
  }
  const asks = [...groups.keys()].filter((key): key is string => key !== null);
  return [...asks, ...(groups.has(null) ? [null] : [])].map((ask) => ({ ask, sessions: groups.get(ask)! }));
}
