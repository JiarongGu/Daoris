import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ago } from '../locales/format';
import { ExternalLink } from '../design/links';
import { Button, Icon, Inline, Pill, Prose, SkeletonRows } from '../design/ui';
import type { CurrentWorkflow } from './current';
import { NodeWord } from './NodeWord';
import { type NodeWordAnswer, type NodeWordAsk, type RunStep, runTone, type WorkflowRun, type WorkflowRunAnswer } from './run';
import { whySaid } from './routeSaid';
import { addedSaid, chatChecksSaid, graphSaid, noRunSaid, runSaid, runShort, stateSaid } from './runSaid';
import { type RunMarks, WorkflowChart } from './WorkflowChart';
import { WorkflowHold } from './WorkflowHold';

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
  /** A landed branch's page in its repository, with how it landed (FLOWCHART1c). */
  branch?: (repository: string, branch: string) => void;
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

/** The person's words a graph run's node keeps (FABRIC-CUTOVER1e): a skip's, a refusal's and a stop's, by its record's state. */
const NODE_QUOTES: ReadonlySet<string> = new Set(['skipped', 'rejected', 'stopped']);

/**
 * One step's place in its run (design §5.2, §7): its state on its pill, *this session* where the attended session is its own,
 * when it came to stand there, what that state says, the person's words where it keeps them, why a step Current does not draw is
 * here, the owner's control where it waits, and the door to where its record is. On the flowchart (FLOWCHART1d) the node wears
 * the state's pill itself, so its mark is drawn without it.
 */
function StepMark({ run, step, here, doors, control, pill = true, onNodeWord }: {
  run: WorkflowRun;
  step: RunStep;
  here: boolean;
  doors?: RunDoors;
  control?: ReactNode;
  pill?: boolean;
  onNodeWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
}) {
  const { t } = useTranslation();
  const said = runSaid(t, step);
  const added = addedSaid(t, step);
  const kind = step.step.kind;
  // A graph run's node keeps the person's words of a skip, a refusal or a stop (FABRIC-CUTOVER1e); a go-ahead's act is its sentence's.
  const quoted = (step.node ? NODE_QUOTES.has(step.detail) : QUOTES.has(`${kind}.${step.detail}`)) && step.words ? step.words : null;
  const door = doorFor(t, run, step, doors);
  // The second opinion's gate holding the work on the person (XAGENT1f): its owner's control draws *Go on anyway…* under the step
  // (XAGENT1g), where the frame hands it, the attended session's own run; elsewhere its twins are said, the session's own page
  // and the terminal's, and the door opens the review, where the gate's presses stand.
  const twin = !control && kind === 'opinion' && step.state === 'waiting-on-you' && step.session
    ? t('workflow.run.opinionTwin', { session: step.session })
    : null;
  const when = step.at && step.state !== 'not-reached' ? ago(step.at) : null;

  return (
    <div className="mt-1 grid min-w-0 gap-1">
      {(pill || here || when) && (
        <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
          {pill && <Pill tone={runTone(step.state)}>{stateSaid(t, step.state)}</Pill>}
          {here && (
            <span className="rounded-full border border-line-strong px-2 py-px text-meta text-ink-soft">{t('workflow.run.thisSession')}</span>
          )}
          {when && <span className="font-mono text-meta text-ink-faint">{when}</span>}
        </p>
      )}
      {said && <p className="m-0 text-small text-ink-soft"><Inline text={said} /></p>}
      {quoted && <p className="m-0 text-small text-ink">{t('workflow.run.quoted', { words: quoted })}</p>}
      {added && <p className="m-0 text-meta text-ink-faint">{added}</p>}
      {/* FABRIC-CUTOVER1f: the person's skip and stop of a graph run's node, wherever the driver's table takes them, and a word kept. */}
      {step.node && run.graph && <NodeWord run={run.graph.run} step={step} onWord={onNodeWord} />}
      {control}
      {twin && <p className="m-0 text-meta text-ink-faint"><Inline text={twin} /></p>}
      {door && <div className="flex flex-wrap items-center gap-1.5"><DoorPress door={door} /></div>}
    </div>
  );
}

/** A step's door: a press that opens a place in the window, or an address that opens outside it. */
type Door = { label: string; open: () => void } | { href: string };

/** A door drawn: a ghost press, or a link outside the window as any link opens. */
function DoorPress({ door }: { door: Door }) {
  const { t } = useTranslation();
  if ('href' in door) {
    return (
      <ExternalLink href={door.href} className="inline-flex items-center gap-1 px-1.5 text-small text-accent">
        {t('workflow.run.door.pullRequest')}
        <Icon name="external" size={12} />
      </ExternalLink>
    );
  }
  return <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={door.open}>{door.label}</Button>;
}

