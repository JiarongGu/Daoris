import type { FlowNode } from '../../design/Flow';
import type { IconName } from '../../design/ui';

// The chart's step kinds (FLOWCHART2a; D223): which variant of node draws a kind, in the graph's words (`claimed`, `land`) and in a
// Current's (`work`, `landing`), and what the drawing adds to a node for `StepNode`. Pure: no words, no React.

/** The variants `StepNode` draws; a kind this page has none for is `unknown`, shown as recorded and never dropped. */
export type StepVariant = 'work' | 'you' | 'check' | 'holds' | 'decision' | 'plugin' | 'landing' | 'line' | 'other' | 'unknown';

const BY_KIND: Record<string, StepVariant> = {
  claimed: 'work', work: 'work',
  review: 'you', look: 'you', 'go-ahead': 'you', human: 'you',
  check: 'check', opinion: 'check',
  'work.requests': 'holds', 'quest.holds': 'holds', 'content.holds': 'holds',
  land: 'landing', landing: 'landing', handed: 'landing',
  line: 'line', pushed: 'line', 'pull-request': 'line',
  request: 'other', 'wait.unit': 'other', integrate: 'other', 'quest.close': 'other', all: 'other', stop: 'other',
};

/** A step's variant from its kind; a plugin's step (`plugin/step`) declaring outcomes is a decision, and a built-in never is. */
export function variantOf(kind: string, outcomes: readonly string[] = []): StepVariant {
  const known = BY_KIND[kind];
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
  /** Some route follows it; else it ends at a ◎ *No further step*. */
  routed: boolean;
  /** A run's decision returned this outcome. */
  taken?: boolean;
};

/** What a node of the chart is, beside what `Flow` draws of it: its variant, its ports, and whether it is a terminal or a group. */
export type ChartStep = {
  kind: string;
  variant: StepVariant;
  ports: ChartPort[];
  /** ◎ *No further step*, or a run's *Finished*: drawn as a terminal, not a step. */
  terminal?: 'none' | 'finished';
  /** The declared holds, folded to one node or opened (the first member carrying the press that folds them again). */
  group?: { count: number; open: boolean; members: string[] };
  /** What the driver's judgement refused at this step, in the reader's words (FLOWCHART2c): drawn as words, never a border alone. */
  refused?: string[];
};

/** A node of the chart: `Flow`'s node, and what the workflow's components read of it. A node of the canvas's own (a fold) carries none. */
export type ChartNode = FlowNode & { step?: ChartStep };

/** Whether a canvas node is one the chart drew a step for. */
export const isChartNode = (node: FlowNode): node is ChartNode & { step: ChartStep } => (node as ChartNode).step !== undefined;
