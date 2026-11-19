/**
 * Where the person left a conversation on this machine, as `READ_MARK` answers it: the newest event of a part of its line they had
 * on screen while the window had focus (its number and its time), when the mark moved there (`read`), and how many of its run's steps
 * had passed then, where its session runs any.
 */
export type ReadMark = { part: string; seq: number; at: string; read: string; steps?: number | null };

/** An event of a part, by its number and its time: what a mark is set at and compared with. */
export type MarkPlace = { part: string; seq: number; at: string };

/**
 * Whether `next` stands past `standing`: a later event of the same part, or another part's event kept later, since a line's parts
 * follow one another. The driver's `ReadMarks.Forward` is the same rule, so a page and the store never disagree on which way is forward.
 */
export const isForward = (standing: MarkPlace | null | undefined, next: MarkPlace): boolean =>
  !standing || (standing.part === next.part ? next.seq > standing.seq : Date.parse(next.at) > Date.parse(standing.at));

/** Whether an event of `part` happened after the mark: what the digest counts and the mark's row stands above. */
export const isAfter = (mark: MarkPlace, part: string, event: { seq: number; at: string }): boolean =>
  isForward(mark, { part, seq: event.seq, at: event.at });

/** The event a block's key names (`e<seq>`), or null for a key no event gave. */
export const seqOf = (key: string): number | null => {
  const match = /^e(\d+)$/.exec(key);
  return match ? Number(match[1]) : null;
};

/**
 * The newest event on screen: of the blocks shown, by their keys, the last event any of them holds (`where` names each event's
 * block), with its time. Null where no shown block holds an event the page has.
 */
export function newestShown(
  shown: readonly string[], where: Readonly<Record<number, string>>, timeOf: (seq: number) => string | undefined,
): { seq: number; at: string } | null {
  const keys = new Set(shown);
  let newest: number | null = null;
  for (const [seq, key] of Object.entries(where)) {
    if (keys.has(key) && (newest === null || Number(seq) > newest)) newest = Number(seq);
  }
  const at = newest === null ? undefined : timeOf(newest);
  return newest === null || !at ? null : { seq: newest, at };
}
