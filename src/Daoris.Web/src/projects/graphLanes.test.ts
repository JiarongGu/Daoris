import { describe, expect, it } from 'vitest';
import { graphLanes } from './graphLanes';

// A commit graph's lanes (UX6h1, the built-in Git design §2.3): laid by the page from each commit's parents, never read from
// `git log --graph`'s drawing. The commits come as `GIT_GRAPH` answers them, newest first in date order, each with its
// parents' whole ids, the first parent first. A row's lines run from its centre to the next row's.

const commit = (id: string, ...parents: string[]) => ({ commit: id, parents });

describe("a commit graph's lanes", () => {
  it('lays a line of commits in one lane', () => {
    expect(graphLanes([commit('c', 'b'), commit('b', 'a'), commit('a')])).toEqual({
      lanes: 1,
      rows: [
        { commit: 'c', lane: 0, parents: [0], lines: [{ from: 0, to: 0 }] },
        { commit: 'b', lane: 0, parents: [0], lines: [{ from: 0, to: 0 }] },
        { commit: 'a', lane: 0, parents: [], lines: [] },
      ],
    });
  });

  it('keeps the first parent in its lane, gives a merged branch its own, and joins them at the commit they grew from', () => {
    const { lanes, rows } = graphLanes([
      commit('merge', 'line2', 'topic'), commit('topic', 'line1'), commit('line2', 'line1'), commit('line1'),
    ]);
    expect(lanes).toBe(2);
    expect(rows).toEqual([
      { commit: 'merge', lane: 0, parents: [0, 1], lines: [{ from: 0, to: 0 }, { from: 0, to: 1 }] },
      { commit: 'topic', lane: 1, parents: [1], lines: [{ from: 0, to: 0 }, { from: 1, to: 1 }] },
      { commit: 'line2', lane: 0, parents: [0], lines: [{ from: 0, to: 0 }, { from: 1, to: 0 }] },
      { commit: 'line1', lane: 0, parents: [], lines: [] },
    ]);
  });

  it("reaches a merge's second parent in the lane already leading to it", () => {
    const { lanes, rows } = graphLanes([commit('tip', 'base'), commit('merge', 'side', 'base'), commit('side', 'base'), commit('base')]);
    expect(lanes).toBe(2);
    expect(rows[1]).toEqual({ commit: 'merge', lane: 1, parents: [1, 0], lines: [{ from: 0, to: 0 }, { from: 1, to: 0 }, { from: 1, to: 1 }] });
    expect(rows[2]).toEqual({ commit: 'side', lane: 1, parents: [1], lines: [{ from: 0, to: 0 }, { from: 1, to: 0 }] });
    expect(rows[3]).toEqual({ commit: 'base', lane: 0, parents: [], lines: [] });
  });

  it('ends each of two roots in its own lane', () => {
    const { lanes, rows } = graphLanes([commit('joined', 'one', 'two'), commit('one'), commit('two')]);
    expect(lanes).toBe(2);
    expect(rows).toEqual([
      { commit: 'joined', lane: 0, parents: [0, 1], lines: [{ from: 0, to: 0 }, { from: 0, to: 1 }] },
      { commit: 'one', lane: 0, parents: [], lines: [{ from: 1, to: 1 }] },
      { commit: 'two', lane: 1, parents: [], lines: [] },
    ]);
  });

  it('runs a parent the list does not hold off the bottom, in its lane', () => {
    const { rows } = graphLanes([commit('b', 'a'), commit('a', 'older')]);
    expect(rows.at(-1)).toEqual({ commit: 'a', lane: 0, parents: [0], lines: [{ from: 0, to: 0 }] });
  });

  it('gives each of many branches a lane, and joins them all at the commit they share', () => {
    const tips = ['t1', 't2', 't3', 't4', 't5', 't6', 't7', 't8'];
    const { lanes, rows } = graphLanes([...tips.map((tip) => commit(tip, 'base')), commit('base', 'root'), commit('root')]);
    expect(lanes).toBe(8);
    expect(rows.slice(0, 8).map((row) => row.lane)).toEqual([0, 1, 2, 3, 4, 5, 6, 7]);
    expect(rows[7]!.lines).toEqual(tips.map((_, lane) => ({ from: lane, to: 0 })));
    expect(rows[8]).toEqual({ commit: 'base', lane: 0, parents: [0], lines: [{ from: 0, to: 0 }] });
  });

  it('takes a lane a branch left free for the next branch that starts', () => {
    const { rows } = graphLanes([commit('a', 'x'), commit('b', 'y'), commit('y'), commit('c', 'x'), commit('x')]);
    expect(rows.map((row) => row.lane)).toEqual([0, 1, 1, 1, 0]);
  });

  it('keeps the first lanes for the tips in the order they were asked, though a later tip is newer', () => {
    // A branch beside its line: the line asked first keeps the first lane, the branch beside it, joining at their parting.
    const { lanes, rows } = graphLanes([commit('branch', 'line'), commit('line', 'parted'), commit('parted')], ['line', 'branch']);
    expect(lanes).toBe(2);
    expect(rows).toEqual([
      { commit: 'branch', lane: 1, parents: [1], lines: [{ from: 1, to: 0 }] },
      { commit: 'line', lane: 0, parents: [0], lines: [{ from: 0, to: 0 }] },
      { commit: 'parted', lane: 0, parents: [], lines: [] },
    ]);
  });

  it("reaches a tip a merge names in the tip's own lane, drawn from the merge down", () => {
    const { lanes, rows } = graphLanes(
      [commit('branch', 'work', 'line'), commit('work', 'parted'), commit('line', 'parted'), commit('parted')], ['line', 'branch'],
    );
    expect(lanes).toBe(2);
    expect(rows[0]).toEqual({ commit: 'branch', lane: 1, parents: [1, 0], lines: [{ from: 1, to: 0 }, { from: 1, to: 1 }] });
    expect(rows[1]).toEqual({ commit: 'work', lane: 1, parents: [1], lines: [{ from: 0, to: 0 }, { from: 1, to: 1 }] });
    expect(rows[2]).toEqual({ commit: 'line', lane: 0, parents: [0], lines: [{ from: 0, to: 0 }, { from: 1, to: 0 }] });
  });

  it('lays an accumulated list whole, so a later page continues the lanes the first left running', () => {
    const first = [commit('d', 'c', 'side'), commit('side', 'a'), commit('c', 'b')];
    const second = [commit('b', 'a'), commit('a')];
    const whole = graphLanes([...first, ...second]);
    // The first page's last row runs its two lanes off its bottom; the second page's rows pick them up.
    expect(whole.rows.slice(0, 3)).toEqual(graphLanes(first).rows);
    expect(whole.rows[2]!.lines).toEqual([{ from: 0, to: 0 }, { from: 1, to: 1 }]);
    expect(whole.rows[3]).toEqual({ commit: 'b', lane: 0, parents: [0], lines: [{ from: 0, to: 0 }, { from: 1, to: 0 }] });
  });

  it('lays nothing for no commits', () => {
    expect(graphLanes([])).toEqual({ lanes: 0, rows: [] });
  });
});
