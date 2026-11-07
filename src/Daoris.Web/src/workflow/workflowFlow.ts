import type { TFunction } from 'i18next';
import type { PillTone } from '../design/FlowCanvas';
import type { IconName } from '../design/ui';
import { textOf, type CurrentWorkflow, type WorkflowStep } from './current';
import { type DrawnNode, type DrawnWire, type DrawOptions, FINISHED, type GraphFlow, graphFlow, WORKFLOW_TRACK } from './graphFlow';
import type { GraphRun } from './run';
import { executorSaid, stepTitle } from './said';

// A workflow and its run drawn as flowcharts: what
// `WORKFLOW_CURRENT` and `WORKFLOW_RUN` answer, read onto the one drawing (`graphFlow`). The page derives nothing the driver
// did not answer: a Current's answer holds each step's kind, part, executor and settings and no members and no wires (`current.ts:27-42`),
// so it is drawn as its steps in their version's order. A run bound to a graph version answers its graph's wires, the
// first read that does, and is drawn by them, a fork and its join included.

export { FINISHED, WORKFLOW_TRACK };

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

/** How a run lights one step, and its end: the state's word and tone (`runTone`), and whether it is reached, is current or is done. */
export type StepLit = { state: { word: string; tone: PillTone }; reached: boolean; current: boolean; settled: boolean };
export type FlowLit = { steps: Partial<Record<string, StepLit>>; finished: { word: string; tone: PillTone } | null };

export type WorkflowFlow = GraphFlow;

/**
 * A run bound to a graph version's own graph (the driver's answer's `graph`): its entries, its end and its wires, which the
 * drawing follows in place of the steps' order, so a fork and its join are drawn as the graph holds them.
 */
export type FlowGraph = Pick<GraphRun, 'entries' | 'end' | 'wires'> & Partial<Pick<GraphRun, 'backs' | 'outcomes'>>;

/**
 * **A workflow as a flowchart**, drawn through `graphFlow`: its steps in their version's order, each edge the move the
 * step it leaves names (the person's press, else automatically), a failure path where a step names one, and *Before it lands* and
 * *After it lands* as its sections, the landing their hinge. Lit by a run, each step wears its state, the step the run
 * stands at is current, a step not reached is dashed, and no path is drawn: a run keeps where each step stands now, not the order the
 * work went through. A run's *Finished* is drawn only once its record says it finished. A skip says who skipped it in its own words,
 * the run's (`runSaid`), never here.
 *
 * Worded, with no node's lines and no door: the chart adds those, a molecule's.
 *
 * **A graph run's drawing follows its graph**: handed `graph`, the edges are its wires and the
 * configured end is flagged, so a fork and its join, and a failure's path, are drawn as the version holds them; a step no wire names (a
 * look the work added beside the graph) stands as an entry of its own.
 */
export function workflowFlow(
  t: TFunction, workflow: Pick<CurrentWorkflow, 'steps'>, name: string, lit?: FlowLit, graph?: FlowGraph | null,
  holds?: DrawOptions['holds'],
): WorkflowFlow {
  const { steps } = workflow;
  // A decision the run reached returned an outcome; one with no route of its own is drawn as ◎ *No further step*, reached. A run's record
  // declares no outcome beyond the one it returned, so none other is drawn.
  const returned = (step: WorkflowStep): Pick<DrawnNode, 'outcome' | 'outcomes'> => {
    const outcome = graph?.outcomes?.[step.id];
    const routes = graph?.wires.filter((wire) => wire.kind === 'when' && wire.from === step.id) ?? [];
    if (!outcome || routes.length === 0) return {};
    return { outcome, ...(routes.some((wire) => wire.outcome === outcome) ? {} : { outcomes: [outcome] }) };
  };
  const nodeOf = (step: WorkflowStep): DrawnNode => {
    const on = lit?.steps[step.id];
    return {
      id: step.id,
      kind: step.kind,
      ...returned(step),
      title: plain(stepTitle(t, step)),
      glyph: glyphOf(step),
      actor: t('workflow.flow.who', { part: t(`workflow.participation.${step.participation}`), executor: plain(executorSaid(t, step)) }),
      ...(on ? { state: on.state, reached: on.reached, current: on.current, settled: on.settled } : {}),
    };
  };
  const options: DrawOptions = { label: t('workflow.chart', { name }), ...(holds ? { holds } : {}), ...(lit?.finished ? { finished: lit.finished } : {}) };
  const move = (step: WorkflowStep | undefined) => t(`workflow.flow.move.${step?.press ?? 'automatic'}`);

  if (graph) {
    const byId = new Map(steps.map((step) => [step.id, step]));
    const wires = graph.wires.filter((wire) => byId.has(wire.from) && byId.has(wire.to));
    const named = new Set([...graph.entries, graph.end, ...wires.flatMap((wire) => [wire.from, wire.to])]);
    const drawn: DrawnWire[] = wires.map((wire) => {
      const twice = wires.some((other) => other !== wire && other.from === wire.from && other.to === wire.to);
      const id = twice ? `${wire.from}>${wire.to}:${wire.kind}` : `${wire.from}>${wire.to}`;
      return {
        id, kind: wire.kind, from: wire.from, to: wire.to, outcome: wire.outcome ?? null,
        ...(wire.kind === 'after' ? { says: move(byId.get(wire.from)) } : {}),
      };
    });
    const backs = (graph.backs ?? []).filter((back) => byId.has(back.from) && byId.has(back.to))
      .map((back): DrawnWire => ({ id: `${back.from}<${back.to}:${back.on}`, kind: 'back', from: back.from, to: back.to, on: back.on }));
    const entries = [...graph.entries.filter((entry) => byId.has(entry)), ...steps.filter((step) => !named.has(step.id)).map((step) => step.id)];
    return graphFlow(t, { nodes: steps.map(nodeOf), wires: [...drawn, ...backs], entries, end: graph.end }, options);
  }

  const nodes: DrawnNode[] = [];
  const wires: DrawnWire[] = [];
  steps.forEach((step, index) => {
    nodes.push(nodeOf(step));
    const next = steps[index + 1];
    if (next) wires.push({ id: `${step.id}>${next.id}`, kind: 'after', from: step.id, to: next.id, says: move(step) });
    const outcome = outcomeOf(step);
    if (outcome) {
      nodes.push({ id: outcome.id, kind: outcome.kind, title: outcomeTitle(t, outcome), faint: true });
      wires.push({
        id: `${step.id}>${outcome.id}`, kind: outcome.kind === 'failed' ? 'failed' : 'rejected', from: step.id, to: outcome.id,
      });
    }
  });
  return graphFlow(t, { nodes, wires, entries: steps[0] ? [steps[0].id] : [] }, options);
}
