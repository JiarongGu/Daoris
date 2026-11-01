import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import type { WorkflowPaletteAnswer } from '../../bridge/repositories/workflowEdit';
import { PALETTE } from './editFixtures';
import {
  addStep, addWire, carryOf, choiceOf, commit, connect, copySteps, type DraftWire, draftOf, duplicateStep, earlierThan, emptyDraft, entriesOf,
  type GraphDraft, type GraphJson, graphOf, historyOf, insertStep, joinModeOf, joinOf, leavesOf, moveInChain, movePlan, moreWaysOf, pasteSteps,
  redo, removeStep, removeWire, retarget, rowsOf, setCarry, setEnd, setJoinMode, setOption, sourcesOf, stepChoices, targetsOf, triggerTargetsOf,
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
    expect(said(atNone.draft.wires.at(-1)!)).toBe(`when:find>${atNone.id}?none`);
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
