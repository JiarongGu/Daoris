import { type ReactNode, useEffect, useState } from 'react';
import type { TFunction } from 'i18next';
import {
  useConsidered, useResolveHeld, useReviewedPatch, useSessionStreams, useStopTask, useTreeFile, useWorkflowRun,
} from '../../bridge/shell';
import type { useRunNodeWord, useTerminals } from '../../bridge/shell';
import type { useSessionAtWork } from '../../bridge/sessions/atWork';
import { type Notify, SESSION_ACTIVE } from '../../design/ui';
import { cn } from '../../lib/cn';
import { sentence } from '../../locales/format';
import { WorkflowRunView } from '../../workflow/WorkflowRunView';
import { noteIsInTheHead } from '../AttendedSession';
import { AtWorkView } from '../atWork/AtWorkView';
import type { ExchangeSeek } from '../atWork/exchangeSeek';
import { useLineChannel, usePostLineChannel } from '../../bridge/sessions/asks';
import { lastMessages, lastWords, repositoriesOf } from '../channel';
import { SessionConsole } from '../console/SessionConsole';
import { TerminalView } from '../terminals/TerminalView';
import { panelTabs } from '../console/streams';
import type { useAttendedBox } from '../conversation/useAttendedBox';
import { askOfEntry, entryOfSession, isIntakeEntry } from '../entries';
import { OpinionGate } from '../gates/OpinionGate';
import { opinionHolds } from '../gates/opinion';
import { ReviewGate } from '../gates/ReviewGate';
import { reviewState } from '../gates/review';
import { shownOf } from '../groups';
import type { useAttendedRecord } from '../head/useAttendedRecord';
import type { useAttendedSession } from '../head/useAttendedSession';
import type { useHeadAsks } from '../head/useHeadAsks';
import { SessionTimeline } from '../head/SessionTimeline';
import { ownTree, sessionTitle } from '../identity';
import { DiffPane } from '../review/DiffPane';
import { RunReviewPage } from '../review/run/RunReviewPage';
import { FilePreview } from '../review/FilePreview';
import type { useSessionLanding } from '../review/useSessionLanding';
import { takesWords } from '../say';
import type { HandOverRoster } from '../start/HandOverAsk';
import { StreamTabs } from './OutputPanel';
import { NothingFollowed } from './RightDock';
import type { Place, ViewId } from './placements';
import type { useDockViews } from './useDockViews';
import type { useFrameLayout } from './useFrameLayout';

/**
 * What the frame's regions draw: each view's surface in whichever region it stands, the console and the streams the
 * session runs beside itself, a file's preview, and the line that says whose session a view off Sessions
 * speaks for.
 */