/**
 * A step's door (design §7): where its record is. The work's session, or the ask where a go-ahead waits, or the quest its taker
 * waits on; the reviewer's session; the set-up step's page, where the review is answered; the session's review, where its work
 * is accepted; the pull request's address, outside the window as any link opens. A step not reached has none. On the flowchart
 * (FLOWCHART1d) a press is its node's door too, which Enter opens.
 */
function doorFor(t: ReturnType<typeof useTranslation>['t'], run: WorkflowRun, step: RunStep, doors?: RunDoors): Door | null {
  if (step.state === 'not-reached') return null;
  const kind = step.step.kind;

  // A graph run's node (FABRIC-CUTOVER1e): where the record of what it observes is. A go-ahead's ask, where it is answered; its quest; the
  // landed branch's page for what follows the landing; the session whose branch it observes.
  if (step.node) {
    if (step.node === 'go-ahead' && step.state === 'waiting-on-you' && run.ask && doors?.ask) {
      return { label: t('workflow.run.door.ask'), open: () => doors.ask?.(run.ask!) };
    }
    if (step.on === 'quest' && step.quest && doors?.quest) {
      return { label: t('workflow.run.door.quest', { id: step.quest }), open: () => doors.quest?.(step.quest!) };
    }
    if (step.on === 'landing' && step.branch && doors?.branch) {
      return { label: t('landing.door', { branch: step.branch }), open: () => doors.branch?.(run.repository, step.branch!) };
    }
    if (step.on === 'branch' && step.session && doors?.session) {
      return { label: t('workflow.run.door.session'), open: () => doors.session?.(step.session!) };
    }
    return null;
  }

  if (kind === 'work' && step.detail === 'go-ahead' && run.ask && doors?.ask) {
    return { label: t('workflow.run.door.ask'), open: () => doors.ask?.(run.ask!) };
  }
  // A go-on (FABRIC-CLOSE1c7) names the held step, whose page offers the person's go-on and abandon.
  if (kind === 'work' && ['awaits', 'queued', 'unclosed', 'declined', 'reopened', 'go-on'].includes(step.detail) && step.quest && doors?.quest) {
    return { label: t('workflow.run.door.quest', { id: step.quest }), open: () => doors.quest?.(step.quest!) };
  }
  // The gate holds the work on the person: its presses stand beside the review's Accept (XAGENT1f's `LANDING` answers it there).
  if (kind === 'opinion' && step.state === 'waiting-on-you' && step.session && doors?.review) {
    return { label: t('workflow.run.door.review'), open: () => doors.review?.(step.session!) };
  }
  if ((kind === 'work' || kind === 'opinion') && step.session && doors?.session) {
    return { label: t('workflow.run.door.session'), open: () => doors.session?.(step.session!) };
  }
  if (kind === 'look' && step.quest && doors?.quest) {
    return { label: t('workflow.run.door.quest', { id: step.quest }), open: () => doors.quest?.(step.quest!) };
  }
  if (kind === 'landing' && ['waiting-on-you', 'cannot-start'].includes(step.state) && step.session && doors?.review) {
    return { label: t('workflow.run.door.review'), open: () => doors.review?.(step.session!) };
  }
  // Landed on a branch (FLOWCHART1c): its page draws how it landed. A landing into the line keeps no branch, so it has none.
  if (kind === 'landing' && step.state === 'done' && step.detail === 'branch' && step.branch && doors?.branch) {
    return { label: t('landing.door', { branch: step.branch }), open: () => doors.branch?.(run.repository, step.branch!) };
  }
  if (kind === 'pull-request' && step.pullRequest) return { href: step.pullRequest };
  return null;
}

