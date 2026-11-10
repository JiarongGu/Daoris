import type { Place } from './focus';

/**
 * The message that carried a change of focus, as the conversation's file keeps it: the message's id and time, the place before and
 * after, and whether the driver widened a place no longer on this machine.
 */
export type FocusMark = { word: string | null; at: string | null; from: Place; to: Place; widened: boolean };

/**
 * One of Ask Daoris's conversations as its history lists it: the driver's `HELP_CONVERSATIONS` row, field for
 * field what `daoris ask-daoris list --json` prints. Kept on this machine only: its words are this machine's record, and its
 * name and pin a file beside them.
 */
export type HelpConversationRow = {
  /** The conversation's records in order, including its newest. */
  parts?: string[];
  session: string;
  /** What the list calls it: the person's name for it, else its first question. */
  title: string;
  name: string | null;
  opening: string | null;
  /**
   * A line of what it is about: the first line of Ask Daoris's last answer in it, as Markdown, whole up to 1000 characters,
   * so the page parses it before it cuts it to a row.
   */
  about: string | null;
  created: string;
  /** When it is last spoken in. */
  last: string;
  pinned: string | null;
  live: boolean;
  /**
   * The access mode it ran under, its record's: `restricted` or `full`, the effective one its last
   * start got, never the mode chosen now. Absent from a driver before it.
   */
  mode?: string;
  /**
   * The workspace it started in, its record's: kept from its start and never
   * moved, `default` for a record from before. Absent from a driver before it, which is `default`'s too (`workspaceOf`).
   */
  workspace?: string;
  /** The place its last turn ran under, and the one its next words take; absent from a driver before them (`homeFocus`). */
  focus?: Place | null;
  focusNext?: Place | null;
  /** Each message that carried a change of focus, first to last; absent from a driver before them. */
  marks?: FocusMark[];
  /** Whether words written to it once it ended go on in its own conversation; else a new one starts from its words. */
  resumable: boolean;
  /** The earlier conversation it started from, and what it is handed of it. */
  from: string | null;
  handed: string | null;
  /** Where a search found its words: the title they are in, or 60 characters of what is said around them, raw. */
  found: string | null;
  /**
   * Where a search found its words in what is said: the line they are on, as Markdown, whole up to 1000 characters, else a
   * window around them with an ellipsis. Null when the title held them; absent from a driver before pages.
   */
  foundLine?: string | null;
};

/** Whether two workspace names name one workspace, whatever their letters' case, as the driver matches them. */
export const sameWorkspace = (a: string, b: string): boolean => a.toLowerCase() === b.toLowerCase();

/** A name's longest: a title, as the driver keeps it (`HelpConversations.NameLimit`). */
export const HELP_NAME_LIMIT = 80;

/**
 * A page of the history as the driver answers it: its rows, how many the whole list holds or the search found,
 * and the offset the next page starts at, null on the last.
 */
export type HelpListing = { conversations: HelpConversationRow[]; total: number; next: number | null };

const count = (value: unknown): number | null =>
  typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : null;

/**
 * One `HELP_CONVERSATIONS` answer read as a page. A driver without pages answers `{conversations, cut}`,
 * which is read as one page holding all it listed: its `cut` says only that older ones were left out, never where they start.
 */
export function listingPage(answer: unknown): HelpListing {
  const held = (answer ?? {}) as { conversations?: unknown; total?: unknown; next?: unknown };
  const conversations = Array.isArray(held.conversations) ? held.conversations as HelpConversationRow[] : [];
  return { conversations, total: count(held.total) ?? conversations.length, next: count(held.next) };
}

/**
 * A history's pages as one list: in the order they are asked, each conversation once by its session. One spoken
 * in or pinned between two asks can be on two pages; it stays where it is first listed. The count
 * and the next page are the last page's, the newest the driver said.
 */
export function joinPages(pages: readonly HelpListing[]): HelpListing {
  const seen = new Set<string>();
  const conversations = pages.flatMap((page) => page.conversations).filter((row) => {
    if (seen.has(row.session)) return false;
    seen.add(row.session);
    return true;
  });
  const last = pages.at(-1);
  return { conversations, total: last?.total ?? 0, next: last?.next ?? null };
}

/** The history's groups, in the order a person reads them. */
export const HISTORY_GROUPS = ['pinned', 'today', 'yesterday', 'week', 'older'] as const;
export type HistoryGroup = (typeof HISTORY_GROUPS)[number];

const DAY = 86_400_000;
const midnight = (at: Date) => new Date(at.getFullYear(), at.getMonth(), at.getDate());

/**
 * The history grouped as a person reads time: the pinned first, then by the local calendar day each is last
 * spoken in, today, yesterday, the rest of this week from its Monday, and older. Each group keeps the driver's order (pinned
 * first, then the newest), and a group with nothing in it is left out.
 *
 * @remarks
 * By this machine's calendar, not by elapsed hours: a conversation at 23:50 is yesterday at 00:10. A week starts on Monday,
 * as ISO 8601 and 中文 count it, so on a Monday the week holds today alone and Sunday is yesterday. A time ahead of this
 * machine's clock is today: records travel and clocks do not.
 */
export function historyGroups(rows: readonly HelpConversationRow[], now = new Date()): { id: HistoryGroup; rows: HelpConversationRow[] }[] {
  const today = midnight(now).getTime();
  // By the calendar, not by 24-hour days, so a change of clocks inside the week cannot move its Monday by an hour.
  const monday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - ((now.getDay() + 6) % 7)).getTime();
  const groupOf = (row: HelpConversationRow): HistoryGroup => {
    if (row.pinned) return 'pinned';
    const day = midnight(new Date(row.last)).getTime();
    // Rounded, since a day that crosses a change of clocks is 23 or 25 hours long.
    const before = Math.round((today - day) / DAY);
    if (before <= 0) return 'today';
    if (before === 1) return 'yesterday';
    return day >= monday ? 'week' : 'older';
  };
  const held = new Map<HistoryGroup, HelpConversationRow[]>();
  for (const row of rows) {
    const id = groupOf(row);
    held.set(id, [...(held.get(id) ?? []), row]);
  }
  return HISTORY_GROUPS.filter((id) => held.has(id)).map((id) => ({ id, rows: held.get(id)! }));
}