export function useRegionSurfaces({
  t, notify, ask, notifications, elsewhere, onOpenSessions, onOpenQuest, onAnswerAsk, onOpenBranch, onSendBack, terminals, terminalCwd,
  session, record, work, views, frame, boxState, asks, agentRoster, attend, runNodeWord, atWork, atWorkSeek, reviewRun = null,
}: {
  reviewRun?: string | null;
  t: TFunction;
  notify: Notify;
  ask?: ReactNode;
  notifications?: ReactNode;
  elsewhere: boolean;
  onOpenSessions?: () => void;
  onOpenQuest?: (quest: string) => void;
  onAnswerAsk?: (ask: string) => void;
  onOpenBranch?: (repository: string, branch: string) => void;
  onSendBack?: (repository: string) => void;
  terminals: ReturnType<typeof useTerminals>;
  terminalCwd: string | undefined;
  session: ReturnType<typeof useAttendedSession>;
  record: ReturnType<typeof useAttendedRecord>;
  work: ReturnType<typeof useSessionLanding>;
  views: ReturnType<typeof useDockViews>;
  frame: ReturnType<typeof useFrameLayout>;
  boxState: ReturnType<typeof useAttendedBox>;
  asks: ReturnType<typeof useHeadAsks>;
  agentRoster: HandOverRoster;
  attend: (id: string) => void;
  runNodeWord: ReturnType<typeof useRunNodeWord>;
  atWork: ReturnType<typeof useSessionAtWork>;
  /** The request the head's *Waiting on* opened *At work* at. */
  atWorkSeek?: ExchangeSeek | null;
}) {
  const { attended, entries, quest, openings, registry, groups, here, attendedEntry, channelAsk } = session;
  const { following, previewing, openPreview, openView, dock, panelView } = views;
  const { placed, collapsed, dockClosed, layout, closeDock } = frame;
  const { box, taking } = boxState;
  const { doors, setAnswerFocus } = asks;
  const { entryAt, discards, discardHeld, gateHolds, headReview, headOpinion, landing } = work;
  const { channel, setChannelOpened } = record;

  // What the session runs beside itself, a tab each in the panel. The panel shows the
  // session's own console unless the person picked a stream of it, and one it no longer lists falls
  // back to the session rather than to an empty well.
  const streams = useSessionStreams(attended?.id ?? null);
  const [picked, setPicked] = useState<string | null>(null);
  useEffect(() => setPicked(null), [attended?.id]);
  const shown = attended
    ? (picked && streams.some((row) => row.key === picked) ? picked : attended.id)
    : null;

  // Where the followed session's work stands: asked only while the Workflow view shows,
  // in whichever region it stands, and again at every tick under the driver's key.
  const workflowShown = following !== null && (placed.places.workflow === 'panel'
    ? !collapsed && panelView === 'workflow'
    : !dockClosed && dock === 'workflow');
  const workflowRun = useWorkflowRun(following ? { session: following.id } : null, { enabled: workflowShown });
  // A line on no ask held here has a channel of its own, which *At work* draws: read only while *At work* shows.
  const atWorkShown = following !== null && (placed.places.atWork === 'panel'
    ? !collapsed && panelView === 'atWork'
    : !dockClosed && dock === 'atWork');
  const lineChannel = useLineChannel(attended && !channelAsk ? attended.id : null, { enabled: atWorkShown });
  const postLine = usePostLineChannel();
  // Why an untaken quest sits is the driver's last look's, read beside the answer from memory.
  const considered = useConsidered().data;
  // *Run again* and *Not now* under a check held unknown, the person's word on the held attempt.
  const resolveHeld = useResolveHeld();
  const previewFile = useTreeFile(previewing ? following!.id : null, previewing?.path ?? null);
  const previewPatch = useReviewedPatch(previewing ? following!.id : null, previewing?.path ?? null);

  // The console and its streams, for whichever region it stands in. A session with nothing
  // held here says so as a sentence, not as an empty bordered well, which would read as a field.
  const consoleView = following && shown
    ? <SessionConsole id={shown} fill quiet={t(shown === following.id ? 'work.panel.silent' : 'work.panel.streamSilent')} />
    : null;
  const streamTabs = following ? panelTabs(following.id, streams) : undefined;
  const pickStream = (key: string) => setPicked(key === attended?.id ? null : key);
  // A task stopped from its tab: asked of the session that runs it; its tab changes when the
  // wire says how it ended. What the task is called is the tab's, for the notice.
  const stopTask = useStopTask();
  const stopStream = (key: string) => {
    if (!attended) return;
    const name = streams.find((row) => row.key === key)?.name ?? key;
    stopTask.mutate({ id: attended.id, key }, {
      onSuccess: ({ stopped }) => notify(t(stopped ? 'work.panel.stream.stopping' : 'work.panel.stream.notStopped', { name }),
        stopped ? undefined : 'error'),
      onError: (error) => notify(sentence(error), 'error'),
    });
  };

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

  // *Send back…* in a review: the box on that session, with the focus in it, which is what the
  // glossary's send back means — its work returned to it with a note, so it carries on. Off Sessions, Sessions is opened,
  // where the box is. Where the session takes no words, the door to the quest composer stays.
  const sendBack = () => {
    if (!attended) return;
    if (!takesWords(box)) {
      onSendBack?.(attended.repository);
      return;
    }
    if (elsewhere) onOpenSessions?.();
    setAnswerFocus((was) => ({ session: attended.id, at: (was?.at ?? 0) + 1 }));
  };

  const nothing = () => (elsewhere
    // Off Sessions there is no session list beside it to point at: it says nothing is
    // attended, or that the one attended has ended and is read on Sessions, and offers the view.
    ? <NothingFollowed ended={attended !== null} onOpenSessions={onOpenSessions} />
    : <p className="m-0 p-3 text-small text-ink-faint">{t('work.attended.none.body')}</p>);

  const reviewSurface = () => {
    // The line of work the reviewed part belongs to, and its ask: either's one Accept holds this one's while it lands.
    const followingEntry = following ? entryOfSession(entries, following.id) : null;
    if (reviewRun || following) {
      return withWhose(
        <RunReviewPage key={reviewRun ?? following?.id} run={reviewRun} session={following?.id ?? null} fallback={ownChanges(followingEntry)} />,
      );
    }
    return withWhose(ownChanges(followingEntry));
  };

  const ownChanges = (followingEntry: ReturnType<typeof entryOfSession>) => (
      <DiffPane
        // The session's one press, where the part reviewed is the attended one.
        entryPress={following && following.id === attended?.id ? entryAt('review') ?? null : null}
        entry={followingEntry?.entry ?? null}
        ask={followingEntry && !isIntakeEntry(followingEntry) ? askOfEntry(followingEntry) : null}
        // The followed session's: off Sessions an ended one is reviewed on Sessions, where it is attended.
        session={following?.id ?? null}
        // What its frame says while git reads; off Sessions the line above already names the session.
        record={following}
        title={following && !elsewhere ? sessionTitle(following, quest, openings[following.id]) : null}
        // A tree of its OWN: the repository's checkout is never merged or discarded.
        hasTree={Boolean(following && ownTree(following, (registry.data ?? []).find((row) => row.repository === following.repository)?.root))}
        onSendBack={following && (takesWords(box) || onSendBack) ? sendBack : undefined}
        onPreview={following && here ? (path) => openPreview({ path }) : undefined}
        // Work the line holds by content offers the head's discard in Accept's place, through the head's press.
        discards={following && following.id === attended?.id ? discards : null}
        onDiscardTree={following && following.id === attended?.id ? discardHeld : undefined}
      />
  );

  const workflowSurface = () => {
    // Where the followed session's work stands in its workflow. Its look's presses are the review's
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
    // anyway…* and every other press through the gate's one owner, never a second implementation. No *Accept…* is drawn beside
    // it here, so *Go on anyway…* is offered where the session's page lists the dispute inside *Accept…*.
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
              review: (id) => { doors.review?.(id); if (elsewhere) onOpenSessions?.(); },
              ...(onOpenBranch ? { branch: onOpenBranch } : {}),
            }}
            controls={lookControl || opinionControl ? controls : undefined}
            onNodeWord={runNodeWord.mutateAsync}
            onHeld={resolveHeld.mutateAsync}
          />
        </div>,
      )
      : nothing();
  };

  const atWorkSurface = () => {
    // Who is at work on the followed session: its parts and its subagents, from Daoris's
    // records; each part opens its page.
    return following
      ? withWhose(
        <AtWorkView
          held={atWork.data}
          error={atWork.error ? sentence(atWork.error) : null}
          onOpen={(id) => { attend(id); if (elsewhere) onOpenSessions?.(); }}
          onOpenQuest={onOpenQuest}
          considered={considered}
          // Its co-workers' agents as the person calls them, as the head's *Waiting on* names them.
          nameOf={(agent) => agentRoster.names?.[agent] ?? agent}
          seek={atWorkSeek}
          // The newest three messages of the page's own channel read, the panel's; for a line on no ask held here,
          // the newest three of its own channel, which no panel holds.
          channel={attendedEntry && channelAsk && channel.available && channel.channel ? {
            words: lastWords(channel.channel, { entry: attendedEntry.entry, repository: attendedEntry.repository }),
            onOpen: () => {
              setChannelOpened((n) => n + 1);
              // Under 768 px the view replaces the conversation: leaving it shows the panel it opened.
              if (layout.dock.mode === 'full') closeDock(true);
            },
          } : lineChannel && attended ? {
            words: lastMessages(lineChannel),
            // The person's words on it, to every part of the line or one repository its messages name.
            recipients: repositoriesOf(lineChannel.items, []),
            over: !SESSION_ACTIVE.has(attended.state),
            onPost: (post, answered) => postLine.mutate({ session: attended.id, post }, {
              onSuccess: (posted) => (posted.posted ? answered.done() : answered.refused(posted.code ?? 'other')),
              onError: (error) => answered.refused(sentence(error)),
            }),
          } : undefined}
          onRetry={() => { void atWork.refetch(); }}
          // A child's console tab, while the window holds its stream (the harness's agent id ends the stream's key).
          streamOf={(child) => streams.find((row) => row.key.endsWith(`/${child}`))?.key}
          onOpenStream={(key) => { pickStream(key); openView('console'); }}
        />,
      )
      : nothing();
  };

  /** A view's surface, drawn in whichever region it stands. The panel draws the console itself. */
  const surface = (view: ViewId, where: Place = 'panel') => {
    if (view === 'ask') return ask;
    if (view === 'notifications') return notifications;
    if (view === 'terminal') return <TerminalView terminals={terminals} cwd={terminalCwd} />;
    if (view === 'review') return reviewSurface();
    if (view === 'workflow') return workflowSurface();
    if (view === 'atWork') return atWorkSurface();
    if (view === 'console' && where === 'right') {
      // In the side bar its streams go with it, above it, where the panel carries them in its header.
      return withWhose(
        <div className="flex min-h-0 flex-1 flex-col gap-2 p-3">
          {streamTabs && streamTabs.length > 1 && (
            <StreamTabs tabs={streamTabs} selected={shown ?? undefined} onSelect={pickStream} onStop={stopStream} />
          )}
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

  return { surface, previewSurface, consoleView, streamTabs, shown, pickStream, stopStream, withWhose };
}
