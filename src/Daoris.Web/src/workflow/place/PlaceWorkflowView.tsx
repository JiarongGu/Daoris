import { type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Prose, SkeletonRows } from '../../design/ui';
import type { IntegrationRequest } from '../../settings/repository/IntegrationDeclaration';
import type { Answered } from '../../work/InlineConfirm';
import { WorkflowEditor } from '../edit/WorkflowEditor';
import { type WorkflowTabProps, WorkflowHead } from '../WorkflowTab';
import { WorkflowChart } from '../WorkflowChart';
import { WorkflowStartNode } from '../WorkflowStartNode';
import { ChangeWorkflow, type PresetStart } from './ChangeWorkflow';
import { PlaceGates } from './PlaceGates';

/** What the section draws under the chart: the line's landing rows, handed in by the page that holds the settings. */
export type PlaceWorkflowProps = WorkflowTabProps & {
  /** The repository whose place it is. */
  name: string;
  /** The integration declaration's acceptance or withdrawal; none, and Gates offers no integration choice. */
  onAcceptIntegration?: (request: IntegrationRequest, answered: Answered) => void;
  /**
   * The line and its landing: `integrate`, commits, your look and the second opinion, drawn from the one molecule Setup draws them with.
   * Told which section a chart's step door asked to see open.
   */
  landing?: (asked: 'work' | null) => ReactNode;
};

type Panel = { is: 'change' } | { is: 'edit'; from?: PresetStart } | null;

/**
 * **A place's Workflow** (AGENTSPLACE1h; the Agents-place design §3, §5): the governing configuration of how a repository's work moves.
 * Its head says which workflow is Current and why; the route draws the default work, a kind of task and its paths and the landed work,
 * each wired to the workflow it names, with the chart and its start node under it; **Gates** shows the declaration once with its two
 * acceptances; **Landing** is the line's rule, `integrate` among its keys, with the commits, your look and the second opinion; and
 * **Change…** and **Edit steps…** choose and compose a version in the step editor, which saves a new version and chooses it here.
 *
 * @remarks
 * The two policy owners stay visible and apart (D215): the workflow owns the unit's process, the landing rule owns the line's batching,
 * order, gates and promotion. A chart step's door to the settings that set it opens Landing on this page, since the Repositories page's
 * Workflow tab went with this section. Nothing here is the page's judgement: every refusal is the driver's sentence.
 *
 * A molecule: the answers arrive as props, and every press goes out.
 */
export function PlaceWorkflowView({
  name, current, reading = false, refusal = null, doors, onAcceptGates, accepting = false, onVerify, editor, onAcceptIntegration, landing,
}: PlaceWorkflowProps) {
  const { t } = useTranslation();
  const headId = useId();
  const [panel, setPanel] = useState<Panel>(null);
  const [asked, setAsked] = useState<'work' | null>(null);
  const place = { repository: name };
  const show = (next: Panel) => { setPanel(next); editor?.onOpen?.(next !== null); };

  const body = !current
    ? (
      <div aria-busy={reading || undefined} className="grid gap-2">
        <Prose className="text-small"><Inline text={refusal ?? t('workflow.reading')} /></Prose>
        {!refusal && <SkeletonRows rows={4} />}
      </div>
    )
    : (
      <>
        <WorkflowHead page="repository" name={name} current={current} />
        {current.start && (
          <WorkflowStartNode
            start={current.start}
            page="repository"
            scope={{ repository: current.repository ?? name, workspace: current.workspace }}
            onVerify={onVerify}
            gatesApart
          />
        )}
        <WorkflowChart
          workflow={current}
          page="repository"
          name={name}
          doors={{
            ...doors,
            // The settings that set a step are Landing and Sessions, on this page.
            setup: (section) => {
              if (section === 'work') setAsked('work');
              else if (section === 'sessions') document.getElementById('place-sessions')?.scrollIntoView?.({ block: 'start' });
            },
          }}
        />
        {current.start && (
          <PlaceGates
            landed={current.start.landed}
            integration={current.start.integration ?? null}
            busy={accepting}
            onAcceptLanded={onAcceptGates && current.start.landed.workflow !== 'current' ? onAcceptGates : undefined}
            onAcceptIntegration={onAcceptIntegration}
          />
        )}
        {landing?.(asked)}
        <section aria-labelledby={`${headId}-steps`} className="grid min-w-0 gap-2">
          <h3 id={`${headId}-steps`} className="m-0 text-body font-semibold text-ink">{t('workflow.place.steps.title')}</h3>
          {editor && panel?.is === 'edit'
            ? <WorkflowEditor {...editor} place={place} startFrom={panel.from} onClose={() => show(null)} />
            : editor && panel?.is === 'change'
              ? (
                <ChangeWorkflow
                  list={editor.list}
                  error={editor.error}
                  current={current}
                  place={place}
                  using={editor.using}
                  onUse={editor.onUse}
                  onStartFrom={(from) => show({ is: 'edit', from })}
                  onClose={() => show(null)}
                />
              )
              : (
                <div className="grid min-w-0 gap-2">
                  <Prose className="text-small">{t('workflow.place.steps.body')}</Prose>
                  {editor && (
                    <div className="flex flex-wrap gap-1.5">
                      <Button variant="ghost" className="border border-line" onClick={() => show({ is: 'change' })}>{t('workflow.place.change.open')}</Button>
                      <Button variant="ghost" className="border border-line" onClick={() => show({ is: 'edit' })}>{t('workflow.place.steps.open')}</Button>
                    </div>
                  )}
                  <p className="m-0 text-meta text-ink-faint"><Inline text={t('workflow.compose.twin')} /></p>
                </div>
              )}
        </section>
      </>
    );

  return (
    <section aria-labelledby="place-workflow" className="mt-5 grid min-w-0 gap-5">
      <h2 id="place-workflow" className="m-0 text-body font-medium text-ink">{t('agents.setup.workflow.title')}</h2>
      {body}
    </section>
  );
}
