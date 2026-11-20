import { type RefObject, useEffect, useMemo, useRef, useState } from 'react';
import { useDebounced } from '../../lib/useDebounced';
import { useHarnesses, useSessionEvents, useSessionSearch } from '../../bridge/shell';
import { type BesideFocus, lifetimesIn } from '../beside/besides';
import type { BandTold } from '../band/bandRows';
import { accountNames } from '../accountNames';
import { type AnchorAsks, type Anchors, anchorsOf, type AnchorWait, marksOf } from '../waits/anchors';
import { firstAnswerWait, lastWords, retryingAt, type RunningCall, runningCall, settle, toTurns, type Usage } from './conversation';
import { useEverySession } from '../../data/queries';
import { isPartnerRecord } from './sender';
import { exchangesOf, NO_EXCHANGES } from './exchanges';
import type { HelperAskRow } from '../waits/HelperAsks';
import { useFollowTail } from './followTail';
import type { Wait } from '../waits/waits';
import { type ChatLimitHeld, chatLimitHeldOf } from '../waits/limitWait';
import { type CatchUpFacts, useCatchUp } from '../catchup/useCatchUp';
import { useNow } from '../useNow';

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

/** What a session's reading is handed: its record, the region it scrolls in, and what it tells its host. */
export type ReadingFacts = {
  session: string;
  /** The harness it runs on — whose declaration says whether its door keeps a conversation. */
  adapter?: string;
  chat?: boolean;
  live: boolean;
  /** `attended` owns the scroll, the frame's facts and the acts; `earlier`, a part a line continues, owns none of them. */
  mode?: 'attended' | 'earlier';
  turnRunning?: boolean;
  scroller: RefObject<HTMLElement | null>;
  catchup?: CatchUpFacts;
  helpersAsked?: { asks: readonly HelperAskRow[]; nameOf?: (agent: string) => string; onOpen?: (helper: string) => void };
  onBeside?: (told: BandTold) => void;
  waits?: Omit<AnchorAsks, 'session'>;
  onAnchors?: (session: string, anchors: AnchorState) => void;
  onToWait?: (wait: Wait) => void;
  onTail?: (tail: TailState) => void;
  onUsage?: (session: string, usage?: Usage) => void;
  onWaiting?: (session: string, since?: string, calling?: RunningCall, retrying?: string) => void;
  onLimitWait?: (session: string, held: ChatLimitHeld | null) => void;
};

/**
 * **A session's reading**: its record's events read into settled turns, and every way through them the page offers, one owner for
 * the conversation (`SessionConversation`) and the session panel's story (`work/session/SessionStory`) alike. It follows the tail
 * until the person scrolls up; a long run gets a jump to where it first failed, to its last words and a search within it; a jump
 * lands on an event, loading the pages before it until it is held; each wait is anchored to the event that raised it; the read mark
 * opens it under *Since you left*; and the panel's focus holds a subagent or a task. It tells its host its usage, its first answer's
 * wait, its limit's wait, its tail, its anchors and what runs beside it, each on a change only.
 */
export function useSessionReading({
  session, adapter, chat = false, live, mode = 'attended', turnRunning, scroller, catchup, helpersAsked, onBeside, waits, onAnchors,
  onToWait, onTail, onUsage, onWaiting, onLimitWait,
}: ReadingFacts) {
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

  const words = lastWords(turns);
  // Each subagent's and task's start and end, which a subagent's own timeline is joined with.
  const lifetimes = useMemo(() => lifetimesIn(events), [events]);
  const caught = useCatchUp({
    facts: own ? catchup : undefined, session, scroller, events, turns, where, lifetimes, exchanges, loaded, earlier, loadEarlier,
  });
  const now = useNow(60_000);
  // What the panel's focus holds, which the live band opens too; another session's starts with none.
  const [focus, setFocus] = useState<BesideFocus | null>(null);
  useEffect(() => setFocus(null), [session]);
  // The band's: what runs beside it, and the agent's act while a turn runs (the call it is inside, else it thinks since its last event).
  const working = live && (turnRunning ?? (turns.length > 0 && !turns[turns.length - 1]!.ended));
  const act = call ? { title: call.title, since: call.since } : working ? { since: last?.at } : null;
  const besideKey = `${lifetimes.length}:${lifetimes[lifetimes.length - 1]?.seq ?? 0}|${act ? `${act.title ?? ''}@${act.since ?? ''}` : ''}`;
  const tellBeside = useRef(onBeside);
  tellBeside.current = own ? onBeside : undefined;
  useEffect(() => {
    tellBeside.current?.({ session, lifetimes, act, open: setFocus });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `besideKey` stands for `lifetimes` and `act`
  }, [session, besideKey]);
  useEffect(() => () => tellBeside.current?.({ session, lifetimes: [], act: null, open: () => {} }), [session]);
  const step = (by: 1 | -1) => {
    if (hits.length === 0) return;
    const next = (at + by + hits.length) % hits.length;
    setAt(next);
    setPending(hits[next]!.seq);
  };
  /** The find bar's facts and presses, offered on a long run alone. */
  const find = long ? {
    hasFailure: firstFailure !== null,
    onFirstFailure: () => { if (firstFailure !== null) setPending(firstFailure); },
    hasWords: words !== undefined,
    onLastWords: () => { if (words) show(words); },
    query,
    onQuery: (next: string) => {
      setQuery(next);
      setAt(0);
    },
    settled,
    hits: found.data ? hits.length : undefined,
    at,
    cut: found.data?.cut,
    onStep: step,
  } : null;

  return {
    own, turns, structured, names, loaded, earlier, loadEarlier, reveal, show, marks, exchanges, caught, now, focus, setFocus, lifetimes,
    away, toTail, find,
  };
}
