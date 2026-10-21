import { type KeyboardEvent, type ReactNode, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Flow, type FlowNode, type PillTone } from '../design/Flow';
import { Button, Icon, Inline, Tip } from '../design/ui';
import { aroundTheLanding, setupDoorOf, textOf, type CurrentWorkflow, type WorkflowParticipation, type WorkflowStep } from './current';
import { stateSaid } from './runSaid';
import { edgeSaid, executorSaid, holdsSaid, limitSaid, sourceSaid, stepSet, stepTitle } from './said';
import { type FlowLit, glyphOf, outcomeOf, outcomeSaid, WORKFLOW_TRACK, workflowFlow } from './workflowFlow';

/** Where a step's door leads: this page's own Setup at a section, the workspace's Setup, or a repository's own workflow. */
export type WorkflowDoors = {
  /** This page's Setup, at the section that sets the step: a repository's `work` or `sessions`, a workspace's `defaults`. */
  setup?: (section: 'work' | 'sessions' | 'defaults') => void;
  /** A repository's workspace's Setup, from a step its workspace set. */
  workspaceSetup?: () => void;
  /** A repository's own workflow, from its workspace's page. */
  repository?: (repository: string) => void;
};

/**
 * Where each step of a run stands (WORKFLOW1c, design §7), drawn on the same chart in place of what each step is set to: a
 * step's mark, whether the work has come to it (a step not reached is drawn faint), and whether the run stands at it. Its node
 * on the flowchart (FLOWCHART1d) wears the state's word and tone as its pill, says whether the step is done, holds the mark's
 * lines without that pill, and Enter opens its door.
 */
export type RunMarks = Record<string, {
  mark: ReactNode;
  reached: boolean;
  at?: boolean;
  state?: { word: string; tone: PillTone };
  settled?: boolean;
  node?: ReactNode;
  open?: () => void;
}>;

/** A row on the rail: the rail's column, and what it carries beside it. */
const RAIL_ROW = 'grid grid-cols-[1.25rem_minmax(0,1fr)] gap-x-3';

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

/** The door to the Setup that sets a step: this page's own at its section, or a repository's workspace's for a step it set. */
function setupPress(
  t: ReturnType<typeof useTranslation>['t'], step: WorkflowStep, page: 'repository' | 'workspace', doors?: WorkflowDoors,
): { label: string; open: () => void } | null {
  const where = setupDoorOf(step, page);
  if (where?.where === 'workspace' && page === 'repository') {
    return doors?.workspaceSetup ? { label: t('workflow.door.workspaceSetup'), open: doors.workspaceSetup } : null;
  }
  return where && doors?.setup ? { label: t('workflow.door.setup'), open: () => doors.setup?.(where.section) } : null;
}

/** What follows a step's failure or its refusal, where the step names one (FLOWCHART1d): the rail's words for the flowchart's path. */
function FailurePath({ step }: { step: WorkflowStep }) {
  const { t } = useTranslation();
  const outcome = outcomeOf(step);
  if (!outcome) return null;
  return <p data-failed="" className="m-0 mt-1 text-meta text-ink-soft"><Inline text={outcomeSaid(t, outcome)} /></p>;
}

/**
 * **One step** (design §6.2): its kind's glyph and title, the person's part as a word and who acts; then what it is set
 * to; where that was set, with a door to the Setup that sets it, and how much of it this build runs; its limit whole,
 * never folded away; and at its foot, beside the rail, what moves work on to the next.
 */
function StepRow({ step, first, into, page, workspace, limits, doors, run }: {
  step: WorkflowStep;
  /** The first row the keys stop at: Tab lands here, and ↑ and ↓ go on from it. */
  first: boolean;
  /** What its rail runs into: the next step, or the section title the next step follows. */
  into: 'step' | 'title';
  page: 'repository' | 'workspace';
  workspace: string;
  limits: Record<string, string>;
  doors?: WorkflowDoors;
  /** Where the step stands in a run (WORKFLOW1c): drawn in place of what it is set to, where it was set and its edge. */
  run?: RunMarks[string];
}) {
  const { t } = useTranslation();
  const titleId = useId();
  const limit = limitSaid(t, step, limits);
  // A step the work has not come to keeps its words at a token ink, never faint by opacity (the flowchart design §3.3, trap 9).
  const faint = run !== undefined && !run.reached;

  return (
    <li
      data-step={step.id}
      data-reached={faint ? 'false' : undefined}
      aria-labelledby={titleId}
      // The step a run stands at (design §7), which a door into the run opens on.
      aria-current={run?.at ? 'step' : undefined}
      tabIndex={first ? 0 : -1}
      className={cn(RAIL_ROW, 'rounded-control')}
    >
      <div aria-hidden className="flex flex-col items-center">
        <span className="flex h-5 items-center"><PartMark participation={step.participation} /></span>
        <RailLine into={into} />
      </div>
      <div className="min-w-0 pb-1">
        <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
          <h4 id={titleId} className={cn('m-0 inline-flex min-w-0 items-baseline gap-1.5 text-body font-semibold', faint ? 'text-ink-soft' : 'text-ink')}>
            <Icon name={glyphOf(step)} size={14} className="translate-y-0.5 text-ink-soft" />
            <span className="min-w-0"><Inline text={stepTitle(t, step)} /></span>
          </h4>
          <span className="rounded-full border border-line-strong px-2 py-px text-meta text-ink-soft">
            {t(`workflow.participation.${step.participation}`)}
          </span>
          <span className="min-w-0 text-small text-ink-soft"><Inline text={executorSaid(t, step)} /></span>
        </div>
        {run
          ? (
            <>
              {run.mark}
              <RunLimit limit={limit} />
              <FailurePath step={step} />
              <span aria-hidden className="block h-1.5" />
            </>
          )
          : <StepSettings step={step} page={page} workspace={workspace} limit={limit} doors={doors} edge />}
      </div>
    </li>
  );
}

