import { createContext, type ReactNode, useContext, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { clockOf, compact, elapsed, moment, span } from '../../locales/format';
import { cn } from '../../lib/cn';
import { Button, Chip, Dot, Icon, Tip } from '../../design/ui';
import { turnEnd } from '../waits/anchors';
import { CHANNEL_CLAIM_OPENING } from '../channel';
import {
  type Ask, type Block, type Level, type Receipts, runCount, type RunningCall, runningCall, segments, type Turn,
} from './conversation';
import type { Author } from './sender';
import { useNow } from '../useNow';
import type { AccountNames } from '../accountNames';
import { HandedAccount } from './HandedAccount';
import { PlanView, RawLine, ThoughtRow } from './BlockLines';
import { CannotLine, CodedLine, handedTo, HandedLine, NewSessionDoor, NoteLine, WentLine } from './NoteLines';
import { type Cooling, SaidDoors } from './saidDoors';
import { Markdown } from './Markdown';
import type { ReasonValues } from '../say';
import { ToolCard } from './ToolCard';
import { ExchangeCardView, TakenLine } from './ExchangeCard';
import { type Exchanges, NO_EXCHANGES } from './exchanges';
import { WaitMark } from '../waits/WaitAnchor';
import type { Wait } from '../waits/waits';

/**
 * What waits on the person from each block that raised it, by its key, and the way to the wait (WAITFOOT1c, D168 point 4): the
 * block carries the line. No way, no marks.
 */
const WaitMarks = createContext<{ marks?: Readonly<Record<string, readonly Wait[]>>; onToWait?: (wait: Wait) => void }>({});

/**
 * The requests the session's agent made, each drawn as its card at its call (COMMS1h, the comms design §7): the cards, the doors to a
 * helper's conversation and to a card from the turn that took its answer.
 */
const AskedCards = createContext<{ exchanges: Exchanges; onOpenHelper?: (session: string) => void; onShow?: (call: string) => void }>({
  exchanges: NO_EXCHANGES,
});

/**
 * A session's conversation (D76, CONV2): what was asked, what the agent said and did, and how each
 * turn ended — drawn from the typed events the driver keeps, never parsed from the console.
 *
 * @remarks
 * - **Every word the agent said stays in view, and the work between two of them folds** (SESS1): a
 *   run of calls and thinking is one row that counts it and its failures, one press from its cards.
 *   The run in hand stays open while the turn goes on, and where the session ended inside a turn.
 *   Folding a finished turn whole hid a driven session's account of its run behind one row, since a
 *   driven session is one turn (the first real workspace, 2026-09-28).
 * - **A long run reads from what it was asked**: the ask comes first, and *load earlier* sits where
 *   the events the page does not hold belong.
 * - **The agent's words are Markdown**; the person's are shown as written. Both are content, never
 *   translated (translation-parity).
 * - **A session whose door carries only text** has no conversation to draw, and says so, pointing at
 *   the console, wherever it stands (DOCK1b) — D76 §1's "stays text, and the page says so".
 * - **Words said after it parked or ended wait at its foot** (MSG1f, D137 §3.1), saying the same session
 *   goes on with them; where they went to a new session the line says which, a door to it; where they
 *   cannot go on here it says why, with one press that starts a conversation with them.
 * - **A driver's note with a code is said in the reader's language** (MSG1c3, D142 point 1): the line that
 *   a stop cut a word off on its way. A code this page does not know leaves the driver's English.
 * - **Words a cooling account holds** (MSG1g2, D137 §2.2) say they wait for its reset, and under them
 *   *Go on in a new session* carries them on now, without this conversation's context; a driven
 *   session's alone, since nothing carries a chat's words on by itself.
 * - **A block that raised what waits on the person says so** (WAITFOOT1c, D168 point 4), with *To the wait*: the question, a
 *   go-ahead's call (on its run's line while folded), the work at its turn's end.
 *
 * A molecule: turns in, a press out. The organism above it holds the record.
 */
export function ConversationView({
  turns, tree, structured, chat = false, live = false, turnRunning, loaded = true, earlier = false, onLoadEarlier,
  reveal, toolbar, began, onSession, onStartFrom, onGoOnBrief, onSayAgain, reasons = {}, cooling, names, marks, onToWait, callMark,
  exchanges = NO_EXCHANGES, onOpenHelper, onShow,
}: {
  /** The requests its agent made, each drawn as its card at its call, its answer folded in (COMMS1h); none, every call is a tool card. */
  exchanges?: Exchanges;
  /** Open a helper's conversation from its card; absent, the card offers none. */
  onOpenHelper?: (session: string) => void;
  /** Bring the page to a card from the turn that took its answer, by the card's call; absent, that turn's line opens nothing. */
  onShow?: (call: string) => void;
  /**
   * *Go on with Daoris's brief* (SESSCTX1c part B, D189 point 3): beside the fresh start under a line saying the words wait for the person,
   * the session goes on in a new part of itself, handed Daoris's brief and then the words. Absent, no press is drawn.
   */
  onGoOnBrief?: () => void;
  /**
   * *Say it again* (COWORK1c, the message-urgency design §6, §8.2): the person's own words a shell before attempted, which may have
   * reached the agent and were never sent again, said anew, a new word. Absent, no press is drawn.
   */
  onSayAgain?: (words: string[]) => void;
  /** The call the driver's grouping pushes for this session (AUTONOMY1b3e): when held for the person's answer, the foot says it waits. */
  callMark?: CallMark;
  /**
   * What waits on the person, by the key of the block that raised it (WAITFOOT1c, D168 point 4): a line under that block says
   * so, with *To the wait*. A turn's end is keyed `turnEnd(turn)`, for the work it did; a call folded in a run is marked on the
   * run's line until it is open.
   */
  marks?: Readonly<Record<string, readonly Wait[]>>;
  /** Go to a wait's line where it is answered; absent, no block is marked. */
  onToWait?: (wait: Wait) => void;
  /** What the person calls each account, by id (NOTENAME1): a driver's note that names one says the name, not the id. */
  names?: AccountNames;
  /**
   * The account the person's words wait for cools (MSG1g2): until when, and *Go on in a new session* with what it came to.
   * Absent where nothing cools.
   */
  cooling?: Cooling;
  /** Attend a session the person's words went to (MSG1f): the went line's door. Absent, the session is named as text. */
  onSession?: (id: string) => void;
  /**
   * Start a conversation with words that cannot go on in this session (MSG1f, D137 §2.2): the one press under the line
   * saying so. Absent, no press is drawn.
   */
  onStartFrom?: (words: string[]) => void;
  /** What a reason the driver gave by code may name (D137 §5.1): the adapters and the agent. */
  reasons?: ReasonValues;
  turns: Turn[];
  /**
   * The block a jump or a search landed on (SESS1 S9), by its key: its fold opens and it is marked.
   * The organism scrolls to it; this only draws it.
   */
  reveal?: string;
  /** The way through a long run, where the organism offers one — above the conversation, kept in view. */
  toolbar?: ReactNode;
  /**
   * How the part began, in the strip's words, where it began by no move and so holds no first line of its own (SWITCHCTX1c-i): the
   * boundary above its first turn, one line, never a separator of the page's own.
   */
  began?: string;
  /**
   * Whether this session's door keeps a conversation's structure, as its harness declares (D76 §1) —
   * or undefined where that is not known. It is what an empty record is read by.
   */
  structured?: boolean;
  /** Whether this session is a conversation with a person, which opens with nothing said. */
  chat?: boolean;
  /** The session's tree, so a tool's path inside it reads relative to it. */
  tree?: string | null;
  /** Whether the session is still working — a running turn shows it is. */
  live?: boolean;
  /**
   * Whether a turn is in flight, where the driver says so (a conversation it holds), or undefined
   * where nothing says, and then a live session's last open turn is taken as running. 🔴 The agent
   * speaks after its turn has ended when its background work finishes (CONSOLE2a), and those words
   * are no turn running: read from `live` alone, they said *working…* for good, with nothing running.
   */
  turnRunning?: boolean;
  /** Whether the record has answered; before it has, nothing is claimed about it. */
  loaded?: boolean;
  /** Whether earlier turns exist beyond what is held. */
  earlier?: boolean;
  onLoadEarlier?: () => void;
}) {
  const { t } = useTranslation();

  if (!loaded) return null;

  // 🔴 What an empty record means is the DOOR's to say, never a guess from the emptiness. A driven
  // session's record opens with its target, so empty there is a door that carries only text or a record
  // from before conversations were kept. A chat opens with nothing said — and on a structured door that
  // is exactly what empty means: told otherwise, a fresh chat read that its door carries only text
  // (CONV3b), as a parked pipe chat once read "Nothing said yet" (CONV2's look).
  if (turns.length === 0) {
    const sentence = structured && chat
      ? t(live ? 'work.conversation.nothingYet' : 'work.conversation.nothingSaid')
      : t('work.conversation.textOnly');
    return <p className="m-0 mt-2 text-small text-ink-faint">{sentence}</p>;
  }

  return (
    // `minmax(0,1fr)`: a code block's long line scrolls inside its own box, and never widens the
    // conversation past the centre (seen on the window with a real session, CONV3). No measure of its
    // own: the agent's words are content, shown at the width they are given (UX5 U16: a 768px
    // cap was half a maximized window).
    // A chat's words go on with the next word said to it, never by themselves at a reset (MSG1c), so its cool-off is not said.
    <SaidDoors.Provider value={{ chat, onSession, onStartFrom, onGoOnBrief, onSayAgain, reasons, cooling: chat ? undefined : cooling, names }}>
      <WaitMarks.Provider value={onToWait ? { marks, onToWait } : {}}>
        <AskedCards.Provider value={{ exchanges, onOpenHelper, onShow }}>
        <section aria-label={t('work.conversation.label')} className="grid w-full grid-cols-[minmax(0,1fr)] gap-3">
          {/* Pinned beneath the session's page header where one is pinned above it (SESSUX1d, D126 §3.2): the header says its
              height on the main area as `--session-head`, and a window with none (a detached session) pins at the top. */}
          {toolbar && (
            <div className="sticky top-[calc(var(--session-head,0px)-0.75rem)] z-[9] -mx-1 border-b border-line bg-page px-1 pb-1.5 pt-3">
              {toolbar}
            </div>
          )}
          {/* Where the page began past the ask, the gap is inside its turn, after the ask (SESS1 S1). */}
          {earlier && onLoadEarlier && !turns[0]?.gap && <Earlier onLoadEarlier={onLoadEarlier} />}
          {began && <BeganLine words={began} />}
          {turns.map((turn, index) => (
            <TurnView
              key={turn.key}
              turn={turn}
              tree={tree}
              // Words waiting at the foot are no run of the agent's (MSG1f): nothing runs until its first prompt takes them.
              running={(turnRunning ?? live) && index === turns.length - 1 && !turn.ended && !turn.waiting}
              onLoadEarlier={earlier && turn.gap ? onLoadEarlier : undefined}
              reveal={reveal}
              callMark={index === turns.length - 1 ? callMark : undefined}
            />
          ))}
        </section>
        </AskedCards.Provider>
      </WaitMarks.Provider>
    </SaidDoors.Provider>
  );
}

/** A part's boundary where it began by no move (SWITCHCTX1c-i): the strip's *began* words, as one of the driver's lines. */
function BeganLine({ words }: { words: string }) {
  const { t } = useTranslation();
  return (
    <p className="m-0 text-small text-ink-soft wrap-anywhere">
      <span className="mr-1.5 text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.conversation.driver')}</span>
      {words}
    </p>
  );
}

function Earlier({ onLoadEarlier }: { onLoadEarlier: () => void }) {
  const { t } = useTranslation();
  return (
    <Button variant="ghost" onClick={onLoadEarlier} className="justify-self-center text-small">
      {t('work.conversation.earlier')}
    </Button>
  );
}

/**
 * A block as the page holds it: named by its key, so a jump can find it, and marked when it is the one; with the line of what
 * waits from it, where it raised a wait (WAITFOOT1c).
 */
function Held({ id, reveal, children }: { id: string; reveal?: string; children?: ReactNode }) {
  const { marks, onToWait } = useContext(WaitMarks);
  const raised = onToWait ? marks?.[id] : undefined;
  return (
    <div
      data-block={id}
      className={cn('min-w-0 scroll-mt-12', reveal === id && 'rounded-control outline-2 outline-offset-4 outline-accent/60')}
    >
      {children}
      {raised && raised.length > 0 && <WaitMark waits={raised} onPress={() => onToWait!(raised[0]!)} />}
    </div>
  );
}

function TurnView({ turn, tree, running, onLoadEarlier, reveal, callMark }: {
  turn: Turn;
  callMark?: CallMark;
  tree?: string | null;
  running: boolean;
  reveal?: string;
  /** The page began past this turn's ask: the earlier events belong between it and what is held. */
  onLoadEarlier?: () => void;
}) {
  const { t } = useTranslation();
  const { marks } = useContext(WaitMarks);
  const { exchanges, onShow } = useContext(AskedCards);
  // The run in hand stays open while the turn goes on, and where the session ended inside it.
  const parts = segments(turn.items, running || Boolean(turn.cut), (block) => block.key in exchanges.cards);
  const calling = running ? runningCall([turn]) : null;
  // COMMS1h: an answer its card holds is drawn there; the turn that took it holds a line that opens the card.
  const taken = turn.ask ? exchanges.cards[exchanges.takenAt[turn.ask.key] ?? ''] : undefined;

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5">
      {turn.ask && (
        <Held id={turn.ask.key} reveal={reveal}>
          {taken ? <TakenLine card={taken} onShow={onShow} /> : <AskView ask={turn.ask} />}
        </Held>
      )}
      {onLoadEarlier && <Earlier onLoadEarlier={onLoadEarlier} />}
      {parts.map((part) => (part.kind === 'run'
        ? <RunView key={part.key} items={part.items} open={part.open} tree={tree} reveal={reveal} />
        : <Held key={part.block.key} id={part.block.key} reveal={reveal}><BlockView block={part.block} tree={tree} reveal={reveal} /></Held>))}
      {turn.waiting && <NewSessionDoor turn={turn} />}
      {running && <Dot tone="live" label={t('work.conversation.working')} className="mt-1" />}
      {/* A resumed conversation can wait many minutes on its model before anything shows (WAITSHOWN1): said, with the count. */}
      {running && turn.waitingSince && <FirstAnswer since={turn.waitingSince} />}
      {running && (calling || callMark?.waiting) && <CallingLine call={calling} mark={callMark} />}
      {/* The session ended inside this turn (SESS1 S4): said once, in the passive, never as a failure —
          the driver's note above says why, when it knows. */}
      {turn.cut && <p className="m-0 text-meta text-ink-faint">{t('work.conversation.cut')}</p>}
      {/* Stopped, in the passive (CONV4b): a driven session's timeout cancels a turn too, and the
          page cannot know whose stop it was. Never the wire's word, and never a failure's tone. */}
      {turn.ended === 'cancelled' && <p className="m-0 text-meta text-ink-faint">{t('work.conversation.stopped')}</p>}
      {turn.ended && turn.ended !== 'end_turn' && turn.ended !== 'cancelled' && (
        <p className="m-0 text-meta text-ink-faint">{t('work.conversation.ended', { reason: turn.ended })}</p>
      )}
      {turn.ended && <TurnMeter turn={turn} />}
      {/* The work this turn did waits on the person (WAITFOOT1c): said at its end, where a jump to it lands. */}
      {marks?.[turnEnd(turn.key)] && <Held id={turnEnd(turn.key)} reveal={reveal} />}
    </div>
  );
}

