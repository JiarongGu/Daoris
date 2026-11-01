import { describe, expect, it } from 'vitest';
import type { WorkflowPaletteAnswer } from '../../bridge/repositories/workflowEdit';
import {
  addStep, addWire, draftOf, emptyDraft, entriesOf, graphOf, leavesOf, onChoices, removeStep, removeWire, setEnd, setOption, stepChoices,
} from './graphDraft';

// WORKFLOWEDIT1b: the step editor's draft, the inverse of the run view's drawing (workflowFlow.ts): a schema-2 graph read into steps and
// the ways each goes on, and written back as the driver reads it, a saved graph's fields kept as written.

const PALETTE: WorkflowPaletteAnswer = {
  kinds: [
    { kind: 'claimed', reach: 'observed', reachBy: 'agent', failureBy: null, personJudges: false, personSkips: false, mayEnd: false, evidenced: false,
      skipBy: null, options: [{ name: 'role', type: 'text', choices: [], required: false }], leaves: ['after', 'data', 'trigger'], roleTakes: ['design', 'build'] },
    { kind: 'review', reach: 'pressed', reachBy: 'person', failureBy: null, personJudges: true, personSkips: false, mayEnd: true, evidenced: false,
      skipBy: null, options: [], leaves: ['after', 'data', 'trigger', 'rejected', 'back'], roleTakes: [] },
    { kind: 'land', reach: 'performed', reachBy: 'landing', failureBy: 'landing', personJudges: false, personSkips: false, mayEnd: true, evidenced: true,
      skipBy: null, options: [{ name: 'method', type: 'choice', choices: ['merge', 'branch'], required: false }],
      leaves: ['after', 'data', 'trigger', 'failed', 'back'], roleTakes: [] },
  ],
  wires: [],
  plugins: [{ plugin: 'local-release', steps: [
    { kind: 'local-release/find', version: 1, reach: 'performed', rejectable: false, skippable: true, inputs: {},
      outputs: { release: 'local-release/release@1' }, evidence: 'local-release/receipt@1', outcomes: ['release', 'none'],
      outputsOn: { release: ['release'] }, leaves: ['after', 'data', 'trigger', 'failed', 'when', 'back'] },
    { kind: 'local-release/release', version: 1, reach: 'performed', rejectable: false, skippable: true, inputs: { release: 'local-release/release@1' },
      outputs: { release: 'local-release/release@1' }, evidence: 'local-release/receipt@1', outcomes: [], leaves: ['after', 'data', 'trigger', 'failed', 'back'] },
  ] }],
};

const choice = (kind: string) => stepChoices(PALETTE).find((each) => each.kind === kind)!;

