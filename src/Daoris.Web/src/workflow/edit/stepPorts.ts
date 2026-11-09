import { NODE_KINDS } from '../../data/generated/service';

// A step's ports as the driver reads them (`NodePorts`): its kind's own, as the kit declares them, with what its declaration adds beside
// them. A port that arrives (declared before the change that wires it) is no port of a step the editor composes.

const KIT = new Map(NODE_KINDS.map((row) => [row.kind, row]));

type Ports = Record<string, string>;

const record = (value: unknown): Ports =>
  value && typeof value === 'object' && !Array.isArray(value)
    ? Object.fromEntries(Object.entries(value as Record<string, unknown>).filter((each): each is [string, string] => typeof each[1] === 'string'))
    : {};

/** The inputs and the outputs of a step: its kind's first, then those its declaration names that its kind does not. */
export function portsOf(step: { kind: string; node: Record<string, unknown> }): { inputs: Ports; outputs: Ports } {
  const kit = KIT.get(step.kind);
  return { inputs: { ...record(step.node.inputs), ...(kit?.inputs ?? {}) }, outputs: { ...record(step.node.outputs), ...(kit?.outputs ?? {}) } };
}