/** One run: what it follows, where it stands in a few words, then its chart with each step's place. */
function Run({ run, here, doors, controls, named, onNodeWord }: {
  run: WorkflowRun;
  here?: string | null;
  doors?: RunDoors;
  controls?: Partial<Record<string, ReactNode>>;
  /** Several runs are drawn, so each says its repository first. */
  named: boolean;
  onNodeWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
}) {
  const { t } = useTranslation();
  const at = run.at ?? null;
  const reached = new Set(run.steps.filter((step) => step.state !== 'not-reached').map((step) => step.step.id));
  const marks: RunMarks = Object.fromEntries(run.steps.map((step) => {
    const mark = (pill: boolean) => (
      <StepMark
        run={run}
        step={step}
        here={here === step.step.id}
        doors={doors}
        control={step.step.id === at ? controls?.[step.step.id] : undefined}
        pill={pill}
        onNodeWord={onNodeWord}
      />
    );
    const door = doorFor(t, run, step, doors);
    return [step.step.id, {
      reached: reached.has(step.step.id),
      at: step.step.id === at,
      mark: mark(true),
      // Its node on the flowchart (FLOWCHART1d): its state's word in `runTone`'s tone, done only where it is (a skip stays in view
      // when a long run folds), its lines without the pill the node draws, and its door for Enter.
      state: { word: stateSaid(t, step.state), tone: runTone(step.state) },
      settled: step.state === 'done',
      node: mark(false),
      ...(door && 'open' in door ? { open: door.open } : {}),
    }];
  }));
  const says = runShort(t, run);
  const checks = chatChecksSaid(t, run);

  return (
    // The chart names itself by the repository (`workflow.chart`), so the run's own box needs no second name.
    <div className="grid min-w-0 gap-3">
      <div className="grid min-w-0 gap-0.5">
        <Prose className="text-ink">
          <Inline text={named ? t('workflow.run.lineIn', { repository: run.repository, says }) : t('workflow.run.line', { says })} />
        </Prose>
        <p className="m-0 text-meta text-ink-faint">{run.workflow.id && run.workflow.boundVersion != null
          ? t('workflow.run.namedHead', { repository: run.repository, workflow: run.workflow.id, version: run.workflow.boundVersion })
          : run.problem ? t('workflow.run.unreadHead', { repository: run.repository })
            : t('workflow.run.head', { repository: run.repository, version: run.workflow.version })}</p>
        {/* FABRIC-PIPELINE1a: the route that chose its workflow, as its binding keeps it; a driver before it keeps none. */}
        {run.workflow.route && (
          <p className="m-0 text-meta text-ink-faint">{t('workflow.run.route', {
            why: whySaid(t, run.workflow.route.level, { repository: run.repository, workspace: run.workspace },
              run.workflow.route.label ?? run.workflow.route.kind ?? null),
          })}</p>
        )}
      </div>
      {/* FABRIC-CUTOVER1e: a graph run says what its record says, what its graph observes, that its branches are its graph's and that
          every landing door still holds it, before its nodes. */}
      {run.graph && (
        <ul aria-label={t('workflow.run.graph.says')} className="m-0 grid list-none gap-1 p-0">
          {graphSaid(t, run.graph).map((line) => (
            <li key={line} className="m-0 text-small text-ink-soft"><Inline text={line} /></li>
          ))}
        </ul>
      )}
      {/* CHATFLOW1c: a chat run's checks are not yet enforced for a conversation, so they are never shown as a limit its tree holds. */}
      {checks && <p className="m-0 text-small text-ink-soft"><Inline text={checks} /></p>}
      {run.workflowGate?.holds && <WorkflowHold gate={run.workflowGate} />}
      {run.problem ? <Prose className="text-small"><Inline text={run.problem} /></Prose> : <WorkflowChart
        workflow={runWorkflow(run)}
        page="repository"
        name={run.repository}
        run={marks}
        end={at === null ? <Pill tone="done">{stateSaid(t, 'done')}</Pill> : undefined}
        finished={at === null}
        graph={run.graph ?? null}
      />}
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
export function WorkflowRunView({ answer, reading = false, refusal = null, here = null, doors, controls, onNodeWord, onRetry }: {
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
  /**
   * The person's skip or stop of a graph run's node (FABRIC-CUTOVER1f), answered by the driver; absent where nothing presses it, and only a
   * word kept is drawn.
   */
  onNodeWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  /** Read again, where the service did not answer (CHATFLOW1b): *Try again*; absent where nothing reads again. */
  onRetry?: () => void;
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

  // No run (CHATFLOW1b): its code in the reader's language, or the driver's sentence for a code the page does not know; a service that
  // did not answer offers *Try again*, which reads again before the view's next tick.
  if (answer.runs.length === 0) {
    return (
      <div className="grid justify-items-start gap-2">
        <Prose className="text-small"><Inline text={noRunSaid(t, answer)} /></Prose>
        {answer.code === 'unanswered' && onRetry && <Button onClick={onRetry}>{t('workflow.run.retry')}</Button>}
      </div>
    );
  }

  return (
    <div className="grid min-w-0 gap-6">
      {answer.runs.map((run) => (
        <Run
          key={`${run.repository}:${run.chat?.run ?? run.quests[0] ?? ''}`}
          run={run}
          here={here}
          doors={doors}
          controls={controls}
          named={answer.runs.length > 1}
          onNodeWord={onNodeWord}
        />
      ))}
    </div>
  );
}
