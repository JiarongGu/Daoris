import { type ReactNode, useMemo, useRef, useState } from 'react';
import type { TFunction } from 'i18next';
import { useSessionDiff, usePostChannel, type withCall } from '../../bridge/shell';
import { type Notify, SESSION_ACTIVE } from '../../design/ui';
import { BESIDE_READER, BesideReading } from '../beside/BesideReading';
import { ChannelHomes } from '../conversation/AskView';
import { channelHomeOf } from '../conversation/channelHome';
import { type EarlierPart, EarlierParts } from '../conversation/EarlierParts';
import { toChannelRow } from '../exchanges/seek';
import { ReviewCards } from '../review/card/ReviewCards';
import { dockName } from '../waits/dock';
import { type Said, SaidDoors } from '../conversation/saidDoors';
import type { useHeadAsks } from '../head/useHeadAsks';
import { beganLine } from '../head/partsLife';
import type { PartRows } from '../head/partRows';
import { lineMembersBefore } from '../lineState';
import type { coolingFor } from '../say';
import { AttendedComposer } from './AttendedComposer';
import { sentence } from '../../locales/format';
import { channelRefusal } from '../channel';
import { useLineRows } from '../exchanges/useLineRows';
import { FileOpener, type FileOpen } from '../review/preview';
import type { Roster } from '../start/agentRoster';
import type { useAttendedBox } from '../conversation/useAttendedBox';
import type { useBoxWords } from '../conversation/useBoxWords';
import type { Refusal, useWords } from '../conversation/useWords';
import { useSessionReading } from '../conversation/useSessionReading';
import type { useAttendedHead } from '../head/useAttendedHead';
import type { useAttendedSession } from '../head/useAttendedSession';
import { opinionAsked } from '../gates/opinion';
import { anchorOf } from '../waits/anchors';
import { rulesAsked, type Wait } from '../waits/waits';
import type { useAttendedWaits } from '../waits/useAttendedWaits';
import type { StateLine } from '../head/stateLine';
import { stepsNow } from '../catchup/digest';
import type { ChangesFacts } from './ChangesRail';
import { StoryTree } from './FoldedCalls';
import { HeadChips } from './HeadChips';
import { HeadLineOf, PartBegan } from './LineMarks';
import { beginsByMove } from '../conversation/conversation';
import type { Turn } from '../conversation/conversationTypes';
import { headFactsOf } from './headFacts';
import type { Reads } from './PanelComposer';
import { PanelComposer } from './PanelComposer';
import { SessionPanel } from './SessionPanel';
import { SessionStory } from './SessionStory';
import { storyLineOf, storyOf } from './story';
import { useModelWords } from './PanelHead';

type Box = ReturnType<typeof useAttendedBox>['box'];

const NO_TURNS: readonly Turn[] = [];

/** When the agent reads the person's words, by the box the session is offered. */
export function readsOf(box: Box, taking: boolean): Reads {
  switch (box.kind) {
    case 'chat': return taking ? 'next' : 'now';
    case 'steer': return 'next';
    case 'say': return box.mode === 'goOn' ? 'finished' : 'back';
    default: return 'back';
  }
}

/** How often a running session's changes are read again while they are shown: at most once in this many ms, and at its turn's end. */
export const CHANGES_EVERY = 15_000;

/**
 * The record's moment the Changes rail reads the session's diff at: every move of a session that does not run, and a running one's
 * at most once each `CHANGES_EVERY`, so a turn's stream of events runs git a few times rather than once an event.
 */
function useChangesMoment(session: string, updated: string, live: boolean): string {
  const [kept, setKept] = useState({ session, updated });
  const due = kept.session !== session || !live || Date.parse(updated) - Date.parse(kept.updated) >= CHANGES_EVERY;
  if (due && (kept.session !== session || kept.updated !== updated)) setKept({ session, updated });
  return due ? updated : kept.updated;
}

