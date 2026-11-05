import { describe, expect, it } from 'vitest';
import type { MapLine } from '../mapLayout';
import { mapListGroups } from './mapList';

// CANVAS1e (the canvas design §2.6): the workspace map as a list, the whole reading, grouped as the drawing is read.

const line = (id: string, kind: MapLine['kind']): MapLine => ({ id, kind, from: 'a', to: 'b', d: '', mid: { x: 0, y: 0 } }) as unknown as MapLine;
const words = { repositories: 'Repositories', kind: (kind: string) => `kind ${kind}`, node: (id: string) => `card ${id}`, line: (each: MapLine) => `line ${each.id}` };

describe('the map as a list', () => {
  it('reads the repositories in the map\'s order, then the lines by kind, the work before the declarations', () => {
    const groups = mapListGroups(['b', 'a'], [line('k1', 'knowledge'), line('q1', 'quests'), line('d1', 'depends'), line('q2', 'quests')], words);
    expect(groups.map((group) => group.title)).toEqual(['Repositories', 'kind quests', 'kind depends', 'kind knowledge']);
    expect(groups[0]!.items).toEqual([{ id: 'b', title: 'card b' }, { id: 'a', title: 'card a' }]);
    expect(groups[1]!.items.map((item) => item.id)).toEqual(['q1', 'q2']);
  });

  it('says no kind the map draws none of, and keeps every line once', () => {
    const groups = mapListGroups(['a'], [line('c1', 'chains')], words);
    expect(groups.map((group) => group.id)).toEqual(['repositories', 'chains']);
    expect(groups.flatMap((group) => group.items)).toHaveLength(2);
  });
});
