import { NODE_KINDS } from '../../bridge/generated/routes';
import type { FlowNode } from '../../design/FlowCanvas';
import type { IconName } from '../../design/ui';

// The chart's step kinds (FLOWCHART2a; D223): which variant of node draws a kind, in the graph's words (`claimed`, `land`) and in a
// Current's (`work`, `landing`), and what the drawing adds to a node for `StepNode`. Pure: no words, no React.

/** The variants `StepNode` draws; a kind this page has none for is `unknown`, shown as recorded and never dropped. */
export type StepVariant = 'work' | 'you' | 'check' | 'holds' | 'decision' | 'plugin' | 'landing' | 'line' | 'other' | 'unknown';

// A built-in's variant is its kit's `look`, by its id and by the step a run draws it as (`work`, `landing`); the pull-request half of a
// plugin's step is drawn on the line beside the steps around it.
const BY_KIND = new Map<string, StepVariant>([
  ...NODE_KINDS.flatMap((row): [string, StepVariant][] => [[row.kind, row.look as StepVariant], [row.drawn, row.look as StepVariant]]),
  ['pull-request', 'line'],
]);

/** A step's variant from its kind; a plugin's step (`plugin/step`) declaring outcomes is a decision, and a built-in never is. */
export function variantOf(kind: string, outcomes: readonly string[] = []): StepVariant {
  const known = BY_KIND.get(kind);
  if (known) return known;
  if (kind.indexOf('/') > 0) return outcomes.length > 0 ? 'decision' : 'plugin';
  return 'unknown';
}

/** The glyph each variant is drawn with, where the step's own (a landing's method) is not given. */
export const VARIANT_GLYPH: Record<StepVariant, IconName> = {
  work: 'stepWork', you: 'stepReview', check: 'stepCheck', holds: 'stepHold', decision: 'stepDecision', plugin: 'stepPlugin',
  landing: 'stepMerge', line: 'stepLine', other: 'tool', unknown: 'tool',
};

/** One outcome of a decision, drawn as a port on the node's lower edge. */
export type ChartPort = {
  outcome: string;
  /** The outcome in the words of the kind that declares it; the raw name is the port's identity. */
  said?: string;
  /** Some route follows it; else it ends at a ◎ *No further step*. */
  routed: boolean;
  /** A run's decision returned this outcome. */
  taken?: boolean;
};

/** What a node of the chart is, beside what the generic box draws of it: its variant, its ports, and whether it is a terminal or a group. */
export type ChartStep = {
  kind: string;
  variant: StepVariant;
  ports: ChartPort[];
  /** ◎ *No further step*, or a run's *Finished*: drawn as a terminal, not a step. */
  terminal?: 'none' | 'finished';
  /** The declared holds, folded to one node or opened (the first member carrying the press that folds them again). */
  group?: { count: number; open: boolean; members: string[] };
  /** What the driver's judgement refused at this step, in the reader's words (FLOWCHART2c): an outline on the step, its chip the canvas's, and the words in its description. */
  refused?: string[];
};

/** A node of the chart: the canvas's `FlowNode`, and what the workflow's components read of it. A node of the canvas's own (a fold) carries none. */
export type ChartNode = FlowNode & { step?: ChartStep };

/** Whether a canvas node is one the chart drew a step for. */
export const isChartNode = (node: FlowNode): node is ChartNode & { step: ChartStep } => (node as ChartNode).step !== undefined;