/** The call a turn is inside (WAITSAY1): its title and how long it has run, ticking, so a long tool call is no bare *working…*. */
function CallingLine({ call, mark }: { call: RunningCall | null; mark?: CallMark }) {
  const { t } = useTranslation();
  const now = useNow(10_000);
  const at = new Date(now).toISOString();
  // AUTONOMY1b3e (D184): a call held for the person's answer is the driver's mark, which the record cannot show; it is timed from
  // the hold and, as on the rail, an untitled one is a tool call. No mark (an older host) reads as running.
  if (mark?.waiting) {
    const title = mark.title || call?.title;
    return (
      <p role="status" className="m-0 text-meta text-ink-faint">
        {title
          ? t('work.conversation.callWaits', { title, elapsed: elapsed(mark.since, at) })
          : t('work.conversation.callWaitsUnnamed', { elapsed: elapsed(mark.since, at) })}
      </p>
    );
  }
  if (!call) return null;
  return (
    <p role="status" className="m-0 text-meta text-ink-faint">
      {t('work.conversation.calling', { title: call.title, elapsed: elapsed(call.since, at) })}
    </p>
  );
}

/** The call the driver says the session is inside, as the grouping pushes it: `waiting` when it is held for the person's answer. */
export type CallMark = { title: string; since: string; waiting?: boolean };

