/**
 * How many rows a capped list shows — search hits and convergence groups alike. The page asks the
 * service for one more, to learn whether there are more.
 */
export const PAGE_SHOWN = 40;

/**
 * A page of results, and whether the service had more to give.
 *
 * @remarks
 * 🔴 **On the deployed application** the index holds hundreds of entries, not the
 * fixture's handful. A broad query that returned exactly forty results and rendered forty, with
 * nothing anywhere saying that is a limit, would answer "what has the family already learned about this"
 * with a silently truncated list, which is the one answer a search must never give
 * quietly.
 *
 * **Asking for one more is what turns a guess into a fact.** The alternative — noticing that the
 * count equals the limit — is a heuristic that is wrong exactly when a query legitimately matches
 * forty things, and it is wrong in the direction that cries wolf. One extra row costs nothing and
 * the answer is then certain.
 *
 * 🔴 **Deliberately NOT the same helper as the Overview's `ranked`**, which reports how many it
 * hid. That view holds the whole population and can count what it left out; this one cannot — the
 * service capped it, so the honest statement is "there are more", never a number. Two views, two
 * true sentences, and collapsing them would make one of them a lie.
 */
export function page<T>(received: readonly T[], shown: number = PAGE_SHOWN): {
  shown: T[];
  more: boolean;
} {
  const limit = Math.max(0, shown);
  return { shown: received.slice(0, limit), more: received.length > limit };
}
