/** The driver's workflow gate, read once for a landing preview or press. Its words include the available terminal doors. */
export interface WorkflowGateState {
  state: string;
  holds: boolean;
  says: string;
  workflow?: string | null;
  version?: number | null;
  kind?: string | null;
  outside?: string[];
  lowered?: string[];
  otherwise?: string | null;
}

export const workflowHolds = (plan?: { workflow?: WorkflowGateState | null } | null): WorkflowGateState | null =>
  plan?.workflow?.holds ? plan.workflow : null;
