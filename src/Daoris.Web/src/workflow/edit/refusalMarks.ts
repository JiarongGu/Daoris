import type { TFunction, i18n as I18n } from 'i18next';
import type { GraphFlow } from '../graphFlow';
import type { GraphProblem } from './answers';
import { refusalSaid } from './editWords';

// The driver's refusals placed on the drawing: a step the judgement names wears its sentence and
// declined's outline and a *To put right* chip the canvas places beside it, its sentence in the description, a way it names
// a ⚠ chip with a word and its sentence in the source's foot and to a reader. A refusal naming neither
// is the head's alone. Pure: the drawing in, the drawing out.

/** The drawing with each refusal at the way it names if that is drawn, else at the step it names. */
export function withRefusals(t: TFunction, i18n: I18n, flow: GraphFlow, problems: readonly GraphProblem[]): GraphFlow {
  if (problems.length === 0) return flow;
  const drawn = new Set(flow.edges.map((edge) => edge.id));
  const atStep = new Map<string, string[]>();
  const atWay = new Map<string, string[]>();
  for (const problem of problems) {
    const onWay = problem.wire !== null && drawn.has(problem.wire);
    const id = onWay ? problem.wire : problem.node;
    if (!id) continue;
    const places = onWay ? atWay : atStep;
    places.set(id, [...(places.get(id) ?? []), refusalSaid(t, i18n, problem)]);
  }
  return {
    ...flow,
    nodes: flow.nodes.map((node) => {
      const refused = atStep.get(node.id);
      return refused && node.step
        ? { ...node, step: { ...node.step, refused }, marks: [...(node.marks ?? []), { word: t('workflow.edit.chart.markRefused'), kind: 'refused' as const }] }
        : node;
    }),
    edges: flow.edges.map((edge) => {
      const refused = atWay.get(edge.id);
      if (!refused) return edge;
      // The sentence is said in the source's description, never as a line in its foot, which would move every rank below.
      const note = refused.map((words) => t('workflow.edit.chart.refusedSays', { says: edge.says, words })).join(' ');
      return { ...edge, problem: t('workflow.edit.chart.refusedWay'), note };
    }),
  };
}
