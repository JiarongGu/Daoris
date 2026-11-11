import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FlowChart } from '../../workflow/chart/FlowChart';
import { drawnFromJson, graphFlow, kindTitle } from '../../workflow/graphFlow';

/**
 * One change a saved version makes to the version it replaces (the driver's `GraphChange`): a step, a way or a field of the graph,
 * added, removed or changed. The page words it from these fields; `said` is the driver's English, kept for a reader with no catalogue.
 */
export type PressGraphChange = {
  id: string;
  what: 'step' | 'way' | 'graph';
  change: 'added' | 'removed' | 'changed';
  step?: string | null;
  way?: string | null;
  kind?: string | null;
  fields: string[];
  needs: string[];
  said: string;
};

/** What a workflow save's card draws: the graph it saves, and its changes against the version it replaces (none for a new workflow). */
export type PressWorkflow = {
  graph: Record<string, unknown>;
  changes?: PressGraphChange[] | null;
};

/**
 * A workflow's version before the person's yes: the graph drawn as the editor draws it, then each change against the version it
 * replaces. Chrome translates; every id is the graph's own.
 */
export function WorkflowDesign({ workflow, base }: { workflow: PressWorkflow; base?: string }) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState(false);
  const label = t('person.view.workflow.drawing');
  const flow = graphFlow(t, drawnFromJson(t, i18n, workflow.graph), { label, holds: { open, toggle: () => setOpen((was) => !was) } });
  const changes = workflow.changes ?? null;
  return (
    <div className="grid gap-1">
      <div role="group" aria-label={label} className="max-h-96 overflow-auto rounded-control border border-line">
        <FlowChart label={flow.label} nodes={flow.nodes} edges={flow.edges} entries={flow.entries} sections={flow.sections} track={flow.track} />
      </div>
      {changes && changes.length > 0 && (
        <ul aria-label={t('person.view.workflow.changes', { base })} className="m-0 flex list-none flex-col gap-0.5 p-0 text-meta text-ink-soft">
          {changes.map((change) => (
            <li key={change.id} className="m-0 break-words">
              {t(`person.view.workflow.change.${change.what}-${change.change}`, {
                id: change.step ?? change.way ?? '',
                kind: change.kind ? kindTitle(t, i18n, change.kind) : '',
                fields: change.fields.join(', '),
              })}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
