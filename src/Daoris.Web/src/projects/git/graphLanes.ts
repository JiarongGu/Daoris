/**
 * A commit as the graph lays it: its whole id and its parents' whole ids, the first parent first, as `GIT_GRAPH` answers
 * each one (`GitLogCommit`).
 */
export type GraphCommit = { commit: string; parents: readonly string[] };

/** A line from one row's centre to the next row's: the lane it leaves this row in, and the lane it reaches the next in. */
export type GraphLine = { from: number; to: number };

/** A commit's row in the graph. */
export type GraphRow = {
  commit: string;
  /** The lane its dot is in. */
  lane: number;
  /** The lane each parent is reached by, the first parent first. A parent the list does not hold runs off the bottom in it. */
  parents: number[];
  /**
   * The lines between this row and the next, each from a lane at this row's centre to a lane at the next row's, in order of
   * where they leave and then where they reach. The last row's run off the bottom, each in its own lane.
   */
  lines: GraphLine[];
};

/** A graph laid out: its rows in the list's order, and how many lanes the widest of them spans. */
export type GraphLayout = { lanes: number; rows: GraphRow[] };

/**
 * **A commit graph's lanes** (UX6h1, the built-in Git design §2.3): laid out from each commit's parents, a pure helper the
 * page draws from. They are never read from `git log --graph`'s drawing, which is parsing another program's screen (the
 * working surface design §3).
 *
 * @remarks
 * The commits come newest first, in git's date order, which shows no parent before all of its children. Each lane holds the
 * commit it leads down to:
 *
 * - **A commit takes the leftmost lane leading to it**, and every other lane leading to it joins it there: two branches
 *   meet at the commit they grew from, not before. One no lane leads to (a tip) takes the leftmost free lane.
 * - **Its first parent keeps its lane**, so a line of first parents runs straight. Each other parent of a merge is reached
 *   in the lane already leading to it, or in the leftmost free one.
 * - **A root frees its lane**, which the next branch to start takes. A parent the list does not hold keeps its lane to the
 *   bottom, where the next page picks it up: the layout takes the whole accumulated list (UX6h3 pages by `sinceParted`).
 * - **The tips keep the first lanes in the order they were asked** (`tips`), held empty until each is reached or a merge
 *   names it, so a branch's line asked first stays leftmost though the branch's commits are newer.
 */
export function graphLanes(commits: readonly GraphCommit[], tips: readonly string[] = []): GraphLayout {
  // What each lane leads down to, or null where it is free; a tip's lane is held for it, drawing nothing until it is reached.
  const slots: (string | null)[] = [...new Set(tips)];
  const held = new Set(slots.map((_, lane) => lane));
  const free = () => {
    const at = slots.indexOf(null);
    return at < 0 ? slots.length : at;
  };
  const leadingTo = (commit: string) => slots.flatMap((each, lane) => (each === commit ? [lane] : []));
  let lanes = 0;

  const rows = commits.map((row, at): GraphRow => {
    const into = leadingTo(row.commit);
    const lane = into[0] ?? free();
    for (const each of into) {
      slots[each] = null;
      held.delete(each);
    }

    // The lanes whose line starts at this row's dot, and the merge parents reached in a lane already leading to them.
    const started = new Set<number>();
    const joined: number[] = [];
    const parents = row.parents.map((parent, index) => {
      if (index === 0) {
        slots[lane] = parent;
        started.add(lane);
        return lane;
      }
      const already = slots.indexOf(parent);
      if (already >= 0) {
        // A tip's held lane is drawn from here down; any other already runs from above, and this dot's line joins it.
        if (held.delete(already)) started.add(already);
        else if (!started.has(already)) joined.push(already);
        return already;
      }
      const opened = free();
      slots[opened] = parent;
      started.add(opened);
      return opened;
    });

    // The next row's commit is reached in the leftmost lane leading to it, held or not.
    const next = commits[at + 1]?.commit;
    const nextLane = next === undefined ? -1 : slots.indexOf(next);
    const reach = (slot: number) => (next !== undefined && slots[slot] === next ? nextLane : slot);
    const lines: GraphLine[] = [];
    slots.forEach((each, slot) => {
      if (each !== null && !held.has(slot)) lines.push({ from: started.has(slot) ? lane : slot, to: reach(slot) });
    });
    for (const slot of joined) lines.push({ from: lane, to: reach(slot) });

    while (slots.length > 0 && slots[slots.length - 1] === null) slots.pop();
    lanes = Math.max(lanes, lane + 1, ...lines.map((line) => Math.max(line.from, line.to) + 1));
    return { commit: row.commit, lane, parents, lines: unique(lines) };
  });

  return { lanes, rows };
}

/** The lines in order of where they leave and then where they reach, each once. */
function unique(lines: GraphLine[]): GraphLine[] {
  const sorted = [...lines].sort((a, b) => a.from - b.from || a.to - b.to);
  return sorted.filter((line, at) => at === 0 || line.from !== sorted[at - 1]!.from || line.to !== sorted[at - 1]!.to);
}
