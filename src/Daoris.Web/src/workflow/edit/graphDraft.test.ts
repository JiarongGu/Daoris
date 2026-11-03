import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import type { WorkflowPaletteAnswer } from '../../bridge/repositories/workflowEdit';
import { PALETTE } from './editFixtures';
import {
  addStep, addWire, askedInputs, carryOf, choiceOf, commit, connect, copySteps, type DraftWire, draftOf, duplicateStep, earlierThan, emptyDraft, entriesOf,
  type GraphDraft, type GraphJson, graphOf, historyOf, insertStep, joinModeOf, joinOf, leavesOf, moveInChain, movePlan, moreWaysOf, pasteSteps,
  optionOf, redo, removeStep, removeWire, retarget, rowsOf, setCarry, setEnd, setJoinMode, setLandMethod, setOption, sourcesOf, stepChoices, targetsOf, triggerTargetsOf,
  undo, type WayRowKey, wayTo, workflowIdFor,
} from './graphDraft';

// WORKFLOWEDIT1b, FLOWCHART2b (D223): the step editor's draft, the inverse of the run view's drawing (workflowFlow.ts): a schema-2 graph
// read into steps and the ways each goes on, and written back as the driver reads it, a saved graph's fields kept as written. The editing
// operations are proved over every shipped workflow, read from the driver's own data, each against its undo; the ways a step's table
// offers are proved against the refusals of the driver's wire rules (`Nodes/NodeWires.cs`), mirrored below.

const DEFAULTS = join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Defaults', 'workflows');
const SHIPPED = readdirSync(DEFAULTS).filter((file) => file.endsWith('.json')).sort()
  .map((file) => JSON.parse(readFileSync(join(DEFAULTS, file), 'utf8')) as { id: string; graph: GraphJson });
const shipped = (id: string) => draftOf(SHIPPED.find((each) => each.id === id)!.graph);

const choice = (kind: string) => stepChoices(PALETTE).find((each) => each.kind === kind)!;
const ids = (draft: GraphDraft) => draft.nodes.map((node) => node.id);
const wire = (draft: GraphDraft, id: string) => draft.wires.find((each) => each.id === id)!;
const said = (each: DraftWire) => `${each.kind}:${each.from}>${each.to}${each.outcome ? `?${each.outcome}` : ''}${each.on ? `@${each.on}` : ''}`;

/** A wire as its file names its ends: a data wire's ports after a dot. */
const endOf = (named: unknown, kind: string): string => {
  const text = typeof named === 'string' ? named : '';
  return kind === 'data' && text.includes('.') ? text.slice(0, text.lastIndexOf('.')) : text;
};

/**
 * The refusals of the driver's wire rules this table answers for, mirrored from `NodeWires.Problems` over the palette's authorities: a way
 * back that is never taken, goes forward or goes back twice on one result; a failure's or a refusal's path nothing can take; a decision's
 * route it never comes to; and a cycle through any wire but a way back.
 */
function refusals(palette: WorkflowPaletteAnswer, graph: GraphJson): string[] {
  const nodes = new Map(graph.nodes.map((node) => [node.id as string, node]));
  const wires = graph.wires.map((each) => ({
    id: each.id as string, kind: each.kind as string, from: endOf(each.from, each.kind as string), to: endOf(each.to, each.kind as string),
    outcome: (each.outcome as string | undefined) ?? null, on: (each.on as string | undefined) ?? null,
  }));
  const authority = (id: string) => {
    const node = nodes.get(id)!;
    const kind = palette.kinds.find((each) => each.kind === node.kind);
    if (kind) return { fails: kind.failureBy !== null, judged: kind.personJudges };
    const step = palette.plugins.flatMap((each) => each.steps).find((each) => each.kind === node.kind);
    return { fails: true, judged: step?.rejectable ?? false };
  };
  const outcomes = (id: string) => (Array.isArray(nodes.get(id)!.outcomes) ? nodes.get(id)!.outcomes as string[] : []);
  const ordered = ['after', 'trigger', 'data', 'when'];
  const problems: string[] = [];
  const backs = new Set<string>();
  for (const each of wires) {
    if (!nodes.has(each.from) || !nodes.has(each.to)) { problems.push(`missing-node ${each.id}`); continue; }
    const { fails, judged } = authority(each.from);
    if (each.kind === 'back') {
      const never = each.on === 'failed' ? !fails : each.on === 'rejected' ? !judged : !outcomes(each.from).includes(each.on ?? '');
      if (never) { problems.push(`back-never ${each.id}`); continue; }
      const upstream = new Set<string>();
      const queue = [each.from];
      while (queue.length > 0) {
        const at = queue.shift()!;
        for (const earlier of wires.filter((other) => ordered.includes(other.kind) && other.to === at).map((other) => other.from)) {
          if (!upstream.has(earlier)) { upstream.add(earlier); queue.push(earlier); }
        }
      }
      if (!upstream.has(each.to)) { problems.push(`back-forward ${each.id}`); continue; }
      const key = `${each.from}\n${each.on}`;
      if (backs.has(key)) problems.push(`back-twice ${each.id}`);
      backs.add(key);
      continue;
    }
    if (each.kind === 'failed' && !fails) problems.push(`never-fails ${each.id}`);
    if (each.kind === 'rejected' && !judged) problems.push(`never-rejected ${each.id}`);
    if (each.kind === 'when' && outcomes(each.from).length === 0) problems.push(`no-outcomes ${each.id}`);
    else if (each.kind === 'when' && !outcomes(each.from).includes(each.outcome ?? '')) problems.push(`unknown-outcome ${each.id}`);
  }
  // A cycle: a node reached again from itself along any wire but a way back.
  const next = (id: string) => wires.filter((each) => each.kind !== 'back' && each.from === id).map((each) => each.to);
  const state = new Map<string, 'open' | 'done'>();
  const visit = (id: string): boolean => {
    if (state.get(id) === 'open') return true;
    if (state.get(id) === 'done') return false;
    state.set(id, 'open');
    const found = next(id).some(visit);
    state.set(id, 'done');
    return found;
  };
  if ([...nodes.keys()].some(visit)) problems.push('cycle');
  return problems;
}

