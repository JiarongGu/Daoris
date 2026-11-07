import { describe, expect, it } from 'vitest';
import { foldGroups, type GroupShape, groupFolds } from './groupFolds';

// A drawing of places is drawn whole up to 24 nodes; past that each
// group of places (a workspace and its repositories) folds to one node, every edge across its boundary drawn to it, unless the person
// opened it. Pure: the folds are a function of the nodes' groups, the edges and which folds were opened.

/** Places in groups, roles beside them, and an edge from each place to a role. */
function places(groups: Record<string, number>, roles = 4): GroupShape {
  const nodes: GroupShape['nodes'][number][] = [{ id: 'place:machine' }];
  for (const [group, count] of Object.entries(groups)) {
    for (let n = 0; n < count; n += 1) nodes.push({ id: `place:${group}/r${n}`, group });
  }
  for (let n = 0; n < roles; n += 1) nodes.push({ id: `role:${n}` });
  const edges = nodes.filter((node) => node.id.startsWith('place:')).map((node, index) => ({
    id: `placed:${node.id}`, from: node.id, to: `role:${index % roles}`,
  }));
  return { nodes, edges };
}

const foldId = (group: string) => `place:${group}`;

describe('a drawing of places too large to show whole', () => {
  it('draws 24 nodes whole, and folds nothing', () => {
    const shape = places({ aurora: 10, borealis: 9 });
    expect(shape.nodes).toHaveLength(24);
    expect(groupFolds(shape, new Set(), foldId)).toEqual([]);
  });

  it('folds each workspace past 24, its members in declared order, and a workspace of one place not at all', () => {
    const shape = places({ aurora: 12, borealis: 9, lone: 1 });
    const folds = groupFolds(shape, new Set(), foldId);
    expect(folds.map((fold) => [fold.id, fold.group, fold.members.length])).toEqual([['place:aurora', 'aurora', 12], ['place:borealis', 'borealis', 9]]);
    expect(folds[0]!.members[0]).toBe('place:aurora/r0');
  });

  it('keeps a fold the person opened open, by its id', () => {
    const shape = places({ aurora: 12, borealis: 9 });
    expect(groupFolds(shape, new Set(['place:aurora']), foldId).map((fold) => fold.id)).toEqual(['place:borealis']);
  });

  it('draws each fold in its first member\'s place, every edge across its boundary drawn to it, those between its members said', () => {
    const shape: GroupShape = {
      nodes: [...places({ aurora: 12, borealis: 9 }).nodes, { id: 'place:aurora', group: 'aurora' }],
      edges: [...places({ aurora: 12, borealis: 9 }).edges, { id: 'inside', from: 'place:aurora', to: 'place:aurora/r1' }],
    };
    const folds = groupFolds(shape, new Set(), foldId);
    expect(folds[0]!.within).toEqual(['inside']);
    const folded = foldGroups(shape, folds);
    expect(folded.nodes.map((node) => node.id)).toEqual(['place:machine', 'place:aurora', 'place:borealis', 'role:0', 'role:1', 'role:2', 'role:3']);
    expect(folded.nodes.find((node) => node.id === 'place:aurora')?.fold?.members).toHaveLength(13);
    // Twelve of aurora's edges go to four roles: each keeps its id and is drawn from the fold, so a layout merges those of one end.
    const fromAurora = folded.edges.filter((edge) => edge.from === 'place:aurora');
    expect(fromAurora).toHaveLength(12);
    expect(new Set(fromAurora.map((edge) => edge.to))).toEqual(new Set(['role:0', 'role:1', 'role:2', 'role:3']));
    expect(folded.edges.some((edge) => edge.id === 'inside')).toBe(false);
    expect(folded.foldOf.get('place:aurora/r3')).toBe('place:aurora');
    expect(folded.foldOf.has('role:0')).toBe(false);
  });

  it('changes nothing with no folds', () => {
    const shape = places({ aurora: 2 });
    const folded = foldGroups(shape, []);
    expect(folded.nodes.map((node) => node.id)).toEqual(shape.nodes.map((node) => node.id));
    expect(folded.edges).toEqual(shape.edges);
  });
});
