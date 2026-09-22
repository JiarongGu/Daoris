/** What one repository contributes to the index — the shape the Overview's chart reads. */
export interface IndexedRepository {
  name: string;
  total: number;
  local: number;
}

/**
 * The repositories worth a bar, and how many were left out.
 *
 * @remarks
 * 🔴 **Written from the deployed application, which is the only place it shows** (D62). The
 * fixture the dev loop runs against holds two repositories, so the card was a chart of two rows and
 * looked finished. A real machine has fourteen: the card ran past the fold, its legend — the
 * paragraph that explains what the dot and "local" mean — was cut off below the window, and the
 * card beside it ended a screen earlier.
 *
 * **Ranked and capped, never scrolled.** A scroll region inside a card on a page that also scrolls
 * is two scrollbars for one gesture. The point of this card is *which repositories hold the most*,
 * which the top of a ranking answers completely — and the ones below the cut are one press away in
 * Projects, which the card's own header already offers.
 *
 * The remainder is **counted, not hidden**: a list that silently stops is a list a person reads as
 * complete, and then "where is the rest of my family" is a question about a defect rather than
 * about a button.
 */
export function ranked(repositories: readonly IndexedRepository[], limit = 8): {
  shown: IndexedRepository[];
  hidden: number;
} {
  const byTotal = [...repositories].sort((a, b) => b.total - a.total);
  return {
    shown: byTotal.slice(0, Math.max(0, limit)),
    hidden: Math.max(0, byTotal.length - Math.max(0, limit)),
  };
}

/**
 * The widest bar's value, so every other bar is drawn relative to it.
 *
 * 🔴 Taken from what is SHOWN, not from everything. Scaling a capped list against a total that is
 * off the list draws a chart whose longest bar is not full — which reads as "nothing here is
 * significant" when the truth is "the significant one is on this list, at the top".
 */
export function widest(repositories: readonly IndexedRepository[]): number {
  return Math.max(1, ...repositories.map((repository) => repository.total));
}
