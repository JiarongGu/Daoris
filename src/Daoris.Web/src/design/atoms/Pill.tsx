import { type ReactNode } from 'react';
import { type Quest, type SaidWord, SESSION_ACTIVE, type SessionState } from '../../data/api';
import { wordSenderOf } from '../../work/conversation/sender';
import { cn } from '../../lib/cn';
import { DOT_TONE } from './Marks';

/* ---------------------------------------------------------------- pills & chips */

const PILL_TONE: Record<string, string> = {
  neutral: 'border-line text-ink-soft bg-raised',
  // Each word is its hue's ink on its own soft field: open's and done's hues read 2.8 to 3.4:1 there in light.
  open: 'border-st-open text-ink-open bg-st-open/10',
  taken: 'border-st-taken text-ink-taken bg-st-taken/10',
  done: 'border-st-done text-ink-done bg-st-done/10',
  // Declined's word is the danger ink on its own soft field, which the hue itself read at 3.2:1 in dark.
  declined: 'border-st-declined text-ink-danger bg-st-declined/10',
};

/**
 * The one mapping from a quest's wire status to its pill tone — exhaustive at compile time, so a
 * fifth status cannot ship half-toned. Views read this rather than lower-casing the wire value.
 */
export const QUEST_TONE: Record<Quest['status'], keyof typeof PILL_TONE> = {
  Open: 'open',
  Taken: 'taken',
  Done: 'done',
  Declined: 'declined',
};

/**
 * The same mapping for a session's nine states — exhaustive at compile time for the same reason,
 * and shared for a second one: the rail, the head and the quest card must not disagree about what
 * `stood-down` looks like, and three copies of a nine-row map disagree eventually.
 *
 * `awaiting-person` wears the waiting tone deliberately: it is the one state nothing but a person
 * can clear, so it should not sit quietly among the running ones, and it is not an outcome, so it
 * does not wear declined's red (UX5 U1).
 *
 * **Open's hue is the person's alone** (D126 §2.3, audit M6): *waiting on you* and *parked* wear it, and
 * nothing else does. `queued` shared it and left the word to tell the two apart; it is neutral now,
 * keeping its idle mark. *Awaiting reply* is neutral too: nothing runs, and nothing waits on the person.
 */
export const SESSION_TONE: Record<ShownState, keyof typeof PILL_TONE> = {
  'queued': 'neutral',
  'starting': 'taken',
  'working': 'taken',
  'awaiting-person': 'open',
  'completed': 'done',
  'declined': 'declined',
  'stood-down': 'neutral',
  'failed': 'declined',
  'stopped': 'neutral',
  'idle': 'neutral',
  'parked': 'open',
  'awaiting-reply': 'neutral',
  // Answered, it waits on the driver's next look, as a queued session does, and no longer on the person.
  'answered': 'neutral',
  // Going on, its run is opening with the person's words, and nothing waits on the person: answered's twin.
  'going-on': 'neutral',
  // Waiting on its own helper, nothing runs and nothing waits on the person: awaiting reply's twin.
  'awaiting-helper': 'neutral',
  // Ended by an account's limit, its line going on by itself: quiet, never the failure's red.
  'limited': 'neutral',
  // An ended record held for an account's reset, going on by itself: quiet, as *limited* is.
  'limit-waits': 'neutral',
  // Ended before anything ran, its agent refusing the choice (MODELROLE1c3, D205 §3.5(c)): quiet, never the failure's red.
  'not-started': 'neutral',
};

