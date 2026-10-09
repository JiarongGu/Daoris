import { type KeyboardEvent, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, Icon, type IconName, Inline, Tip } from '../ui';
import { aroundTheLanding, setupDoorOf, textOf, type CurrentWorkflow, type WorkflowParticipation, type WorkflowStep } from './current';
import { edgeSaid, executorSaid, holdsSaid, limitSaid, sourceSaid, stepSet, stepTitle } from './said';

/** Where a step's door leads: this page's own Setup at a section, the workspace's Setup, or a repository's own workflow. */
export type WorkflowDoors = {
  /** This page's Setup, at the section that sets the step: a repository's `work` or `sessions`, a workspace's `defaults`. */
  setup?: (section: 'work' | 'sessions' | 'defaults') => void;
  /** A repository's workspace's Setup, from a step its workspace set. */
  workspaceSetup?: () => void;
  /** A repository's own workflow, from its workspace's page. */
  repository?: (repository: string) => void;
};

/** A step's kind as its glyph: what it is, beside the rail's mark for who takes part. */
const GLYPH: Record<string, IconName> = {
  work: 'stepWork',
  opinion: 'stepOpinion',
  look: 'stepLook',
  'pull-request': 'stepPullRequest',
};

/** A row on the rail: the rail's column, and what it carries beside it. */
const RAIL_ROW = 'grid grid-cols-[1.25rem_minmax(0,1fr)] gap-x-3';

const glyphOf = (step: WorkflowStep): IconName =>
  step.kind === 'landing' ? (textOf(step, 'form') === 'branch' ? 'stepBranch' : 'stepMerge') : GLYPH[step.kind] ?? 'tool';

/**
 * **The rail's mark for the person's part** (the workflow design §3.1, §6.2): a shape for each, so who takes part scans
 * down the rail before a word is read, and the word beside the title says it to everyone the shape does not. Hollow, an
 * agent alone; half, an agent and the person; whole, the person; a diamond, automatic. It is drawn in ink and wears no
 * status hue: a participation is a declaration, and only a run's state may (design §3.1, §7).
 */
