import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { addWire, draftOf, type GraphDraft, type GraphJson, graphOf, removeWire, setOption } from './graphDraft';
import { diffOf } from './proposalFixtures';
import {
  canon, closureOf, decide, type Decided, type DraftProposal, everyNamed, leftWith, staleOf, take, UNDECIDED, unionOf,
} from './proposalTake';

// FLOWCHART2f (D223 §4, §8.6): Ask Daoris's proposal taken into the open draft as a transaction. Over every pair of shipped workflows, the
// driver's changes (mirrored in `proposalFixtures.ts`, held here to the driver's own answer) taken whole, and one by one, give the proposed
// graph exactly; each change brings what it needs: a way its steps, a removed step its ways and a replaced end, a landing's method its tail,
// a group its ways. A change the draft no longer holds the base of is refused by name, and the rest stay takeable.

const DEFAULTS = join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Defaults', 'workflows');
const SHIPPED = readdirSync(DEFAULTS).filter((file) => file.endsWith('.json')).sort()
  .map((file) => JSON.parse(readFileSync(join(DEFAULTS, file), 'utf8')) as { id: string; graph: GraphJson });
const shipped = (id: string) => SHIPPED.find((each) => each.id === id)!.graph;

/** The graph as the editor writes it from what it read: what Ask Daoris is told of an open draft. */
const told = (graph: GraphJson) => graphOf(draftOf(graph));

const proposalOf = (base: GraphJson, proposed: GraphJson): DraftProposal => ({
  id: 'p1', base: told(base), proposed, changes: diffOf(told(base), proposed),
});

const byId = (list: Record<string, unknown>[]) => Object.fromEntries(list.map((each) => [each.id as string, each]));

/** What a graph runs as: its steps and ways by id, its entries, its end and its other fields, the order of keys and of the lists aside. */
const meaning = (graph: Record<string, unknown>) =>
  canon({ ...graph, nodes: byId(graph.nodes as Record<string, unknown>[]), wires: byId(graph.wires as Record<string, unknown>[]) });

const ids = (proposal: DraftProposal) => proposal.changes.map((change) => change.id);

/** Takes `wanted` into `draft`, failing the test where it is refused. */
function took(draft: GraphDraft, proposal: DraftProposal, wanted: readonly string[], decided: Decided = UNDECIDED) {
  const result = take(draft, proposal, wanted, decided);
  if (!result.ok) throw new Error(`refused at ${result.at}`);
  return result;
}

const PAIRS = SHIPPED.flatMap((a) => SHIPPED.filter((b) => b !== a).map((b) => [a.id, b.id] as const));

describe("the driver's changes, mirrored", () => {
  it('say the release workflow from the Merge workflow as `WorkflowGraphDiffTests` holds the driver to', () => {
    const changes = diffOf(shipped('merge-after-accept'), shipped('release'));
    const by = Object.fromEntries(changes.map((change) => [change.id, change]));
    expect(changes.map((change) => change.id)).toEqual([
      'step:+check', 'step:+find', 'step:+release', 'step:+publish',
      'way:~l', 'way:~m', 'way:~b', 'way:+c', 'way:~d', 'way:~e', 'way:+f', 'way:+g', 'way:+h', 'way:+i', 'way:+j', 'way:+k',
    ]);
    expect(by['way:~l']).toMatchObject({ fields: ['id'], was: 'h' });
    expect(by['way:~b']).toMatchObject({ fields: ['to'], was: null, needs: ['step:+check'] });
    expect(by['way:+c']!.needs).toEqual(['step:+check', 'way:~e']);
    expect(by['way:+h']!.needs).toEqual(['step:+find', 'step:+release', 'way:~l']);
    expect(by['way:+i']!.needs).toEqual(['step:+find', 'step:+release', 'way:~m']);
    expect(by['way:+k']!.needs).toEqual(['step:+release', 'step:+publish']);

    const back = diffOf(shipped('release'), shipped('merge-after-accept'));
    const from = Object.fromEntries(back.map((change) => [change.id, change]));
    expect(back.filter((change) => change.what === 'step').map((change) => change.id)).toEqual(['step:-check', 'step:-find', 'step:-release', 'step:-publish']);
    expect(from['step:-check']!.needs).toEqual(['way:~b', 'way:-c']);
    expect(from['step:-find']!.needs).toEqual(['way:-g', 'way:-h', 'way:-i']);
  });

  it('say nothing of a shipped graph against itself', () => {
    for (const each of SHIPPED) expect(diffOf(told(each.graph), each.graph), each.id).toEqual([]);
  });
});

