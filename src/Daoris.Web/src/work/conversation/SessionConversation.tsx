import { type ComponentProps, type ReactNode, type RefObject, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { useDebounced } from '../../lib/useDebounced';
import { searchable } from '../../lib/searchable';
import { useHarnesses, useSessionEvents, useSessionSearch } from '../../bridge/shell';
import { Icon } from '../../design/ui';
import { accountNames } from '../accountNames';
import { type AnchorAsks, type Anchors, anchorsOf, type AnchorWait, marksOf } from '../waits/anchors';
import { beginsByMove, firstAnswerWait, lastWords, retryingAt, type RunningCall, runningCall, settle, toTurns, type Usage } from './conversation';
import { ConversationFind } from './ConversationFind';
import { useEverySession } from '../../data/queries';
import { isPartnerRecord } from './sender';
import { type CallMark, ConversationView } from './ConversationView';
import type { Cooling } from './saidDoors';
import { exchangesOf, NO_EXCHANGES } from './exchanges';
import type { ChannelHome } from './channelHome';
import type { HelperAskRow } from '../waits/HelperAsks';
import type { ReasonValues } from '../say';
import { useFollowTail } from './followTail';
import type { Wait } from '../waits/waits';
import { type ChatLimitHeld, chatLimitHeldOf } from '../waits/limitWait';

/** How many blocks a conversation holds before it is offered a way through. */
const LONG = 20;

/** Whether the reader is at the conversation's tail, and the way there: what a host that draws the press is told. */
export type TailState = { atTail: boolean; toTail: () => void };

/**
 * The waits whose raising event the conversation holds, or may hold on a page before, and the way to one:
 * what a host that draws *In the conversation* is told.
 */
export type AnchorState = { anchored: AnchorWait[]; show: (wait: AnchorWait) => void };

const NO_ANCHORS: Anchors = { held: {}, earlier: [] };

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
  waits, onAnchors, onToWait, callMark, onSayAgain, onGoOnBrief, onLimitWait, helpersAsked, channel, dividers, beneath,
}: {
  dividers?: Readonly<Record<string, ReactNode>>;
  /** What the host draws under a turn's answer — `ConversationView`'s, the attended session's alone. */
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
  const { events, opening, firstFailure, earlier, loaded, loadEarlier } = useSessionEvents(session);
  // An earlier part of a line owns none of what the attended one does: the scroll, the frame's facts, the acts.
  const own = mode === 'attended';
  // The door's own word on what an empty record means — undefined until the roster answers, which reads
  // as the console sentence: it claims least.
  const harnesses = useHarnesses();
  const structured = harnesses.data?.harnesses?.find((row) => row.door === adapter)?.structured;
  // What the person calls each account, so the opening line and a wait name one as the agents' page does.
  const roster = harnesses.data?.harnesses;
  const names = useMemo(() => accountNames(Array.isArray(roster) ? roster : []), [roster]);
  // Read from the ask, and as an ended session leaves it: a turn it ended inside says
  // so, and its open calls read stopped rather than running for good.
  // A helper's word is a partner's by the work of the record it names: an event carries no key to tell them by.
  const everySession = useEverySession(true).data;
  const { turns, usage, where } = useMemo(() => {
    const workOf = (id: string) => {
      const record = everySession?.find((each) => each.id === id);
      return record && isPartnerRecord(record) ? 'partner' : null;
    };
    const read = toTurns(events, { opening, workOf });
    return { turns: settle(read.turns, live), usage: read.usage, where: read.where };
  }, [events, opening, live, everySession]);

  // Told on a change of reading only: a callback that changed on every render would otherwise tell the
  // frame, which re-renders, which hands a new callback, round and round.
  const report = useRef(onUsage);
  report.current = own ? onUsage : undefined;
  useEffect(() => { report.current?.(session, usage); }, [session, usage]);
  // The wait for the first answer goes up the same way, for the head's facts; only while the session is live.
  const waitingSince = live ? firstAnswerWait(turns) : undefined;
  // And the call it is inside, read from the settled turns, so a session not live says none.
  const call = live ? runningCall(turns) : null;
  const callTitle = call?.title;
  const callSince = call?.since;
  // And when a transient episode tries again, read from its quiet events, for the head's *Trying again at*.
  const retrying = live ? retryingAt(events) : undefined;
  const reportWait = useRef(onWaiting);
  reportWait.current = own ? onWaiting : undefined;
  useEffect(() => {
    reportWait.current?.(session, waitingSince, callTitle && callSince ? { title: callTitle, since: callSince } : undefined, retrying);
  }, [session, waitingSince, callTitle, callSince, retrying]);

  // Its limit's wait goes up the same way, told again only where the line it reads changes.
  const held = useMemo(() => (chat && own ? chatLimitHeldOf(events) : null), [chat, own, events]);
  const heldKey = held ? `${held.until}:${held.stated}` : '';
  const reportHeld = useRef(onLimitWait);
  reportHeld.current = own ? onLimitWait : undefined;
  const heldNow = useRef(held);
  heldNow.current = held;
  useEffect(() => { reportHeld.current?.(session, heldNow.current); }, [session, heldKey]);
  useEffect(() => () => reportHeld.current?.(session, null), [session]);

  // What changes when the conversation grows: the last event, and its text as chunks join it.
  const last = events[events.length - 1];
  // An ended session opens at its head, which says how it ended; a live one follows its tail.
  // An earlier part follows nothing and opens nothing: it is handed no region, so the attended part alone owns the scroll.
  const nobody = useRef<HTMLElement | null>(null);
  const { atTail, toTail } = useFollowTail(own ? scroller : nobody, `${last?.seq ?? 0}:${turns.length}`, `${session}:${loaded}`, !live);
  // Told on a change only, as the usage is; and at the tail again once this conversation is gone, so no host keeps a press
  // for a transcript it no longer shows.
  const tell = useRef(onTail);
  tell.current = own ? onTail : undefined;
  const away = own && !atTail && turns.length > 0;
  useEffect(() => {
    tell.current?.({ atTail: !away, toTail: () => toTail(true) });
  }, [away, toTail]);
  useEffect(() => () => tell.current?.({ atTail: true, toTail: () => {} }), []);

  // The way through a long run.
  const long = own && (earlier || turns.reduce((count, turn) => count + turn.items.length, 0) > LONG);
  const [query, setQuery] = useState('');
  const settled = useDebounced(query, 300);
  const found = useSessionSearch(long ? settled : '', session);
  const hits = found.data?.hits ?? [];
  const [at, setAt] = useState(0);
  // Where a jump is headed, by event, until the page holds it; and the block it landed on, each time.
  const [pending, setPending] = useState<number | null>(null);
  const [reveal, setReveal] = useState<{ key: string; n: number } | null>(null);
  const show = (key: string) => setReveal((was) => ({ key, n: (was?.n ?? 0) + 1 }));

  // The wait a press is headed for, until the page holds the event that raised it.
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

  // Where each wait is raised, read from what the page holds: the asks compared by value, since the
  // frame hands a new object on every render.
  const asked = JSON.stringify(own ? waits ?? null : null);
  const from = events[0]?.at;
  const anchors = useMemo(
    () => (own && waits ? anchorsOf(turns, { ...waits, session }, { earlier, from }) : NO_ANCHORS),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `asked` stands for `waits`
    [turns, asked, session, earlier, from],
  );
  const marks = useMemo(() => (own && onToWait && waits ? marksOf(waits.waits, anchors.held) : undefined),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `asked` stands for `waits`
    [anchors, asked, Boolean(onToWait)]);
  const anchored = [...Object.keys(anchors.held), ...anchors.earlier] as AnchorWait[];
  // Each helper its agent asked for, drawn once as its card at its call, the turn that took its answer a line to it.
  const askedFor = JSON.stringify(own ? helpersAsked?.asks ?? null : null);
  const exchanges = useMemo(
    () => (own && helpersAsked ? exchangesOf(turns, helpersAsked.asks, helpersAsked.nameOf) : NO_EXCHANGES),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `askedFor` stands for `helpersAsked.asks`
    [turns, askedFor, own],
  );
  const said = anchored.join(' ');
  const tellAnchors = useRef(onAnchors);
  tellAnchors.current = own ? onAnchors : undefined;
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
      {away && own && !onTail && (
        <BackToBottom onPress={() => toTail(true)} className="sticky bottom-2 ml-auto bg-overlay shadow-sm" />
      )}
    </>
  );
}
