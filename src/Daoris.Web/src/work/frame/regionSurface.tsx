import type { ReactNode } from 'react';
import type { TFunction } from 'i18next';
import { useResolveHeld, useReviewedPatch, useTreeFile, useWorkflowRun } from '../../bridge/shell';
import type { useRunNodeWord, useTerminals } from '../../bridge/shell';
import { cn } from '../../lib/cn';
import { sentence } from '../../locales/format';
import { ReleaseStepDoors } from '../../workflow/release/ReleaseStepDoors';
import { WorkflowRunView } from '../../workflow/WorkflowRunView';
import { stepHoldBridge } from '../../bridge/repositories/cast';
import { noteIsInTheHead } from '../AttendedSession';
import { SessionConsole } from '../console/SessionConsole';
import { TerminalView } from '../terminals/TerminalView';
import type { useAttendedBox } from '../conversation/useAttendedBox';
import { OpinionGate } from '../gates/OpinionGate';
import { opinionHolds } from '../gates/opinion';
import { ReviewGate } from '../gates/ReviewGate';
import { reviewState } from '../gates/review';
import { shownOf } from '../groups';
import type { useAttendedSession } from '../head/useAttendedSession';
import { SessionTimeline } from '../head/SessionTimeline';
import { ownTree, sessionTitle } from '../identity';
import { DiffPane } from '../review/DiffPane';
import { OutputsPage } from '../outputs/OutputsPage';
import { FilePreview } from '../review/FilePreview';
import type { useSessionLanding } from '../review/useSessionLanding';
import { NothingFollowed } from './RightDock';
import type { Place, ViewId } from './placements';
import type { useDockViews } from './useDockViews';

/**
 * What the frame's regions draw: each view's surface in whichever region it stands, the console and the streams the
 * session runs beside itself, a file's preview, and the line that says whose session a view off Sessions
 * speaks for.
 */
