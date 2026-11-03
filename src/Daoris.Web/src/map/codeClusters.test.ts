import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { Box } from '../design/canvas/viewMath';
import {
  buildTree, CLOSED_PAST, defaultOpen, layoutCode, listByFolder, openingPath,
} from './codeClusters';
import { type CodeFixture, syntheticMap, WITH_A_CYCLE } from './codeFixtures';

// CANVAS1f (D224; the canvas design §3.4, §9 row f): the code map's pure layout — folders from each module's path, packed nested boxes,
// aggregated folder-pair edges with exact counts, a cycle condensed, folders closed past 60 and the path find opens.

const repository = (): CodeFixture => JSON.parse(readFileSync('../../docs/code-map.json', 'utf8')) as CodeFixture;
const overlap = (a: Box, b: Box) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
const noneOverlap = (boxes: Iterable<Box>) => {
  const all = [...boxes];
  for (let i = 0; i < all.length; i += 1) for (let j = i + 1; j < all.length; j += 1) if (overlap(all[i]!, all[j]!)) return false;
  return true;
};
const mod = (id: string, path: string) => ({ id, path, summary: '' });

describe('folders', () => {
  it('come from each module path, a lone chain of folders joined into one', () => {
    const tree = buildTree(repository().modules, repository().dependencies);
    const names = [...tree.groups.values()].filter((group) => group.kind === 'folder').map((group) => group.name);
    expect(names).toContain('src');
    expect(names).toContain('Daoris.Desktop');
    expect(tree.owner.get('Daoris.Desktop.Driver')).toBe('folder:src/Daoris.Desktop');
    const joined = buildTree([mod('a', 'one/two/three/a'), mod('b', 'one/two/three/b')], []);
    expect([...joined.groups.values()].map((group) => group.name)).toEqual(['']);
    expect(joined.owner.get('a')).toBe('folder:');
    const split = buildTree([mod('a', 'one/two/a'), mod('b', 'one/three/b'), mod('c', 'one/three/four/c')], []);
    expect([...split.groups.values()].map((group) => group.name).sort()).toEqual(['', 'four', 'three', 'two']);
  });

  it('are no evidence of a dependency: two modules of one folder with none are joined by no line', () => {
    const scene = layoutCode(buildTree([mod('a', 'x/a'), mod('b', 'x/b')], []), defaultOpen(buildTree([mod('a', 'x/a'), mod('b', 'x/b')], [])));
    expect(scene.lines).toHaveLength(0);
  });

  it('are packed by name, none over another, opened whole', () => {
    const { modules, dependencies } = syntheticMap(60);
    const tree = buildTree(modules, dependencies);
    const scene = layoutCode(tree, defaultOpen(tree));
    expect([...scene.world.boxes.keys()].sort()).toEqual(modules.map((m) => m.id).sort());
    expect(noneOverlap(scene.world.boxes.values())).toBe(true);
    // A folder's frame holds every card of it.
    for (const group of scene.world.groups ?? []) {
      const members = tree.groups.get(group.id)!.members;
      for (const id of members) {
        const card = scene.world.boxes.get(id)!;
        expect(card.x).toBeGreaterThanOrEqual(group.box.x);
        expect(card.x + card.w).toBeLessThanOrEqual(group.box.x + group.box.w);
        expect(card.y + card.h).toBeLessThanOrEqual(group.box.y + group.box.h);
      }
    }
  });

  it('are closed past 60 modules and open at 60', () => {
    const sixty = syntheticMap(CLOSED_PAST);
    const treeAt = buildTree(sixty.modules, sixty.dependencies);
    expect(defaultOpen(treeAt).size).toBeGreaterThan(0);
    const more = syntheticMap(CLOSED_PAST + 1);
    const tree = buildTree(more.modules, more.dependencies);
    expect(defaultOpen(tree).size).toBe(0);
    const scene = layoutCode(tree, defaultOpen(tree));
    expect([...scene.world.boxes.keys()].every((id) => id.startsWith('folder:'))).toBe(true);
    expect(scene.world.groups ?? []).toHaveLength(0);
  });
});

