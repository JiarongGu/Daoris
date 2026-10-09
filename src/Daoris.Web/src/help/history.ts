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
  /** A line of what it was about: the first line of Ask Daoris's last answer in it. */
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
  /** Where a search found its words. */
  found: string | null;
};

/** A name's longest: a title, as the driver keeps it (`HelpConversations.NameLimit`). */
export const HELP_NAME_LIMIT = 80;

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