/** The wait for a turn's first answer (WAITSHOWN1): when it was asked, and for how long it has waited, ticking. */
function FirstAnswer({ since }: { since: string }) {
  const { t, i18n } = useTranslation();
  const now = useNow(10_000);
  return (
    <p role="status" className="m-0 text-meta text-ink-faint">
      {t('work.conversation.firstAnswer', {
        time: moment(since, i18n.language), elapsed: elapsed(since, new Date(now).toISOString()),
      })}
    </p>
  );
}

/**
 * How long a finished turn took and what it consumed (CONV5): the driver's clock, and the counts the
 * harness reported for the whole turn — one quiet line, with the breakdown on hover. A count the wire
 * did not give is a dash in the breakdown and absent from the line, never a zero; no price is claimed.
 */
function TurnMeter({ turn }: { turn: Turn }) {
  const { t, i18n } = useTranslation();
  const { tokens, took, firstAfter } = turn;
  const exact = (value?: number | null) => (typeof value === 'number' ? value.toLocaleString(i18n.language) : '—');

  // METER1: "in" is what the harness read anew this turn, new or written to its cache. What it re-read from
  // its cache is said apart: a long turn re-reads its whole context on every call, and summed into "in" it
  // made 69M of a turn whose new input was 663K.
  const inputs = [tokens?.input, tokens?.cacheWrite].filter((n): n is number => typeof n === 'number');
  const input = inputs.length > 0 ? inputs.reduce((sum, n) => sum + n, 0) : undefined;
  const line = [
    took !== undefined ? span(took) : null,
    input !== undefined ? t('work.meter.in', { tokens: compact(input) }) : null,
    typeof tokens?.cacheRead === 'number' ? t('work.meter.cached', { tokens: compact(tokens.cacheRead) }) : null,
    typeof tokens?.output === 'number' ? t('work.meter.out', { tokens: compact(tokens.output) }) : null,
  ].filter((part): part is string => part !== null);
  if (line.length === 0) return null;

  const said = [
    took === undefined ? null
      : firstAfter === undefined ? t('work.meter.took', { took: span(took) })
        : t('work.meter.tookFirst', { took: span(took), first: span(firstAfter) }),
    tokens ? t('work.meter.tokens', {
      input: exact(input), fresh: exact(tokens.input), read: exact(tokens.cacheRead),
      written: exact(tokens.cacheWrite), output: exact(tokens.output),
    }) : null,
  ].filter((sentence): sentence is string => sentence !== null)
    // Sentences are joined the catalogue's way: English puts a space after a full stop, and Chinese does not.
    .reduce((first, second) => t('work.meter.join', { first, second }));

  return (
    <Tip content={said} side="top">
      <span tabIndex={0} className="justify-self-start text-meta text-ink-faint">{line.join(' · ')}</span>
    </Tip>
  );
}

