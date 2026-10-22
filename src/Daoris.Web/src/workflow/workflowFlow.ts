import type { TFunction } from 'i18next';
import type { FlowEdge, FlowNode, FlowSection, PillTone } from '../design/Flow';
import type { IconName } from '../design/ui';
import { aroundTheLanding, textOf, type CurrentWorkflow, type WorkflowStep } from './current';
import type { GraphRun, GraphWire } from './run';
import { executorSaid, stepTitle } from './said';

// A workflow and its run drawn as flowcharts (FLOWCHART1d; D186 point 5, the flowchart design §2.4, §2.5 and §5.1): what
// `WORKFLOW_CURRENT` and `WORKFLOW_RUN` answer, read onto `Flow`'s nodes and edges. The page derives nothing the driver did not
// answer: a Current's answer holds each step's kind, part, executor and settings and no members and no wires (`current.ts:27-42`), so
// it is drawn as its steps in their version's order. A run bound to a graph version (FABRIC-CUTOVER1e) answers its graph's wires, the
// first read that does, and is drawn by them, a fork and its join included.

/** A step's kind as its glyph: what it is, beside the person's part by its shape. */
const GLYPH: Record<string, IconName> = {
  work: 'stepWork',
  opinion: 'stepOpinion',
  look: 'stepLook',
  'pull-request': 'stepPullRequest',
};

/** A step's glyph: its kind's, a landing by its form, and a tool for a kind this page has no glyph for. */
export const glyphOf = (step: WorkflowStep): IconName =>
  step.kind === 'landing' ? (textOf(step, 'form') === 'branch' ? 'stepBranch' : 'stepMerge') : GLYPH[step.kind] ?? 'tool';

/** The end of the line, a node of its own: an id no step can take, since a step's is lower-case letters, digits and dashes. */
export const FINISHED = ':finished';

/**
 * A workflow's node track, in rem: a step holds its settings, where they were set and its limit, or its run's facts and doors, so
 * its floor is 19rem (304 px), which leaves the side bar's 300 px floor the rail before its gutters as after them and draws top to
 * bottom from the main area's 400 px floor (376 px inside its gutters), and it reads up to 36rem across.
 */
export const WORKFLOW_TRACK = { least: 19, most: 36 } as const;