export function PartMark({ participation, className }: { participation: WorkflowParticipation; className?: string }) {
  return (
    <svg viewBox="0 0 14 14" width="14" height="14" aria-hidden className={cn('shrink-0 text-ink-soft', className)}>
      {participation === 'automatic'
        ? <path d="M7 1.5 12.5 7 7 12.5 1.5 7Z" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinejoin="round" />
        : <circle cx="7" cy="7" r="5.3" fill={participation === 'you' ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth="1.4" />}
      {participation === 'agent-and-you' && <path d="M7 1.7a5.3 5.3 0 0 0 0 10.6Z" fill="currentColor" />}
    </svg>
  );
}

/** The end of the line: a ring with its centre, unlike every participation's mark. */
function EndMark() {
  return (
    <svg viewBox="0 0 14 14" width="14" height="14" aria-hidden className="shrink-0 text-ink-soft">
      <circle cx="7" cy="7" r="5.3" fill="none" stroke="currentColor" strokeWidth="1.4" />
      <circle cx="7" cy="7" r="2.4" fill="currentColor" />
    </svg>
  );
}

/**
 * The rail below a mark: ink, since an edge is content (platform-ux §3), ending in its head where the next step starts, or
 * running on unbroken into a section title that the next step follows.
 */
function RailLine({ from = 'mark', into = 'step' }: { from?: 'mark' | 'title'; into?: 'step' | 'title' }) {
  return (
    <>
      <span className={cn('w-px flex-1 bg-ink-faint', from === 'mark' && 'mt-1')} />
      {into === 'step' && (
        <svg viewBox="0 0 8 5" width="8" height="5" className="mb-1 shrink-0 text-ink-faint">
          <path d="M0 0h8L4 5Z" fill="currentColor" />
        </svg>
      )}
    </>
  );
}

/** Rows that move with ↑ and ↓ (design §6.4): the step rows of the whole chart, before and after the landing. */
function moveAlong(event: KeyboardEvent<HTMLElement>) {
  if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
  const row = (event.target as HTMLElement).closest<HTMLElement>('[data-step]');
  const chart = event.currentTarget;
  if (!row || row !== event.target) return;
  const rows = [...chart.querySelectorAll<HTMLElement>('[data-step]')];
  const next = rows[rows.indexOf(row) + (event.key === 'ArrowDown' ? 1 : -1)];
  event.preventDefault();
  next?.focus();
}

/**
 * **One step** (design §6.2): its kind's glyph and title, the person's part as a word and who acts; then what it is set
 * to; where that was set, with a door to the Setup that sets it, and how much of it this build runs; its limit whole,
 * never folded away; and at its foot, beside the rail, what moves work on to the next.
 */
function StepRow({ step, first, into, page, workspace, limits, doors }: {
  step: WorkflowStep;
  /** The first row the keys stop at: Tab lands here, and ↑ and ↓ go on from it. */
  first: boolean;
  /** What its rail runs into: the next step, or the section title the next step follows. */
  into: 'step' | 'title';
  page: 'repository' | 'workspace';
  workspace: string;
  limits: Record<string, string>;
  doors?: WorkflowDoors;
}) {
  const { t } = useTranslation();
  const titleId = useId();
  const set = stepSet(t, step);
  const limit = limitSaid(t, step, limits);
  const where = setupDoorOf(step, page);
  const press = where?.where === 'workspace' && page === 'repository'
    ? doors?.workspaceSetup && { label: t('workflow.door.workspaceSetup'), open: doors.workspaceSetup }
    : where && doors?.setup && { label: t('workflow.door.setup'), open: () => doors.setup?.(where.section) };
  const standing = step.kind === 'work' ? textOf(step, 'standing') : null;

  return (
    <li
      data-step={step.id}
      aria-labelledby={titleId}
      tabIndex={first ? 0 : -1}
      className={cn(RAIL_ROW, 'rounded-control')}
    >
      <div aria-hidden className="flex flex-col items-center">
        <span className="flex h-5 items-center"><PartMark participation={step.participation} /></span>
        <RailLine into={into} />
      </div>
      <div className="min-w-0 pb-1">
        <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
          <h4 id={titleId} className="m-0 inline-flex min-w-0 items-baseline gap-1.5 text-body font-semibold text-ink">
            <Icon name={glyphOf(step)} size={14} className="translate-y-0.5 text-ink-soft" />
            <span className="min-w-0"><Inline text={stepTitle(t, step)} /></span>
          </h4>
          <span className="rounded-full border border-line-strong px-2 py-px text-meta text-ink-soft">
            {t(`workflow.participation.${step.participation}`)}
          </span>
          <span className="min-w-0 text-small text-ink-soft"><Inline text={executorSaid(t, step)} /></span>
        </div>
        {set && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={set} /></p>}
        {standing && (
          <p className="m-0 mt-1 text-small text-ink-soft">
            {t('workflow.set.standing', { says: standing })}
            {doors?.setup && page === 'repository' && (
              <Button variant="ghost" className="ml-1 px-1.5 py-0 text-small" onClick={() => doors.setup?.('sessions')}>
                {t('workflow.door.setup')}
              </Button>
            )}
          </p>
        )}
        <p className="m-0 mt-1 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-meta text-ink-faint">
          <span className="min-w-0">{sourceSaid(t, step, page, workspace)}</span>
          <Tip content={t(`workflow.runtimeTip.${step.runtime}`)}>
            <span className="text-ink-soft">{t(`workflow.runtime.${step.runtime}`)}</span>
          </Tip>
          {press && (
            <Button variant="ghost" className="px-1.5 py-0 text-small" onClick={press.open}>{press.label}</Button>
          )}
        </p>
        {limit && (
          <p className="m-0 mt-2 border-l-[3px] border-warn bg-raised px-3 py-1.5 text-small text-ink-soft">
            <Inline text={limit.text} />
            {limit.recorded && <span className="ml-1.5 text-meta text-ink-faint">{t('workflow.limit.recorded')}</span>}
          </p>
        )}
        <p data-edge className="m-0 mb-1 mt-2 text-meta text-ink-soft">{edgeSaid(t, step)}</p>
      </div>
    </li>
  );
}

/**
 * **A workflow drawn as a flowchart** (WORKFLOW1b; D157 point 12, the workflow design §6.2, §6.4): a vertical list drawn as
 * a graph, laid out by Daoris with no free positioning, no panning and no zoom, since the question it answers is *what
 * happens next* and a list answers it at every width.
 *
 * @remarks
 * **Steps are nodes and the rail is the edges.** The rail's mark says who takes part, a shape each (`PartMark`), and each
 * edge says what moves work on: the person's press, or automatically. The plugins that may hold a start are drawn above
 * the work, never as a step (design §3.2). *Before it lands* and *After it lands* are small section titles, and the landing
 * is the hinge between them: last before it.
 *
 * **Read-only.** Current is the rules as they stand, so a step's door opens the Setup that sets it (design §4.7) and
 * nothing here edits.
 *
 * **For a reader and a keyboard** (design §6.4): each part is a list, each step a row named by its title, its words in
 * the order a reader hears them; ↑ and ↓ move between steps across both lists, and Tab reaches each row's doors.
 *
 * **Narrow**: nothing has a fixed width. A title, its chip and who acts wrap as one line of words, a path or a command
 * breaks at its separators, and at the main area's floor the chip goes under the title.
 *
 * A molecule: the workflow arrives answered, and every door goes out.
 */
export function WorkflowChart({ workflow, page, name, doors }: {
  workflow: CurrentWorkflow;
  page: 'repository' | 'workspace';
  /** The repository or the workspace whose work it draws, which names the chart for a reader. */
  name: string;
  doors?: WorkflowDoors;
}) {
  const { t } = useTranslation();
  const beforeId = useId();
  const afterId = useId();
  const { before, after } = aroundTheLanding(workflow.steps);
  const holds = holdsSaid(t, workflow.startHolds);
  const row = (step: WorkflowStep, first: boolean, into: 'step' | 'title') => (
    <StepRow
      key={step.id}
      step={step}
      first={first}
      into={into}
      page={page}
      workspace={workflow.workspace}
      limits={workflow.limits}
      doors={doors}
    />
  );

  return (
    <div role="group" aria-label={t('workflow.chart', { name })} onKeyDown={moveAlong} className="min-w-0">
      <h3 id={beforeId} className="m-0 mb-2 text-small font-semibold text-ink-faint">{t('workflow.section.before')}</h3>
      {holds && (
        <div className={RAIL_ROW}>
          <div aria-hidden className="flex flex-col items-center">
            <span className="flex h-5 items-center"><Icon name="stepHold" size={14} className="text-ink-soft" /></span>
            <RailLine />
          </div>
          <p className="m-0 mb-3 text-small text-ink-soft"><Inline text={holds} /></p>
        </div>
      )}
      <ol aria-labelledby={beforeId} className="m-0 list-none p-0">
        {before.map((step, index) => row(step, index === 0, index === before.length - 1 && after.length > 0 ? 'title' : 'step'))}
      </ol>
      {after.length > 0 && (
        <>
          {/* The hinge: the landing's rail runs on through the title into the first step after it. */}
          <div className={RAIL_ROW}>
            <div aria-hidden className="flex flex-col items-center"><RailLine from="title" /></div>
            <h3 id={afterId} className="m-0 pb-2 pt-1 text-small font-semibold text-ink-faint">{t('workflow.section.after')}</h3>
          </div>
          <ol aria-labelledby={afterId} className="m-0 list-none p-0">{after.map((step) => row(step, false, 'step'))}</ol>
        </>
      )}
      <div className={RAIL_ROW}>
        <span aria-hidden className="flex h-5 items-center justify-center"><EndMark /></span>
        <p className="m-0 inline-flex items-baseline gap-1.5 text-body font-semibold text-ink">
          <Icon name="stepFinished" size={14} className="translate-y-0.5 text-ink-soft" />
          {t('workflow.finished')}
        </p>
      </div>
    </div>
  );
}