describe("the step editor's draft", () => {
  it('composes a documents workflow, work then its landing, as the driver reads a graph', () => {
    let draft = emptyDraft();
    let work: string; let land: string;
    ({ draft, id: work } = addStep(draft, choice('claimed')));
    ({ draft, id: land } = addStep(draft, choice('land')));
    draft = addWire(draft, { kind: 'after', from: work, to: land });
    draft = setOption(draft, land, 'method', 'merge');
    draft = setEnd(draft, land);

    expect(graphOf(draft)).toEqual({
      schemaVersion: 2,
      entries: ['claimed'],
      end: 'land',
      nodes: [
        { id: 'claimed', kind: 'claimed', version: 1, reach: 'observed' },
        { id: 'land', kind: 'land', version: 1, reach: 'performed', options: { method: 'merge' } },
      ],
      wires: [{ id: 'a', kind: 'after', from: 'claimed', to: 'land' }],
    });
  });

  it("copies a plugin's step as it declares it, and writes a decision's route, a way back and a data wire's ports", () => {
    let draft = emptyDraft();
    let review: string; let find: string; let release: string;
    ({ draft, id: review } = addStep(draft, choice('review')));
    ({ draft, id: find } = addStep(draft, choice('local-release/find')));
    ({ draft, id: release } = addStep(draft, choice('local-release/release')));
    draft = addWire(draft, { kind: 'after', from: review, to: find });
    draft = addWire(draft, { kind: 'when', from: find, to: release, outcome: 'release' });
    draft = addWire(draft, { kind: 'data', from: find, fromPort: 'release', to: release, toPort: 'release' });
    draft = addWire(draft, { kind: 'back', from: find, to: review, on: 'none' });

    const graph = graphOf(draft);
    expect(graph.nodes[1]).toEqual({
      id: 'find', kind: 'local-release/find', version: 1, reach: 'performed', outcomes: ['release', 'none'], outputsOn: { release: ['release'] },
      outputs: { release: 'local-release/release@1' }, evidence: 'local-release/receipt@1',
    });
    expect(graph.wires).toEqual([
      { id: 'a', kind: 'after', from: 'review', to: 'find' },
      { id: 'b', kind: 'when', from: 'find', outcome: 'release', to: 'release' },
      { id: 'c', kind: 'data', from: 'find.release', to: 'release.release' },
      { id: 'd', kind: 'back', from: 'find', on: 'none', to: 'review' },
    ]);
    // A way back goes into an entry, which stays one.
    expect(entriesOf(draft)).toEqual(['review']);
  });

  it("keeps an output a plugin's step carries on some outcomes alone on those outcomes (DECIDE1d), a copy the palette is not shared through", () => {
    const { draft, id } = addStep(emptyDraft(), choice('local-release/find'));
    const carried = (graphOf(draft).nodes[0] as { outputsOn: Record<string, string[]> }).outputsOn;

    expect(carried).toEqual({ release: ['release'] });
    carried.release.push('none');
    expect(choice('local-release/find').outputsOn).toEqual({ release: ['release'] });
    // A step that names no outputsOn, and a palette from a driver before DECIDE1d, write none: every output stays carried on every reach.
    expect(graphOf(addStep(emptyDraft(), choice('local-release/release')).draft).nodes[0]).not.toHaveProperty('outputsOn');
    const older = { ...PALETTE, plugins: [{ plugin: 'local-release', steps: [{ ...PALETTE.plugins[0].steps[0], outputsOn: undefined }] }] };
    expect(graphOf(addStep(emptyDraft(), stepChoices(older).find((each) => each.kind === 'local-release/find')!).draft).nodes[0]).not.toHaveProperty('outputsOn');
    expect(id).toBe('find');
  });

  it('reads a saved graph back to what it was, the fields it does not draw kept as written', () => {
    const saved = {
      schemaVersion: 2, entries: ['work'], end: 'line', cleanup: { until: 'held' },
      nodes: [
        { id: 'work', kind: 'claimed', version: 1, reach: 'observed', options: { role: 'build' } },
        { id: 'line', kind: 'line', version: 1, reach: 'observed', timeoutSeconds: 60 },
      ],
      wires: [{ id: 'a', kind: 'after', from: 'work', to: 'line', group: { id: 'g', mode: 'allOf' } }],
    };
    expect(graphOf(draftOf(saved))).toEqual(saved);
  });

  it('offers from each step only the ways the palette lets leave it, and a way back on what can send it back', () => {
    let draft = emptyDraft();
    let work: string; let review: string; let find: string;
    ({ draft, id: work } = addStep(draft, choice('claimed')));
    ({ draft, id: review } = addStep(draft, choice('review')));
    ({ draft, id: find } = addStep(draft, choice('local-release/find')));
    const node = (id: string) => draft.nodes.find((each) => each.id === id)!;

    expect(leavesOf(PALETTE, node(work))).toEqual(['after', 'data', 'trigger']);
    expect(leavesOf(PALETTE, node(find))).toContain('when');
    expect(onChoices(PALETTE, node(review))).toEqual(['rejected']);
    expect(onChoices(PALETTE, node(find))).toEqual(['failed', 'release', 'none']);
  });

  it('removes a step with every way into and out of it, and the end where it was the end', () => {
    let draft = emptyDraft();
    let work: string; let land: string;
    ({ draft, id: work } = addStep(draft, choice('claimed')));
    ({ draft, id: land } = addStep(draft, choice('land')));
    draft = setEnd(addWire(draft, { kind: 'after', from: work, to: land }), land);

    const after = removeStep(draft, land);
    expect(after.nodes.map((each) => each.id)).toEqual(['claimed']);
    expect(after.wires).toEqual([]);
    expect(after.end).toBe('');
    expect(removeWire(draft, 'a').wires).toEqual([]);
    // A second step of a kind takes the next free id.
    expect(addStep(draft, choice('land')).id).toBe('land-2');
  });
});
