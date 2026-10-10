import { type RefObject, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { useDebounced } from '../lib/useDebounced';
import { searchable } from '../searchable';
import { useHarnesses, useSessionEvents, useSessionSearch } from '../shell';
import { Icon } from '../ui';
import { settle, toTurns, type Turn, type Usage } from './conversation';
import { ConversationFind } from './ConversationFind';
import { type Cooling, ConversationView } from './ConversationView';
import type { ReasonValues } from './say';
import { useFollowTail } from './followTail';

/** How many blocks a conversation holds before it is offered a way through (SESS1 S9). */
const LONG = 20;

/** Whether the reader is at the conversation's tail, and the way there: what a host that draws the press is told. */
export type TailState = { atTail: boolean; toTail: () => void };

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
 * held, then its block is shown, its fold open, and scrolled to.
 *
 * Desktop-only for the console's reason (D47 §4): the record arrives over the bridge.
 */
export function SessionConversation({
  session, adapter, chat = false, tree, live, turnRunning, scroller, onUsage, onSession, onStartFrom, reasons, cooling, onTail,
}: {
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
}) {
  const { events, opening, firstFailure, earlier, loaded, loadEarlier } = useSessionEvents(session);
  // The door's own word on what an empty record means — undefined until the roster answers, which reads
  // as the console sentence: it claims least.
  const harnesses = useHarnesses();
  const structured = harnesses.data?.harnesses?.find((row) => row.harness === adapter)?.structured;
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

  useEffect(() => {
    setQuery('');
    setPending(null);
    setReveal(null);
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
        loaded={loaded}
        earlier={earlier}
        onLoadEarlier={() => void loadEarlier()}
        reveal={reveal?.key}
        onSession={onSession}
        onStartFrom={onStartFrom}
        reasons={reasons}
        cooling={cooling}
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

/** The key of the last thing the agent said, where it said anything. */
function lastWords(turns: readonly Turn[]): string | undefined {
  for (let turn = turns.length - 1; turn >= 0; turn -= 1) {
    const items = turns[turn]!.items;
    for (let item = items.length - 1; item >= 0; item -= 1) {
      if (items[item]!.kind === 'message') return items[item]!.key;
    }
  }
  return undefined;
}