/**
 * What the page shows a session as: the nine states the record carries; **idle** — a live chat
 * whose turn has ended, waiting for the person's next message (UX5 U17); the two words the session
 * list's reader derives from the session's quest: **parked**, the last session here of a quest
 * parked on its failed sessions, and **awaiting-reply**, the last session here of a quest waiting on a
 * question asked of another repository; **answered**, a park the person answered, which the same session
 * goes on from at the driver's next look; **going-on**, a record going on with the person's
 * words, its run opening; and **awaiting-helper**, a session that ended its turn on its own helper's
 * ask and goes on with its answer; and **limited**, a `failed` record whose failure an account's limit made, its
 * `limit` flag; and **limit-waits**, an ended record held for an account's reset, which the list's reader
 * derives from the hold, so it never reads *completed* where Daoris waits to go on; and **not-started**, a `failed` record its agent's refusal of the choice ended before
 * anything ran, its note's code `ended.choice-not-offered` (MODELROLE1c3, D205 §3.5(c)). None is a record state: no new session
 * state.
 *
 * The record says `working` for a chat's whole life, since its process is; whether a turn is in flight
 * is the driver's to say.
 */
export type ShownState =
  | SessionState | 'idle' | 'parked' | 'awaiting-reply' | 'answered' | 'going-on' | 'awaiting-helper' | 'limited' | 'limit-waits' | 'not-started';

/**
 * The catalogue key naming a shown state: the record's states and idle in `sessionState`, the reader's derived
 * words in `work.shown`. One place, so a row, a strip mark and a page header say one word.
 */
export function shownKey(shown: ShownState): string {
  if (shown === 'parked') return 'work.shown.parked';
  if (shown === 'awaiting-reply') return 'work.shown.awaitingReply';
  if (shown === 'answered') return 'work.shown.answered';
  if (shown === 'going-on') return 'work.shown.goingOn';
  if (shown === 'awaiting-helper') return 'work.shown.awaitingHelper';
  if (shown === 'limited') return 'work.limit.word';
  if (shown === 'limit-waits') return 'work.shown.limitWaits';
  if (shown === 'not-started') return 'work.notStarted.word';
  return `sessionState.${shown}`;
}

/** The end code a record its agent's refusal of the choice ended before anything ran carries (D205 §3.5(c), MODELROLE1c3). */
export const CHOICE_NOT_OFFERED = 'ended.choice-not-offered';

/**
 * A record's state with the one word its `limit` flag makes: a `failed` record an account's limit
 * ended is **limited**; a `failed` one ended `choice-not-offered` is **not-started** (MODELROLE1c3); every other is its own state. For a
 * surface that shows the record's state and no other reading of it.
 */
export function limitedOr(session: { state: SessionState; limit?: boolean; noteParts?: readonly { code?: string | null }[] | null }): ShownState {
  if (session.state !== 'failed') return session.state;
  if (session.limit) return 'limited';
  return session.noteParts?.some((part) => part.code === CHOICE_NOT_OFFERED) ? 'not-started' : 'failed';
}

/**
 * The person's words waiting on a record, joined by a blank line, or null where none wait: read from its keyed `said`
 * words by the one sender rule, so Daoris's, a helper's, a partner's and a second opinion's words are never theirs.
 */
export function personsSaid(session: { said?: readonly SaidWord[] | null }): string | null {
  const persons = (session.said ?? []).filter((word) => wordSenderOf(word) === 'person').map((word) => word.text);
  return persons.length > 0 ? persons.join('\n\n') : null;
}

/**
 * A park the person answered: the service keeps the record `awaiting-person` with the answer kept until
 * the driver's next look takes it up, and the same session goes on then, so nothing about it waits on the
 * person. The words are answered to this machine only, so a teammate's park, or one read over a keyed remote, is never
 * one: it stays as its record says.
 */
export function answeredPark(session: { state: SessionState; said?: readonly SaidWord[] | null }): boolean {
  return session.state === 'awaiting-person' && personsSaid(session) !== null;
}

/**
 * A session as the page shows it (UX5 U17, decided by the reference console, which draws a running
 * turn as live and a session between turns with no live mark at all). A live chat is idle when the
 * driver says no turn is in flight, and working while one is; a turn the driver has not answered for
 * is the record's own word, never a guess. Driven work is one long turn, so it is never idle. A park the
 * person answered is **answered**, read from its own record, as the list's reader reads it.
 *
 * A live record that still keeps the person's words is **going on**: it went on with them, and
 * its run's first prompt has not taken them yet. An ended record with words waiting is the list's reader's to say,
 * since words it cannot take stay waiting there too, and only the reader knows which.
 *
 * A `failed` record an account's limit ended is **limited**: no failure, and quiet.
 */
