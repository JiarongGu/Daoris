import type { OptionField } from './answers';

/** A step the palette offers, a built-in kind's or a plugin's declared step, in one shape. */
export type StepChoice = {
  kind: string; version: number; reach: string; plugin: string | null; outcomes: string[]; outputsOn: Record<string, string[]>;
  inputs: Record<string, string>; outputs: Record<string, string>; evidence: string | null; rejectable: boolean; skippable: boolean; leaves: string[];
  options: OptionField[]; roleTakes: string[]; mayEnd: boolean;
  /** How the picker offers it, as the palette says: `alone` or `tail:<method>`. */
  offered: string;
};

/** A step of the draft: its id and kind, and its node's JSON as the graph will hold it. */
export type DraftNode = { id: string; kind: string; node: Record<string, unknown> };

/** A way one step goes on to another: a wire, its ends (a `data` wire's ports apart), a decision's outcome and a way back's `on`. */
export type DraftWire = {
  id: string; kind: string; from: string | null; to: string; fromPort: string | null; toPort: string | null; outcome: string | null;
  on: string | null; wire: Record<string, unknown>;
};

/** A draft: its steps and wires in the order composed, its end, and the graph's other fields and entries' order as read. */
export type GraphDraft = { nodes: DraftNode[]; wires: DraftWire[]; end: string; rest: Record<string, unknown>; order: string[] };

/** A wire as `addWire` takes it. */
export type WireAsk = { kind: string; from: string; to: string; fromPort?: string; toPort?: string; outcome?: string; on?: string };

/** The graph a draft writes: schema 2, what `WORKFLOW_SAVE` takes as `graph`. */
export type GraphJson = {
  schemaVersion: 2; entries: string[]; end: string; nodes: Record<string, unknown>[]; wires: Record<string, unknown>[]; [field: string]: unknown;
};
