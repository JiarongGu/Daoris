import type { i18n as I18n, TFunction } from 'i18next';
import type { WorkflowEffective, WorkflowStart, WorkflowStep } from '../current';
import { type DrawnGraph, type DrawOptions, drawnFromJson, graphFlow, type GraphFlow } from '../graphFlow';
import { type RouteScope, whySaid } from '../routeSaid';

// The place's routes and the chart the chosen one draws (SETUPUX1o; D221, the Setup design's §2.3): the route rows from the start node, the
// label of the graph drawn, and the driver's `effective` answer read onto the one drawing. The page decides nothing the driver did not
// answer: which workflow a route follows, how it was chosen and its graph are the driver's, and only the words are the page's.

/** One route of the place as a row to choose: what it is called, the workflow it follows, and what chose it. */
export type RouteRow = {
  /** The route as `WORKFLOW_CURRENT` takes it: `default`, `kind:<name>` or `landed`. */
  key: string;
  label: string;
  /** The workflow it follows, or *Current*; for the landed route under Current, *No verification here*. */
  target: string;
  why: string;
  landed: boolean;
};

/** The route a kind of task is asked under: none is the default work. */
export const routeKeyOf = (kind: string | null): string => (kind === null ? 'default' : `kind:${kind}`);

/** The route in a sentence: *default work*, *landed work*, or the kind as the person named it. */
export function routeWords(t: TFunction, route: string, label: string | null): string {
  if (route === 'default') return t('workflow.place.route.defaultWords');
  if (route === 'landed') return t('workflow.place.route.landedWords');
  return label ?? route.slice('kind:'.length);
}

/**
 * Why a chart or a run is not drawn, in the page's words: the driver's code and values worded from the catalogue in the language shown,
 * and the driver's own sentence where it sent no code, or one a newer driver names.
 */
export function bindingProblemSaid(t: TFunction, effective: Pick<WorkflowEffective, 'problemCoded'> & { problem?: string | null }): string {
  const coded = effective.problemCoded;
  const values = coded?.values ?? {};
  switch (coded?.code) {
    case 'not-saved': return t('workflow.place.chart.problem.notSaved', values);
    case 'unread': return t('workflow.place.chart.problem.unread', values);
    case 'no-version': return t('workflow.place.chart.problem.noVersion', values);
    case 'version-unread': return t('workflow.place.chart.problem.versionUnread', values);
    case 'pull-request-unserved': return t('workflow.place.chart.problem.pullRequestUnserved', values);
    default: return effective.problem ?? '';
  }
}

/** The rows of the start node's routes: default work, each kind the workspace declares, and landed work. */
export function routeRows(t: TFunction, start: WorkflowStart, scope: RouteScope): RouteRow[] {
  const rows = start.routes.map((route): RouteRow => {
    const kind = route.kind === null ? null : route.label ?? route.kind;
    return {
      key: routeKeyOf(route.kind),
      label: kind ?? t('workflow.place.route.default'),
      target: route.workflow === 'current' ? t('workflow.start.current') : route.name ?? route.workflow,
      why: whySaid(t, route.level, scope, kind, false),
      landed: false,
    };
  });
  const { landed } = start;
  rows.push({
    key: 'landed',
    label: t('workflow.start.landed.title'),
    target: landed.workflow === 'current' ? t('workflow.start.noVerification') : landed.name ?? landed.workflow,
    why: whySaid(t, landed.level, scope, null, true),
    landed: true,
  });
  return rows;
}

/**
 * What the chart is of: *Current, read from the rules as they stand*, or the saved workflow with its version and the route that follows it.
 * `route` is the route in a sentence (`routeWords`).
 */
export function effectiveLabel(t: TFunction, effective: WorkflowEffective, route: string): string {
  return effective.workflow === 'current' || effective.version === null
    ? t('workflow.place.chart.current')
    : t('workflow.place.chart.saved', { name: effective.name ?? effective.workflow, version: effective.version, route });
}

export type EffectiveDrawing = {
  flow: GraphFlow;
  /** Each node's cell, by its id: what the step is, who acts and what it is set to. */
  cells: Map<string, WorkflowStep>;
};

const NOTHING: DrawnGraph = { nodes: [], wires: [], entries: [] };

/**
 * An effective graph read onto the one drawing: its nodes titled by their kind, a way on saying the press its step waits on, as the cell
 * the driver read for the node names it. A route with no graph (landed Current, or a workflow that does not read) draws nothing.
 */
export function effectiveFlow(t: TFunction, i18n: I18n, effective: WorkflowEffective, options: DrawOptions): EffectiveDrawing {
  const cells = new Map(effective.cells.map((cell) => [cell.node, cell.step]));
  if (effective.graph === null || effective.graph === undefined) return { flow: graphFlow(t, NOTHING, options), cells };
  const drawn = drawnFromJson(t, i18n, effective.graph);
  const wires = drawn.wires.map((wire) => {
    const step = cells.get(wire.from);
    return wire.kind === 'after' && step ? { ...wire, says: t(`workflow.flow.move.${step.press ?? 'automatic'}`) } : wire;
  });
  return { flow: graphFlow(t, { ...drawn, wires }, options), cells };
}