/** A run's step's limit, never folded away, on its warning rail. */
function RunLimit({ limit }: { limit: { text: string; recorded: boolean } | null }) {
  const { t } = useTranslation();
  if (!limit) return null;
  return (
    <p className="m-0 mt-1.5 border-l-[3px] border-warn bg-raised px-2.5 py-1 text-meta text-ink-soft">
      <Inline text={limit.text} />
      {limit.recorded && <span className="ml-1.5 text-ink-faint">{t('workflow.limit.recorded')}</span>}
    </p>
  );
}

/**
 * What a step is set to, where that was set with its door, how much of it this build runs, its limit, and on the rail its edge
 * and its failure path: the flowchart's node says those in its foot, as its edges.
 */
function StepSettings({ step, page, workspace, limit, doors, edge }: {
  step: WorkflowStep;
  page: 'repository' | 'workspace';
  workspace: string;
  limit: { text: string; recorded: boolean } | null;
  doors?: WorkflowDoors;
  /** Say the edge to the next and the failure path in words: the rail's, which draws no edge. */
  edge: boolean;
}) {
  const { t } = useTranslation();
  const set = stepSet(t, step);
  const standing = step.kind === 'work' ? textOf(step, 'standing') : null;
  const press = setupPress(t, step, page, doors);
  return (
    <>
      {set && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={set} /></p>}
      {standing && (
        <p className="m-0 mt-1 text-small text-ink-soft">
          {t('workflow.set.standing', { says: standing })}
          {doors?.setup && page === 'repository' && (
            <Button variant="ghost" className="ml-1 min-h-0 px-1.5 py-0 text-small" onClick={() => doors.setup?.('sessions')}>
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
          <Button variant="ghost" className="min-h-0 px-1.5 py-0 text-small" onClick={press.open}>{press.label}</Button>
        )}
      </p>
      {limit && (
        <p className="m-0 mt-2 border-l-[3px] border-warn bg-raised px-3 py-1.5 text-small text-ink-soft">
          <Inline text={limit.text} />
          {limit.recorded && <span className="ml-1.5 text-meta text-ink-faint">{t('workflow.limit.recorded')}</span>}
        </p>
      )}
      {edge && <p data-edge className="m-0 mb-1 mt-2 text-meta text-ink-soft">{edgeSaid(t, step)}</p>}
      {edge && <FailurePath step={step} />}
    </>
  );
}

/** Who acts in a step's node (FLOWCHART1d): the person's part by its shape and its word, and the executor, names as code. */
function Who({ step }: { step: WorkflowStep }) {
  const { t } = useTranslation();
  return (
    <span className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
      <span className="inline-flex items-center gap-1.5 rounded-full border border-line-strong px-2 py-px text-meta text-ink-soft">
        <PartMark participation={step.participation} />
        {t(`workflow.participation.${step.participation}`)}
      </span>
      <span className="min-w-0"><Inline text={executorSaid(t, step)} /></span>
    </span>
  );
}

/** The plugins that may hold a start, drawn above the work and never as a step (design §3.2). */
function Holds({ holds }: { holds: string }) {
  return (
    <p className="m-0 flex min-w-0 items-start gap-1.5 text-small text-ink-soft">
      <Icon name="stepHold" size={14} className="mt-0.5 shrink-0 text-ink-soft" />
      <span className="min-w-0"><Inline text={holds} /></span>
    </p>
  );
}

/**
 * **The rail** (WORKFLOW1b; the workflow design §6.2, §6.4): a vertical list, the flowchart's narrow placement (FLOWCHART1d, the
 * flowchart design §5.1): the same marks, words, edge line, ↑/↓ and section titles. Steps are nodes and the rail is the edges.
 * The rail's mark says who takes part, a shape each (`PartMark`), and each edge says what moves work on: the person's press, or
 * automatically. The plugins that may hold a start are drawn above the work, never as a step (design §3.2). *Before it lands* and
 * *After it lands* are small section titles, and the landing is the hinge between them: last before it.
 *
 * **For a reader and a keyboard** (design §6.4): each part is a list, each step a row named by its title, its words in the order
 * a reader hears them; ↑ and ↓ move between steps across both lists, and Tab reaches each row's doors.
 */
function WorkflowRail({ workflow, page, name, doors, run, end }: {
  workflow: CurrentWorkflow;
  page: 'repository' | 'workspace';
  name: string;
  doors?: WorkflowDoors;
  run?: RunMarks;
  end?: ReactNode;
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
      run={run?.[step.id]}
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
        <p className="m-0 inline-flex flex-wrap items-baseline gap-1.5 text-body font-semibold text-ink">
          <Icon name="stepFinished" size={14} className="translate-y-0.5 text-ink-soft" />
          {t('workflow.finished')}
          {end}
        </p>
      </div>
    </div>
  );
}

/**
 * **A workflow drawn as a flowchart** (WORKFLOW1b, FLOWCHART1d; D157 point 12 as D186 amends it, the flowchart design §2.4, §2.5,
 * §5.1): a layered graph laid out by Daoris on `Flow`, top to bottom where the pane holds a step's 18rem, with the rail, today's
 * list, as its narrow placement at the side bar's floor and wherever the person chose *Show as list*. No free positioning, no
 * panning and no zoom (D157's rejection stands).
 *
 * @remarks
 * **Its steps in their version's order** (`workflowFlow`): each a node with its kind's glyph and title, the person's part by its
 * shape and word and who acts, then what it is set to and where, its limit whole; each edge the move its step names, in the
 * node's foot. A step that names what follows its failure or its refusal hangs that outcome on a dashed failure path. *Before it
 * lands* heads the drawing, with the plugins that may hold a start, and *After it lands* divides it where the landing hands on.
 * Today's answer holds no group, so none is drawn until a decoded graph is read.
 *
 * **Read-only.** Current is the rules as they stand, so a step's door opens the Setup that sets it (design §4.7), Enter on its
 * node too, and nothing here edits.
 *
 * **A run is drawn on the same chart** (WORKFLOW1c, design §7; the flowchart design §2.5): handed `run`, each node wears where its
 * step stands, the one the run stands at current, one the work has not come to dashed, the end reached once the run finished; no
 * path is drawn, since a run keeps where each step stands now and not the order the work went through.
 *
 * A molecule: the workflow arrives answered, and every door goes out.
 */
export function WorkflowChart({ workflow, page, name, doors, run, end, finished = false }: {
  workflow: CurrentWorkflow;
  page: 'repository' | 'workspace';
  /** The repository or the workspace whose work it draws, which names the chart for a reader. */
  name: string;
  doors?: WorkflowDoors;
  /** Where each step of a run stands, by its id: the chart then draws the run. */
  run?: RunMarks;
  /** What stands beside the end of the line on the rail: a run's own finish. */
  end?: ReactNode;
  /** A run's: it finished, so its end is reached. */
  finished?: boolean;
}) {
  const { t } = useTranslation();
  const lit: FlowLit | undefined = run && {
    steps: Object.fromEntries(workflow.steps.flatMap((step) => {
      const mark = run[step.id];
      if (!mark) return [];
      return [[step.id, {
        state: mark.state ?? { word: '', tone: 'neutral' as const }, reached: mark.reached, current: mark.at === true, settled: mark.settled === true,
      }]];
    })),
    finished: finished ? { word: stateSaid(t, 'done'), tone: 'done' } : null,
  };
  const flow = workflowFlow(t, workflow, name, lit);
  const steps = new Map(workflow.steps.map((step) => [step.id, step]));
  const nodes = flow.nodes.map((node): FlowNode => {
    const step = steps.get(node.id);
    if (!step) return node;
    const limit = limitSaid(t, step, workflow.limits);
    const mark = run?.[step.id];
    const press = mark ? null : setupPress(t, step, page, doors);
    const open = mark ? mark.open : press?.open;
    return {
      ...node,
      who: <Who step={step} />,
      lines: mark
        ? <div className="grid min-w-0">{mark.node ?? mark.mark}<RunLimit limit={limit} /></div>
        : <StepSettings step={step} page={page} workspace={workflow.workspace} limit={limit} doors={doors} edge={false} />,
      ...(open ? { open } : {}),
    };
  });
  const holds = holdsSaid(t, workflow.startHolds);
  const sections = flow.sections.map((section, index) => (index === 0 && holds ? { ...section, note: <Holds holds={holds} /> } : section));

  return (
    <Flow
      label={flow.label}
      nodes={nodes}
      edges={flow.edges}
      entries={flow.entries}
      sections={sections}
      track={WORKFLOW_TRACK}
      rail={<WorkflowRail workflow={workflow} page={page} name={name} doors={doors} run={run} end={end} />}
    />
  );
}
