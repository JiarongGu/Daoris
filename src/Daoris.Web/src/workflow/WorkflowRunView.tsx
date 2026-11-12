import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { askCommand } from '../plugins/commandAsks';
import { authoredText } from '../locales/authored';
import i18n from '../locales/i18n';
import { ago } from '../locales/format';
import { ExternalLink } from '../design/links';
import { Button, Icon, Inline, Pill, Prose, SkeletonRows } from '../design/ui';
import type { CurrentWorkflow } from './current';
import { NodeWord } from './NodeWord';
import {
  type CheckHeldAnswer, type CheckHeldAsk, finishedBy, notTakenBy, type NodeWordAnswer, type NodeWordAsk, openedBy, type RunRequest, type RunStep, runTone,
  type WorkflowRun, type WorkflowRunAnswer,
} from './run';
import { outcomeSaid } from './graphFlow';
import { bindingProblemSaid } from './place/effective';
import { whySaid } from './routeSaid';
import { addedSaid, chatChecksSaid, graphSaid, noRunSaid, placeSaid, requestSaid, runSaid, stateSaid, stepWord } from './runSaid';
import { stepTitle } from './said';
import { CheckHeld } from './CheckHeld';
import { StepWait } from './wait/StepWait';
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
  review?: (run: string) => void;
  /** A landed branch's page in its repository, with how it landed. */
  branch?: (repository: string, branch: string) => void;
};

/** The run its review is read by: its record's, a conversation's, else the first quest of the run. */
const reviewRunOf = (run: WorkflowRun): string | null => run.graph?.run ?? run.chat?.run ?? run.quests[0] ?? null;

/** The run as a chart's workflow: its Current, each step as the driver drew it, a step the work added among them. */
export const runWorkflow = (run: WorkflowRun): CurrentWorkflow => ({
  repository: run.repository,
  workspace: run.workspace,
  startHolds: run.workflow.startHolds,
  steps: run.steps.map((step) => step.step),
  version: run.workflow.version,
  limits: run.workflow.limits,
});

const notTakenIn = notTakenBy;

