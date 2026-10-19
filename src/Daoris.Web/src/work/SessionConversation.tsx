import { type RefObject, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { useDebounced } from '../lib/useDebounced';
import { searchable } from '../lib/searchable';
import { useHarnesses, useSessionEvents, useSessionSearch } from '../bridge/shell';
import { Icon } from '../design/ui';
import { accountNames } from './accountNames';
import { type AnchorAsks, type Anchors, anchorsOf, type AnchorWait, marksOf } from './anchors';
import { firstAnswerWait, lastWords, type RunningCall, runningCall, settle, toTurns, type Usage } from './conversation';
import { ConversationFind } from './ConversationFind';
import { type CallMark, type Cooling, ConversationView } from './ConversationView';
import type { ReasonValues } from './say';
import { useFollowTail } from './followTail';
import type { Wait } from './waits';

/** How many blocks a conversation holds before it is offered a way through (SESS1 S9). */
const LONG = 20;

/** Whether the reader is at the conversation's tail, and the way there: what a host that draws the press is told. */
export type TailState = { atTail: boolean; toTail: () => void };

/**
 * The waits whose raising event the conversation holds, or may hold on a page before, and the way to one (WAITFOOT1c, D168
 * point 4): what a host that draws *In the conversation* is told.
 */
export type AnchorState = { anchored: AnchorWait[]; show: (wait: AnchorWait) => void };

const NO_ANCHORS: Anchors = { held: {}, earlier: [] };

/**
 * *Back to bottom* as a press of its own (ASKHIST1c): 28 px, for a strip its host keeps outside the transcript, so it takes
 * its own room and never covers the words it leads back to.
 */
export function BackToBottom({ onPress, className }: { onPress: () => void; className?: string }) {
  const { t } = useTranslation();
  return (
    <button
      type="button"
      onClick={onPress}
      className={cn(
        'flex h-7 cursor-pointer items-center gap-1.5 rounded-control border border-line-strong bg-raised px-2.5 text-small text-ink-soft hover:text-ink',
        className,
      )}
    >
      <Icon name="toBottom" size={13} />
      {t('work.conversation.bottom')}
    </button>
  );
}

/**
 * The attended session's conversation (D76, CONV2): the organism that holds its record and its
 * scroll, so the view below it holds neither.
 *
 * @remarks
 * **It follows the tail until the person scrolls up**, and then offers the way back — the rule the
 * console has kept since SURF4c, applied to the region the conversation scrolls in, which is the
 * frame's centre (handed in, since the head above scrolls with it).
 *
 * **A long run gets a way through** (SESS1 S9): a jump to where it first failed and to its last words,
 * and a search within it. **A jump lands on an event**: the pages before it are loaded until it is
 * held, then its block is shown, its fold open, and scrolled to. The frame jumps too, to the event that raised a wait on the
 * person (WAITFOOT1c, `onAnchors`).
 *
 * Desktop-only for the console's reason (D47 §4): the record arrives over the bridge.
 */
export function SessionConversation({
  session, adapter, chat = false, tree, live, turnRunning, scroller, onUsage, onWaiting, onSession, onStartFrom, reasons, cooling, onTail,
  waits, onAnchors, onToWait, callMark,
}: {
  /** The call the driver's grouping says the session is in, held or not (AUTONOMY1b3e) — `ConversationView`'s. */
  callMark?: CallMark;
  /**
   * What waits on the person in this session, and what its joins name (WAITFOOT1c, D168 point 4): the waits that stand, its
   * go-aheads, the opinion the landing's gate names. Each is anchored to the event that raised it where the join finds one.
   */
  waits?: Omit<AnchorAsks, 'session'>;
  /**
   * Told which waits it holds the raising event of, and the way to one, on a change only (WAITFOOT1c): the jump this organism
   * kept to itself, the frame's to press, as `onTail` is. Pressed, the event is shown and outlined, the pages before it loaded
   * first where it is older than the page.
   */
  onAnchors?: (session: string, anchors: AnchorState) => void;
  /** Go to a wait's line where it is answered: the raising block's *To the wait*. Absent, no block is marked. */
  onToWait?: (wait: Wait) => void;
  /**
   * Told whether the reader is at the tail and how to go there (ASKHIST1c), where the host draws *Back to bottom* in a strip
   * of its own outside the transcript (`BackToBottom`). Absent, the press is drawn at the transcript's foot, as before.
   */
  onTail?: (tail: TailState) => void;
  /** The account the person's words wait for cools, and *Go on in a new session* (MSG1g2) — `ConversationView`'s. */
  cooling?: Cooling;
  /** Attend the session the person's words went to (MSG1f); absent, it is named as text. */
  onSession?: (id: string) => void;
  /** Start a conversation with words that cannot go on here (MSG1f, D137 §2.2); absent, no press is offered. */
  onStartFrom?: (words: string[]) => void;
  /** What a reason the driver gave by code may name (D137 §5.1). */
  reasons?: ReasonValues;
  session: string;
  /** The harness it runs on — whose declaration says whether its door keeps a conversation (D76 §1). */
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
   * Told the context reading whenever it changes (CONV5), for the ring under the composer — which
   * reads this record rather than holding a second one: the stream has one home.
   */
  onUsage?: (session: string, usage?: Usage) => void;
  /** Told when the turn its agent is on was asked, while the agent has answered nothing (WAITSHOWN1); undefined once it has. */
  onWaiting?: (session: string, since?: string, calling?: RunningCall) => void;
}) {
  const { events, opening, firstFailure, earlier, loaded, loadEarlier } = useSessionEvents(session);
  // The door's own word on what an empty record means — undefined until the roster answers, which reads
  // as the console sentence: it claims least.
  const harnesses = useHarnesses();
  const structured = harnesses.data?.harnesses?.find((row) => row.harness === adapter)?.structured;
  // What the person calls each account, so the opening line and a wait name one as the agents' page does (NOTENAME1).
  const roster = harnesses.data?.harnesses;
  const names = useMemo(() => accountNames(Array.isArray(roster) ? roster : []), [roster]);
  // Read from what was asked (SESS1 S1), and as an ended session leaves it: a turn it ended inside says
  // so, and its open calls read stopped rather than running for good (S4).
  const { turns, usage, where } = useMemo(() => {
    const read = toTurns(events, { opening });
    return { turns: settle(read.turns, live), usage: read.usage, where: read.where };
  }, [events, opening, live]);

  // Told on a change of reading only: a callback that changed on every render would otherwise tell the
  // frame, which re-renders, which hands a new callback, round and round.
  const report = useRef(onUsage);
  report.current = onUsage;
  useEffect(() => { report.current?.(session, usage); }, [session, usage]);
  // The wait for the first answer goes up the same way (WAITSHOWN1), for the head's facts; only while the session is live.
  const waitingSince = live ? firstAnswerWait(turns) : undefined;
  // And the call it is inside (WAITSAY1), read from the settled turns, so a session not live says none.
  const call = live ? runningCall(turns) : null;
  const callTitle = call?.title;
  const callSince = call?.since;
  const reportWait = useRef(onWaiting);
  reportWait.current = onWaiting;
  useEffect(() => {
    reportWait.current?.(session, waitingSince, callTitle && callSince ? { title: callTitle, since: callSince } : undefined);
  }, [session, waitingSince, callTitle, callSince]);

  // What changes when the conversation grows: the last event, and its text as chunks join it.
  const last = events[events.length - 1];
  // An ended session opens at its head, which says how it ended (SESS2 H1); a live one follows its tail.
  const { atTail, toTail } = useFollowTail(scroller, `${last?.seq ?? 0}:${turns.length}`, `${session}:${loaded}`, !live);
  // Told on a change only, as the usage is; and at the tail again once this conversation is gone, so no host keeps a press
  // for a transcript it no longer shows.
  const tell = useRef(onTail);
  tell.current = onTail;
  const away = !atTail && turns.length > 0;
  useEffect(() => {
    tell.current?.({ atTail: !away, toTail: () => toTail(true) });
  }, [away, toTail]);
  useEffect(() => () => tell.current?.({ atTail: true, toTail: () => {} }), []);

  // The way through a long run (SESS1 S9).
  const long = earlier || turns.reduce((count, turn) => count + turn.items.length, 0) > LONG;
  const [query, setQuery] = useState('');
  const settled = useDebounced(query, 300);
  const found = useSessionSearch(long ? settled : '', session);
  const hits = found.data?.hits ?? [];
  const [at, setAt] = useState(0);
  // Where a jump is headed, by event, until the page holds it; and the block it landed on, each time.
  const [pending, setPending] = useState<number | null>(null);
  const [reveal, setReveal] = useState<{ key: string; n: number } | null>(null);
  const show = (key: string) => setReveal((was) => ({ key, n: (was?.n ?? 0) + 1 }));

  // The wait a press is headed for, until the page holds the event that raised it (WAITFOOT1c).
  const [seeking, setSeeking] = useState<AnchorWait | null>(null);

  useEffect(() => {
    setQuery('');
    setPending(null);
    setReveal(null);
    setSeeking(null);
  }, [session]);
  useEffect(() => { setAt(0); }, [found.data]);

  // 🔴 A jump to an event the page does not hold loads the pages before it until it does — one at a
  // time, each load changing what is held and so running this again — and gives up where none are left.
  useEffect(() => {
    if (pending === null) return;
    const key = where[pending];
    if (key) {
      show(key);
      setPending(null);
    } else if (earlier) {
      void loadEarlier();
    } else {
      setPending(null);
    }
  }, [pending, where, earlier, loadEarlier]);

  // Where each wait was raised (WAITFOOT1c, D168 point 4), read from what the page holds: the asks compared by value, since the
  // frame hands a new object on every render.
  const asked = JSON.stringify(waits ?? null);
  const from = events[0]?.at;
  const anchors = useMemo(
    () => (waits ? anchorsOf(turns, { ...waits, session }, { earlier, from }) : NO_ANCHORS),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `asked` stands for `waits`
    [turns, asked, session, earlier, from],
  );
  const marks = useMemo(() => (onToWait && waits ? marksOf(waits.waits, anchors.held) : undefined),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `asked` stands for `waits`
    [anchors, asked, Boolean(onToWait)]);
  const anchored = [...Object.keys(anchors.held), ...anchors.earlier] as AnchorWait[];
  const said = anchored.join(' ');
  const tellAnchors = useRef(onAnchors);
  tellAnchors.current = onAnchors;
  useEffect(() => {
    tellAnchors.current?.(session, { anchored: said ? said.split(' ') as AnchorWait[] : [], show: setSeeking });
  }, [session, said]);
  useEffect(() => () => tellAnchors.current?.(session, { anchored: [], show: () => {} }), [session]);

  // 🔴 A press for a wait whose event the page does not hold loads the pages before until it does, as a jump to an event does,
  // and gives up where none are left or the join says it would be held by now.
  useEffect(() => {
    if (seeking === null) return;
    const key = anchors.held[seeking];
    if (key) {
      show(key);
      setSeeking(null);
    } else if (earlier && anchors.earlier.includes(seeking)) {
      void loadEarlier();
    } else {
      setSeeking(null);
    }
  }, [seeking, anchors, earlier, loadEarlier]);

  useEffect(() => {
    if (!reveal) return;
    const frame = requestAnimationFrame(() => {
      scroller.current?.querySelector(`[data-block="${reveal.key}"]`)?.scrollIntoView({ block: 'center' });
    });
    return () => cancelAnimationFrame(frame);
  }, [reveal, scroller]);

  const words = lastWords(turns);
  const step = (by: 1 | -1) => {
    if (hits.length === 0) return;
    const next = (at + by + hits.length) % hits.length;
    setAt(next);
    setPending(hits[next]!.seq);
  };

  return (
    <>
      <ConversationView
        turns={turns}
        tree={tree}
        structured={structured}
        chat={chat}
        live={live}
        turnRunning={turnRunning}
        callMark={callMark}
        loaded={loaded}
        earlier={earlier}
        onLoadEarlier={() => void loadEarlier()}
        reveal={reveal?.key}
        onSession={onSession}
        onStartFrom={onStartFrom}
        reasons={reasons}
        cooling={cooling}
        names={names}
        marks={marks}
        onToWait={onToWait}
        toolbar={long ? (
          <ConversationFind
            hasFailure={firstFailure !== null}
            onFirstFailure={() => { if (firstFailure !== null) setPending(firstFailure); }}
            hasWords={words !== undefined}
            onLastWords={() => { if (words) show(words); }}
            query={query}
            onQuery={(next) => {
              setQuery(next);
              setAt(0);
            }}
            hits={searchable(settled) && found.data ? hits.length : undefined}
            at={at}
            cut={found.data?.cut}
            onStep={step}
          />
        ) : undefined}
      />
      {/* Where no host draws it: at the transcript's foot, held in view as it scrolls (Sessions' centre, a detached window,
          the monitor's tile). */}
      {away && !onTail && (
        <BackToBottom onPress={() => toTail(true)} className="sticky bottom-2 ml-auto bg-overlay shadow-sm" />
      )}
    </>
  );
}
