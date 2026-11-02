// Find on a large canvas (CANVAS1d; the canvas design §4): every item is searched, folded or culled, by its title; the matches keep the
// scene's order, and stepping wraps.

/** From this many nodes the toolbar offers find. */
export const FIND_FROM = 40;

const fold = (text: string) => text.normalize('NFKC').toLocaleLowerCase();

/** The ids whose title holds the query (a word-for-word, case-folded substring); none for an empty query. */
export function findMatches(items: readonly { id: string; title: string }[], query: string): string[] {
  const needle = fold(query.trim());
  if (needle === '') return [];
  const found: string[] = [];
  for (const item of items) if (fold(item.title).includes(needle)) found.push(item.id);
  return found;
}

/** The index stepped to, wrapping; -1 steps from before the first. */
export const stepMatch = (at: number, count: number, back: boolean): number =>
  count === 0 ? -1 : back ? (at <= 0 ? count - 1 : at - 1) : (at + 1) % count;
