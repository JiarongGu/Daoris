import { describe, expect, it } from 'vitest';
import type { MapNode, Topology } from './topology';
import { CARD_H, layoutLayers, lineKey, ROUND } from './layers';

// MAP4: a circle too big for a ring, laid out in layers by who asks whom, with what nothing connects
// set apart below. Pure and deterministic, so two looks at the same data draw the same picture.

const node = (id: string): MapNode => ({ id, owns: [], accepts: [], open: 0, working: false, parked: false });

function topology(ids: string[], quests: [string, string][] = [], knowledge: [string, string][] = []): Topology {
  return {
    nodes: ids.map(node),
    quests: quests.map(([from, to]) => ({ from, to, quests: [], open: 1 })),
    knowledge: knowledge.map(([a, b]) => ({ a, b, groups: 1 })),
    outside: 0,
    circles: 1,
  };
}

describe('the layered map', () => {
  it('puts an asker to the left of whom it asks, one column a step', () => {
    const { at } = layoutLayers(topology(['report-ui', 'db', 'api'], [['report-ui', 'api'], ['api', 'db']]));
    expect(at['report-ui']!.x).toBeLessThan(at.api!.x);
    expect(at.api!.x).toBeLessThan(at.db!.x);
  });

  it('sets what nothing connects apart, below, in a grid read by name', () => {
    const ids = ['a', 'b', 'lone-3', 'lone-1', 'lone-2'];
    const { at, loose } = layoutLayers(topology(ids, [['a', 'b']]));
    const bottom = Math.max(at.a!.y, at.b!.y) + CARD_H / 2;
    for (const id of ['lone-1', 'lone-2', 'lone-3']) expect(at[id]!.y - CARD_H / 2).toBeGreaterThan(bottom);
    expect(loose).toEqual(['lone-1', 'lone-2', 'lone-3']);
    // Read left to right, by name.
    expect(at['lone-1']!.x).toBeLessThan(at['lone-2']!.x);
  });

  it('wraps the grid inside the width the viewport shows, each card its own width', () => {
    // Twenty-four long names, as the first real workspace had, and a map half a window wide beside its
    // detail: no row runs past what the viewport opens on.
    const ids = ['a', 'b', ...Array.from({ length: 24 }, (_, index) => `customer-facing-service-${index}`)];
    const { at, loose, frame } = layoutLayers(topology(ids, [['a', 'b']]), 600);
    const rows = new Set(loose.map((id) => at[id]!.y));
    expect(rows.size).toBeGreaterThan(2);
    for (const id of loose) expect(at[id]!.x + at[id]!.width / 2 - frame.x).toBeLessThanOrEqual(600);
    // A wider viewport, fewer rows.
    const wide = layoutLayers(topology(ids, [['a', 'b']]), 1400);
    expect(new Set(wide.loose.map((id) => wide.at[id]!.y)).size).toBeLessThan(rows.size);
    // A short name's card is narrower than a long one's: no equal cells sized to the longest.
    const { at: mixed } = layoutLayers(topology(['a', 'b', 'x', 'a-much-longer-repository'], [['a', 'b']]));
    expect(mixed.x!.width).toBeLessThan(mixed['a-much-longer-repository']!.width);
  });

  it('counts a shared finding as a connection, and keeps its pair together', () => {
    const { at, loose } = layoutLayers(topology(['a', 'b', 'c', 'd'], [['a', 'b']], [['b', 'c']]));
    expect(loose).toEqual(['d']);
    // Knowing only b, c stands in b's column rather than on its own.
    expect(at.c!.x).toBe(at.b!.x);
  });

  it('survives a loop of asks, drawing each node once', () => {
    const { at } = layoutLayers(topology(['a', 'b', 'c'], [['a', 'b'], ['b', 'c'], ['c', 'a']]));
    expect(Object.keys(at).sort()).toEqual(['a', 'b', 'c']);
    expect(new Set(Object.values(at).map((p) => `${p.x},${p.y}`)).size).toBe(3);
  });

  it('is the same picture for the same data, and sizes a card to its name', () => {
    const data = topology(['a-very-long-repository-name', 'b', 'c'], [['b', 'c']], [['a-very-long-repository-name', 'b']]);
    expect(layoutLayers(data)).toEqual(layoutLayers(data));
    const { at } = layoutLayers(data);
    expect(at['a-very-long-repository-name']!.width).toBeGreaterThan(at.b!.width);
  });

  it('frames everything it drew', () => {
    const { at, frame } = layoutLayers(topology(['a', 'b', 'c', 'lone'], [['a', 'b']], [['b', 'c']]));
    for (const card of Object.values(at)) {
      expect(card.x - card.width / 2).toBeGreaterThanOrEqual(frame.x);
      expect(card.x + card.width / 2).toBeLessThanOrEqual(frame.x + frame.width);
      expect(card.y + CARD_H / 2).toBeLessThanOrEqual(frame.y + frame.height);
    }
  });

  it('keeps room left of the columns for a shared finding within one, and only then', () => {
    // Two askers of one repository share a finding: one column, so the line bows out to the left.
    const pair = layoutLayers(topology(['a', 'b', 'c'], [['a', 'c'], ['b', 'c']], [['a', 'b']]));
    const left = Math.min(pair.at.a!.x - pair.at.a!.width / 2, pair.at.b!.x - pair.at.b!.width / 2);
    expect(pair.frame.x).toBeLessThanOrEqual(left - ROUND);
    // Nothing bows: no room kept.
    const plain = layoutLayers(topology(['a', 'b'], [['a', 'b']]));
    expect(plain.frame.x).toBeGreaterThan(-ROUND);
  });

  it('threads a line that skips a column between the cards it passes, never through one', () => {
    // A loop of three: its closing quest runs back from the third column to the first, past the second.
    const { at, routes } = layoutLayers(topology(['a', 'b', 'c', 'd'], [['a', 'b'], ['b', 'c'], ['c', 'a'], ['a', 'd'], ['d', 'c']]));
    const back = routes[lineKey.quests('c', 'a')]!;
    expect(back).toHaveLength(1);
    // In the middle column, and in a slot of its own there: clear of every card in that column.
    const [way] = back;
    expect(way!.x).toBe(at.b!.x);
    for (const id of ['b', 'd']) expect(Math.abs(way!.y - at[id]!.y)).toBeGreaterThanOrEqual(CARD_H / 2);
    // A line between neighbouring columns needs none.
    expect(routes[lineKey.quests('a', 'b')]).toBeUndefined();
  });

  it('threads a shared finding across columns the same way, in its own direction', () => {
    const { at, routes } = layoutLayers(topology(['a', 'b', 'c', 'x'], [['a', 'b'], ['b', 'c']], [['c', 'a']]));
    const ways = routes[lineKey.knowledge('c', 'a')]!;
    expect(ways).toHaveLength(1);
    expect(ways[0]!.x).toBe(at.b!.x);
  });
});
