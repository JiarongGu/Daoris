import { describe, expect, it } from 'vitest';
import { buildTree, defaultOpen, layoutCode, ROOT } from './codeClusters';
import { CYCLE_ACROSS_FOLDERS } from './codeFixtures';
import { foldGroup } from './codeFolds';

// One folder folded alone, its siblings and the folders above it left open.

const mod = (id: string, path: string) => ({ id, path, summary: '' });
const tree = buildTree(
  [mod('a1', 'src/a/a1'), mod('a2', 'src/a/deep/a2'), mod('b1', 'src/b/b1'), mod('top', 'top')],
  [{ from: 'a1', to: 'b1', kind: 'k' }, { from: 'a2', to: 'a1', kind: 'k' }],
);

describe('one folder folded alone', () => {
  it('closes the folder and every one beneath it, and nothing beside or above it', () => {
    const open = defaultOpen(tree);
    expect(open.has('folder:src/a/deep')).toBe(true);
    const next = foldGroup(tree, open, 'folder:src/a');
    expect(next.has('folder:src/a')).toBe(false);
    expect(next.has('folder:src/a/deep')).toBe(false);
    expect(next.has('folder:src/b')).toBe(true);
    expect(next.has('folder:src')).toBe(open.has('folder:src'));
    const scene = layoutCode(tree, next);
    expect(scene.world.boxes.has('folder:src/a')).toBe(true);
    expect(scene.world.boxes.has('b1')).toBe(true);
    // What crossed into it is counted to its card.
    expect(scene.lines.find((line) => line.from === 'folder:src/a' && line.to === 'b1')!.count).toBe(1);
  });

  it('folds a cycle across folders to its one card', () => {
    const cycles = buildTree(CYCLE_ACROSS_FOLDERS.modules, CYCLE_ACROSS_FOLDERS.dependencies);
    const open = defaultOpen(cycles);
    const cycle = cycles.owner.get('session')!;
    expect(cycle).not.toBe(ROOT);
    expect(cycles.groups.get(cycle)!.kind).toBe('cycle');
    const scene = layoutCode(cycles, foldGroup(cycles, open, cycle));
    expect(scene.world.boxes.has(cycle)).toBe(true);
    expect(scene.world.boxes.has('session')).toBe(false);
    expect(scene.world.boxes.has('rules')).toBe(true);
  });
});