describe('a take (§8.6)', () => {
  it.each(PAIRS)('takes every change from %s to %s whole, and one by one, to the proposed graph exactly', (from, to) => {
    const proposal = proposalOf(shipped(from), shipped(to));
    const start = draftOf(proposal.base);
    expect(proposal.changes.length).toBeGreaterThan(0);

    const whole = took(start, proposal, ids(proposal));
    expect(meaning(graphOf(whole.draft))).toBe(meaning(shipped(to)));
    expect([...whole.taken].sort()).toEqual([...ids(proposal)].sort());

    let draft = start;
    let decided: Decided = UNDECIDED;
    for (const change of proposal.changes) {
      if (decided.taken.includes(change.id)) continue;
      const result = took(draft, proposal, [change.id], decided);
      expect(result.taken).toContain(change.id);
      draft = result.draft;
      decided = decide(decided, { taken: result.taken });
    }
    expect(meaning(graphOf(draft))).toBe(meaning(shipped(to)));
    expect(everyNamed(proposal, decided)).toBe(true);
  });

  it.each(PAIRS)('takes any one change of %s to %s with what it needs, and leaves no way to a step the draft does not hold', (from, to) => {
    const proposal = proposalOf(shipped(from), shipped(to));
    for (const change of proposal.changes) {
      const { draft, taken } = took(draftOf(proposal.base), proposal, [change.id]);
      expect([...taken].sort(), change.id).toEqual([...closureOf(proposal, change.id)].sort());
      const steps = new Set(draft.nodes.map((node) => node.id));
      for (const wire of draft.wires) expect(steps.has(wire.to) && (wire.from === null || steps.has(wire.from)), `${change.id}: ${wire.id}`).toBe(true);
      expect(draft.end === '' || steps.has(draft.end), `${change.id}: the end`).toBe(true);
    }
  });

  it('brings the steps a way joins, and the way that held an id it takes', () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('release'));
    expect([...closureOf(proposal, 'way:+h')].sort()).toEqual(['step:+find', 'step:+release', 'way:+h', 'way:~l']);
    expect([...closureOf(proposal, 'step:+find')]).toEqual(['step:+find']);
  });

  it("brings a landing's tail with its method, and a removed step's ways and the end it replaces", () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('pull-request'));
    expect(ids(proposal)).toEqual(['step:-line', 'step:~landing', 'step:+push', 'step:+pull-request', 'way:~c', 'way:+d', 'graph:~end']);
    expect([...closureOf(proposal, 'step:~landing')].sort()).toEqual(['step:+pull-request', 'step:+push', 'step:~landing', 'way:+d', 'way:~c']);
    expect([...closureOf(proposal, 'step:+push')].sort()).toEqual(['step:+pull-request', 'step:+push', 'step:~landing', 'way:+d', 'way:~c']);
    expect(closureOf(proposal, 'step:-line')).toEqual(expect.arrayContaining(['step:-line', 'way:~c', 'graph:~end']));
  });

  it("brings a group's other ways with one of them", () => {
    const base: GraphJson = {
      schemaVersion: 2, entries: ['x', 'y', 'z'], end: 'j',
      nodes: ['x', 'y', 'z', 'j'].map((id) => ({ id, kind: 'claimed', version: 1, reach: 'observed' })),
      wires: [{ id: 'a', kind: 'after', from: 'x', to: 'j', group: { id: 'j-any', mode: 'anyOf' } }],
    };
    const proposed: GraphJson = {
      ...base, entries: ['x', 'y', 'z'],
      wires: [...base.wires, { id: 'b', kind: 'after', from: 'y', to: 'j', group: { id: 'j-any', mode: 'anyOf' } },
        { id: 'c', kind: 'after', from: 'z', to: 'j', group: { id: 'j-any', mode: 'anyOf' } }],
    };
    const proposal = proposalOf(base, proposed);
    expect(ids(proposal)).toEqual(['way:+b', 'way:+c']);
    expect([...closureOf(proposal, 'way:+b')].sort()).toEqual(['way:+b', 'way:+c']);
  });

  it('refuses by name a change whose base the draft no longer holds, and leaves the rest takeable', () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('release'));
    // The person took away the way the proposal renumbers: what renumbers it, and what takes its id, are stale.
    const draft = removeWire(draftOf(proposal.base), 'c');
    const stale = staleOf(draft, proposal, UNDECIDED);
    expect([...stale.keys()].sort()).toEqual(['way:+c', 'way:~e']);
    expect(stale.get('way:+c')).toBe('way:~e');
    expect(take(draft, proposal, ['way:+c'], UNDECIDED)).toEqual({ ok: false, at: 'way:~e' });
    expect(took(draft, proposal, ['way:+h']).taken).toContain('step:+find');
  });

  it("refuses a step's change where the person changed the step, by that change's name", () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('pull-request'));
    const draft = setOption(draftOf(proposal.base), 'landing', 'squash', true);
    expect(take(draft, proposal, ['step:~landing'], UNDECIDED)).toEqual({ ok: false, at: 'step:~landing' });
  });

  it('refuses a removal that would leave a way of the person to a step it takes away', () => {
    const proposal = proposalOf(shipped('release'), shipped('merge-after-accept'));
    const draft = addWire(draftOf(proposal.base), { kind: 'trigger', from: 'line', to: 'publish' });
    expect(take(draft, proposal, ['step:-publish'], UNDECIDED).ok).toBe(false);
    expect(took(draft, proposal, ['step:-check']).taken).toEqual(expect.arrayContaining(['step:-check', 'way:~b', 'way:-c']));
  });

  it('holds every change stale where the draft it was proposed against is not held', () => {
    const proposal: DraftProposal = { ...proposalOf(shipped('merge-after-accept'), shipped('release')), base: null };
    const stale = staleOf(draftOf(shipped('merge-after-accept')), proposal, UNDECIDED);
    expect(stale.size).toBe(proposal.changes.length);
  });

  it('leaves with a change every change that needs it', () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('release'));
    expect([...leftWith(proposal, 'step:+find')].sort()).toEqual(['step:+find', 'way:+g', 'way:+h', 'way:+i']);
    const decided = decide(UNDECIDED, { left: leftWith(proposal, 'step:+find') });
    // Taken again, a change left comes back with what it needs.
    const { taken } = took(draftOf(proposal.base), proposal, ['way:+h'], decided);
    expect(decide(decided, { taken }).left).toEqual(['way:+g', 'way:+i']);
  });

  it('is settled once every change is named once, taken or left', () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('pull-request'));
    expect(everyNamed(proposal, UNDECIDED)).toBe(false);
    expect(everyNamed(proposal, { taken: ['step:-line', 'step:~landing', 'step:+push'], left: ['step:+pull-request', 'way:~c', 'way:+d', 'graph:~end'] })).toBe(true);
  });
});

