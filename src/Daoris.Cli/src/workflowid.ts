// A workflow's id (WORKFLOW1d, WORKFLOW1e; the workflow design §2.5): its own module, a leaf, because both `namedworkflows.ts`
// and `workflowchoice.ts` read it, and `driverconfig.ts` reads the choice, while `namedworkflows.ts` reads `driverconfig.ts`'s
// landing rule: holding the id in either of the first two would make the three a cycle. The driver's `WorkflowNamed.IsId` is
// the twin.

/** A workflow's, a step's and an environment's id: lower-case letters, digits and dashes. */
export const ID_SHAPE = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/** A workflow's or a step's id: lower-case letters, digits and dashes, at most 40. `current` has the shape, and names Current. */
export function isWorkflowId(value: unknown): value is string {
  return typeof value === 'string' && value.length <= 40 && ID_SHAPE.test(value);
}