/** Each step's rows of *What happens next*, over a draft. */
const everyRow = (draft: GraphDraft) => draft.nodes.flatMap((node) => rowsOf(PALETTE, draft, node.id).map((row) => ({ from: node.id, row: row.key })));

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

  it('reads every shipped graph back to what it was, and a saved one with the fields it does not draw kept as written', () => {
    for (const { graph } of SHIPPED) expect(graphOf(draftOf(graph))).toEqual(graph);
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

  it('offers from each step only the ways the palette lets leave it', () => {
    let draft = emptyDraft();
    let work: string; let find: string;
    ({ draft, id: work } = addStep(draft, choice('claimed')));
    ({ draft, id: find } = addStep(draft, choice('local-release/find')));
    const node = (id: string) => draft.nodes.find((each) => each.id === id)!;

    expect(leavesOf(PALETTE, node(work))).toEqual(['after', 'data', 'trigger']);
    expect(leavesOf(PALETTE, node(find))).toContain('when');
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

describe('What happens next: the rows of a step, and the kind of way the row and the target give', () => {
  it('rows each way on by what it is taken on: once done, if you ask for changes, if it fails, and each outcome; the rest are more ways', () => {
    const release = shipped('release');
    expect(rowsOf(PALETTE, release, 'review').map((row) => [row.key, row.ways.map(said)])).toEqual([
      ['done', ['after:review>landing']],
      ['changes', ['back:review>work@rejected']],
    ]);
    expect(rowsOf(PALETTE, release, 'find').map((row) => [row.key, row.ways.map(said)])).toEqual([
      ['done', []], ['fails', []], ['outcome:release', ['when:find>release?release']], ['outcome:none', []],
    ]);
    expect(rowsOf(PALETTE, release, 'check').map((row) => row.key)).toEqual(['done', 'fails']);
    // A trigger and a recorded wire are more ways; a data wire is the target's *Carries*, never a way on.
    const more = addWire(addWire(release, { kind: 'trigger', from: 'line', to: 'find' }), { kind: 'wait', from: 'line', to: 'find' });
    expect(moreWaysOf(PALETTE, more, 'line').map(said)).toEqual(['trigger:line>find', 'wait:line>find']);
    expect(moreWaysOf(PALETTE, release, 'find')).toEqual([]);
    // A trigger starts a step the source does not wait on, once.
    expect(triggerTargetsOf(more, 'line')).toEqual(['release', 'publish']);
  });

  it('reads earlier as an ancestor upstream through after, trigger, data and when, never a step drawn above', () => {
    const release = shipped('release');
    expect([...earlierThan(release, 'review')].sort()).toEqual(['check', 'content', 'holds', 'requests', 'work']);
    expect([...earlierThan(release, 'publish')]).toContain('find');
    // A failure's path orders nothing: what a failure leads to is not earlier than its source's next step.
    const failing = addWire(release, { kind: 'failed', from: 'check', to: 'line' });
    expect(earlierThan(failing, 'line').has('check')).toBe(true);
    expect(earlierThan(addWire(emptyDraftOf(['a', 'b']), { kind: 'failed', from: 'a', to: 'b' }), 'b').has('a')).toBe(false);
  });

  it('gives the kind from the row and the target: on to a later step, back to an earlier one, never a kind the person names', () => {
    const release = shipped('release');
    const kindOf = (from: string, row: WayRowKey, to: string, draft = release) => {
      const ask = wayTo(PALETTE, draft, from, row, to);
      return ask ? `${ask.kind}${ask.outcome ? `?${ask.outcome}` : ''}${ask.on ? `@${ask.on}` : ''}` : null;
    };
    expect(kindOf('review', 'changes', 'work', shipped('merge-after-accept'))).toBe('back@rejected');
    expect(kindOf('review', 'changes', 'line')).toBe('rejected');
    expect(kindOf('check', 'fails', 'work')).toBe('back@failed');
    expect(kindOf('check', 'fails', 'publish')).toBe('failed');
    expect(kindOf('find', 'outcome:none', 'work')).toBe('back@none');
    expect(kindOf('find', 'outcome:none', 'publish')).toBe('when?none');
    expect(kindOf('landing', 'done', 'find')).toBe('after');
    // Once done only goes on: an earlier step, itself, a step it goes on to already, or a second way back on one result is none.
    expect(kindOf('landing', 'done', 'work')).toBeNull();
    expect(kindOf('landing', 'done', 'landing')).toBeNull();
    expect(kindOf('review', 'done', 'landing')).toBeNull();
    expect(kindOf('review', 'changes', 'holds')).toBeNull();
  });

  it("offers a target exactly where the driver's wire rules take the way, over every shipped workflow", () => {
    for (const { id, graph } of SHIPPED) {
      const draft = draftOf(graph);
      expect(refusals(PALETTE, graph), id).toEqual([]);
      for (const { from, row } of everyRow(draft)) {
        const on = row === 'changes' ? 'rejected' : row === 'fails' ? 'failed' : row.startsWith('outcome:') ? row.slice('outcome:'.length) : null;
        const forward = row === 'done' ? 'after' : row === 'changes' ? 'rejected' : row === 'fails' ? 'failed' : 'when';
        const offered = targetsOf(PALETTE, draft, from, row).map((target) => target.id);
        for (const to of ids(draft)) {
          const ask = wayTo(PALETTE, draft, from, row, to);
          const at = `${id}: ${from} ${row} → ${to}`;
          expect(offered.includes(to), at).toBe(ask !== null);
          if (ask) {
            expect(refusals(PALETTE, graphOf(connect(PALETTE, draft, from, row, to)!.draft)), at).toEqual([]);
            continue;
          }
          // Withheld: each way the row could make to it is refused, already there, or a way back to itself.
          const candidates = [
            { kind: forward, from, to, ...(forward === 'when' ? { outcome: on! } : {}) },
            ...(on === null ? [] : [{ kind: 'back', from, to, on }]),
          ];
          for (const candidate of candidates) {
            const there = draft.wires.some((each) => each.kind === candidate.kind && each.from === from && each.to === to
              && each.outcome === ((candidate as { outcome?: string }).outcome ?? null));
            if (there || to === from) continue;
            expect(refusals(PALETTE, graphOf(addWire(draft, candidate))), `${at} as ${candidate.kind}`).not.toEqual([]);
          }
        }
      }
    }
  });
});

describe('the editing operations', () => {
  it('inserts a step on a way, A → new → B: the way keeps its id and kind into the new step, which goes on to B once done', () => {
    const merge = shipped('merge-after-accept');
    const { draft, id } = insertStep(PALETTE, merge, choice('check'), { on: 'b' });
    expect(id).toBe('check');
    expect(said(wire(draft, 'b'))).toBe('after:content>check');
    expect(draft.wires.filter((each) => each.from === 'check').map(said)).toEqual(['after:check>review']);
    // On a decision's route the outcome stays on the way into the new step; a data wire is never carried over.
    const release = shipped('release');
    const onRoute = insertStep(PALETTE, release, choice('check'), { on: 'h' });
    expect(onRoute.id).toBe('check-2');
    expect(said(wire(onRoute.draft, 'h'))).toBe('when:find>check-2?release');
    expect(said(wire(onRoute.draft, 'i'))).toBe('data:find>release');
    // A way back and a data wire take no step between their ends.
    expect(insertStep(PALETTE, release, choice('check'), { on: 'f' }).draft).toBe(release);
    expect(insertStep(PALETTE, release, choice('check'), { on: 'i' }).draft).toBe(release);
  });

  it("adds a step at a row's end, after a step in its chain, or unconnected, the new step a way the row's own kind", () => {
    const release = shipped('release');
    const atNone = insertStep(PALETTE, release, choice('local-release/release'), { from: 'find', row: 'outcome:none' });
    // Its input is proposed beside the way, from the step the way leaves.
    expect(atNone.draft.wires.slice(release.wires.length).map(said)).toEqual([`when:find>${atNone.id}?none`, `data:find>${atNone.id}`]);
    // After a step whose one way on is plain, it goes between; after one with no way on, it follows.
    const between = insertStep(PALETTE, release, choice('line'), { after: 'landing' });
    expect(said(wire(between.draft, 'e'))).toBe('after:landing>line-2');
    expect(said(between.draft.wires.at(-1)!)).toBe('after:line-2>line');
    const last = insertStep(PALETTE, release, choice('line'), { after: 'publish' });
    expect(said(last.draft.wires.at(-1)!)).toBe('after:publish>line-2');
    expect(insertStep(PALETTE, release, choice('check'), { free: true }).draft.wires).toEqual(release.wires);
  });

  it('duplicates a step after it, a copy of its options under a new id, and connects a target by the derived kind, retargets and also-to', () => {
    const release = shipped('release');
    const copy = duplicateStep(release, 'landing');
    expect(copy.id).toBe('land');
    expect(copy.draft.nodes.find((node) => node.id === 'land')!.node).toMatchObject({ kind: 'land', options: { method: 'merge' } });
    expect(said(wire(copy.draft, 'e'))).toBe('after:landing>land');

    const back = connect(PALETTE, release, 'check', 'fails', 'work')!;
    expect(said(wire(back.draft, back.wire))).toBe('back:check>work@failed');
    expect(connect(PALETTE, release, 'landing', 'done', 'work')).toBeNull();
    // Retargeted, a way keeps its id and takes the kind its new target gives; another target is an *also to*, removable alone.
    const moved = retarget(PALETTE, release, 'f', 'line')!;
    expect(said(wire(moved, 'f'))).toBe('rejected:review>line');
    const also = connect(PALETTE, release, 'review', 'done', 'line')!.draft;
    expect(rowsOf(PALETTE, also, 'review')[0]!.ways.map(said)).toEqual(['after:review>landing', 'after:review>line']);
    expect(rowsOf(PALETTE, removeWire(also, 'd'), 'review')[0]!.ways.map(said)).toEqual(['after:review>line']);
  });

  it('moves a step within a plain chain and back, naming the steps around it, and not where its ways are more than a chain', () => {
    const merge = shipped('merge-after-accept');
    expect(movePlan(merge, 'review', 'earlier')).toEqual({ before: 'holds', first: 'review', second: 'content', after: 'landing' });
    const moved = moveInChain(merge, 'review', 'earlier')!;
    expect(['i', 'b', 'e'].map((each) => said(wire(moved, each)))).toEqual(['after:holds>review', 'after:review>content', 'after:content>landing']);
    expect(graphOf(moveInChain(moved, 'review', 'later')!)).toEqual(graphOf(merge));
    // The release's review also goes back to the work, and its find goes on by its outcomes, so each is more than a chain's link; the
    // work has nothing before it.
    expect(movePlan(shipped('release'), 'review', 'earlier')).toBeNull();
    expect(movePlan(shipped('release'), 'find', 'earlier')).toBeNull();
    expect(movePlan(merge, 'work', 'earlier')).toBeNull();
  });

  it('removes a step alone, or joins its one plain way in to its one plain way on; removing the end asks where the end goes', () => {
    const merge = shipped('merge-after-accept');
    expect(joinOf(merge, 'review')).toEqual({ from: 'content', to: 'landing', in: 'b', out: 'e' });
    const joined = removeStep(merge, 'review', { join: true });
    expect(said(wire(joined, 'b'))).toBe('after:content>landing');
    expect(joined.wires.some((each) => each.id === 'e')).toBe(false);
    expect(removeStep(merge, 'review').wires.some((each) => each.id === 'b')).toBe(false);
    // The end removed takes the end the person chose, else none; a step with a way back is no plain chain's link.
    expect(removeStep(merge, 'line', { end: 'landing' }).end).toBe('landing');
    expect(removeStep(merge, 'line').end).toBe('');
    expect(joinOf(shipped('release'), 'review')).toBeNull();
  });

  it("carries what a step made into another's input, from a step that is not after it, and keeps the wire's id when its source changes", () => {
    const release = shipped('release');
    expect(sourcesOf(release, 'publish', 'release')).toEqual([{ from: 'find', port: 'release' }, { from: 'release', port: 'release' }]);
    // The step it feeds is never a source of its own input, nor a step after it.
    expect(sourcesOf(release, 'release', 'release')).toEqual([{ from: 'find', port: 'release' }]);
    const changed = setCarry(release, 'publish', 'release', { from: 'find', port: 'release' });
    expect(said(wire(changed, 'k'))).toBe('data:find>publish');
    expect(carryOf(changed, 'publish', 'release')!.fromPort).toBe('release');
    expect(setCarry(release, 'publish', 'release', null).wires.some((each) => each.id === 'k')).toBe(false);
  });

  it('copies steps with the ways between them and pastes them under fresh ids, the ways to other steps left out and counted', () => {
    const release = shipped('release');
    const clip = copySteps(release, ['release', 'publish']);
    expect(clip.dropped).toBe(2);
    const pasted = pasteSteps(release, clip);
    expect(pasted.ids).toEqual(['release-2', 'publish-2']);
    expect(pasted.draft.wires.slice(release.wires.length).map(said)).toEqual(['after:release-2>publish-2', 'data:release-2>publish-2']);
  });

  it("joins a step's ways in at the first one, as one group, or waits for every one; a single way in has no join to choose", () => {
    const two = addWire(addWire(shipped('merge-after-accept'), { kind: 'after', from: 'work', to: 'review' }), { kind: 'trigger', from: 'requests', to: 'review' });
    expect(joinModeOf(two, 'review')).toBe('all');
    const any = setJoinMode(two, 'review', 'any');
    expect(joinModeOf(any, 'review')).toBe('any');
    expect(graphOf(any).wires.filter((each) => each.to === 'review').map((each) => each.group)).toEqual([
      { id: 'review-any', mode: 'anyOf' }, { id: 'review-any', mode: 'anyOf' }, { id: 'review-any', mode: 'anyOf' },
    ]);
    expect(graphOf(setJoinMode(any, 'review', 'all'))).toEqual(graphOf(two));
    expect(joinModeOf(two, 'landing')).toBeNull();
  });

  it('names a workflow from its name, else from what it was copied from, numbered past one saved', () => {
    expect(workflowIdFor('Release for engine', null, [])).toBe('release-for-engine');
    expect(workflowIdFor('Release for engine', null, ['release-for-engine'])).toBe('release-for-engine-2');
    expect(workflowIdFor('发布流程', 'release', [])).toBe('release-2');
    expect(workflowIdFor('发布流程', null, ['workflow'])).toBe('workflow-2');
  });
});

describe('each operation, over every shipped workflow, and its undo', () => {
  /** Every operation the editor offers, at every place it applies in a draft, with its inverse where the editor has one. */
  function operations(draft: GraphDraft): { name: string; next: GraphDraft; inverse?: GraphDraft }[] {
    const all: { name: string; next: GraphDraft; inverse?: GraphDraft }[] = [];
    for (const each of draft.wires) {
      const inserted = insertStep(PALETTE, draft, choice('check'), { on: each.id });
      if (inserted.draft === draft) continue;
      const join = joinOf(inserted.draft, inserted.id);
      all.push({ name: `insert on ${each.id}`, next: inserted.draft, ...(join ? { inverse: removeStep(inserted.draft, inserted.id, { join: true }) } : {}) });
    }
    for (const node of draft.nodes) {
      const copy = duplicateStep(draft, node.id);
      all.push({ name: `duplicate ${node.id}`, next: copy.draft, inverse: removeStep(copy.draft, copy.id, { join: joinOf(copy.draft, copy.id) !== null }) });
      for (const dir of ['earlier', 'later'] as const) {
        const moved = moveInChain(draft, node.id, dir);
        if (moved) all.push({ name: `move ${node.id} ${dir}`, next: moved, inverse: moveInChain(moved, node.id, dir === 'earlier' ? 'later' : 'earlier')! });
      }
      all.push({ name: `remove ${node.id}`, next: removeStep(draft, node.id) });
      if (joinOf(draft, node.id)) all.push({ name: `remove and join ${node.id}`, next: removeStep(draft, node.id, { join: true }) });
    }
    for (const { from, row } of everyRow(draft)) {
      for (const target of targetsOf(PALETTE, draft, from, row)) {
        const made = connect(PALETTE, draft, from, row, target.id)!;
        all.push({ name: `connect ${from} ${row} ${target.id}`, next: made.draft, inverse: removeWire(made.draft, made.wire) });
      }
    }
    return all;
  }

  it.each(SHIPPED.map((each) => [each.id, each.graph] as const))('%s: each comes back to the graph as it was by its undo, and by its inverse', (_, graph) => {
    const draft = draftOf(graph);
    const done = operations(draft);
    expect(done.length).toBeGreaterThan(0);
    for (const { name, next, inverse } of done) {
      expect(graphOf(next), name).not.toEqual(graph);
      const history = commit(historyOf(draft), next);
      expect(graphOf(undo(history).present), name).toEqual(graph);
      expect(graphOf(redo(undo(history)).present), name).toEqual(graphOf(next));
      if (inverse) expect(graphOf(inverse), `${name}, then its inverse`).toEqual(graph);
    }
  });

  it('keeps one step of history for each change, and none to redo once a new change is made', () => {
    let history = historyOf(1);
    history = commit(commit(history, 2), 3);
    expect(undo(undo(history)).present).toBe(1);
    expect(undo(historyOf(1))).toEqual(historyOf(1));
    expect(redo(commit(undo(history), 4))).toEqual(commit(undo(history), 4));
  });
});

/** A draft of plain steps of the work's kind, unconnected. */
function emptyDraftOf(names: string[]): GraphDraft {
  return names.reduce((draft, name) => ({ ...draft, nodes: [...draft.nodes, { id: name, kind: 'claimed', node: { id: name, kind: 'claimed' } }] }), emptyDraft());
}

// The choice of a kind this build's palette does not hold is none: such a step's ways are its own, offered as the palette leaves them.
it('offers a step of a kind the palette does not hold only once done', () => {
  const draft = emptyDraftOf(['a']);
  const strange = { ...draft, nodes: [{ id: 'a', kind: 'gone', node: { id: 'a', kind: 'gone' } }] };
  expect(choiceOf(PALETTE, 'gone')).toBeNull();
  expect(rowsOf(PALETTE, strange, 'a').map((row) => row.key)).toEqual(['done']);
});

// SETUPUX1r (D221 §2.3, D223 §3): a landing method brings its tail in one change, and an input is proposed from the step it is routed from.

/** The refusals of `NodeGraphs`' `land-tail` rule, mirrored: the method's tail and no other, each step of it reached from the landing. */
function tailProblems(graph: GraphJson): string[] {
  const land = graph.nodes.find((node) => node.kind === 'land');
  const method = (land?.options as { method?: string } | undefined)?.method;
  if (!land || !method) return [];
  const reach = (from: string[]): Set<string> => {
    const found = new Set<string>();
    const queue = [...from];
    while (queue.length > 0) {
      const at = queue.shift();
      for (const each of graph.wires) {
        if (each.kind !== 'data' && each.kind !== 'back' && each.from === at && !found.has(each.to as string)) { found.add(each.to as string); queue.push(each.to as string); }
      }
    }
    return found;
  };
  const after = reach([land.id as string]);
  const problems: string[] = [];
  const check = (kind: string, brought: boolean) => {
    const held = graph.nodes.filter((node) => node.kind === kind).map((node) => node.id as string);
    if (!brought) problems.push(...held.map((id) => `${id}: brings no ${kind}`));
    else if (held.length === 0) problems.push(`${kind}: none held`);
    else problems.push(...held.filter((id) => !after.has(id)).map((id) => `${id}: not after the landing`));
  };
  check('pushed', method === 'push' || method === 'pull-request');
  check('pull-request', method === 'pull-request');
  check('integrate', method === 'integrate');
  const pushes = graph.nodes.filter((node) => node.kind === 'pushed').map((node) => node.id as string);
  if (method === 'pull-request' && pushes.length > 0) {
    const afterPush = reach(pushes);
    problems.push(...graph.nodes.filter((node) => node.kind === 'pull-request' && !afterPush.has(node.id as string)).map((node) => `${String(node.id)}: before the push`));
  }
  return problems;
}

describe('a landing method brings its tail, in one change (SETUPUX1r)', () => {
  const METHODS = ['merge', 'branch', 'push', 'pull-request', 'integrate'];

  it('adds the pushed and the pull request after the landing for a pull request, between the landing and what followed it', () => {
    const merge = shipped('merge-after-accept');
    const made = setLandMethod(PALETTE, merge, 'landing', 'pull-request');
    expect(made.added).toEqual(['pushed', 'pull-request']);
    expect(made.removed).toEqual([]);
    expect(optionOf(made.draft.nodes.find((node) => node.id === 'landing')!, 'method')).toBe('pull-request');
    expect(made.draft.wires.filter((each) => each.kind === 'after' && ['landing', 'pushed', 'pull-request'].includes(each.from!)).map(said))
      .toEqual(['after:landing>pushed', 'after:pushed>pull-request', 'after:pull-request>line']);
    expect(made.draft.end).toBe('line');
    expect(tailProblems(graphOf(made.draft))).toEqual([]);
  });

  it('takes the tail back out on the way to another method, joining what it stood between, so merge comes back to the graph it was', () => {
    const merge = shipped('merge-after-accept');
    const there = setLandMethod(PALETTE, merge, 'landing', 'pull-request').draft;
    const back = setLandMethod(PALETTE, there, 'landing', 'merge');
    expect([...back.removed].sort()).toEqual(['pull-request', 'pushed']);
    expect(graphOf(back.draft)).toEqual(graphOf(merge));
    // A push keeps its pushed and drops the request.
    const pushOnly = setLandMethod(PALETTE, there, 'landing', 'push');
    expect(pushOnly.removed).toEqual(['pull-request']);
    expect(pushOnly.added).toEqual([]);
    expect(pushOnly.draft.nodes.map((node) => node.kind).filter((kind) => ['pushed', 'pull-request'].includes(kind))).toEqual(['pushed']);
  });

  it('moves the end to the last of the tail where the landing was the end, and back to the landing when the end was a tail step', () => {
    const branch = shipped('branch-to-push');
    expect(branch.end).toBe('landing');
    const request = setLandMethod(PALETTE, branch, 'landing', 'pull-request');
    expect(request.draft.end).toBe('pull-request');
    expect(tailProblems(graphOf(request.draft))).toEqual([]);
    const push = setLandMethod(PALETTE, request.draft, 'landing', 'push');
    expect(push.draft.end).toBe('pushed');
    expect(setLandMethod(PALETTE, push.draft, 'landing', 'branch').draft.end).toBe('landing');
  });

  it('adds an integrate between the landing and the line, and drops the options its method refuses', () => {
    const merge = shipped('merge-after-accept');
    const withOptions = setOption(setOption(setOption(merge, 'landing', 'squash', true), 'landing', 'plugin', 'host'), 'landing', 'pattern', 'x/{quest}');
    const integrate = setLandMethod(PALETTE, withOptions, 'landing', 'integrate');
    expect(integrate.added).toEqual(['integrate']);
    expect(integrate.draft.wires.filter((each) => each.from === 'landing' || each.from === 'integrate').map(said)).toEqual(['after:landing>integrate', 'after:integrate>line']);
    const land = integrate.draft.nodes.find((node) => node.id === 'landing')!;
    expect(optionOf(land, 'plugin')).toBeUndefined();
    expect(optionOf(land, 'squash')).toBeUndefined();
    expect(optionOf(land, 'pattern')).toBe('x/{quest}');
    const onMerge = setLandMethod(PALETTE, withOptions, 'landing', 'merge').draft.nodes.find((node) => node.id === 'landing')!;
    expect([optionOf(onMerge, 'pattern'), optionOf(onMerge, 'plugin'), optionOf(onMerge, 'squash')]).toEqual([undefined, undefined, true]);
  });

  it.each(SHIPPED.filter((each) => each.graph.nodes.some((node) => node.kind === 'land')).map((each) => [each.id] as const))(
    '%s: every method leaves a tail the driver\'s land-tail rule accepts, from every other',
    (id) => {
      const land = shipped(id).nodes.find((node) => node.kind === 'land')!.id;
      for (const from of METHODS) {
        for (const to of METHODS) {
          const made = setLandMethod(PALETTE, setLandMethod(PALETTE, shipped(id), land, from).draft, land, to).draft;
          expect(tailProblems(graphOf(made)), `${id}: ${from} to ${to}`).toEqual([]);
        }
      }
    });

  it('keeps every field of the graph it does not draw, through a method', () => {
    const graph = {
      schemaVersion: 2, entries: ['work'], end: 'landing', cleanup: { keep: true }, note: 'kept',
      nodes: [
        { id: 'work', kind: 'claimed', version: 1, reach: 'observed', retry: { times: 2 } },
        { id: 'landing', kind: 'land', version: 1, reach: 'performed', options: { method: 'branch', future: 'x', plugin: 'none' }, extra: [1] },
      ],
      wires: [{ id: 'a', kind: 'after', from: 'work', to: 'landing', delay: 5 }],
    };
    const written = graphOf(setLandMethod(PALETTE, draftOf(graph), 'landing', 'branch').draft);
    expect(written).toEqual({ ...graph, nodes: [graph.nodes[0], { ...graph.nodes[1] }] });
    const request = graphOf(setLandMethod(PALETTE, draftOf(graph), 'landing', 'pull-request').draft);
    expect(request).toMatchObject({ cleanup: { keep: true }, note: 'kept', entries: ['work'] });
    expect(request.nodes[0]).toEqual(graph.nodes[0]);
    // A pull request is a plugin's to push, so the `none` a branch left is the one option it refuses.
    expect(request.nodes[1]).toEqual({ ...graph.nodes[1], options: { method: 'pull-request', future: 'x' } });
    expect(request.wires.find((each) => each.id === 'a')).toEqual(graph.wires[0]);
  });
});

describe('an input is proposed from the step it is routed from, and asked where two fit (SETUPUX1r)', () => {
  const merge = shipped('merge-after-accept');

  it('proposes both data wires of the release workflow as its steps are put in', () => {
    const find = insertStep(PALETTE, merge, choice('local-release/find'), { after: 'line' });
    expect(find.carried).toEqual([]);
    const release = insertStep(PALETTE, find.draft, choice('local-release/release'), { from: find.id, row: 'outcome:release' });
    expect(release.carried).toEqual([{ step: release.id, input: 'release', from: find.id, port: 'release' }]);
    const publish = insertStep(PALETTE, release.draft, choice('local-release/publish'), { after: release.id });
    expect(publish.carried).toEqual([{ step: publish.id, input: 'release', from: release.id, port: 'release' }]);
    expect(publish.draft.wires.filter((each) => each.kind === 'data').map(said)).toEqual(['data:find>release', 'data:release>publish']);
    expect(askedInputs(publish.draft, publish.id)).toEqual([]);
  });

  it("prefers the routed step's own output, and proposes the one other that fits where it has none", () => {
    const loose = removeWire(removeWire(shipped('release'), 'k'), 'i');
    // Routing a way into publish from release proposes release's output, though find's fits as well.
    const viaRelease = connect(PALETTE, removeWire(loose, 'j'), 'release', 'done', 'publish')!;
    expect(viaRelease.carried).toEqual([{ step: 'publish', input: 'release', from: 'release', port: 'release' }]);
    // Where only one other step makes it, it is taken though the routed step makes none.
    const alone = removeStep(loose, 'release');
    const fromLine = connect(PALETTE, alone, 'line', 'done', 'publish')!;
    expect(fromLine.carried).toEqual([{ step: 'publish', input: 'release', from: 'find', port: 'release' }]);
  });

  it('asks where two others fit and the routed step has none, proposing neither', () => {
    const loose = removeWire(removeWire(shipped('release'), 'k'), 'j');
    const routed = connect(PALETTE, loose, 'line', 'done', 'publish')!;
    expect(routed.carried).toEqual([]);
    expect(carryOf(routed.draft, 'publish', 'release')).toBeNull();
    expect(askedInputs(routed.draft, 'publish')).toEqual([{ input: 'release', sources: [{ from: 'find', port: 'release' }, { from: 'release', port: 'release' }] }]);
    // Chosen, it no longer asks.
    expect(askedInputs(setCarry(routed.draft, 'publish', 'release', { from: 'find', port: 'release' }), 'publish')).toEqual([]);
  });

  it('proposes nothing for an input already fed', () => {
    const made = connect(PALETTE, removeWire(shipped('release'), 'j'), 'find', 'done', 'publish')!;
    expect(made.carried).toEqual([]);
    expect(carryOf(made.draft, 'publish', 'release')!.from).toBe('release');
  });
});

// WORKFLOWCLEAN1 1b: the palette says how each kind is offered, and the page keeps no list of its own.
describe('how the palette offers each kind', () => {
  it('reads `offered` from the palette: handed retired, a landing\'s tail with its method, every other step alone', () => {
    expect(choice('handed').offered).toBe('retired');
    expect([choice('pushed').offered, choice('pull-request').offered, choice('integrate').offered]).toEqual(['tail:push', 'tail:pull-request', 'tail:integrate']);
    expect(['claimed', 'review', 'land', 'line', 'check'].map((kind) => choice(kind).offered)).toEqual(Array(5).fill('alone'));
    expect(choice('local-release/find').offered).toBe('alone');
  });

  it('reads a palette without the field as offering everything alone', () => {
    const older = { ...PALETTE, kinds: PALETTE.kinds.map(({ offered: _offered, ...rest }) => rest) };
    expect(stepChoices(older).every((each) => each.offered === 'alone')).toBe(true);
  });

  it('keeps a retired kind a node already holds drawn: the palette still answers for it', () => {
    expect(choiceOf(PALETTE, 'handed')).not.toBeNull();
  });
});
