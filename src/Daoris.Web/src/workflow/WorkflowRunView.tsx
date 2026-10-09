import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ago } from '../format';
import { ExternalLink } from '../links';
import { Button, Icon, Inline, Pill, Prose, SkeletonRows } from '../ui';
import type { CurrentWorkflow } from './current';
import { type RunStep, runTone, type WorkflowRun, type WorkflowRunAnswer } from './run';
import { addedSaid, runSaid, runShort, stateSaid } from './runSaid';
import { type RunMarks, WorkflowChart } from './WorkflowChart';

/** Where a step's door leads (design §7: *a step's door opens where its record is*). */
export type RunDoors = {
  /** A session, attended in Sessions: the work's, the reviewer's, the set-up step's. */
  session?: (id: string) => void;
  /** A quest's page: the work's, the set-up step's (where its review is answered), the one its taker waits on. */
  quest?: (id: string) => void;
  /** The ask's page, where its go-aheads are answered. */
  ask?: (id: string) => void;
  /** A session's review, where its work is accepted. */
  review?: (session: string) => void;
};

/** The run as a chart's workflow: its Current, each step as the driver drew it, a step the work added among them. */
export const runWorkflow = (run: WorkflowRun): CurrentWorkflow => ({
  repository: run.repository,
  workspace: run.workspace,
  startHolds: run.workflow.startHolds,
  steps: run.steps.map((step) => step.step),
  version: run.workflow.version,
  limits: run.workflow.limits,
});

/** The person's words a step keeps, shown as written: a skip's and a *not yet*'s (D154). */
const QUOTES: ReadonlySet<string> = new Set(['look.skip', 'look.not-yet']);

/**
 * One step's place in its run (design §5.2, §7): its state on its pill, *this session* where the attended session is its own,
 * when it came to stand there, what that state says, the person's words where it keeps them, why a step Current does not draw is
 * here, the owner's control where it waits, and the door to where its record is.
 */
function StepMark({ run, step, here, doors, control }: {
  run: WorkflowRun;
  step: RunStep;
  here: boolean;
  doors?: RunDoors;
  control?: ReactNode;
}) {
  const { t } = useTranslation();
  const said = runSaid(t, step);
  const added = addedSaid(t, step);
  const kind = step.step.kind;
  const quoted = QUOTES.has(`${kind}.${step.detail}`) && step.words ? step.words : null;
  const door = doorOf(t, run, step, doors);
  // The second opinion's gate holding the work on the person (XAGENT1f): its owner's control draws *Go on anyway…* under the step
  // (XAGENT1g), where the frame hands it, the attended session's own run; elsewhere its twins are said, the session's own page
  // and the terminal's, and the door opens the review, where the gate's presses stand.
  const twin = !control && kind === 'opinion' && step.state === 'waiting-on-you' && step.session
    ? t('workflow.run.opinionTwin', { session: step.session })
    : null;

  return (
    <div className="mt-1 grid min-w-0 gap-1">
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        <Pill tone={runTone(step.state)}>{stateSaid(t, step.state)}</Pill>
        {here && (
          <span className="rounded-full border border-line-strong px-2 py-px text-meta text-ink-soft">{t('workflow.run.thisSession')}</span>
        )}
        {step.at && step.state !== 'not-reached' && <span className="font-mono text-meta text-ink-faint">{ago(step.at)}</span>}
      </p>
      {said && <p className="m-0 text-small text-ink-soft"><Inline text={said} /></p>}
      {quoted && <p className="m-0 text-small text-ink">{t('workflow.run.quoted', { words: quoted })}</p>}
      {added && <p className="m-0 text-meta text-ink-faint">{added}</p>}
      {control}
      {twin && <p className="m-0 text-meta text-ink-faint"><Inline text={twin} /></p>}
      {door && <div className="flex flex-wrap items-center gap-1.5">{door}</div>}
    </div>
  );
}

/**
 * A step's door (design §7): where its record is. The work's session, or the ask where a go-ahead waits, or the quest its taker
 * waits on; the reviewer's session; the set-up step's page, where the review is answered; the session's review, where its work
 * is accepted; the pull request's address, outside the window as any link opens. A step not reached has none.
 */
function doorOf(t: ReturnType<typeof useTranslation>['t'], run: WorkflowRun, step: RunStep, doors?: RunDoors): ReactNode {
  if (step.state === 'not-reached') return null;
  const ghost = 'min-h-0 px-1.5 py-0.5 text-small';
  const button = (label: string, open: () => void) => <Button variant="ghost" className={ghost} onClick={open}>{label}</Button>;
  const kind = step.step.kind;

  if (kind === 'work' && step.detail === 'go-ahead' && run.ask && doors?.ask) {
    return button(t('workflow.run.door.ask'), () => doors.ask?.(run.ask!));
  }
  if (kind === 'work' && ['awaits', 'queued', 'unclosed', 'declined'].includes(step.detail) && step.quest && doors?.quest) {
    return button(t('workflow.run.door.quest', { id: step.quest }), () => doors.quest?.(step.quest!));
  }
  // The gate holds the work on the person: its presses stand beside the review's Accept (XAGENT1f's `LANDING` answers it there).
  if (kind === 'opinion' && step.state === 'waiting-on-you' && step.session && doors?.review) {
    return button(t('workflow.run.door.review'), () => doors.review?.(step.session!));
  }
  if ((kind === 'work' || kind === 'opinion') && step.session && doors?.session) {
    return button(t('workflow.run.door.session'), () => doors.session?.(step.session!));
  }
  if (kind === 'look' && step.quest && doors?.quest) {
    return button(t('workflow.run.door.quest', { id: step.quest }), () => doors.quest?.(step.quest!));
  }
  if (kind === 'landing' && ['waiting-on-you', 'cannot-start'].includes(step.state) && step.session && doors?.review) {
    return button(t('workflow.run.door.review'), () => doors.review?.(step.session!));
  }
  if (kind === 'pull-request' && step.pullRequest) {
    return (
      <ExternalLink href={step.pullRequest} className="inline-flex items-center gap-1 px-1.5 text-small text-accent">
        {t('workflow.run.door.pullRequest')}
        <Icon name="external" size={12} />
      </ExternalLink>
    );
  }
  return null;
}

