import { describe, expect, it } from 'vitest';
import type { GraphDraft } from './graphDraftTypes';
import { isApproval, setApproval } from './humanDraft';

const draft = (outcome: string): GraphDraft => ({
  nodes: [{ id: 'sign', kind: 'human', node: { id: 'sign', kind: 'human', outcomes: [outcome] } }, { id: 'ship', kind: 'land', node: { id: 'ship', kind: 'land' } }],
  wires: [{ id: 'a', kind: 'when', from: 'sign', to: 'ship', fromPort: null, toPort: null, outcome, on: null, wire: { id: 'a', kind: 'when', from: 'sign', to: 'ship', outcome } }],
  end: 'ship', rest: {}, order: [],
});

describe('a person\'s task as a task or an approval', () => {
  it('declares approved for an approval and done for a task, and moves the route that left by the other', () => {
    const approval = setApproval(draft('done'), 'sign', true);
    expect(isApproval(approval.nodes[0]!)).toBe(true);
    expect(approval.nodes[0]!.node.outcomes).toEqual(['approved']);
    expect(approval.wires[0]).toMatchObject({ outcome: 'approved', wire: { outcome: 'approved' } });

    const task = setApproval(approval, 'sign', false);
    expect(isApproval(task.nodes[0]!)).toBe(false);
    expect(task.nodes[0]!.node.outcomes).toEqual(['done']);
    expect(task.wires[0]!.outcome).toBe('done');
  });

  it('leaves another step and a way that leaves by neither alone', () => {
    const made = setApproval(draft('done'), 'sign', true);
    expect(made.nodes[1]).toEqual(draft('done').nodes[1]);
    const other = draft('done');
    other.wires[0]!.outcome = 'later';
    expect(setApproval(other, 'sign', true).wires[0]!.outcome).toBe('later');
  });
});