/** A name set as code, read as words: a node's title and a reader's description are plain text. */
const plain = (text: string) => text.replace(/`/g, '');

/** What follows a check's or a stage's failure (the workflow design §3.3), and a go-ahead's refusal (§3.6), as worded here. */
const OUTCOMES = { failed: ['wait', 'send-back', 'stop'], refused: ['stop', 'wait'] } as const;

/** What follows a step's failure or its refusal, where the step names one: the outcome node's id, its kind and the outcome. */
export type StepOutcome = { id: string; kind: 'failed' | 'refused'; outcome: string };

/**
 * The failure path a step names (the flowchart design §2.4): a check's, a stage's or a group's `failed` (wait for you, sent back
 * once, or stop), and a go-ahead's `refused` (stop, or wait). A forward edge to its outcome, never a return: a send-back is the
 * kind's bounded return. None where the step names none, which is every step today's Current draws.
 */
export function outcomeOf(step: WorkflowStep): StepOutcome | null {
  const failed = textOf(step, 'failed');
  if (failed) return { id: `${step.id}:failed`, kind: 'failed', outcome: failed };
  const refused = step.kind === 'go-ahead' ? textOf(step, 'refused') : null;
  if (refused) return { id: `${step.id}:refused`, kind: 'refused', outcome: refused };
  return null;
}

const known = (outcome: StepOutcome) => (OUTCOMES[outcome.kind] as readonly string[]).includes(outcome.outcome);

/** An outcome's node title: in the reader's words, or as the driver spelled one this page has no words for. */
export function outcomeTitle(t: TFunction, outcome: StepOutcome): string {
  return known(outcome) ? t(`workflow.outcome.${outcome.outcome}`) : t('workflow.outcome.recorded', { outcome: outcome.outcome });
}

/** The rail's line for a failure path, under its step (the workflow design §6.2: *failed: sent back once, then you*). */
export function outcomeSaid(t: TFunction, outcome: StepOutcome): string {
  return known(outcome)
    ? t(`workflow.${outcome.kind}.${outcome.outcome}`)
    : t(`workflow.${outcome.kind}.recorded`, { outcome: outcome.outcome });
}

/** How a run lights one step, and its end: the state's word and tone (`runTone`), and whether it was reached, is current or is done. */
export type StepLit = { state: { word: string; tone: PillTone }; reached: boolean; current: boolean; settled: boolean };
export type FlowLit = { steps: Partial<Record<string, StepLit>>; finished: { word: string; tone: PillTone } | null };

export type WorkflowFlow = { label: string; nodes: FlowNode[]; edges: FlowEdge[]; entries: string[]; sections: FlowSection[] };

/**
 * A run bound to a graph version's own graph (FABRIC-CUTOVER1e, the driver's answer's `graph`): its entries, its end and its wires, which the
 * drawing follows in place of the steps' order, so a fork and its join are drawn as the graph holds them.
 */
export type FlowGraph = Pick<GraphRun, 'entries' | 'end' | 'wires'>;

/** A wire's edge on the drawing (FABRIC-CUTOVER1e): what it waits on, said; a failure's and a refusal's path in their own style. */
function wireEdge(t: TFunction, wire: GraphWire, from: WorkflowStep | undefined, twice: boolean): FlowEdge {
  const id = twice ? `${wire.from}>${wire.to}:${wire.kind}` : `${wire.from}>${wire.to}`;
  switch (wire.kind) {
    case 'after': return { id, from: wire.from, to: wire.to, says: t(`workflow.flow.move.${from?.press ?? 'automatic'}`) };
    case 'failed':
      return { id, from: wire.from, to: wire.to, style: 'failed', says: t('workflow.flow.failed'), label: t('workflow.flow.failedLabel') };
    case 'rejected':
      return { id, from: wire.from, to: wire.to, style: 'rejected', says: t('workflow.flow.refused'), label: t('workflow.flow.refusedLabel') };
    case 'data':
    case 'wait':
    case 'trigger': return { id, from: wire.from, to: wire.to, says: t(`workflow.flow.wire.${wire.kind}`) };
    default: return { id, from: wire.from, to: wire.to, says: t('workflow.flow.wire.recorded', { kind: wire.kind }) };
  }
}

/**
 * **A workflow as a flowchart** (FLOWCHART1d): its steps in their version's order and the end of the line, each edge the move the
 * step it leaves names (the person's press, else automatically), a failure path where a step names one, and *Before it lands* and
 * *After it lands* as its sections, the landing their hinge. Lit by a run (§2.5), each step wears its state, the step the run
 * stands at is current, a step not reached is dashed, the end is reached only once the run finished, and no path is drawn: a run
 * keeps where each step stands now, not the order the work went through. A skip says who skipped it in its own words, the run's
 * (`runSaid`), never here.
 *
 * Worded, with no node's lines and no door: the chart adds those, a molecule's.
 *
 * **A graph run's drawing follows its graph** (FABRIC-CUTOVER1e; D186's FLOWCHART1d note): handed `graph`, the edges are its wires and the
 * end of the line follows its end, so a fork and its join, and a failure's path, are drawn as the version holds them; a step no wire
 * names (a look the work added beside the graph, §6 of the cutover design) stands as an entry of its own.
 */
export function workflowFlow(
  t: TFunction, workflow: Pick<CurrentWorkflow, 'steps'>, name: string, lit?: FlowLit, graph?: FlowGraph | null,
): WorkflowFlow {
  const nodes: FlowNode[] = [];
  const edges: FlowEdge[] = [];
  const { steps } = workflow;
  const nodeOf = (step: WorkflowStep): FlowNode => {
    const on = lit?.steps[step.id];
    return {
      id: step.id,
      title: plain(stepTitle(t, step)),
      glyph: glyphOf(step),
      actor: t('workflow.flow.who', { part: t(`workflow.participation.${step.participation}`), executor: plain(executorSaid(t, step)) }),
      ...(on ? { state: on.state, reached: on.reached, current: on.current, settled: on.settled } : {}),
    };
  };
  const finished = (): FlowNode => ({
    id: FINISHED,
    title: t('workflow.finished'),
    glyph: 'stepFinished',
    ...(lit ? lit.finished ? { state: lit.finished, reached: true, settled: true } : { reached: false } : {}),
  });
  const { after } = aroundTheLanding(steps);
  const sections: FlowSection[] = [
    ...(steps[0] ? [{ title: t('workflow.section.before'), from: steps[0].id }] : []),
    ...(after[0] ? [{ title: t('workflow.section.after'), from: after[0].id }] : []),
  ];

  if (graph) {
    const byId = new Map(steps.map((step) => [step.id, step]));
    const wires = graph.wires.filter((wire) => byId.has(wire.from) && byId.has(wire.to));
    const named = new Set([...graph.entries, graph.end, ...wires.flatMap((wire) => [wire.from, wire.to])]);
    nodes.push(...steps.map(nodeOf), finished());
    edges.push(...wires.map((wire) => wireEdge(t, wire, byId.get(wire.from),
      wires.some((other) => other !== wire && other.from === wire.from && other.to === wire.to))));
    if (byId.has(graph.end)) {
      edges.push({ id: `${graph.end}>${FINISHED}`, from: graph.end, to: FINISHED, says: t(`workflow.flow.move.${byId.get(graph.end)!.press ?? 'automatic'}`) });
    }
    const entries = [...graph.entries.filter((entry) => byId.has(entry)), ...steps.filter((step) => !named.has(step.id)).map((step) => step.id)];
    return { label: t('workflow.chart', { name }), nodes, edges, entries: entries.length > 0 ? entries : [FINISHED], sections };
  }

  steps.forEach((step, index) => {
    nodes.push(nodeOf(step));
    const next = steps[index + 1]?.id ?? FINISHED;
    edges.push({ id: `${step.id}>${next}`, from: step.id, to: next, says: t(`workflow.flow.move.${step.press ?? 'automatic'}`) });
    const outcome = outcomeOf(step);
    if (outcome) {
      nodes.push({ id: outcome.id, title: outcomeTitle(t, outcome), faint: true });
      edges.push({
        id: `${step.id}>${outcome.id}`, from: step.id, to: outcome.id, style: outcome.kind === 'failed' ? 'failed' : 'rejected',
        says: t(`workflow.flow.${outcome.kind}`), label: t(`workflow.flow.${outcome.kind}Label`),
      });
    }
  });
  nodes.push(finished());
  return { label: t('workflow.chart', { name }), nodes, edges, entries: steps[0] ? [steps[0].id] : [FINISHED], sections };
}
