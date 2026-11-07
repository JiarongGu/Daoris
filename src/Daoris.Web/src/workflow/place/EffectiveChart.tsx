import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import { FlowChart } from '../chart/FlowChart';
import type { ChartNode } from '../chart/stepKinds';
import type { WorkflowEffective } from '../current';
import { executorSaid, limitSaid } from '../said';
import { StepSettings, Who, type WorkflowDoors } from '../WorkflowChart';
import { bindingProblemSaid, effectiveFlow, effectiveLabel, routeWords } from './effective';

/**
 * **The chart of the chosen route**: the graph the driver answers as `effective`, Current drawn
 * from the rules as they stand or a saved workflow's newest version, through the one drawing (`graphFlow`). Its caption says which; each
 * step is a card with its title and who acts, and its cell, with what it is set to, where that is set and its limit, shows on selection.
 *
 * @remarks
 * The page draws what the driver answered: a route with no graph says why in the driver's words (a workflow that does not read) or its own
 * (landed work under Current, which is no verification). A saved version's steps are set by the version, so a door to the settings that
 * set a Current step is offered only under Current.
 *
 * A molecule: the selection is its holder's, and every press goes out.
 */
export function EffectiveChart({ effective, routeLabel, page, name, workspace, selected, onSelect, doors, caption }: {
  effective: WorkflowEffective;
  /** What the chart is of, where it is not what the route follows: *Change…*'s preview of a candidate says so. */
  caption?: string;
  /** The route's kind as the person named it, where it is a kind's. */
  routeLabel: string | null;
  page: 'repository' | 'workspace';
  /** The repository or the workspace whose work it draws, which names the chart for a reader. */
  name: string;
  /** The workspace whose rules reach it, for where a step is set. */
  workspace: string;
  /** The node whose cell shows, or none. */
  selected: string | null;
  onSelect: (node: string | null) => void;
  doors?: WorkflowDoors;
}) {
  const { t, i18n } = useTranslation();
  const [holdsOpen, setHoldsOpen] = useState(false);
  const route = routeWords(t, effective.route, routeLabel);

  if (effective.problem) return <p className="m-0 text-small text-ink-soft"><Inline text={bindingProblemSaid(t, effective)} /></p>;
  if (effective.graph === null || effective.graph === undefined) {
    return (
      <p className="m-0 text-small text-ink-soft">
        {t(effective.route === 'landed' ? 'workflow.place.chart.landedNone' : 'workflow.place.chart.older')}
      </p>
    );
  }

  const { flow, cells } = effectiveFlow(t, i18n, effective, {
    label: t('workflow.chart', { name }), holds: { open: holdsOpen, toggle: () => setHoldsOpen((was) => !was) },
  });
  const nodes = flow.nodes.map((node): ChartNode => {
    const step = cells.get(node.id);
    if (!step) return node;
    const here = selected === node.id;
    const toggle = () => onSelect(here ? null : node.id);
    return {
      ...node,
      actor: t('workflow.flow.who', {
        part: t(`workflow.participation.${step.participation}`), executor: executorSaid(t, step).replace(/`/g, ''),
      }),
      who: <Who step={step} />,
      lines: (
        <>
          {node.lines}
          <Button variant="ghost" aria-pressed={here} className="min-h-0 px-1.5 py-0 text-small" onClick={toggle}>
            {t(here ? 'workflow.place.cell.hide' : 'workflow.place.cell.show')}
          </Button>
        </>
      ),
      open: toggle,
    };
  });
  const cell = selected === null ? undefined : cells.get(selected);
  const title = selected === null ? '' : flow.nodes.find((node) => node.id === selected)?.title ?? selected;

  return (
    <section aria-label={t('workflow.place.chart.region', { route })} className="grid min-w-0 gap-2">
      <p className="m-0 text-small font-semibold text-ink">{caption ?? effectiveLabel(t, effective, route)}</p>
      <FlowChart
        label={flow.label}
        nodes={nodes}
        edges={flow.edges}
        entries={flow.entries}
        sections={flow.sections}
        track={flow.track}
        selected={selected}
        canvas="display"
      />
      {cell && (
        <section aria-label={t('workflow.place.cell.region', { step: title })} className="min-w-0 rounded-control border border-line px-3 py-2">
          <h4 className="m-0 text-body font-semibold text-ink">{title}</h4>
          <StepSettings
            step={cell}
            page={page}
            workspace={workspace}
            limit={limitSaid(t, cell, effective.limits)}
            doors={effective.workflow === 'current' ? doors : undefined}
            edge={false}
          />
        </section>
      )}
    </section>
  );
}