/**
 * What the person asked, or the target the driver composed — the latter folded, since it is long, with what it was composed
 * of beneath it (CONTEXT1). Another agent's findings handed as the session's next turn (XAGENT1g, the second-agent design §6.3)
 * are a block from that agent, open, under Daoris's fixed words that name it and its maker, never the person's. Daoris's own words
 * (COWORK1b), a reopen told or a step's go-on, are named as Daoris's by their mark, open, and never as the person's.
 */
/** What each author's words are headed with: never *You* for words that are not the person's (SUPERVISOR1a-i, D208 §6.1). */
const AUTHOR_KEYS = {
  daoris: 'work.conversation.daoris',
  supervisor: 'work.conversation.supervisor',
  unattributed: 'work.conversation.unattributed',
  helper: 'work.conversation.helper',
  partner: 'work.conversation.partner',
} as const satisfies Record<Author, string>;

function AskView({ ask }: { ask: Ask }) {
  const { t } = useTranslation();
  const findings = Boolean(ask.opinion);
  // Words by an author's mark (COWORK1b; SUPERVISOR1a-i): Daoris's, the supervisor's on the owner's behalf, or not attributed.
  const marked = Boolean(ask.from);
  // Another session's claim on the ask's channel (SESSONE2c): Daoris's words around it, kept as the driver's own, short enough to
  // read whole and never the target Daoris composed. Told by its channel mark (SESSCTX1b), and a record from before by its opening.
  const claim = ask.origin === 'target' && !findings && !marked
    && (Boolean(ask.channel) || (ask.text ?? '').startsWith(CHANNEL_CLAIM_OPENING));
  const target = ask.origin === 'target' && !claim && !marked;
  const [open, setOpen] = useState(!target || findings);

  return (
    <div
      className={cn(
        'rounded-card border border-line border-l-[3px] bg-raised px-3 py-2',
        findings || marked ? 'border-l-line-strong' : 'border-l-accent',
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-meta text-ink-faint">
          {findings ? t('work.conversation.opinion') : ask.from ? t(AUTHOR_KEYS[ask.from]) : claim ? t('work.conversation.claim')
            // SESSCTX1b: a new part's first prompt is Daoris's brief of the line it continues, folded as a target is.
            : target && ask.continues ? t('work.conversation.brief')
              : target ? t('work.conversation.target') : t('work.conversation.you')}
        </span>
        {target && (
          <button
            type="button"
            aria-expanded={open}
            onClick={() => setOpen((was) => !was)}
            className="border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
          >
            {open ? t('work.conversation.hide') : t('work.conversation.show')}
          </button>
        )}
      </div>
      {/* A word wider than the column, a pasted URL, breaks inside it rather than scrolling the region sideways (ASKHIST1b). */}
      {ask.choice
        ? <ChoiceWords choice={ask.choice} text={ask.text ?? ''} className="text-ink" />
        : ask.text && (
          <p className={cn('m-0 mt-0.5 whitespace-pre-wrap wrap-anywhere text-body text-ink', !open && 'line-clamp-2')}>{ask.text}</p>
        )}
      <Attached files={ask.files} />
      {ask.receipts && <ReceiptsLine receipts={ask.receipts} />}
      {/* What the target was composed of, section by section (CONTEXT1), beside the words it composed. */}
      {target && !findings && <HandedAccount account={ask.account} />}
    </div>
  );
}

/**
 * What the door observed of these words (COWORK1c, the message-urgency design §7, §8.2): *sent 14:02 · taken 14:03 · turn ended 14:09*,
 * each only where it was observed, folded under them as a quiet line. A turn's end is what Daoris saw, never an answer (COMMS1h).
 */
function ReceiptsLine({ receipts }: { receipts: Receipts }) {
  const { t, i18n } = useTranslation();
  const seen = (['sent', 'taken', 'ended'] as const)
    .filter((receipt) => receipts[receipt])
    .map((receipt) => `${t(`work.receipt.${receipt}`)} ${clockOf(receipts[receipt]!, new Date(), i18n.language)}`);
  if (seen.length === 0) return null;
  return <p data-receipts className="m-0 mt-1 text-meta text-ink-faint">{seen.join(' · ')}</p>;
}

/** What the person attached (CONV4c): names, as the record keeps them. */
function Attached({ files }: { files?: string[] }) {
  const { t } = useTranslation();
  if (!files || files.length === 0) return null;
  return (
    <ul aria-label={t('work.conversation.attached')} className="m-0 mt-1.5 flex list-none flex-wrap gap-1.5 p-0">
      {files.map((name, index) => (
        <li
          key={`${index}:${name}`}
          className="inline-flex min-w-0 items-center gap-1 rounded-control border border-line bg-page px-2 py-0.5 text-meta text-ink-soft"
        >
          <Icon name="attach" size={11} className="shrink-0 text-ink-faint" />
          <span className="min-w-0 truncate">{name}</span>
        </li>
      ))}
    </ul>
  );
}

/**
 * When the person's words reach the session, by the door's reach (D136, MSG1f): a chat's conversation opens again. Held for the
 * turn's end at a level the door could not honour (COWORK1a, the message-urgency design §3.1, §8.2), the line says so: urgent
 * words, and words at now, whose door takes words only when its turn ends.
 */
const heldKey = (reaches: string | null | undefined, chat: boolean, level?: Level) => {
  if (reaches === 'next-step') return 'work.conversation.held.nextStep';
  if (reaches === 'now') return 'work.conversation.held.now';
  if (reaches === 'resume') return chat ? 'work.conversation.held.reopen' : 'work.conversation.held.resume';
  if (level === 'urgent') return 'work.conversation.held.urgentTurnEnd';
  if (level === 'now') return 'work.steer.notNowTurnEnd';
  return 'work.conversation.held.turnEnd';
};

/**
 * The person's words to a session, waiting to reach it (STEER1, D136; MSG1f, D137 §3.1): theirs, as written, dashed as
 * the composer's waiting words are, and saying when the session reads them — at its next step, at its turn's end, or as
 * the same session goes on, or at its account's reset where that account cools (MSG1g2) — or, the session over, that it
 * never did. Where the session takes them they become the ask of that turn, and this goes. Where a driver's note below
 * says where they went, or that they cannot go on here, that note is their line and this says none. Daoris's own words held for
 * a running session (COWORK1b) are named as Daoris's, never as the person's.
 */
function HeldAsk({ block }: { block: Block }) {
  const { t } = useTranslation();
  const { chat, cooling } = useContext(SaidDoors);
  const reset = block.reaches === 'resume' && cooling;
  const when = block.settled ? null
    : block.unreached ? t('work.conversation.held.never')
      : reset ? t('work.conversation.held.cooling', { time: moment(cooling.until) })
        : t(heldKey(block.reaches, chat, block.level));

  return (
    // Dashed while it waits; words taken already (`now`, STEER2b) are drawn solid, as a turn's ask is.
    <div className={cn('rounded-card border px-3 py-2', block.reaches === 'now' ? 'border-line-strong' : 'border-dashed border-line-strong')}>
      <span className="flex items-center gap-2">
        <span className="text-meta text-ink-faint">{t(block.from ? AUTHOR_KEYS[block.from] : 'work.conversation.you')}</span>
        {/* The level named only where it is not quiet (COWORK1a, the design §8.2): the person's own words sent now. A word in a
            neutral chip, never a hue, and never a wait's. */}
        {block.level === 'now' && <Chip>{t('work.level.now')}</Chip>}
      </span>
      {block.choice
        ? <ChoiceWords choice={block.choice} text={block.text ?? ''} className="text-ink-soft" />
        : block.text && <p className="m-0 mt-0.5 whitespace-pre-wrap wrap-anywhere text-body text-ink-soft">{block.text}</p>}
      <Attached files={block.files} />
      {when && <p className="m-0 mt-1 text-meta text-ink-faint">{when}</p>}
    </div>
  );
}

/**
 * Words the person pressed as an answer of its park's question (PARKVIEW1; Fable's read, Q4): *Chose* and the answer as its question
 * offered it, then the person's own words, so a press reads as a choice and never as words they typed. The words begin with the label,
 * as the page composes a press; where a record says otherwise they are drawn whole beneath the mark.
 */
function ChoiceWords({ choice, text, className }: { choice: string; text: string; className: string }) {
  const { t } = useTranslation();
  const own = text === choice ? '' : text.startsWith(`${choice}\n\n`) ? text.slice(choice.length + 2) : text;
  return (
    <>
      <p className="m-0 mt-0.5 flex min-w-0 flex-wrap items-baseline gap-x-1.5 text-body">
        <Chip>{t('work.conversation.chose')}</Chip>
        <span className={cn('wrap-anywhere font-semibold', className)}>{choice}</span>
      </p>
      {own && <p className={cn('m-0 mt-0.5 whitespace-pre-wrap wrap-anywhere text-body', className)}>{own}</p>}
    </>
  );
}

function BlockView({ block, tree, reveal }: { block: Block; tree?: string | null; reveal?: string }) {
  const { exchanges, onOpenHelper } = useContext(AskedCards);
  switch (block.kind) {
    case 'message':
      return <Markdown text={block.text ?? ''} />;
    case 'thought':
      return <ThoughtRow text={block.text ?? ''} />;
    case 'tool': {
      // A call that asked a helper is that request's card (COMMS1h), its answer folded in.
      const card = exchanges.cards[block.key];
      if (card) return <ExchangeCardView card={card} revealed={reveal === block.key} onOpen={onOpenHelper} />;
      return <ToolCard call={block} tree={tree} />;
    }
    case 'plan':
      return <PlanView entries={block.entries ?? []} />;
    case 'note':
      // A note the page words by its code (MSG1c3): a word a stop cut off on its way.
      if (block.code) return <CodedLine block={block} />;
      // A note about the person's words (MSG1f): where they went, or why they cannot go on here.
      if (block.to) return <WentLine block={block} />;
      if (block.why) return <CannotLine block={block} />;
      // A conversation's end, where it went on with another agent (MIXAGENT1b-i): the line, and a press to the new one.
      if (handedTo(block)) return <HandedLine block={block} to={handedTo(block)!} />;
      // A note worded by its parts (CONVNOTE1), or the driver's English where it has none.
      return <NoteLine text={block.text ?? ''} parts={block.parts} under={block.under} />;
    case 'held':
      return <HeldAsk block={block} />;
    default:
      return <RawLine block={block} />;
  }
}

/**
 * A run of work between two things the agent said: one line that counts its calls, its failures in
 * the failed tone, and whether it thought — a press opens its cards, and another folds them again.
 */
function RunView({ items, open: startsOpen, tree, reveal }: {
  items: Block[]; open: boolean; tree?: string | null; reveal?: string;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(startsOpen);
  // A jump that lands inside shows the fold it lands in, drawn at once so the jump can reach it; the
  // person may fold it again, which puts that jump aside.
  const [dismissed, setDismissed] = useState<string | undefined>();
  const holds = Boolean(reveal) && reveal !== dismissed && items.some((item) => item.key === reveal);
  const shown = open || holds;
  // A wait a folded call raised is said on the run's line, so the fold never hides it (WAITFOOT1c); open, the call says it.
  const { marks, onToWait } = useContext(WaitMarks);
  const raised = !shown && onToWait && marks ? items.flatMap((item) => marks[item.key] ?? []) : [];
  const { tools, failed, thought } = runCount(items);
  const parts = [
    tools > 0 && t('work.conversation.tools', { count: tools }),
    thought && t('work.conversation.thought'),
  ].filter(Boolean);

  const said = parts.length > 0 ? parts.join(' · ') : t('work.conversation.worked');

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5">
      <button
        type="button"
        aria-expanded={shown}
        // One name for the whole line: the failed count is its own span for its tone.
        aria-label={failed > 0 ? `${said} · ${t('work.conversation.failed', { count: failed })}` : said}
        onClick={() => {
          if (holds) setDismissed(reveal);
          setOpen(!shown);
        }}
        className="flex items-center gap-1.5 justify-self-start border-0 bg-transparent p-0 text-small text-ink-faint hover:text-ink"
      >
        <Icon name={shown ? 'chevronDown' : 'chevronRight'} size={13} />
        {said}
        {failed > 0 && (
          <span className="text-ink-danger">{`· ${t('work.conversation.failed', { count: failed })}`}</span>
        )}
      </button>
      {raised.length > 0 && <WaitMark waits={raised} onPress={() => onToWait!(raised[0]!)} />}
      {shown && items.map((item) => (
        <Held key={item.key} id={item.key} reveal={reveal}><BlockView block={item} tree={tree} /></Held>
      ))}
    </div>
  );
}

