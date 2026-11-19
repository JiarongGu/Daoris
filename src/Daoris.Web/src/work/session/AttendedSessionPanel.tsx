import { type ReactNode, useRef } from 'react';
import type { TFunction } from 'i18next';
import { type useSessionDiff, usePostChannel, type withCall } from '../../bridge/shell';
import type { Notify } from '../../design/ui';
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
import { rulesAsked } from '../waits/waits';
import type { useAttendedWaits } from '../waits/useAttendedWaits';
import type { StateLine } from '../head/stateLine';
import { stepsNow } from '../catchup/digest';
import type { ChangesFacts } from './ChangesRail';
import { HeadChips } from './HeadChips';
import { headFactsOf } from './headFacts';
import type { Reads } from './PanelComposer';
import { PanelComposer } from './PanelComposer';
import { SessionPanel } from './SessionPanel';
import { SessionStory } from './SessionStory';
import { storyLineOf, storyOf } from './story';
import { useModelWords } from './PanelHead';

type Box = ReturnType<typeof useAttendedBox>['box'];

/** When the agent reads the person's words, by the box the session is offered. */
export function readsOf(box: Box, taking: boolean): Reads {
  switch (box.kind) {
    case 'chat': return taking ? 'next' : 'now';
    case 'steer': return 'next';
    case 'say': return box.mode === 'goOn' ? 'finished' : 'back';
    default: return 'back';
  }
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
 * path (a chat's turn, a parked intake's ask, else the words the session goes on with); and the Changes rail from the session's
 * diff. Only where the session ran here: a teammate's record came without its transcript.
 */
export function AttendedSessionPanel({
  t, notify, session, boxState, words, boxWords, waitsState, headState, grouping, roster, refusal, openPreview, along, band, dock,
  prelude, diff,
}: {
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
  diff: ReturnType<typeof useSessionDiff>['data'];
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
    onToWait: (wait) => {
      const anchor = anchorOf(wait);
      if (anchor) goToWait(anchor);
    },
    onTail: setTail,
    onUsage,
    onWaiting,
    onLimitWait: (id, held) => setChatHeld((was) => (held ? { session: id, held } : was?.session === id ? null : was)),
  });
  if (!attended || !panelHead) return null;

  const passage = grouping?.passage?.steps.length ? grouping.passage : null;
  // A teammate's record came without its transcript, which stays on their machine: its story is its run's steps alone.
  const story = storyOf(storyLineOf({ turns: here ? reading.turns : [], passage, exchanges: reading.exchanges, rows: lines?.rows }));
  const line: StateLine = panelHead.line ?? { kind: 'unread', since: null, cause: null } as unknown as StateLine;
  const maker = roster.find((row) => row.door === attended.door)?.product || attended.door;
  const head = headFactsOf({ session: attended, line, title: panelHead.title, maker });
  const refused = refusal?.session === attended.id ? refusal.text : null;
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
  const takes = box.kind !== 'none' && box.kind !== 'line';
  return (
    <FileOpener.Provider value={openPreview}>
      <SessionPanel
        facts={{
          id: attended.id, head, track: null, story, waits: [], changes: changesOf(diff), now: Date.now(),
          reads: readsOf(box, Boolean(turns.taking)), onAnswer: () => {}, onSend,
          onFile: (path) => openPreview({ path }),
        }}
        acts={panelHead.acts}
        chips={<HeadChips session={attended} supervision={panelHead.chip} />}
        track={panelHead.track || undefined}
        asking={panelHead.asking || undefined}
        band={band}
        dock={dock}
        reading={{ scroller, atTail: !reading.away, toTail: reading.toTail }}
        story={(
          <SessionStory
            session={attended.id} reading={reading} story={story} actor={{ maker, model: head.model }} scroller={scroller} prelude={prelude}
            rowHome={lines?.home}
          />
        )}
        composer={(
          <div className="grid gap-1">
            <PanelComposer
              key={attended.id}
              reads={readsOf(box, Boolean(turns.taking))}
              chip={`${maker} · ${modelWords(head)}`}
              onSend={onSend}
              disabled={!takes}
            />
            {refused && <p role="alert" className="m-0 text-small text-ink-danger">{refused}</p>}
          </div>
        )}
      />
    </FileOpener.Provider>
  );
}