export function useRegionSurfaces({
  t, ask, notifications, elsewhere, onOpenSessions, onOpenQuest, onAnswerAsk, onOpenBranch, terminals, terminalCwd,
  session, work, views, boxState, onReviewRun, attend, runNodeWord, reviewRun = null,
  reviewAttempt = null, flowchartOpen = false,
}: {
  flowchartOpen?: boolean;
  reviewRun?: string | null;
  reviewAttempt?: number | null;
  t: TFunction;
  ask?: ReactNode;
  notifications?: ReactNode;
  elsewhere: boolean;
  onOpenSessions?: () => void;
  onOpenQuest?: (quest: string) => void;
  onAnswerAsk?: (ask: string) => void;
  onOpenBranch?: (repository: string, branch: string) => void;
  terminals: ReturnType<typeof useTerminals>;
  terminalCwd: string | undefined;
  session: ReturnType<typeof useAttendedSession>;
  work: ReturnType<typeof useSessionLanding>;
  views: ReturnType<typeof useDockViews>;
  boxState: ReturnType<typeof useAttendedBox>;
  onReviewRun: (run: string, attempt?: number | null) => void;
  attend: (id: string) => void;
  runNodeWord: ReturnType<typeof useRunNodeWord>;
}) {
  const { attended, quest, openings, registry, groups, here } = session;
  const { following, previewing, openPreview } = views;
  const { taking } = boxState;
  const { discards, discardHeld, gateHolds, headReview, headOpinion, landing } = work;

  // Asked only while the run's flowchart is open, and again at every tick under the driver's key.
  const workflowRun = useWorkflowRun(following ? { session: following.id } : null, { enabled: following !== null && flowchartOpen });
  // *Run again* and *Not now* under a check held unknown, the person's word on the held attempt.
  const resolveHeld = useResolveHeld();
  const previewFile = useTreeFile(previewing ? following!.id : null, previewing?.path ?? null);
  const previewPatch = useReviewedPatch(previewing ? following!.id : null, previewing?.path ?? null);

  // The session's raw console, for whichever region it stands in; what it runs beside itself is the live band's above the dock.
  // A session with nothing held here says so as a sentence, not as an empty bordered well, which would read as a field.
  const consoleView = following && attended
    ? <SessionConsole id={attended.id} fill quiet={t('work.panel.silent')} />
    : null;

  // Off Sessions the session views speak for the session attended there, which nothing else on
  // the screen names — so each says whose it is, with the way back to it.
  const whose = (flush: boolean) => (elsewhere && following ? (
    // Flush in the panel, whose body carries its own gutter; inset in the side bar, as its views are.
    <p className={cn('m-0 flex min-w-0 shrink-0 items-center gap-2 border-b border-line py-1.5 text-meta text-ink-faint', flush ? 'mb-2' : 'px-3')}>
      <span className="min-w-0 truncate">
        {t('work.frame.attending', { title: sessionTitle(following, quest, openings[following.id]) })}
      </span>
      {onOpenSessions && (
        <button
          type="button"
          onClick={onOpenSessions}
          className="shrink-0 cursor-pointer border-0 bg-transparent p-0 text-meta text-ink-soft underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
        >
          {t('work.frame.open')}
        </button>
      )}
    </p>
  ) : null);
  const withWhose = (surface: ReactNode, flush = false) => (elsewhere && following ? <>{whose(flush)}{surface}</> : surface);

  const nothing = () => (elsewhere
    // Off Sessions there is no session list beside it to point at: it says nothing is
    // attended, or that the one attended has ended and is read on Sessions, and offers the view.
    ? <NothingFollowed ended={attended !== null} onOpenSessions={onOpenSessions} />
    : <p className="m-0 p-3 text-small text-ink-faint">{t('work.attended.none.body')}</p>);

  const outputsSurface = () => {
    if (reviewRun || following) {
      return withWhose(
        <OutputsPage
          key={`${reviewRun ?? following?.id}:${reviewAttempt ?? ''}`}
          run={reviewRun}
          session={following?.id ?? null}
          attempt={reviewAttempt ?? 0}
          fallback={ownChanges()}
        />,
      );
    }
    return withWhose(ownChanges());
  };

  const ownChanges = () => (
      <DiffPane
        // The followed session's: off Sessions an ended one is reviewed on Sessions, where it is attended.
        session={following?.id ?? null}
        // What its frame says while git reads; off Sessions the line above already names the session.
        record={following}
        title={following && !elsewhere ? sessionTitle(following, quest, openings[following.id]) : null}
        // A tree of its OWN: the repository's checkout is never merged or discarded.
        hasTree={Boolean(following && ownTree(following, (registry.data ?? []).find((row) => row.repository === following.repository)?.root))}
        onPreview={following && here ? (path) => openPreview({ path }) : undefined}
        // Work the line holds by content offers the head's discard, through the head's press.
        discards={following && following.id === attended?.id ? discards : null}
        onDiscardTree={following && following.id === attended?.id ? discardHeld : undefined}
      />
  );

  const flowchartSurface = () => {
    // Where the followed session's work stands in its workflow, opened from its head's track. Its look's presses are the review's
    // own gate, drawn under the step where it waits, never a second implementation; every other step opens where its record is.
    const lookControl = following && following.id === attended?.id && gateHolds && headReview ? (
      <ReviewGate
        environment={gateHolds.environment ?? ''}
        state={reviewState(gateHolds.state)}
        step={headReview.step ?? null}
        stepId={gateHolds.quest ?? null}
        served={headReview.served ?? null}
        said={gateHolds.says ?? null}
        chat={Boolean(gateHolds.chatRun)}
        acts={headReview.acts}
        busy={headReview.busy}
      />
    ) : null;
    // Its second opinion's own gate under its step while the gate holds the work: *Go on
    // anyway…* and every other press through the gate's one owner, never a second implementation.
    const opinionHeld = following && following.id === attended?.id && headOpinion ? opinionHolds(landing.data) : null;
    const opinionControl = opinionHeld && headOpinion && attended ? (
      <OpinionGate
        gate={opinionHeld}
        detail={headOpinion.detail}
        acts={headOpinion.acts}
        busy={headOpinion.busy}
        working={attended.id}
        onOpenFile={headOpinion.onOpenFile}
      />
    ) : null;
    const cells = (Array.isArray(workflowRun.data?.runs) ? workflowRun.data.runs : []).flatMap((run) => run.steps);
    const controls = Object.fromEntries(cells.flatMap(({ step }) => {
      const control = step.kind === 'look' ? lookControl : step.kind === 'opinion' ? opinionControl : null;
      return control ? [[step.id, control]] : [];
    }));
    const ownKind = following && following.kind !== 'chat' && following.quest ? quest?.setUpIn ? 'look' : 'work' : null;
    const ownStep = cells.find(({ step }) => step.kind === ownKind)?.step.id ?? null;
    return following
      ? withWhose(
        <div className="p-3">
          <WorkflowRunView
            // An answer from another route is something else, which is no run.
            answer={Array.isArray(workflowRun.data?.runs) ? workflowRun.data : null}
            reading={workflowRun.isPending}
            refusal={workflowRun.error ? sentence(workflowRun.error) : null}
            // A service that did not answer is read again at once, before the view's next tick.
            onRetry={() => { void workflowRun.refetch(); }}
            // The step the followed session is its own: a set-up step's session shows the work for the look.
            here={ownStep}
            doors={{
              session: (id) => { attend(id); if (elsewhere) onOpenSessions?.(); },
              ...(onOpenQuest ? { quest: onOpenQuest } : {}),
              ...(onAnswerAsk ? { ask: onAnswerAsk } : {}),
              review: (run) => { onReviewRun(run); if (elsewhere) onOpenSessions?.(); },
              ...(onOpenBranch ? { branch: onOpenBranch } : {}),
            }}
            controls={lookControl || opinionControl ? controls : undefined}
            releaseDoors={(run, step) => <ReleaseStepDoors run={run} step={step} />}
            stepHold={stepHoldBridge}
            onNodeWord={runNodeWord.mutateAsync}
            onHeld={resolveHeld.mutateAsync}
          />
        </div>,
      )
      : nothing();
  };

  /** A view's surface, drawn in whichever region it stands. The panel draws the console itself. */
  const surface = (view: ViewId, where: Place = 'panel') => {
    if (view === 'ask') return ask;
    if (view === 'notifications') return notifications;
    if (view === 'terminal') return <TerminalView terminals={terminals} cwd={terminalCwd} />;
    if (view === 'outputs') return outputsSurface();
    if (view === 'console' && where === 'right') {
      return withWhose(
        <div className="flex min-h-0 flex-1 flex-col gap-2 p-3">
          {consoleView ?? <p className="m-0 text-small text-ink-faint">{t('work.panel.none')}</p>}
        </div>,
      );
    }
    return following
      ? withWhose(
        <div className="p-3">
          <SessionTimeline
            session={following}
            quest={quest}
            // The word the session list shows it as, which the states' flow lights its state with.
            shown={shownOf(following, (groups.data ?? []).find((row) => row.session === following.id), taking[following.id])}
            // Off Sessions there is no head to carry the parked note, so the timeline keeps it.
            hideCurrentNote={!elsewhere && noteIsInTheHead(following, here)}
            titled={false}
          />
        </div>,
      )
      : nothing();
  };

  // The file previewed for the followed session: the host's answer, or its sentence, and the
  // review's patch for that path where the review already holds one.
  const previewSurface = previewing && following
    ? withWhose(
      <FilePreview
        key={`${following.id}:${previewing.path}`}
        path={previewing.path}
        file={previewFile.data ?? null}
        pending={previewFile.isPending && !previewFile.error}
        refusal={previewFile.error ? sentence(previewFile.error) : null}
        lines={previewing.lines}
        patch={previewPatch}
        onReload={() => { void previewFile.refetch(); }}
      />,
    )
    : null;

  return { surface, previewSurface, consoleView, withWhose, flowchart: flowchartOpen ? flowchartSurface() : null };
}