/** One run: what it follows, where it stands in a few words, then its chart with each step's place. */
function Run({ run, here, doors, controls, named }: {
  run: WorkflowRun;
  here?: string | null;
  doors?: RunDoors;
  controls?: Partial<Record<string, ReactNode>>;
  /** Several runs are drawn, so each says its repository first. */
  named: boolean;
}) {
  const { t } = useTranslation();
  const at = run.at ?? null;
  const reached = new Set(run.steps.filter((step) => step.state !== 'not-reached').map((step) => step.step.id));
  const marks: RunMarks = Object.fromEntries(run.steps.map((step) => [step.step.id, {
    reached: reached.has(step.step.id),
    at: step.step.id === at,
    mark: (
      <StepMark
        run={run}
        step={step}
        here={here === step.step.id}
        doors={doors}
        control={step.step.id === at ? controls?.[step.step.id] : undefined}
      />
    ),
  }]));
  const says = runShort(t, run);

  return (
    // The chart names itself by the repository (`workflow.chart`), so the run's own box needs no second name.
    <div className="grid min-w-0 gap-3">
      <div className="grid min-w-0 gap-0.5">
        <Prose className="text-ink">
          <Inline text={named ? t('workflow.run.lineIn', { repository: run.repository, says }) : t('workflow.run.line', { says })} />
        </Prose>
        <p className="m-0 text-meta text-ink-faint">{t('workflow.run.head', { repository: run.repository, version: run.workflow.version })}</p>
      </div>
      <WorkflowChart
        workflow={runWorkflow(run)}
        page="repository"
        name={run.repository}
        run={marks}
        end={at === null ? <Pill tone="done">{stateSaid(t, 'done')}</Pill> : undefined}
      />
    </div>
  );
}

/**
 * **Where a piece of work stands in its workflow** (WORKFLOW1c; D157 point 12, the workflow design §5.2, §7): the session's side
 * bar view *Workflow*, its run drawn on the chart of the workflow it follows, each step where it stands and *this session*
 * marked on its own.
 *
 * @remarks
 * **Derived, never inferred from the conversation** (design §5.1, §7): the driver reads each step from the store that keeps
 * it, and this draws the answer. Its one home is this view; a quest's and an ask's pages carry one line whose door opens it.
 *
 * **The presses are their owners'** (design §7): where the step the run stands at waits on a press, its owner's control is
 * handed in (`controls`, by the step's id) and drawn under it, so no press has a second implementation; every other step's
 * door opens where its record is.
 *
 * **Hues** (design §7, platform-ux §3): waiting on the person in open's hue, theirs alone; a wait on an agent or on Daoris
 * neutral; done's green for a step done; red only for a failure. The person's part stays the rail's shape and its word,
 * never a hue (D157 §6.2).
 *
 * **An ask's work in several repositories is several runs** (design §3.8), each at its own step, drawn one after another.
 *
 * A molecule: the answer arrives as a prop, and every door goes out.
 */
export function WorkflowRunView({ answer, reading = false, refusal = null, here = null, doors, controls }: {
  /** What the shell answered; absent while it is read, or where it was refused. */
  answer?: WorkflowRunAnswer | null;
  reading?: boolean;
  /** The read was refused, in the driver's sentence that says why. */
  refusal?: string | null;
  /** The step the attended session is its own: marked *this session*. */
  here?: string | null;
  doors?: RunDoors;
  /** Each owner's control where the step the run stands at waits on a press, by the step's id. */
  controls?: Partial<Record<string, ReactNode>>;
}) {
  const { t } = useTranslation();

  if (!answer) {
    return (
      <div aria-busy={reading || undefined} className="grid gap-2">
        <Prose className="text-small"><Inline text={refusal ?? t('workflow.run.reading')} /></Prose>
        {!refusal && <SkeletonRows rows={4} />}
      </div>
    );
  }

  // No run: the driver's sentence where it gave one (a chat serves no quest), shown as it said it.
  if (answer.runs.length === 0) {
    return <Prose className="text-small"><Inline text={answer.problem ?? t('workflow.run.none')} /></Prose>;
  }

  return (
    <div className="grid min-w-0 gap-6">
      {answer.runs.map((run) => (
        <Run
          key={`${run.repository}:${run.quests[0] ?? ''}`}
          run={run}
          here={here}
          doors={doors}
          controls={controls}
          named={answer.runs.length > 1}
        />
      ))}
    </div>
  );
}
