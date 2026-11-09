import type { WorkflowPaletteAnswer } from './answers';
import { addStep, addWire, choiceOf, type GraphDraft, setCarry, setJoinMode, sourcesOf } from './graphDraft';

// What a step of the Git group brings in the change that adds it (the process-steps design §3), as a monitor comes with its `when` ways: a
// `rebase` its work from the run's entry, its `ready` way on, and its resolution's tail (the `resolve` its stuck outcomes reach, the person's
// task its failure reaches, and both ways back to it); a `merge` its commit from the nearest producer before it, `merged` and `held` going on
// as one, and `behind` back to the rebase its commit comes from. Every way out of a stuck step then leads on, which the save requires.

/** What a tail brought: the kind whose tail it is, and the steps it added. */
export type GitTail = { draft: GraphDraft; tail: 'rebase' | 'merge' | null; steps: string[] };

const ENTRY = 'claimed';

/** The step's ways on by `after`, which a step reached with outcomes takes on its `outcome` instead. */
function onwardOn(draft: GraphDraft, id: string, outcomes: readonly string[]): GraphDraft {
  let next = draft;
  for (const way of draft.wires.filter((wire) => wire.from === id && wire.kind === 'after')) {
    const [first, ...rest] = outcomes;
    next = { ...next, wires: next.wires.map((wire) => (wire.id === way.id ? { ...wire, kind: 'when', outcome: first! } : wire)) };
    for (const outcome of rest) next = addWire(next, { kind: 'when', from: id, to: way.to, outcome });
    const into = next.wires.filter((wire) => wire.to === way.to && wire.kind !== 'data' && wire.kind !== 'back');
    if (rest.length > 0 && into.every((wire) => wire.from === id)) next = setJoinMode(next, way.to, 'any');
  }
  return next;
}

/** The run's entry's `work`, where the draft has one. */
const entryWork = (draft: GraphDraft, id: string, input: string) =>
  sourcesOf(draft, id, input).find((source) => draft.nodes.find((node) => node.id === source.from)?.kind === ENTRY) ?? null;

function rebaseTail(palette: WorkflowPaletteAnswer, draft: GraphDraft, id: string): GitTail {
  let next = onwardOn(draft, id, ['ready']);
  const work = entryWork(next, id, 'work');
  if (work && !next.wires.some((wire) => wire.kind === 'data' && wire.to === id && wire.toPort === 'work')) next = setCarry(next, id, 'work', work);
  const resolveChoice = choiceOf(palette, 'resolve');
  const taskChoice = choiceOf(palette, 'human');
  if (!resolveChoice || !taskChoice) return { draft: next, tail: 'rebase', steps: [] };

  const resolve = addStep(next, resolveChoice);
  next = resolve.draft;
  for (const outcome of ['conflicted', 'uncommitted']) next = addWire(next, { kind: 'when', from: id, to: resolve.id, outcome });
  next = setJoinMode(next, resolve.id, 'any');
  next = setCarry(next, resolve.id, 'problem', { from: id, port: 'problem' });
  const resolveWork = entryWork(next, resolve.id, 'work');
  if (resolveWork) next = setCarry(next, resolve.id, 'work', resolveWork);
  next = addWire(next, { kind: 'back', from: resolve.id, to: id, on: 'resolved' });

  const task = addStep(next, taskChoice);
  next = addWire(task.draft, { kind: 'failed', from: resolve.id, to: task.id });
  next = addWire(next, { kind: 'back', from: task.id, to: id, on: 'done' });
  return { draft: next, tail: 'rebase', steps: [resolve.id, task.id] };
}

/** The step a `commit` input is fed from, followed back through each step that passes its own `commit` on, to the rebase that made it. */
function rebaseFeeding(draft: GraphDraft, id: string): string | null {
  const seen = new Set<string>();
  for (let at = id; !seen.has(at); ) {
    seen.add(at);
    const fed = draft.wires.find((wire) => wire.kind === 'data' && wire.to === at && wire.toPort === 'commit');
    const from = fed?.from ? draft.nodes.find((node) => node.id === fed.from) : undefined;
    if (!from) return null;
    if (from.kind === 'rebase') return from.id;
    at = from.id;
  }
  return null;
}

/** The nearest step before `id`, by its ways in, among `from`. */
function nearest(draft: GraphDraft, id: string, from: readonly string[]): string | null {
  const queue = [id];
  const seen = new Set(queue);
  while (queue.length > 0) {
    const at = queue.shift()!;
    for (const wire of draft.wires.filter((each) => each.to === at && each.from && each.kind !== 'data' && each.kind !== 'back')) {
      if (from.includes(wire.from!)) return wire.from;
      if (!seen.has(wire.from!)) { seen.add(wire.from!); queue.push(wire.from!); }
    }
  }
  return null;
}

function mergeTail(draft: GraphDraft, id: string): GitTail {
  let next = onwardOn(draft, id, ['merged', 'held']);
  if (!next.wires.some((wire) => wire.kind === 'data' && wire.to === id && wire.toPort === 'commit')) {
    const sources = sourcesOf(next, id, 'commit');
    const from = nearest(next, id, sources.map((source) => source.from));
    const source = sources.find((each) => each.from === from);
    if (source) next = setCarry(next, id, 'commit', source);
  }
  const rebase = rebaseFeeding(next, id);
  if (rebase) next = addWire(next, { kind: 'back', from: id, to: rebase, on: 'behind' });
  return { draft: next, tail: 'merge', steps: [] };
}

/** The tail a step just added to `draft` brings with it; the same draft for a kind that brings none. */
export function gitTail(palette: WorkflowPaletteAnswer, draft: GraphDraft, id: string): GitTail {
  const kind = draft.nodes.find((node) => node.id === id)?.kind;
  if (kind === 'rebase') return rebaseTail(palette, draft, id);
  if (kind === 'merge') return mergeTail(draft, id);
  return { draft, tail: null, steps: [] };
}