/** The Changes rail's facts from the session's diff (`SESSION_DIFF`), as the review reads it; none until it is read. */
export function changesOf(diff: ReturnType<typeof useSessionDiff>['data']): ChangesFacts | null {
  if (!diff || !Array.isArray(diff.files)) return null;
  const change = (status: string): ChangesFacts['files'][number]['change'] => (status === 'deleted' ? 'D' : status === 'renamed' ? 'R'
    : status === 'added' || status === 'copied' ? 'A' : 'M');
  return {
    files: diff.files.map((file) => ({ path: file.path, change: change(file.status), added: file.added ?? 0, removed: file.removed ?? 0 })),
    commits: null, pullRequest: null, checks: null,
  };
}

/**
 * **The attended session's panel**: the session panel (`SessionPanel`) composed from what the page already holds. The head from the
 * driver's reader's state line (`headFactsOf`) with the acts, the chips (`HeadChips`) and the ask under it; the run's track; the
 * story from the session's reading (`useSessionReading`: turns, steps, helpers asked, with its find bar, earlier pages, anchors,
 * *Since you left* and the focus) and its line's rows (`useLineRows`: requests each way, quests, channel messages, each at its
 * moment); the live band; the dock (`SessionDock`, the host's); the composer sending through the session's own
 * path (a chat's turn, a parked intake's ask, else the words the session goes on with); under the story its Accept cards
 * (`ReviewCards`); and the Changes rail from the session's diff, read only while the rail or its narrow fact is shown, for a session
 * with a tree of its own. Only where the session ran here: a teammate's record came without its transcript.
 */
