// Find on a large canvas: every item is searched, folded or culled, by its title; the matches keep the
// scene's order, and stepping wraps. An item may also be found by what it declares: each such text carries the words that say
// so in the result.

/** From this many nodes the toolbar offers find. */
export const FIND_FROM = 40;

/** What an item is found by besides its title, and how a result says so. */
export type FoundBy = { text: string; said: string };
export type Findable = { id: string; title: string; also?: readonly FoundBy[] };

const fold = (text: string) => text.normalize('NFKC').toLocaleLowerCase();

/** What an item matched the query by: its title, else the first text it declares; null where neither holds it. */
export function foundBy(item: Findable, query: string): { by: 'title' } | { by: 'also'; said: string } | null {
  const needle = fold(query.trim());
  if (needle === '') return null;
  if (fold(item.title).includes(needle)) return { by: 'title' };
  const also = item.also?.find((each) => fold(each.text).includes(needle));
  return also ? { by: 'also', said: also.said } : null;
}

/** The ids whose title, or declared text, holds the query (a word-for-word, case-folded substring); none for an empty query. */
export function findMatches(items: readonly Findable[], query: string): string[] {
  const found: string[] = [];
  for (const item of items) if (foundBy(item, query) !== null) found.push(item.id);
  return found;
}

/** The index stepped to, wrapping; -1 steps from before the first. */
export const stepMatch = (at: number, count: number, back: boolean): number =>
  count === 0 ? -1 : back ? (at <= 0 ? count - 1 : at - 1) : (at + 1) % count;