/** A step's title as a name for a reader to hear: its code marks dropped. */
const plain = (text: string) => text.replace(/`/g, '');

/** One route a decision leads along: its outcome, where it goes on in words, and whether the outcome it returned ruled it out. */
type Route = { outcome: string; said: string; to: string; notTaken: boolean };

/**
 * A decision's routes, in the graph's order: each `when` wire by its outcome, to the steps that follow it in a chain; and, where it returned
 * an outcome nothing follows, that outcome going to *No further step*. A step that decides nothing has none.
 */
function routesOf(t: ReturnType<typeof useTranslation>['t'], run: WorkflowRun, step: RunStep): Route[] {
  const graph = run.graph;
  const wires = graph?.wires.filter((wire) => wire.kind === 'when' && wire.from === step.step.id) ?? [];
  if (!graph || wires.length === 0) return [];
  const byId = new Map(run.steps.map((each) => [each.step.id, each]));
  const returned = graph.outcomes?.[step.step.id] ?? null;
  const follow = (start: string): string[] => {
    const titles: string[] = [];
    for (let id: string | undefined = start; id && !titles.includes(id);) {
      const next = byId.get(id);
      if (!next) break;
      titles.push(id);
      const after = graph.wires.filter((wire) => wire.from === id);
      id = after.length === 1 && after[0]!.kind === 'after' ? after[0]!.to : undefined;
    }
    return titles.map((id) => plain(stepTitle(t, byId.get(id)!.step)));
  };
  const said = (outcome: string) => outcomeSaid(t, step.step.kind, outcome);
  const routes = wires.map((wire): Route => ({
    outcome: wire.outcome ?? '', said: said(wire.outcome ?? ''), to: follow(wire.to).join(t('workflow.listJoin')), notTaken: returned !== null && returned !== wire.outcome,
  }));
  if (returned !== null && !wires.some((wire) => wire.outcome === returned)) {
    routes.push({ outcome: returned, said: said(returned), to: t('workflow.graph.noFurther'), notTaken: false });
  }
  return routes;
}

/** A decision's routes as the rail reads them: each outcome and where it goes, the ones its outcome ruled out said to be not taken. */
function Routes({ routes }: { routes: Route[] }) {
  const { t } = useTranslation();
  if (routes.length === 0) return null;
  return (
    <ul aria-label={t('workflow.run.routes')} className="m-0 grid list-none gap-0.5 p-0 text-meta text-ink-soft">
      {routes.map((route, index) => (
        <li key={route.outcome} className="m-0 grid min-w-0 grid-cols-[1.25ch_minmax(0,1fr)] gap-x-1.5">
          <span aria-hidden className="font-mono text-ink-faint">{index === routes.length - 1 ? '└' : '├'}</span>
          <span className="min-w-0">
            <Inline text={t('workflow.run.routeTo', { outcome: route.said, to: route.to })} />
            {route.notTaken && <span className="ml-1.5 text-ink-faint">{t('workflow.run.notTaken.state')}</span>}
          </span>
        </li>
      ))}
    </ul>
  );
}

/**
 * A step's state line: its pill (drawn by the node itself on the flowchart), *this session* where the attended session is its own, and when it
 * came to stand there.
 */
function StateLine({ run, step, here, pill, children }: { run: WorkflowRun; step: RunStep; here: boolean; pill: boolean; children?: ReactNode }) {
  const { t } = useTranslation();
  const notTaken = notTakenIn(run, step);
  const when = step.at && step.state !== 'not-reached' ? ago(step.at) : null;
  if (!pill && !here && !when && !children) return null;
  return (
    <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
      {pill && <Pill tone={notTaken ? 'neutral' : runTone(step.state)}>{notTaken ? t('workflow.run.notTaken.state') : stepWord(t, step)}</Pill>}
      {here && (
        <span className="rounded-full border border-line-strong px-2 py-px text-meta text-ink-soft">{t('workflow.run.thisSession')}</span>
      )}
      {when && <span className="font-mono text-meta text-ink-faint">{when}</span>}
      {children}
    </p>
  );
}

/**
 * **A step the person has not opened**: its state, the outcome a decision came to and the routes it led along, and the press that
 * opens its record. Its record and its owner's control wait for that press.
 */
function StepBrief({ run, step, here, pill, onPick }: { run: WorkflowRun; step: RunStep; here: boolean; pill: boolean; onPick: () => void }) {
  const { t } = useTranslation();
  const returned = run.graph?.outcomes?.[step.step.id] ?? null;
  const outcome = returned === null ? null : outcomeSaid(t, step.step.kind, returned);
  const routes = pill ? routesOf(t, run, step) : [];
  return (
    <div className="mt-1 grid min-w-0 gap-1">
      <StateLine run={run} step={step} here={here} pill={pill}>
        {/* A step the work has not come to keeps no record to open. */}
        {step.state !== 'not-reached' && (
          <Button
            variant="ghost"
            aria-expanded={false}
            aria-label={t('workflow.run.detailsOf', { step: plain(stepTitle(t, step.step)) })}
            className="min-h-0 px-1.5 py-0.5 text-small"
            onClick={onPick}
          >
            {t('workflow.run.details')}
          </Button>
        )}
      </StateLine>
      {outcome && <p className="m-0 text-small text-ink-soft"><Inline text={t('workflow.run.outcome', { outcome })} /></p>}
      <Routes routes={routes} />
    </div>
  );
}

/** The person's words a step keeps, shown as written: a skip's and a *not yet*'s. */
const QUOTES: ReadonlySet<string> = new Set(['look.skip', 'look.not-yet']);

/** The person's words a graph run's node keeps: a skip's, a refusal's and a stop's, by its record's state. */
const NODE_QUOTES: ReadonlySet<string> = new Set(['skipped', 'rejected', 'stopped']);

/**
 * One step's place in its run (design §5.2, §7): its state on its pill, *this session* where the attended session is its own,
 * when it came to stand there, what that state says, the person's words where it keeps them, why a step Current does not draw is
 * here, the owner's control where it waits, and the door to where its record is. The step the person has opened: the others
 * are `StepBrief`s. On the flowchart the node wears the state's pill itself, so its mark is drawn without it, and without
 * the routes, which its edges draw.
 */
function StepMark({ run, step, here, doors, control, pill = true, onNodeWord, onHeld }: {
  run: WorkflowRun;
  step: RunStep;
  here: boolean;
  doors?: RunDoors;
  control?: ReactNode;
  pill?: boolean;
  onNodeWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
}) {
  const { t } = useTranslation();
  // A node the outcomes reached leave no route to says so in place of its state's words; a decision says the outcome it reached.
  const notTaken = notTakenIn(run, step);
  const returned = run.graph?.outcomes?.[step.step.id] ?? null;
  const outcome = returned === null ? null : outcomeSaid(t, step.step.kind, returned);
  const said = notTaken ? t('workflow.run.notTaken.said') : runSaid(t, step);
  const added = addedSaid(t, step);
  const kind = step.step.kind;
  // A graph run's node keeps the person's words of a skip, a refusal or a stop; a go-ahead's act is its sentence's.
  const quoted = (step.node ? NODE_QUOTES.has(step.detail) : QUOTES.has(`${kind}.${step.detail}`)) && step.words ? step.words : null;
  const door = doorFor(t, run, step, doors);
  // The second opinion's gate holding the work on the person: its owner's control draws *Go on anyway…* under the step
  // where the frame hands it, the attended session's own run; elsewhere its twins are said, the session's own page
  // and the terminal's, and the door opens the review, where the gate's presses stand.
  const twin = !control && kind === 'opinion' && step.state === 'waiting-on-you' && step.session
    ? t('workflow.run.opinionTwin', { session: step.session })
    : null;

  return (
    <div className="mt-1 grid min-w-0 gap-1">
      <StateLine run={run} step={step} here={here} pill={pill} />
      {said && <p className="m-0 text-small text-ink-soft"><Inline text={said} /></p>}
      {outcome && <p className="m-0 text-small text-ink-soft"><Inline text={t('workflow.run.outcome', { outcome })} /></p>}
      {pill && <Routes routes={routesOf(t, run, step)} />}
      {/* The requests the work's sessions made, inside the work: each quest they published and each helper they asked. */}
      {step.requests && step.requests.length > 0 && (
        <ul aria-label={t('workflow.run.requests.title')} className="m-0 grid list-none gap-1 p-0">
          {step.requests.map((request) => <RequestRow key={`${request.kind}:${request.id}`} request={request} doors={doors} />)}
        </ul>
      )}
      {/* The integrate node's record, in one line; a state this page has no words for is said as recorded. */}
      {step.integration && (
        <p className="m-0 text-small text-ink-soft">
          {t(`workflow.run.integration.${step.integration.step && step.integration.state === 'in-candidate' ? 'in-candidate.at' : step.integration.state}`,
            { line: step.integration.line, step: step.integration.step ?? '', defaultValue: `${step.integration.state} · ${step.integration.line}` })}
        </p>
      )}
      <StepWait step={step} />
      {quoted && <p className="m-0 text-small text-ink">{t('workflow.run.quoted', { words: quoted })}</p>}
      {added && <p className="m-0 text-meta text-ink-faint">{added}</p>}
      {/* The person's skip and stop of a graph run's node, wherever the driver's table takes them, and a word kept. */}
      {step.node && run.graph && <NodeWord run={run.graph.run} step={step} onWord={onNodeWord} />}
      {/* A check held unknown for the person: *Run again* and *Not now*, the held attempt's own words. */}
      {step.node === 'check' && <CheckHeld step={step} onHeld={onHeld} />}
      {control}
      {twin && <p className="m-0 text-meta text-ink-faint"><Inline text={twin} /></p>}
      {door && <div className="flex flex-wrap items-center gap-1.5"><DoorPress door={door} /></div>}
    </div>
  );
}

/**
 * One request the work's sessions made: where it is asked, its state, whether a session of the work waits on the
 * answer, and its close's note as written; a quest's door opens its page where one is handed in.
 */
function RequestRow({ request, doors }: { request: RunRequest; doors?: RunDoors }) {
  const { t } = useTranslation();
  const { what, state, awaits } = requestSaid(t, request);
  return (
    <li data-request={request.kind} className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small text-ink-soft">
      <Inline text={what} />
      <span className={request.state === 'declined' || request.state === 'failed' ? 'text-ink' : undefined}>{state}</span>
      {awaits && <span className="text-ink-faint">{awaits}</span>}
      {request.words && <span className="text-ink">{t('workflow.run.quoted', { words: request.words })}</span>}
      {request.kind === 'quest' && doors?.quest && (
        <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => doors.quest?.(request.id)}>
          {t('workflow.run.door.quest', { id: request.id })}
        </Button>
      )}
    </li>
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
 * A step's door: where its record is. The work's session, or the ask where a go-ahead waits, or the quest its taker
 * waits on; the reviewer's session; the set-up step's page, where the review is answered; the session's review, where its work
 * is accepted; the pull request's address, outside the window as any link opens. A step not reached has none. On the flowchart
 * a press is its node's door too, which Enter opens.
 */
function doorFor(t: ReturnType<typeof useTranslation>['t'], run: WorkflowRun, step: RunStep, doors?: RunDoors): Door | null {
  if (step.state === 'not-reached') return null;
  const kind = step.step.kind;

  // A refused step's plugin named its fix: the command's round card, which the Plugins page draws.
  if (step.needs) {
    const { plugin, command, title } = step.needs;
    return { label: t('workflow.run.door.needs', { command: authoredText(title, i18n.language) || command }), open: () => askCommand(plugin, command) };
  }

  // A graph run's node: where the record of what it observes is. A go-ahead's ask, where it is answered; its quest; the
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
  // The gate holds the work on the person: its presses stand beside the review's Accept (`LANDING` answers it there).
  const reviewed = reviewRunOf(run);
  if (kind === 'opinion' && step.state === 'waiting-on-you' && reviewed && doors?.review) {
    return { label: t('workflow.run.door.review'), open: () => doors.review?.(reviewed) };
  }
  if ((kind === 'work' || kind === 'opinion') && step.session && doors?.session) {
    return { label: t('workflow.run.door.session'), open: () => doors.session?.(step.session!) };
  }
  if (kind === 'look' && step.quest && doors?.quest) {
    return { label: t('workflow.run.door.quest', { id: step.quest }), open: () => doors.quest?.(step.quest!) };
  }
  if (kind === 'landing' && ['waiting-on-you', 'cannot-start'].includes(step.state) && reviewed && doors?.review) {
    return { label: t('workflow.run.door.review'), open: () => doors.review?.(reviewed) };
  }
  // Landed on a branch: its page draws how it landed. A landing into the line keeps no branch, so it has none.
  if (kind === 'landing' && step.state === 'done' && step.detail === 'branch' && step.branch && doors?.branch) {
    return { label: t('landing.door', { branch: step.branch }), open: () => doors.branch?.(run.repository, step.branch!) };
  }
  if (kind === 'pull-request' && step.pullRequest) return { href: step.pullRequest };
  return null;
}

/** One run: what it follows, where it stands in a few words, then its chart with each step's place. */
function Run({ run, here, doors, controls, named, onNodeWord, onHeld }: {
  run: WorkflowRun;
  here?: string | null;
  doors?: RunDoors;
  controls?: Partial<Record<string, ReactNode>>;
  /** Several runs are drawn, so each says its repository first. */
  named: boolean;
  onNodeWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
}) {
  const { t, i18n } = useTranslation();
  const at = run.at ?? null;
  // The step whose record and owner's control are drawn: the person's choice, else where the run stands.
  const [picked, setPicked] = useState<string | null>(null);
  const opened = picked !== null && run.steps.some((step) => step.step.id === picked) ? picked : openedBy(run);
  const finished = finishedBy(run);
  const reached = new Set(run.steps.filter((step) => step.state !== 'not-reached').map((step) => step.step.id));
  const marks: RunMarks = Object.fromEntries(run.steps.map((step) => {
    const id = step.step.id;
    const pick = () => setPicked(id);
    const mark = (pill: boolean) => id === opened
      ? (
        <StepMark
          run={run}
          step={step}
          here={here === id}
          doors={doors}
          control={id === at ? controls?.[id] : undefined}
          pill={pill}
          onNodeWord={onNodeWord}
          onHeld={onHeld}
        />
      )
      : <StepBrief run={run} step={step} here={here === id} pill={pill} onPick={pick} />;
    return [id, {
      reached: reached.has(id),
      at: id === at && step.state !== 'stopped',
      mark: mark(true),
      // Its node on the flowchart: its state's word in `runTone`'s tone, done only where it is (a skip stays in view
      // when a long run folds), its lines without the pill the node draws, and Enter opens its record. A route not taken says so.
      state: notTakenIn(run, step)
        ? { word: t('workflow.run.notTaken.state'), tone: 'neutral' as const }
        : { word: stepWord(t, step), tone: runTone(step.state) },
      settled: step.state === 'done',
      node: mark(false),
      ...(step.state === 'not-reached' ? {} : { open: pick }),
    }];
  }));
  const checks = chatChecksSaid(t, run);
  // One line of the head: the saved workflow by its name and version, or Current by its version; and what chose it, as its binding keeps it.
  const source = run.workflow.route
    ? whySaid(t, run.workflow.route.level, { repository: run.repository, workspace: run.workspace }, run.workflow.route.label ?? run.workflow.route.kind ?? null)
    : null;
  const bound = run.workflow.id && run.workflow.boundVersion != null
    ? t('workflow.run.headBound', {
      workflow: i18n.exists(`workflow.shipped.${run.workflow.id}.name`) ? t(`workflow.shipped.${run.workflow.id}.name`) : run.workflow.id,
      version: run.workflow.boundVersion,
    })
    : null;
  const head = bound ?? (run.problem ? t('workflow.run.unreadHead', { repository: run.repository })
    : t('workflow.run.head', { repository: run.repository, version: run.workflow.version }));

  return (
    // The chart names itself by the repository (`workflow.chart`), so the run's own box needs no second name.
    <div className="grid min-w-0 gap-3">
      <div className="grid min-w-0 gap-0.5">
        <Prose className="text-ink"><Inline text={placeSaid(t, run, named)} /></Prose>
        <p className="m-0 text-meta text-ink-faint">{source && !run.problem ? t('workflow.run.headSource', { head, source }) : head}</p>
      </div>
      {/* A graph run says what its record says, what its graph observes, that its branches are its graph's and that
          every landing door still holds it, before its nodes. */}
      {run.graph && (
        <ul aria-label={t('workflow.run.graph.says')} className="m-0 grid list-none gap-1 p-0">
          {graphSaid(t, run.graph).map((line) => (
            <li key={line} className="m-0 text-small text-ink-soft"><Inline text={line} /></li>
          ))}
        </ul>
      )}
      {/* A chat run's checks are not yet enforced for a conversation, so they are never shown as a limit its tree holds. */}
      {checks && <p className="m-0 text-small text-ink-soft"><Inline text={checks} /></p>}
      {run.workflowGate?.holds && <WorkflowHold gate={run.workflowGate} />}
      {run.problem ? <Prose className="text-small"><Inline text={bindingProblemSaid(t, run)} /></Prose> : <WorkflowChart
        workflow={runWorkflow(run)}
        page="repository"
        name={run.repository}
        run={marks}
        end={finished ? <Pill tone="done">{stateSaid(t, 'done')}</Pill> : undefined}
        finished={finished}
        graph={run.graph ?? null}
      />}
    </div>
  );
}

/**
 * **Where a piece of work stands in its workflow**: the session's side
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
 * never a hue.
 *
 * **An ask's work in several repositories is several runs** (design §3.8), each at its own step, drawn one after another.
 *
 * A molecule: the answer arrives as a prop, and every door goes out.
 */
export function WorkflowRunView({ answer, reading = false, refusal = null, here = null, doors, controls, onNodeWord, onHeld, onRetry }: {
  /** What the shell answered; absent while it is read, or where it is refused. */
  answer?: WorkflowRunAnswer | null;
  reading?: boolean;
  /** The read is refused, in the driver's sentence that says why. */
  refusal?: string | null;
  /** The step the attended session is its own: marked *this session*. */
  here?: string | null;
  doors?: RunDoors;
  /** Each owner's control where the step the run stands at waits on a press, by the step's id. */
  controls?: Partial<Record<string, ReactNode>>;
  /**
   * The person's skip or stop of a graph run's node, answered by the driver; absent where nothing presses it, and only a
   * word kept is drawn.
   */
  onNodeWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  /** The person's *Run again* or *Not now* on a check held unknown, answered by the driver; absent where nothing presses it. */
  onHeld?: (ask: CheckHeldAsk) => Promise<CheckHeldAnswer>;
  /** Read again, where the service did not answer: *Try again*; absent where nothing reads again. */
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

  // No run: its code in the reader's language, or the driver's sentence for a code the page does not know; a service that
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
          onHeld={onHeld}
        />
      ))}
    </div>
  );
}
