import { type ComponentProps, createContext, type ReactNode, type RefObject, useContext, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { searchable } from '../../lib/searchable';
import { useChildHistory, useSessionConsole } from '../../bridge/shell';
import type { BesideReader, ChildRead } from '../beside/besides';
import type { BandTold } from '../band/bandRows';
import { SessionConsole } from '../console/SessionConsole';
import { Icon } from '../../design/ui';
import type { AnchorAsks } from '../waits/anchors';
import { beginsByMove, type RunningCall, type Usage } from './conversation';
import { ConversationFind } from './ConversationFind';
import { type CallMark, ConversationView } from './ConversationView';
import type { Cooling } from './saidDoors';
import type { ChannelHome } from './channelHome';
import type { HelperAskRow } from '../waits/HelperAsks';
import type { ReasonValues } from '../say';
import type { Wait } from '../waits/waits';
import type { ChatLimitHeld } from '../waits/limitWait';
import type { CatchUpFacts } from '../catchup/useCatchUp';
import { SinceYouLeft } from '../catchup/SinceYouLeft';
import { type AnchorState, type TailState, useSessionReading } from './useSessionReading';

export type { AnchorState, TailState } from './useSessionReading';

/** A long run's way through, as its reading offers it: the jumps, the search and its hits. */
export function ReadingFind({ find }: { find: NonNullable<ReturnType<typeof useSessionReading>['find']> }) {
  const { settled, hits, ...presses } = find;
  return <ConversationFind {...presses} hits={searchable(settled) ? hits : undefined} />;
}

/** The session whose subagents' and tasks' rows the reader below reads for. */
const ReadingSession = createContext('');

/** A subagent's own record, read through the driver for the row that names it. */
function ChildReader({ child, running = false, children }: { child: string; running?: boolean; children: (read: ChildRead) => ReactNode }) {
  return children(useChildHistory(useContext(ReadingSession), child, running));
}

/** A background task's last lines, from its own console stream (`<session>/task/<id>`), and when the page heard its last. */
function TaskTail({ task, children }: { task: string; children: (lines: readonly string[], heard: number | null) => ReactNode }) {
  const tail = useSessionConsole(`${useContext(ReadingSession)}/task/${task}`);
  const last = tail.lines[tail.lines.length - 1]?.sequence ?? 0;
  const [heard, setHeard] = useState<number | null>(null);
  useEffect(() => { if (last > 0) setHeard(Date.now()); }, [last]);
  return children(tail.lines.slice(-3).map((line) => line.text), heard);
}

/** A background task's whole console, in the panel's focus. */
function TaskStream({ task }: { task: string }) {
  const { t } = useTranslation();
  return <SessionConsole id={`${useContext(ReadingSession)}/task/${task}`} quiet={t('work.panel.streamSilent')} />;
}

/** How the rows and the live band read what a session runs beside itself, under `BesideReading`. */
export const BESIDE_READER: BesideReader = { Child: ChildReader, Tail: TaskTail, Stream: TaskStream };

/** The session whose subagents and tasks `BESIDE_READER` reads, for a host outside the conversation: the live band. */
export function BesideReading({ session, children }: { session: string; children: ReactNode }) {
  return <ReadingSession.Provider value={session}>{children}</ReadingSession.Provider>;
}

/**
 * The two scroll presses' one look: a 28 px square with its arrow alone, its name its label and its tooltip. Their words once
 * stood beside the arrow and covered the messages under them (the owner's report, 2026-10-24); the arrow says the act.
 */
const SCROLL_PRESS =
  'flex h-7 w-7 cursor-pointer items-center justify-center rounded-control border border-line-strong bg-raised text-ink-soft hover:text-ink';

/**
 * *Back to bottom* as a press of its own: for a strip its host keeps outside the transcript, so it takes its own
 * room and never covers the words it leads back to.
 */
export function BackToBottom({ onPress, className }: { onPress: () => void; className?: string }) {
  const { t } = useTranslation();
  const name = t('work.conversation.bottom');
  return (
    <button type="button" onClick={onPress} aria-label={name} title={name} className={cn(SCROLL_PRESS, className)}>
      <Icon name="toBottom" size={14} />
    </button>
  );
}

/**
 * *To the top* as a press of its own: the record's head, where every wait stands with its reading. Drawn beside
 * *Back to bottom* above the waiting panel, as the strip's *To the top* is, so neither is ever under the panel.
 */
export function ToTheTop({ onPress, className }: { onPress: () => void; className?: string }) {
  const { t } = useTranslation();
  const name = t('work.wait.top');
  return (
    <button type="button" onClick={onPress} aria-label={name} title={name} className={cn(SCROLL_PRESS, className)}>
      <Icon name="toTop" size={14} />
    </button>
  );
}

/**
 * The attended session's conversation: the organism that holds its record and its
 * scroll, so the view below it holds neither.
 *
 * @remarks
 * **It follows the tail until the person scrolls up**, and then offers the way back — the rule the
 * console keeps, applied to the region the conversation scrolls in, which is the
 * frame's centre (handed in, since the head above scrolls with it).
 *
 * **A long run gets a way through**: a jump to where it first failed and to its last words,
 * and a search within it. **A jump lands on an event**: the pages before it are loaded until it is
 * held, then its block is shown, its fold open, and scrolled to. The frame jumps too, to the event that raised a wait on the
 * person (`onAnchors`).
 *
 * Desktop-only for the console's reason: the record arrives over the bridge.
 */
export function SessionConversation({
  session, adapter, chat = false, tree, live, mode = 'attended', began, turnRunning, scroller, onUsage, onWaiting, onSession, onStartFrom, reasons, cooling, onTail,
  waits, onAnchors, onToWait, callMark, onSayAgain, onGoOnBrief, onLimitWait, helpersAsked, channel, dividers, beneath, onBeside, catchup,
}: {
  /** Its line, what waits and its steps, for the read mark and *Since you left*; absent, no mark is read or moved. */
  catchup?: CatchUpFacts;
  dividers?: Readonly<Record<string, ReactNode>>;
  beneath?: ComponentProps<typeof ConversationView>['beneath'];
  /** The home of the channel messages its turns took, the attended session's alone — `ConversationView`'s. */
  channel?: ChannelHome;
  /**
   * The helpers its agent asked for, each drawn as its card at its call with its answer folded in: the session's asks as the
   * frame read them, the agents' names, and the door to a helper's conversation.
   */
  helpersAsked?: { asks: readonly HelperAskRow[]; nameOf?: (agent: string) => string; onOpen?: (helper: string) => void };
  /** *Go on with Daoris's brief*, beside the fresh start where the words wait for the person — `ConversationView`'s. */
  onGoOnBrief?: () => void;
  /** *Say it again*: the person's own words that may have reached the agent, said anew — `ConversationView`'s. */
  onSayAgain?: (words: string[]) => void;
  /** The call the driver's grouping says the session is in, held or not (AUTONOMY1b3e) — `ConversationView`'s. */
  callMark?: CallMark;
  /**
   * Told what the live band above the dock draws, on a change: the record's starts and ends of what the session runs beside itself,
   * its agent's act, and the way to put one in the panel's focus. Told empty once this conversation is gone.
   */
  onBeside?: (told: BandTold) => void;
  /**
   * What waits on the person in this session, and what its joins name: the waits that stand, its
   * go-aheads, the opinion the landing's gate names. Each is anchored to the event that raised it where the join finds one.
   */
  waits?: Omit<AnchorAsks, 'session'>;
  /**
   * Told which waits it holds the raising event of, and the way to one, on a change only: the jump this organism
   * kept to itself, the frame's to press, as `onTail` is. Pressed, the event is shown and outlined, the pages before it loaded
   * first where it is older than the page.
   */
  onAnchors?: (session: string, anchors: AnchorState) => void;
  /** Go to a wait's line where it is answered: the raising block's *To the wait*. Absent, no block is marked. */
  onToWait?: (wait: Wait) => void;
  /**
   * Told whether the reader is at the tail and how to go there, where the host draws *Back to bottom* in a strip
   * of its own outside the transcript (`BackToBottom`). Absent, the press is drawn at the transcript's foot.
   */
  onTail?: (tail: TailState) => void;
  /** The account the person's words wait for cools, and *Go on in a new session* — `ConversationView`'s. */
  cooling?: Cooling;
  /** Attend the session the person's words went to; absent, it is named as text. */
  onSession?: (id: string) => void;
  /** Start a conversation with words that cannot go on here; absent, no press is offered. */
  onStartFrom?: (words: string[]) => void;
  /** What a reason the driver gave by code may name. */
  reasons?: ReasonValues;
  /**
   * **`earlier`: a part of a line the attended one continues**, drawn read-only above
   * it. It is read with its own record's `live` and no other, since `settle` reads a part that is not live as ended; it owns neither
   * the region's scroll nor any fact of the frame, so it tells its host nothing of its usage, its wait, its limit's wait, its tail or
   * its anchors, and offers no act, no wait and no way through itself. `attended` is the part the page is on, which owns all of them.
   */
  mode?: 'attended' | 'earlier';
  /**
   * How the part began, in the strip's words, drawn as its one boundary line where it began by no move (a sibling's request, a
   * *Try again*, a resume into a new part) and its events hold no first line of a move's own.
   */
  began?: string;
  session: string;
  /** The harness it runs on — whose declaration says whether its door keeps a conversation. */
  adapter?: string;
  /** Whether it is a conversation with a person. */
  chat?: boolean;
  /** The session's tree, so a tool's path inside it reads relative to it. */
  tree?: string | null;
  live: boolean;
  /** Whether a turn is in flight, where the driver says so — `ConversationView`'s. */
  turnRunning?: boolean;
  /** The region the conversation scrolls in. */
  scroller: RefObject<HTMLElement | null>;
  /**
   * Told the context reading whenever it changes, for the ring under the composer — which
   * reads this record rather than holding a second one: the stream has one home.
   */
  onUsage?: (session: string, usage?: Usage) => void;
  /** Told when the turn its agent is on has begun, while the agent has answered nothing; undefined once it has. */
  onWaiting?: (session: string, since?: string, calling?: RunningCall, retrying?: string) => void;
  /**
   * Told whether the conversation waits for its account's limit, read from its events, on a change only:
   * the head and the foot say the wait from it, so the record has one reader.
   */
  onLimitWait?: (session: string, held: ChatLimitHeld | null) => void;
}) {
  const reading = useSessionReading({
    session, adapter, chat, live, mode, turnRunning, scroller, catchup, helpersAsked, onBeside, waits, onAnchors, onToWait, onTail,
    onUsage, onWaiting, onLimitWait,
  });
  const { own, turns, structured, names, loaded, earlier, loadEarlier, reveal, show, marks, exchanges, caught, now, focus, setFocus,
    lifetimes, away, toTail, find } = reading;

  useEffect(() => {
    if (!reveal) return;
    const frame = requestAnimationFrame(() => {
      scroller.current?.querySelector(`[data-block="${reveal.key}"]`)?.scrollIntoView({ block: 'center' });
    });
    return () => cancelAnimationFrame(frame);
  }, [reveal, scroller]);

  return (
    <ReadingSession.Provider value={session}>
      <ConversationView
        beside={{ reader: BESIDE_READER, lifetimes, focus, onFocus: setFocus }}
        atMark={caught.digest && caught.read
          ? { turn: caught.digest.turn, row: <SinceYouLeft digest={caught.digest} read={caught.read} now={now} onJump={show} /> }
          : undefined}
        turns={turns}
        tree={tree}
        structured={structured}
        chat={chat}
        live={live}
        turnRunning={turnRunning}
        callMark={own ? callMark : undefined}
        began={!earlier && !beginsByMove(turns) ? began : undefined}
        loaded={loaded}
        earlier={earlier}
        onLoadEarlier={() => void loadEarlier()}
        reveal={reveal?.key}
        onSession={own ? onSession : undefined}
        onStartFrom={own ? onStartFrom : undefined}
        onGoOnBrief={own ? onGoOnBrief : undefined}
        onSayAgain={own ? onSayAgain : undefined}
        reasons={reasons}
        cooling={own ? cooling : undefined}
        names={names}
        marks={marks}
        onToWait={own ? onToWait : undefined}
        exchanges={exchanges}
        onOpenHelper={own ? helpersAsked?.onOpen : undefined}
        onShow={show}
        channel={own ? channel : undefined}
        dividers={own ? dividers : undefined}
        beneath={own ? beneath : undefined}
        toolbar={find ? <ReadingFind find={find} /> : undefined}
      />
      {/* Where no host draws it: at the transcript's foot, held in view as it scrolls (Sessions' centre, a detached window,
          the monitor's tile). */}
      {away && own && !onTail && (
        <BackToBottom onPress={() => toTail(true)} className="sticky bottom-2 ml-auto bg-overlay shadow-sm" />
      )}
    </ReadingSession.Provider>
  );
}
