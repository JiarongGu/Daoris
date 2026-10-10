/**
 * One of Ask Daoris's conversations as its history lists it (ASKHIST1): the driver's `HELP_CONVERSATIONS` row, field for
 * field what `daoris-driver help list --json` prints. Kept on this machine only: its words are this machine's record, and its
 * name and pin a file beside them.
 */
export type HelpConversationRow = {
  session: string;
  /** What the list calls it: the person's name for it, else its first question. */
  title: string;
  name: string | null;
  opening: string | null;
  /**
   * A line of what it was about: the first line of Ask Daoris's last answer in it, as Markdown, whole up to 1000 characters
   * (ASKHIST1d1), so the page parses it before it cuts it to a row.
   */
  about: string | null;
  created: string;
  /** When it was last spoken in. */
  last: string;
  pinned: string | null;
  live: boolean;
  /** Whether words written to it once it ended go on in its own conversation; else a new one starts from its words. */
  resumable: boolean;
  /** The earlier conversation it started from, and what it was handed of it. */
  from: string | null;
  handed: string | null;
  /** Where a search found its words: the title they were in, or 60 characters of what was said around them, raw. */
  found: string | null;
  /**
   * Where a search found its words in what was said: the line they are on, as Markdown, whole up to 1000 characters, else a
   * window around them with an ellipsis (ASKHIST1d1). Null when the title held them; absent from a driver before pages.
   */
  foundLine?: string | null;
};

/** A name's longest: a title, as the driver keeps it (`HelpConversations.NameLimit`). */
export const HELP_NAME_LIMIT = 80;

/**
 * A page of the history as the driver answers it (ASKHIST1d1): its rows, how many the whole list holds or the search found,
 * and the offset the next page starts at, null on the last.
 */
export type HelpListing = { conversations: HelpConversationRow[]; total: number; next: number | null };

const count = (value: unknown): number | null =>
  typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : null;

/**
 * One `HELP_CONVERSATIONS` answer read as a page (ASKHIST1d2). A driver from before pages answered `{conversations, cut}`,
 * which is read as one page holding all it listed: its `cut` said only that older ones were left out, never where they start.
 */
export function listingPage(answer: unknown): HelpListing {
  const held = (answer ?? {}) as { conversations?: unknown; total?: unknown; next?: unknown };
  const conversations = Array.isArray(held.conversations) ? held.conversations as HelpConversationRow[] : [];
  return { conversations, total: count(held.total) ?? conversations.length, next: count(held.next) };
}

/**
 * A history's pages as one list (ASKHIST1d2): in the order they were asked, each conversation once by its session. One spoken
 * in or pinned between two asks can be on two pages (D158's ASKHIST1d1 note); it stays where it was first listed. The count
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

// The basic block, extension A and the compatibility block; the later extensions are surrogate pairs, two characters already.
const HAN = /^[一-鿿㐀-䶿豈-﫿]$/;

/**
 * Whether these words are enough to search by (ASKHIST1d2): two characters, or one Han character, which is a word on its own
 * (区, 圈). The driver's own rule (`SessionEvents.Searchable`): one letter of any other script would find nearly everything.
 */
export function searchable(words: string): boolean {
  const wanted = words.trim();
  return wanted.length >= 2 || HAN.test(wanted);
}

/** The history's groups, in the order a person reads them (ASKHIST1c). */
export const HISTORY_GROUPS = ['pinned', 'today', 'yesterday', 'week', 'older'] as const;
export type HistoryGroup = (typeof HISTORY_GROUPS)[number];

const DAY = 86_400_000;
const midnight = (at: Date) => new Date(at.getFullYear(), at.getMonth(), at.getDate());

/**
 * The history grouped as a person reads time (ASKHIST1c): the pinned first, then by the local calendar day each was last
 * spoken in, today, yesterday, the rest of this week from its Monday, and older. Each group keeps the driver's order (pinned
 * first, then the newest), and a group with nothing in it is left out.
 *
 * @remarks
 * By this machine's calendar, not by elapsed hours: a conversation at 23:50 was yesterday at 00:10. A week starts on Monday,
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
