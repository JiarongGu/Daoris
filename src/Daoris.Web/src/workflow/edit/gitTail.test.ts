import { describe, expect, it } from 'vitest';
import { GIT_PALETTE } from './editFixtures';
import { gitTail } from './gitTail';
import { choiceOf, draftOf, type GraphDraft, insertStep } from './graphDraft';

// A step of the Git group comes with what its way out needs, in the change that adds it (the process-steps design §3): a rebase with its
// work from the run's entry, `ready` going on, and its resolution's tail; a merge with its commit from the nearest producer before it,
// `merged` and `held` going on, and `behind` back to the rebase its commit comes from.

const WORK = {
  schemaVersion: 2, entries: ['work'], end: 'line',
  nodes: [{ id: 'work', kind: 'claimed' }, { id: 'holds', kind: 'quest.holds' }, { id: 'line', kind: 'line' }],
  wires: [{ id: 'a', kind: 'after', from: 'work', to: 'holds' }, { id: 'b', kind: 'after', from: 'holds', to: 'line' }],
};

const add = (draft: GraphDraft, kind: string, on: string) => {
  const made = insertStep(GIT_PALETTE, draft, choiceOf(GIT_PALETTE, kind)!, { on });
  return { ...gitTail(GIT_PALETTE, made.draft, made.id), id: made.id };
};

const ways = (draft: GraphDraft) => draft.wires.map((wire) =>
  `${wire.kind} ${wire.from}${wire.fromPort ? `.${wire.fromPort}` : ''}${wire.outcome ? `:${wire.outcome}` : ''}${wire.on ? `@${wire.on}` : ''} → ${wire.to}${wire.toPort ? `.${wire.toPort}` : ''}${'group' in wire.wire ? ' (any)' : ''}`);

describe('a Git step\'s tail', () => {
  it('brings a rebase its work, its ready way on, and its resolution with the person\'s task, each way back to it', () => {
    const { draft, tail, steps } = add(draftOf(WORK), 'rebase', 'b');
    expect(tail).toBe('rebase');
    expect(steps).toEqual(['resolve', 'human']);
    expect(ways(draft)).toEqual([
      'after work → holds', 'after holds → rebase', 'when rebase:ready → line',
      'data work.work → rebase.work',
      'when rebase:conflicted → resolve (any)', 'when rebase:uncommitted → resolve (any)',
      'data rebase.problem → resolve.problem', 'data work.work → resolve.work',
      'back resolve@resolved → rebase', 'failed resolve → human', 'back human@done → rebase',
    ]);
    const rebase = draft.nodes.find((node) => node.id === 'rebase')!;
    expect(rebase.node.outcomes).toEqual(['ready', 'conflicted', 'uncommitted']);
    expect(rebase.node.outputsOn).toEqual({ commit: ['ready'], problem: ['conflicted', 'uncommitted'] });
  });

  it('brings a merge its commit from the nearest producer, merged and held going on, and behind back to its rebase', () => {
    const rebased = add(draftOf(WORK), 'rebase', 'b').draft;
    const onward = rebased.wires.find((wire) => wire.from === 'rebase' && wire.outcome === 'ready')!;
    const { draft, tail, steps } = add(rebased, 'merge', onward.id);
    expect(tail).toBe('merge');
    expect(steps).toEqual([]);
    const own = ways(draft).filter((way) => way.includes('merge'));
    expect(own).toEqual([
      'when rebase:ready → merge', 'when merge:merged → line (any)', 'data rebase.commit → merge.commit',
      'when merge:held → line (any)', 'back merge@behind → rebase',
    ]);
  });

  it('brings a merge fed through a check its way back to the rebase before the check, and none where no rebase feeds it', () => {
    const checked = draftOf({
      ...WORK,
      nodes: [...WORK.nodes, { id: 'rebase', kind: 'rebase' }, { id: 'check', kind: 'check' }],
      wires: [{ id: 'a', kind: 'after', from: 'work', to: 'rebase' }, { id: 'r', kind: 'when', from: 'rebase', outcome: 'ready', to: 'check' },
        { id: 'c', kind: 'after', from: 'check', to: 'line' }, { id: 'd', kind: 'data', from: 'rebase.commit', to: 'check.commit' }],
    });
    const through = { ...checked, nodes: checked.nodes.map((node) => node.id === 'check' ? { ...node, node: { ...node.node, outputs: { commit: 'commit' } } } : node) };
    expect(ways(add(through, 'merge', 'c').draft)).toContain('back merge@behind → rebase');

    const alone = add(draftOf(WORK), 'merge', 'b');
    expect(ways(alone.draft).some((way) => way.startsWith('back merge'))).toBe(false);
  });

  it('brings nothing to a step of any other kind, or where the palette offers no resolution', () => {
    const made = insertStep(GIT_PALETTE, draftOf(WORK), choiceOf(GIT_PALETTE, 'check')!, { on: 'b' });
    expect(gitTail(GIT_PALETTE, made.draft, made.id)).toEqual({ draft: made.draft, tail: null, steps: [] });
    const bare = { ...GIT_PALETTE, kinds: GIT_PALETTE.kinds.filter((kind) => kind.kind !== 'resolve') };
    const rebase = insertStep(bare, draftOf(WORK), choiceOf(bare, 'rebase')!, { on: 'b' });
    expect(gitTail(bare, rebase.draft, rebase.id).steps).toEqual([]);
  });
});
