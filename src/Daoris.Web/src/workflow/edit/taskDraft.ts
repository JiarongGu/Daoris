import type { DraftNode, GraphDraft } from './graphDraftTypes';

// A role's task is one of two: a task its holder says *Done* on, whose outcome is `done`, or an approval its holder
// *Approve*s or *Reject*s, whose outcome is `approved` and whose refusal goes on the graph's `rejected` wire as a review's changes do. Whoever
// holds the role settles it, a person or an agent alike: the kind says what settles it, never who. The driver's `TaskNode` holds the two
// outcome lists; the draft says which one a node declares.

/** The kind the choice is for: the driver's `task`. */
export const TASK_KIND = 'task';

const DONE = 'done';
const APPROVED = 'approved';

/** Whether a `task` step is an approval: it declares `approved`. */
export const isApproval = (node: DraftNode): boolean => Array.isArray(node.node.outcomes) && node.node.outcomes[0] === APPROVED;

/** A `task` step made a task or an approval: its outcome declared, and the `when` ways that left it by the other's outcome now leave by this one's. */
export function setApproval(draft: GraphDraft, id: string, approval: boolean): GraphDraft {
  const [was, now] = approval ? [DONE, APPROVED] : [APPROVED, DONE];
  return {
    ...draft,
    nodes: draft.nodes.map((each) => (each.id === id ? { ...each, node: { ...each.node, outcomes: [now] } } : each)),
    wires: draft.wires.map((wire) => (wire.from === id && wire.kind === 'when' && wire.outcome === was
      ? { ...wire, outcome: now, wire: { ...wire.wire, outcome: now } } : wire)),
  };
}