export function AttendedSessionPanel({
  t, notify, session, boxState, words, boxWords, waitsState, headState, grouping, roster, refusal, openPreview, along, band, dock,
  prelude, asks, rootOf, attend, coolsUntil, partRows, branch = null, onOpenOutputs,
}: {
  /** The branch its tree left, by its name, for the head's where. */
  branch?: string | null;
  /** An Accept card's *Open outputs*: the run's outputs at its attempt. */
  onOpenOutputs?: (run: string, attempt: number | null) => void;
  asks: ReturnType<typeof useHeadAsks>;
  rootOf: (repository: string) => string | null | undefined;
  attend: (id: string) => void;
  /** When the account a parked or ended driven record ran on cools until, which holds the words it goes on with. */
  coolsUntil: ReturnType<typeof coolingFor>;
  /** How each part of its line began, for *Earlier* and the parts' boundary lines. */
  partRows: PartRows;
  t: TFunction;
  notify: Notify;
  session: ReturnType<typeof useAttendedSession>;
  boxState: ReturnType<typeof useAttendedBox>;
  words: ReturnType<typeof useWords>;
  boxWords: ReturnType<typeof useBoxWords>;
  waitsState: ReturnType<typeof useAttendedWaits>;
  headState: ReturnType<typeof useAttendedHead>;
  grouping: ReturnType<typeof withCall>;
  roster: Roster;
  refusal: Refusal | null;
  openPreview: (open: FileOpen) => void;
  /** What its line's rows read beside the session (who is at work on it, its channel, the dock's keys) and what they press. */
  along: Omit<Parameters<typeof useLineRows>[0], 'session' | 'quest' | 'quests' | 'sessions' | 'helperAsks' | 'channelAsk' | 'only' | 'opinion'>;
  band?: ReactNode;
  dock?: ReactNode;
  /** What the page says of the session above its story: its record's folds (`AttendedPane`). */
  prelude?: ReactNode;
}) {
  const { attended, live, here, attendedEntry } = session;
  const { talking, chatTurns, turns, box, setDraft, onUsage, onWaiting } = boxState;
  const { waits, standing, setAnchored, helperWaits, goToWait, setTail, setBeside } = waitsState;
  const { setChatHeld, panelHead } = headState;
  const scroller = useRef<HTMLDivElement>(null);
  const postChannel = usePostChannel();
  const modelWords = useModelWords();
  // Its line's requests each way, its quests and its channel's messages, each a row the story places at its moment.
  const lines = useLineRows({
    ...along,
    session: attended, quest: session.quest, quests: session.quests.data ?? [], sessions: session.sessions.data ?? [],
    helperAsks: helperWaits?.asks, channelAsk: session.channelAsk ?? null,
    only: attendedEntry ? { entry: attendedEntry.entry, repository: attendedEntry.repository } : null,
    opinion: opinionAsked(waits?.landing) ?? null,
  });
  const toWait = (wait: Wait) => {
    const anchor = anchorOf(wait);
    if (anchor) goToWait(anchor);
  };
  const reading = useSessionReading({
    session: attended?.id ?? '',
    adapter: attended?.door,
    chat: attended?.kind === 'chat',
    live,
    turnRunning: attended && talking && chatTurns[attended.id] ? turns.taking : undefined,
    scroller,
    catchup: attended ? { line: attended.line ?? attended.id, waiting: standing.length, steps: stepsNow(grouping?.passage) } : undefined,
    helpersAsked: helperWaits ? { asks: helperWaits.asks, nameOf: helperWaits.nameOf, onOpen: helperWaits.onOpen } : undefined,
    onBeside: setBeside,
    waits: waits && attended ? {
      waits: standing, goAheads: waits.goAheads, opinion: opinionAsked(waits.landing)?.opinion ?? null,
      proposals: rulesAsked(waits.rules?.proposals, attended.id), helpers: waits.helpers?.asks, permissions: waits.permissions?.waits,
    } : undefined,
    onAnchors: (id, state) => setAnchored({ session: id, ...state }),
    onToWait: toWait,
    onTail: setTail,
    onUsage,
    onWaiting,
    onLimitWait: (id, held) => setChatHeld((was) => (held ? { session: id, held } : was?.session === id ? null : was)),
  });
  // Its changes, read only while the rail or its narrow fact is shown and only for a tree of its own: a chat with none has no diff.
  const [changesShown, setChangesShown] = useState(false);
  const moment = useChangesMoment(attended?.id ?? '', attended?.updated ?? '', live);
  const diff = useSessionDiff(
    changesShown && here && attended?.tree ? attended.id : null, attended ? { updated: moment, live } : null,
  );
  const passage = grouping?.passage?.steps.length ? grouping.passage : null;
  // A teammate's record came without its transcript, which stays on their machine: its story is its run's steps alone.
  const storyTurns = here ? reading.turns : NO_TURNS;
  const story = useMemo(
    () => storyOf(storyLineOf({ turns: storyTurns, passage, exchanges: reading.exchanges, rows: lines?.rows })),
    [storyTurns, passage, reading.exchanges, lines?.rows],
  );
  if (!attended || !panelHead) return null;

  const line: StateLine = panelHead.line ?? { kind: 'unread', since: null, cause: null } as unknown as StateLine;
  const maker = roster.find((row) => row.door === attended.door)?.product || attended.door;
  const facts = headFactsOf({ session: attended, line, title: panelHead.title, maker, branch });
  // A wait on the person names the oldest, and leads to it in the dock; what it does now, else.
  const oldest = line.wait ? dockName(t, line.wait.wait) : null;
  const head = {
    ...facts,
    doing: oldest ? (line.more > 0 ? t('work.head.state.more', { name: oldest, n: line.more }) : oldest) : facts.doing,
    wraps: Boolean(attended.quest || attended.ask),
  };
  const channel = attendedEntry && session.channelAsk && along.record.channel.available
    ? channelHomeOf(along.record.channel.channel, along.nameOf ?? ((agent) => agent), toChannelRow)
    : undefined;
  const onAskWord = (text: string) => {
    const ask = attended.ask;
    if (!ask) return;
    const giveBack = () => setDraft((was) => [text, was.trim()].filter(Boolean).join('\n\n'));
    postChannel.mutate({ ask, post: { text } }, {
      onSuccess: (posted) => {
        if (posted.posted) { notify(t('work.intake.posted', { ask })); return; }
        giveBack();
        notify(channelRefusal(t, posted.code ?? 'other'), 'error');
      },
      onError: (error) => { giveBack(); notify(sentence(error), 'error'); },
    });
  };
  const onSend = (text: string) => {
    if (box.kind === 'chat') words.onSend(text);
    else if (box.kind === 'ask') onAskWord(text);
    else boxWords.onSay(text);
  };
  const beganOf = (id: string) => {
    const part = attendedEntry?.parts.find((each) => each.session === id);
    return attendedEntry && part && part.began !== 'opened' ? beganLine(t, part, attendedEntry.parts, partRows) : undefined;
  };
  // The line's parts before the one it heads, by its membership (each record's `line`), and how each began.
  const earlier: EarlierPart[] = here ? lineMembersBefore(session.sessions.data, attended.id).flatMap((id) => {
    const row = (session.sessions.data ?? []).find((each) => each.id === id);
    return row ? [{ id, adapter: row.door, chat: row.kind === 'chat', tree: row.tree, live: SESSION_ACTIVE.has(row.state), began: beganOf(id) }] : [];
  }) : [];
  const began = here && reading.loaded && !reading.earlier && !beginsByMove(reading.turns) ? beganOf(attended.id) : undefined;
  const { onStartFrom, onGoOnBrief, onSayAgain, onGoOnNew, goOnNew, newSession } = words;
  const doors: Said = {
    chat: attended.kind === 'chat',
    onSession: attend,
    // Words that cannot go on here: one press starts a conversation with them, or the same session goes on handed Daoris's brief.
    onStartFrom: rootOf(attended.repository) ? onStartFrom : undefined,
    onGoOnBrief: rootOf(attended.repository) ? onGoOnBrief : undefined,
    onSayAgain,
    cooling: coolsUntil && attended.kind !== 'chat' ? {
      until: coolsUntil, quest: attended.quest, onGoOnNew, pending: goOnNew.isPending,
      answer: newSession?.session === attended.id ? newSession.answer : null,
    } : undefined,
    reasons: {
      from: attended.door, to: session.harnesses.data?.adapter, door: session.harnesses.data?.adapter, agent: maker,
    },
  };
  return (
    <FileOpener.Provider value={openPreview}><StoryTree.Provider value={attended.tree ?? null}>
      <SaidDoors.Provider value={doors}>
        <SessionPanel
          facts={{
            id: attended.id, head, track: null, story, waits: [], changes: changesOf(diff.data), now: Date.now(),
            reads: readsOf(box, Boolean(turns.taking)), onAnswer: () => {}, onSend,
            onFile: (path) => openPreview({ path }),
            onReview: waits?.onReview,
          }}
          onChangesShown={setChangesShown}
          onWait={panelHead.onWait}
          // Its facts line, less the repository the where line names.
          headFacts={{ said: panelHead.facts.filter((fact) => fact !== attended.repository), tip: panelHead.factsTip }}
          acts={panelHead.acts}
          chips={(
            <>
              {panelHead.lineOf && <HeadLineOf said={panelHead.lineOf.said} onOpen={panelHead.lineOf.onOpen} />}
              <HeadChips session={attended} supervision={panelHead.chip} />
            </>
          )}
          track={panelHead.track || undefined}
          asking={panelHead.asking || undefined}
          band={band}
          dock={dock}
          reading={{ scroller, atTail: !reading.away, toTail: reading.toTail }}
          story={(
            <BesideReading session={attended.id}><ChannelHomes.Provider value={channel ?? {}}><SessionStory
              reading={reading} story={story} actor={{ maker, model: head.model }} scroller={scroller}
              rowHome={lines?.home} onToWait={toWait} reader={BESIDE_READER}
              after={here ? <ReviewCards session={attended.id} onOpenOutputs={onOpenOutputs} /> : undefined}
              prelude={(
                <>
                  {prelude}
                  {/* The parts it continues, above it and read-only; keyed by the part, so attending another starts the count again. */}
                  {earlier.length > 0 && (
                    <EarlierParts key={`${attended.id}:${session.openedAt ?? ''}`} line={earlier} at={session.openedAt} scroller={scroller} />
                  )}
                  {began && <PartBegan words={began} />}
                </>
              )}
            /></ChannelHomes.Provider></BesideReading>
          )}
          composer={box.kind === 'none' ? null : (
            <PanelComposer reads={readsOf(box, Boolean(turns.taking))} chip={`${maker} · ${modelWords(head)}`}>
              <AttendedComposer
                t={t} notify={notify} session={session} boxState={boxState} words={words} boxWords={boxWords} asks={asks}
                waitsState={waitsState} headState={headState} refusal={refusal} roster={roster} rootOf={rootOf} attend={attend}
                onAskWord={onAskWord} askSending={postChannel.isPending}
              />
            </PanelComposer>
          )}
        />
      </SaidDoors.Provider>
    </StoryTree.Provider></FileOpener.Provider>
  );
}
