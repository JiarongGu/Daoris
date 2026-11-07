import type { DraftNode, GraphDraft } from './graphDraftTypes';

// A person's task is one of two: a task the person presses *Done* on, whose outcome is `done`, or an approval
// they *Approve* or *Reject*, whose outcome is `approved` and whose refusal goes on the graph's `rejected` wire as a review's changes do. The
// driver's `HumanNode` holds the two outcome lists; the draft says which one a node declares.

/** The kind the choice is for: the driver's `human`. */
export const HUMAN = 'human';

const TASK = 'done';
const APPROVAL = 'approved';

/** Whether a `human` step is an approval: it declares `approved`. */
export const isApproval = (node: DraftNode): boolean => Array.isArray(node.node.outcomes) && node.node.outcomes[0] === APPROVAL;

/** A `human` step made a task or an approval: its outcome declared, and the `when` ways that left it by the other's outcome now leave by this one's. */
export function setApproval(draft: GraphDraft, id: string, approval: boolean): GraphDraft {
  const [was, now] = approval ? [TASK, APPROVAL] : [APPROVAL, TASK];
  return {
    ...draft,
    nodes: draft.nodes.map((each) => (each.id === id ? { ...each, node: { ...each.node, outcomes: [now] } } : each)),
    wires: draft.wires.map((wire) => (wire.from === id && wire.kind === 'when' && wire.outcome === was
      ? { ...wire, outcome: now, wire: { ...wire.wire, outcome: now } } : wire)),
  };
}