describe('edges', () => {
  const small = (): CodeFixture => ({
    modules: [mod('a1', 'src/a/a1'), mod('a2', 'src/a/a2'), mod('b1', 'src/b/b1'), mod('b2', 'src/b/b2'), mod('b3', 'src/b/b3')],
    dependencies: [
      { from: 'a1', to: 'b1', kind: 'project' }, { from: 'a1', to: 'b2', kind: 'http' }, { from: 'a2', to: 'b3', kind: 'project' },
      { from: 'b1', to: 'a1', kind: 'callback' }, { from: 'a1', to: 'a2', kind: 'project' },
      { from: 'a1', to: 'ghost', kind: 'project' },
    ],
  });

  it('are only the supplied dependencies, each with its kind as written, and none to a module the map does not hold', () => {
    const tree = buildTree(small().modules, small().dependencies);
    const scene = layoutCode(tree, new Set(tree.groups.keys()));
    expect(scene.lines).toHaveLength(5);
    expect(scene.lines.map((line) => line.kinds.join()).sort()).toEqual(['callback', 'http', 'project', 'project', 'project']);
    expect(scene.lines.every((line) => !line.aggregated && line.count === 1)).toBe(true);
  });

  it('between closed folders are aggregated by pair and direction and counted exactly', () => {
    const tree = buildTree(small().modules, small().dependencies);
    const scene = layoutCode(tree, new Set());
    const across = scene.lines.find((line) => line.from === 'folder:src/a' && line.to === 'folder:src/b')!;
    expect(across.aggregated).toBe(true);
    expect(across.count).toBe(3);
    expect(across.kinds.sort()).toEqual(['http', 'project', 'project']);
    expect(scene.lines.find((line) => line.from === 'folder:src/b' && line.to === 'folder:src/a')!.count).toBe(1);
    // What lies inside one closed folder is no line, and is no loss: every dependency is a line's member or inside a folder.
    const inside = 1;
    expect(scene.lines.reduce((sum, line) => sum + line.count, 0) + inside).toBe(5);
  });

  it('open one folder and its modules each stand for their own, to the other still closed', () => {
    const tree = buildTree(small().modules, small().dependencies);
    const scene = layoutCode(tree, new Set(['folder:src/a']));
    const counts = scene.lines.filter((line) => line.to === 'folder:src/b').map((line) => `${line.from}:${line.count}`).sort();
    expect(counts).toEqual(['a1:2', 'a2:1']);
    expect(scene.standsFor.get('b2')).toBe('folder:src/b');
  });
});

describe('a cycle', () => {
  it('is condensed to one labelled card and whole when opened', () => {
    const tree = buildTree(WITH_A_CYCLE.modules, WITH_A_CYCLE.dependencies);
    const cycle = [...tree.groups.values()].find((group) => group.kind === 'cycle')!;
    expect(cycle.members.sort()).toEqual(['engine', 'planner', 'runner']);
    const closed = layoutCode(tree, new Set(['folder:src/core']));
    expect(closed.world.boxes.has(cycle.id)).toBe(true);
    expect(closed.world.boxes.has('engine')).toBe(false);
    // The ring's three edges are inside the card; the two that leave it stay.
    expect(closed.lines.map((line) => `${line.from}>${line.to}`).sort()).toEqual([`${cycle.id}>folder:src/data`, `folder:src/web>${cycle.id}`].sort());
    const opened = layoutCode(tree, new Set(['folder:src/core', cycle.id]));
    expect(opened.world.boxes.has('engine')).toBe(true);
    expect(opened.lines.filter((line) => line.kinds.includes('callback'))).toHaveLength(1);
    expect(opened.world.groups!.some((group) => group.id === cycle.id)).toBe(true);
  });

  it('spanning folders is left as the edges say, nothing hidden', () => {
    const tree = buildTree([mod('p', 'x/p'), mod('q', 'y/q')], [{ from: 'p', to: 'q', kind: 'k' }, { from: 'q', to: 'p', kind: 'k' }]);
    expect([...tree.groups.values()].some((group) => group.kind === 'cycle')).toBe(false);
    expect(layoutCode(tree, new Set(tree.groups.keys())).lines).toHaveLength(2);
  });
});

describe('find', () => {
  it('opens the path to a match inside closed folders, and the module is then a card', () => {
    const { modules, dependencies } = syntheticMap(500);
    const tree = buildTree(modules, dependencies);
    const closed = defaultOpen(tree);
    const target = 'module-0417';
    expect(layoutCode(tree, closed).world.boxes.has(target)).toBe(false);
    const open = openingPath(tree, closed, target);
    expect(open.size).toBeGreaterThan(0);
    const scene = layoutCode(tree, open);
    expect(scene.world.boxes.has(target)).toBe(true);
    // Only the path is opened, not the repository.
    expect(scene.world.boxes.size).toBeLessThan(modules.length);
    expect(scene.standsFor.get(target)).toBe(target);
  });

  it('lists every module under its folder, closed or open', () => {
    const { modules, dependencies } = syntheticMap(500);
    const groups = listByFolder(buildTree(modules, dependencies));
    expect(groups.reduce((sum, group) => sum + group.items.length, 0)).toBe(500);
    expect(groups.map((group) => group.title)).toEqual([...groups.map((group) => group.title)].sort());
  });
});

describe('at scale', () => {
  it('lays out 5,000 closed in well under a second with every dependency counted once', () => {
    const { modules, dependencies } = syntheticMap(5000);
    const started = performance.now();
    const tree = buildTree(modules, dependencies);
    const scene = layoutCode(tree, defaultOpen(tree));
    expect(performance.now() - started).toBeLessThan(750);
    expect(noneOverlap(scene.world.boxes.values())).toBe(true);
    const inside = dependencies.filter((d) => tree.owner.get(d.from) === tree.owner.get(d.to) ||
      scene.standsFor.get(d.from) === scene.standsFor.get(d.to)).length;
    expect(scene.lines.reduce((sum, line) => sum + line.count, 0) + inside).toBe(dependencies.length);
  });

  it('lays out this repository whole, every module a card', () => {
    const { modules, dependencies } = repository();
    const tree = buildTree(modules, dependencies);
    const scene = layoutCode(tree, defaultOpen(tree));
    expect(scene.world.boxes.size).toBe(20);
    expect(scene.lines).toHaveLength(20);
    expect(noneOverlap(scene.world.boxes.values())).toBe(true);
  });
});