export function shownState(
  session: {
    kind?: 'driven' | 'chat'; state: SessionState; said?: readonly SaidWord[] | null; limit?: boolean;
    noteParts?: readonly { code?: string | null }[] | null;
  },
  taking: boolean | undefined,
): ShownState {
  const ended = limitedOr(session);
  if (ended === 'limited' || ended === 'not-started') return ended;
  if (answeredPark(session)) return 'answered';
  if (session.state !== 'awaiting-person' && SESSION_ACTIVE.has(session.state) && personsSaid(session) !== null) return 'going-on';
  return session.kind === 'chat' && session.state === 'working' && taking === false ? 'idle' : session.state;
}

/** Which states still hold their repository: spelled once, beside the states (`data/api`), and said here for the views. */
export { SESSION_ACTIVE };

/**
 * The same nine states as a LIVENESS mark — `SESSION_TONE`'s sibling, because a pill and a dot
 * answer different questions: the pill says which state, the dot says whether anything is happening.
 *
 * @remarks
 * **Attention outranks activity, structurally rather than by precedence.** The reference console
 * spends a rule on it — a session needing its person wears the attention mark even while its process
 * is busy — and Daoris needs no rule, because `awaiting-person` IS a state and nothing is layered
 * over it. There is deliberately no "is a process alive" input here: the driver's running list is
 * this machine's, and a mirrored session working on another machine is working. The record
 * is what the mark reads — with one exception the driver answers for: a live chat between turns is
 * `idle`, quiet rather than live (`shownState`, UX5 U17).
 */
export const SESSION_DOT: Record<ShownState, keyof typeof DOT_TONE> = {
  'queued': 'idle',
  'starting': 'live',
  'working': 'live',
  'awaiting-person': 'parked',
  'completed': 'ended',
  'declined': 'ended',
  'stood-down': 'ended',
  'failed': 'ended',
  'stopped': 'ended',
  'idle': 'idle',
  // A parked quest's last session waits on the person, whatever its record's ending.
  'parked': 'parked',
  'awaiting-reply': 'idle',
  // Answered, nothing runs yet and nothing waits on the person: quiet until the driver's next look.
  'answered': 'idle',
  // Going on, its run is opening and no turn runs yet: quiet until its first prompt goes.
  'going-on': 'idle',
  // Waiting on its own helper, its own turn has ended and nothing waits on the person: quiet, as awaiting reply is.
  'awaiting-helper': 'idle',
  // Ended by an account's limit: an ended record, as failed is.
  'limited': 'ended',
  // Held for an account's reset: its words go on by itself, so nothing is over, as answered's and going on's are.
  'limit-waits': 'idle',
  // Ended choice-not-offered (MODELROLE1c3): an ended record, as failed is.
  'not-started': 'ended',
};

/** Quest state on its soft field. The label is always present — status never rides on hue alone. */
export function Pill({ tone = 'neutral', title, children }: {
  tone?: keyof typeof PILL_TONE; title?: string; children: ReactNode;
}) {
  return (
    <span
      title={title}
      className={cn(
        'whitespace-nowrap rounded-full border px-2 py-0.5 font-mono text-meta',
        PILL_TONE[tone],
      )}
    >
      {children}
    </span>
  );
}

export function Chip({ accent, children }: { accent?: boolean; children: ReactNode }) {
  return (
    <span className={cn(
      'inline-block rounded-full border px-2.5 py-px text-small leading-[1.45]',
      accent ? 'border-accent bg-accent-soft text-accent' : 'border-line bg-raised text-ink-soft',
    )}
    >
      {children}
    </span>
  );
}