describe('the union drawing', () => {
  it('draws the steps new, to remove and changed, and the ways new and to remove, on the draft', () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('pull-request'));
    const draft = draftOf(proposal.base);
    const union = unionOf(draft, proposal, ids(proposal));
    expect(Object.fromEntries(union.steps)).toEqual({ line: 'removed', landing: 'changed', push: 'added', 'pull-request': 'added' });
    expect(Object.fromEntries(union.ways)).toEqual({ c: 'removed', '+c': 'added', '+d': 'added' });
    expect(union.draft.nodes.map((node) => node.id)).toEqual(['work', 'requests', 'holds', 'content', 'review', 'landing', 'line', 'push', 'pull-request']);
    // Nothing the draft holds is changed by drawing it.
    expect(meaning(graphOf(draft))).toBe(meaning(proposal.base!));
  });

  it('draws a way that keeps its ends once, marked changed, and one that moves to remove and new', () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('release'));
    const union = unionOf(draftOf(proposal.base), proposal, ids(proposal));
    // `h` renumbered `l` keeps its ends; `b` goes on to the new checks instead of the review.
    expect(union.ways.get('h')).toBe('changed');
    expect(union.draft.wires.some((wire) => wire.id === '+l')).toBe(false);
    expect(union.ways.get('b')).toBe('removed');
    expect(union.ways.get('+b')).toBe('added');
  });

  it('draws only the changes still open', () => {
    const proposal = proposalOf(shipped('merge-after-accept'), shipped('release'));
    const union = unionOf(draftOf(proposal.base), proposal, ['step:+check']);
    expect(Object.fromEntries(union.steps)).toEqual({ check: 'added' });
    expect(union.ways.size).toBe(0);
  });
});
