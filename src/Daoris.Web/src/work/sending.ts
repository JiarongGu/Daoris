import { useEffect, useRef, useState } from 'react';
import type { ChatMessage, SessionEvent } from './conversation';

// The person's words on their way to a session (SENDLOST1b, D137 §2.4), as Ask Daoris keeps its own since SENDLOST1 (D89's
// HELPSTART1 note): a chat's composer and every other box at a session's foot. A state hook, not a data hook, as `useDraft`
// is: the frame reads the session's record and queue and hands them in, so this reaches neither the service nor the shell.

/**
 * How far before its send a record's words may be stamped and still be the words sent: the record's time is the driver's,
 * the send's the page's, on one machine. Ask Daoris's own bound (SENDLOST1).
 */
const SAID_SKEW = 5000;

const NONE: readonly ChatMessage[] = [];

/** Words on their way to one session, shown the moment they are sent until it holds them. */
export type Sent = {
  /** Which send, so its answer settles its own words. */
  n: number;
  session: string;
  words: ChatMessage;
  /** When they were sent, on the page's clock. */
  at: number;
  /** The page stopped waiting on the send (the bridge's TIMEOUT): the machine may still take them. */
  late: boolean;
};

/** The session in view, as the words on their way to it are judged. */
export type SendingTo = {
  id: string;
  /** What its queue holds as the driver last told it: words held behind a turn, or waiting on its record. */
  queued?: readonly ChatMessage[];
  /**
   * A chat whose conversation ended: words it had not taken by then never arrived, since a chat's waiting words are in no
   * record until taken (CONV4a). A driven session's words are kept on its record once they arrive, whatever it does
   * meanwhile (D137 §2.4, USERSHOWN1), so its end says nothing of them.
   */
  endedChat: boolean;
  /**
   * The driver now says nothing takes words for it (the box became the line, SENDLOST1c). Said after a send the page stopped
   * waiting on, it is the driver's refusal arriving late: words its record does not hold never arrived.
   */
  refused?: boolean;
};

/** Whether a session holds words sent to it: waiting in its queue, or said in its record since they were sent. */
export const holds = (sent: Sent, queued: readonly ChatMessage[], events: readonly SessionEvent[]) =>
  queued.some((each) => each.text === sent.words.text)
  || events.some((event) => event.kind === 'user' && event.origin === 'person' && event.text === sent.words.text
    && Date.parse(event.at) >= sent.at - SAID_SKEW);

/**
 * The words on their way to the sessions of a page. `begin` shows them at once, as sending; the send's answer lets them go
 * (`answered`), where the caller says what came of them; `unanswered` keeps them where the page stopped waiting on it, never
 * put back in the box, where a second send would say them twice. `going` is what the session in view shows, less what its
 * queue already shows itself; `lateTo` names the session whose record the caller reads for `useSettled`.
 */
export function useSending(to: SendingTo | null) {
  const [sent, setSent] = useState<Sent[]>([]);
  const sends = useRef(0);

  const begin = (session: string, words: ChatMessage) => {
    const n = ++sends.current;
    setSent((all) => [...all, { n, session, words, at: Date.now(), late: false }]);
    return n;
  };
  const answered = (n: number) => setSent((all) => all.filter((each) => each.n !== n));
  const unanswered = (n: number) => setSent((all) => all.map((each) => (each.n === n ? { ...each, late: true } : each)));
  const settle = (settled: ReadonlySet<number>) => setSent((all) => all.filter((each) => !settled.has(each.n)));

  const queued = to?.queued ?? NONE;
  const here = to ? sent.filter((each) => each.session === to.id) : [];
  const late = here.filter((each) => each.late);
  // Words its queue already lists are shown there, by their id: not twice while the send's answer is on its way.
  const shown = here.filter((each) => each.late || !queued.some((held) => held.text === each.words.text));

  return {
    to, queued, late, settle, begin, answered, unanswered,
    lateTo: to && late.length > 0 ? to.id : null,
    going: shown.length > 0 ? { words: shown.map((each) => each.words), late: shown.some((each) => each.late) } : undefined,
  };
}

/**
 * Settles the words whose send the page stopped waiting on, by the session's own record and queue: let go once either holds
 * them, and handed to `giveBack` where a chat ended without them, its record read since, as they never arrived.
 */
export function useSettled(
  sending: ReturnType<typeof useSending>,
  record: { events: readonly SessionEvent[]; loaded: boolean },
  giveBack: (sent: Sent) => void,
) {
  const { to, queued, late, settle } = sending;
  const ended = to?.endedChat === true || to?.refused === true;
  useEffect(() => {
    if (late.length === 0) return;
    const arrived = late.filter((each) => holds(each, queued, record.events));
    const never = ended && record.loaded ? late.filter((each) => !arrived.includes(each)) : [];
    if (arrived.length === 0 && never.length === 0) return;
    settle(new Set([...arrived, ...never].map((each) => each.n)));
    for (const each of never) giveBack(each);
    // Asked again as the record, the queue or the session moves; the words and the give-back are read from this render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [record.events, record.loaded, queued, ended, late.length]);
}
