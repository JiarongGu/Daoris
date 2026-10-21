import { describe, expect, it } from 'vitest';
import {
  DENSE, type Fixture, LARGE_ONE, LARGE_SETTLED, LARGE_STOPPED, LARGE_THREE, LARGE_UNSTARTED, MANY_CURRENT,
} from './flowFixtures';
import { type Fold, type FoldShape, largeFolds, WHOLE_MOST } from './flowFolds';

// FLOWCHART1d (D186 point 7; the flowchart design §3.6 and trap 16): a flow is drawn whole up to 24 nodes. Past that, settled
// stretches fold, then the flow is focused on the ranks around every current node, or with none current where nothing has
// started, before the end where all has settled, or where it got to; *earlier*, *between* and *later* folds hold the rest. Pure:
// the folds are a function of the graph's shape, what is current and settled, and which folds the person opened.

const shapeOf = (fixture: Fixture): FoldShape => ({
  nodes: fixture.nodes.map(({ id, settled, current, reached }) => ({ id, settled, current, reached })),
  edges: fixture.edges,
  entries: fixture.entries,
});

/** The nodes drawn once the folds are applied: each fold in its first member's place, the rest as they are. */
const drawn = (fixture: Fixture, folds: Fold[]) => {
  const foldOf = new Map(folds.flatMap((fold) => fold.members.map((member) => [member, fold.id] as const)));
  return [...new Set(fixture.nodes.map((node) => foldOf.get(node.id) ?? node.id))];
};
const summary = (folds: Fold[]) => folds.map((fold) => [fold.id, fold.kind, fold.members.length]);
const current = (fixture: Fixture) => fixture.nodes.filter((node) => node.current).map((node) => node.id);

describe('a flow too large to show whole (§3.6, trap 16)', () => {
  it('holds its fixtures at the bound: 96 nodes, three lines of work of 31 steps', () => {
    for (const fixture of [LARGE_ONE, LARGE_THREE, LARGE_SETTLED, LARGE_UNSTARTED, LARGE_STOPPED]) expect(fixture.nodes).toHaveLength(96);
    expect(WHOLE_MOST).toBe(24);
  });

  it('draws a flow of 24 nodes or fewer whole, settled or not', () => {
    expect(largeFolds(shapeOf(MANY_CURRENT))).toEqual([]);
    const chain: FoldShape = {
      nodes: Array.from({ length: 24 }, (_, index) => ({ id: `n${index}`, settled: true, reached: true })),
      edges: Array.from({ length: 23 }, (_, index) => ({ id: `e${index}`, from: `n${index}`, to: `n${index + 1}` })),
      entries: ['n0'],
    };
    expect(largeFolds(chain)).toEqual([]);
  });

  it('folds around one current node: the settled stretches ending two ranks before it, then the ranks from two before to three after it', () => {
    const folds = largeFolds(shapeOf(LARGE_ONE));
    expect(summary(folds)).toEqual([
      ['fold:done:a1', 'done', 18], ['fold:done:b1', 'done', 18], ['fold:done:c1', 'done', 18], ['fold:later', 'later', 26],
    ]);
    const shown = drawn(LARGE_ONE, folds);
    expect(shown).toContain('a20');
    // Two ranks before it and three after, in every line of work: the start stays, since a fold of one node hides nothing.
    expect(shown.filter((id) => /^a\d/.test(id))).toEqual(['a19', 'a20', 'a21', 'a22', 'a23']);
    expect(shown).toHaveLength(20);
  });

  it('folds around every current node when several are, their union, with a fold between where they part', () => {
    const folds = largeFolds(shapeOf(LARGE_THREE));
    expect(summary(folds)).toEqual([
      ['fold:done:a1', 'done', 7], ['fold:done:b1', 'done', 7], ['fold:done:c1', 'done', 7],
      ['fold:between:a13', 'between', 15], ['fold:between:a24', 'between', 3], ['fold:later', 'later', 5],
    ]);
    const shown = drawn(LARGE_THREE, folds);
    for (const id of current(LARGE_THREE)) expect(shown).toContain(id);
  });

  it('with none current and all settled, focuses before the end, and folds only what settled two ranks before it', () => {
    const folds = largeFolds(shapeOf(LARGE_SETTLED));
    expect(summary(folds)).toEqual([['fold:done:a1', 'done', 28], ['fold:done:b1', 'done', 28], ['fold:done:c1', 'done', 28]]);
    expect(drawn(LARGE_SETTLED, folds)).toEqual(['start', 'fold:done:a1', 'a29', 'a30', 'a31', 'fold:done:b1', 'b29', 'b30', 'b31',
      'fold:done:c1', 'c29', 'c30', 'c31', 'join', 'land']);
  });

  it('with none current and nothing started, focuses after the entries', () => {
    const folds = largeFolds(shapeOf(LARGE_UNSTARTED));
    expect(summary(folds)).toEqual([['fold:later', 'later', 80]]);
    expect(drawn(LARGE_UNSTARTED, folds).filter((id) => /^a\d/.test(id))).toEqual(['a1', 'a2', 'a3', 'a4', 'a5']);
  });

  it('with none current, stopped part way, focuses where it got to', () => {
    const folds = largeFolds(shapeOf(LARGE_STOPPED));
    expect(folds[0]).toMatchObject({ id: 'fold:done:a1', kind: 'done' });
    const shown = drawn(LARGE_STOPPED, folds);
    expect(shown).toContain('a31');
    expect(shown).toContain('fold:later');
  });

  it('folds an unlit graph past the bound where it starts: nothing has started', () => {
    const folds = largeFolds(shapeOf(DENSE));
    expect(folds.map((fold) => fold.id)).toEqual(['fold:later']);
    expect(drawn(DENSE, folds)).toEqual(['h1', 'h2', 'w1', 'a1', 'a2', 'a3', 'jA', 'w2', 'fold:later']);
  });

  it('names the edges between a fold\'s members apart from those across its boundary (a fold keeps its boundary)', () => {
    const later = largeFolds(shapeOf(LARGE_ONE)).find((fold) => fold.id === 'fold:later')!;
    const members = new Set(later.members);
    const inside = LARGE_ONE.edges.filter((edge) => members.has(edge.from) && members.has(edge.to)).map((edge) => edge.id);
    expect(later.within).toEqual(inside);
    expect(later.within).not.toContain('a23>a24');
  });

  it('leaves a fold the person opened open, by its id, while the rest stay folded', () => {
    const folds = largeFolds(shapeOf(LARGE_ONE), new Set(['fold:later', 'fold:done:b1']));
    expect(folds.map((fold) => fold.id)).toEqual(['fold:done:a1', 'fold:done:c1']);
  });

  it('reads only what it is given: the same graph and state fold the same way twice', () => {
    expect(largeFolds(shapeOf(LARGE_THREE))).toEqual(largeFolds(shapeOf(LARGE_THREE)));
  });
});
