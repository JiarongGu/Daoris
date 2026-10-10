import { createContext, type ReactNode, useContext, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { compact, moment, span } from '../format';
import { cn } from '../lib/cn';
import { Button, Dot, Icon, Inline, Tip } from '../ui';
import type { NotePart } from '../api';
import { type Ask, type Block, CONVERSATION_CODES, type PlanEntry, runCount, segments, type Turn } from './conversation';
import { HandedAccount } from './HandedAccount';
import { Markdown } from './Markdown';
import { Note } from './Note';
import { noteText } from './noteLines';
import { type NewSessionAnswer, newSessionSaid, reasonOf, type ReasonValues } from './say';
import { ToolCard } from './ToolCard';

/**
 * Words a cooling account holds (MSG1g2, D137 §2.2): a resume asks for the account its record ran on, so while that account
 * cools the words wait for its reset, and *Go on in a new session* carries them on now, without this conversation.
 */
export type Cooling = {
  /** When the account cools until (ISO 8601), which the held line names. */
  until: string;
  /** The quest the session served, which a refusal names. */
  quest?: string | null;
  /** *Go on in a new session*; absent, the held line alone. */
  onGoOnNew?: () => void;
  /** The press is on its way. */
  pending?: boolean;
  /** What the press came to, once the route answered. */
  answer?: NewSessionAnswer | null;
};

/**
 * What the person's words to a session that parked or ended need below the conversation (MSG1f, D137 §3.1): whether it
 * is a chat, the doors a note about them offers, what a reason may name, and a cool-off that holds them (MSG1g2). Handed
 * down by the view itself, so a block deep in a turn reads it without every level between carrying it.
 */
type Said = {
  chat: boolean;
  onSession?: (id: string) => void;
  onStartFrom?: (words: string[]) => void;
  reasons: ReasonValues;
  cooling?: Cooling;
};

const SaidDoors = createContext<Said>({ chat: false, reasons: {} });

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
 *
 * A molecule: turns in, a press out. The organism above it holds the record.
 */
export function ConversationView({
  turns, tree, structured, chat = false, live = false, turnRunning, loaded = true, earlier = false, onLoadEarlier,
  reveal, toolbar, onSession, onStartFrom, reasons = {}, cooling,
}: {
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
    <SaidDoors.Provider value={{ chat, onSession, onStartFrom, reasons, cooling: chat ? undefined : cooling }}>
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
        {turns.map((turn, index) => (
          <TurnView
            key={turn.key}
            turn={turn}
            tree={tree}
            // Words waiting at the foot are no run of the agent's (MSG1f): nothing runs until its first prompt takes them.
            running={(turnRunning ?? live) && index === turns.length - 1 && !turn.ended && !turn.waiting}
            onLoadEarlier={earlier && turn.gap ? onLoadEarlier : undefined}
            reveal={reveal}
          />
        ))}
      </section>
    </SaidDoors.Provider>
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

/** A block as the page holds it: named by its key, so a jump can find it, and marked when it is the one. */
function Held({ id, reveal, children }: { id: string; reveal?: string; children: ReactNode }) {
  return (
    <div
      data-block={id}
      className={cn('min-w-0 scroll-mt-12', reveal === id && 'rounded-control outline-2 outline-offset-4 outline-accent/60')}
    >
      {children}
    </div>
  );
}

function TurnView({ turn, tree, running, onLoadEarlier, reveal }: {
  turn: Turn;
  tree?: string | null;
  running: boolean;
  reveal?: string;
  /** The page began past this turn's ask: the earlier events belong between it and what is held. */
  onLoadEarlier?: () => void;
}) {
  const { t } = useTranslation();
  // The run in hand stays open while the turn goes on, and where the session ended inside it.
  const parts = segments(turn.items, running || Boolean(turn.cut));

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5">
      {turn.ask && <Held id={turn.ask.key} reveal={reveal}><AskView ask={turn.ask} /></Held>}
      {onLoadEarlier && <Earlier onLoadEarlier={onLoadEarlier} />}
      {parts.map((part) => (part.kind === 'run'
        ? <RunView key={part.key} items={part.items} open={part.open} tree={tree} reveal={reveal} />
        : <Held key={part.block.key} id={part.block.key} reveal={reveal}><BlockView block={part.block} tree={tree} /></Held>))}
      {turn.waiting && <NewSessionDoor turn={turn} />}
      {running && <Dot tone="live" label={t('work.conversation.working')} className="mt-1" />}
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
    </div>
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
 * are a block from that agent, open, under Daoris's fixed words that name it and its maker, never the person's.
 */
function AskView({ ask }: { ask: Ask }) {
  const { t } = useTranslation();
  const findings = Boolean(ask.opinion);
  const target = ask.origin === 'target';
  const [open, setOpen] = useState(!target || findings);

  return (
    <div
      className={cn(
        'rounded-card border border-line border-l-[3px] bg-raised px-3 py-2',
        findings ? 'border-l-line-strong' : 'border-l-accent',
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-meta text-ink-faint">
          {findings ? t('work.conversation.opinion') : target ? t('work.conversation.target') : t('work.conversation.you')}
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
      {ask.text && (
        <p className={cn('m-0 mt-0.5 whitespace-pre-wrap wrap-anywhere text-body text-ink', !open && 'line-clamp-2')}>{ask.text}</p>
      )}
      <Attached files={ask.files} />
      {/* What the target was composed of, section by section (CONTEXT1), beside the words it composed. */}
      {target && !findings && <HandedAccount account={ask.account} />}
    </div>
  );
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

/** When the person's words reach the session, by the door's reach (D136, MSG1f): a chat's conversation opens again. */
const heldKey = (reaches: string | null | undefined, chat: boolean) => {
  if (reaches === 'next-step') return 'work.conversation.held.nextStep';
  if (reaches === 'resume') return chat ? 'work.conversation.held.reopen' : 'work.conversation.held.resume';
  return 'work.conversation.held.turnEnd';
};

/** Words waiting at a record's foot that no driver's note has settled: what a cool-off holds (MSG1g2). */
const waitingWords = (turn: Turn) =>
  turn.items.filter((item) => item.kind === 'held' && item.reaches === 'resume' && !item.settled && !item.unreached);

/**
 * The person's words to a session, waiting to reach it (STEER1, D136; MSG1f, D137 §3.1): theirs, as written, dashed as
 * the composer's waiting words are, and saying when the session reads them — at its next step, at its turn's end, or as
 * the same session goes on, or at its account's reset where that account cools (MSG1g2) — or, the session over, that it
 * never did. Where the session takes them they become the ask of that turn, and this goes. Where a driver's note below
 * says where they went, or that they cannot go on here, that note is their line and this says none.
 */
function HeldAsk({ block }: { block: Block }) {
  const { t } = useTranslation();
  const { chat, cooling } = useContext(SaidDoors);
  const reset = block.reaches === 'resume' && cooling;
  const when = block.settled ? null
    : block.unreached ? t('work.conversation.held.never')
      : reset ? t('work.conversation.held.cooling', { time: moment(cooling.until) })
        : t(heldKey(block.reaches, chat));

  return (
    <div className="rounded-card border border-dashed border-line-strong px-3 py-2">
      <span className="text-meta text-ink-faint">{t('work.conversation.you')}</span>
      {block.text && <p className="m-0 mt-0.5 whitespace-pre-wrap wrap-anywhere text-body text-ink-soft">{block.text}</p>}
      <Attached files={block.files} />
      {when && <p className="m-0 mt-1 text-meta text-ink-faint">{when}</p>}
    </div>
  );
}

/** Never in a sentence: where a session's id goes, so the sentence can be cut around its door. */
const DOOR = '⁣';

/**
 * The person's words went to a new session (MSG1f, D137 §3.1): said in the page's words, the session a door to it, once,
 * where the words were shown. A reason the page cannot word leaves the driver's own line, which names it too.
 */
function WentLine({ block }: { block: Block }) {
  const { t } = useTranslation();
  const { onSession, reasons } = useContext(SaidDoors);
  const why = block.why ? reasonOf(t, block.why, reasons) : null;
  if (!why || !block.to) return <NoteLine text={block.text ?? ''} />;
  const to = block.to;
  const [before, ...after] = t('work.conversation.went', { id: DOOR, why }).split(DOOR);

  return (
    <p className="m-0 text-small text-ink-soft">
      {before}
      {onSession
        ? (
          <button
            type="button"
            onClick={() => onSession(to)}
            className="cursor-pointer border-0 bg-transparent p-0 font-mono text-small text-ink underline decoration-line-strong underline-offset-2 hover:text-accent hover:decoration-accent"
          >
            {to}
          </button>
        )
        : <span className="font-mono">{to}</span>}
      {after.join(to)}
    </p>
  );
}

/**
 * The person's words cannot go on in this session (MSG1f, D137 §2.2): why, in the page's words, and one press that starts
 * a conversation with them — the person's, since a new conversation has none of this one's context. The press only where
 * the page holds every word the note names, so what it starts with is what they said.
 */
function CannotLine({ block }: { block: Block }) {
  const { t } = useTranslation();
  const { onStartFrom, reasons } = useContext(SaidDoors);
  const why = block.why ? reasonOf(t, block.why, reasons) : null;
  if (!why) return <NoteLine text={block.text ?? ''} />;
  const said = block.said;

  return (
    <div className="grid justify-items-start gap-1.5">
      <p className="m-0 text-small text-ink-soft">{t('work.say.cannot', { why })}</p>
      {onStartFrom && said && said.length > 0 && (
        <Button onClick={() => onStartFrom(said)}>{t('work.say.startChat')}</Button>
      )}
    </div>
  );
}

/**
 * A driver's note the page words itself (MSG1c3, D142 point 1): its code's sentence in the reader's language, said as the
 * went line is, where the word it names was shown. A code this page does not know leaves the driver's English line.
 */
function CodedLine({ block }: { block: Block }) {
  const { t } = useTranslation();
  const key = block.code ? CONVERSATION_CODES[block.code] : undefined;
  if (!key) return <NoteLine text={block.text ?? ''} />;
  return <p className="m-0 text-small text-ink-soft">{t(key)}</p>;
}

/**
 * *Go on in a new session* (MSG1g2, D137 §2.2), under words a cooling account holds: what it costs (the new session starts
 * without this conversation's context, so it is the person's press), the press, and then what it came to in the page's
 * words. Where nothing carries the words on by itself, a conversation or a closed quest's session, MSG1f's press starts a
 * conversation with every word waiting. The press is offered again only for a choice the driver could not keep.
 */
function NewSessionDoor({ turn }: { turn: Turn }) {
  const { t } = useTranslation();
  const { cooling, onStartFrom } = useContext(SaidDoors);
  const words = waitingWords(turn);
  if (!cooling?.onGoOnNew || words.length === 0) return null;
  const { answer, onGoOnNew, pending, quest } = cooling;
  const said = answer ? newSessionSaid(t, answer, { quest }) : null;
  const again = !answer || (!answer.sent && !answer.why);

  return (
    <div className="grid justify-items-start gap-1.5">
      {said && <p className="m-0 text-small text-ink-soft">{said.sentence}</p>}
      {again && (
        <>
          {!said && <p className="m-0 text-small text-ink-soft">{t('work.say.newSession.loses')}</p>}
          <Button onClick={onGoOnNew} disabled={pending}>{t('work.say.goOnNew')}</Button>
        </>
      )}
      {said?.startChat && onStartFrom && (
        <Button onClick={() => onStartFrom(words.map((word) => word.text ?? ''))}>{t('work.say.startChat')}</Button>
      )}
    </div>
  );
}

function BlockView({ block, tree }: { block: Block; tree?: string | null }) {
  switch (block.kind) {
    case 'message':
      return <Markdown text={block.text ?? ''} />;
    case 'thought':
      return <ThoughtRow text={block.text ?? ''} />;
    case 'tool':
      return <ToolCard call={block} tree={tree} />;
    case 'plan':
      return <PlanView entries={block.entries ?? []} />;
    case 'note':
      // A note the page words by its code (MSG1c3): a word a stop cut off on its way.
      if (block.code) return <CodedLine block={block} />;
      // A note about the person's words (MSG1f): where they went, or why they cannot go on here.
      if (block.to) return <WentLine block={block} />;
      if (block.why) return <CannotLine block={block} />;
      // A note worded by its parts (CONVNOTE1), or the driver's English where it has none.
      return <NoteLine text={block.text ?? ''} parts={block.parts} />;
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
      {shown && items.map((item) => (
        <Held key={item.key} id={item.key} reveal={reveal}><BlockView block={item} tree={tree} /></Held>
      ))}
    </div>
  );
}

/** The agent's reasoning: its first line, and the whole on a press. */
function ThoughtRow({ text }: { text: string }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const first = text.trim().split('\n', 1)[0] ?? '';

  return (
    <div className="text-small text-ink-soft">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex w-full min-w-0 items-center gap-1.5 border-0 bg-transparent p-0 text-left text-ink-soft hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={13} className="shrink-0" />
        <Icon name="think" size={13} className="shrink-0" />
        <span className="shrink-0">{t('work.conversation.thought')}</span>
        {!open && <span className="min-w-0 truncate italic text-ink-faint">{first}</span>}
      </button>
      {open && (
        <div className="mt-1 border-l-2 border-line pl-3 italic">
          <Markdown text={text} />
        </div>
      )}
    </div>
  );
}

const PLAN_MARK: Record<string, string> = { completed: '✓', in_progress: '◐', pending: '○' };

/** The agent's plan, as it last stood. */
function PlanView({ entries }: { entries: PlanEntry[] }) {
  const { t } = useTranslation();
  return (
    <div className="rounded-control border border-line bg-raised px-3 py-2">
      <span className="flex items-center gap-1.5 text-meta text-ink-faint">
        <Icon name="plan" size={13} />
        {t('work.conversation.plan')}
      </span>
      <ol className="m-0 mt-1 grid list-none gap-0.5 p-0">
        {entries.map((entry, index) => (
          // Done is a check, never a strike-through: struck text reads as cancelled (CONV2's look).
          <li key={index} className={cn('flex gap-2 text-small', entry.status === 'completed' ? 'text-ink-soft' : 'text-ink')}>
            <span
              aria-hidden
              className={cn(
                'w-3 shrink-0 text-center',
                entry.status === 'completed' ? 'text-st-done' : entry.status === 'in_progress' ? 'text-accent' : 'text-ink-faint',
              )}
            >
              {PLAN_MARK[entry.status ?? ''] ?? '○'}
            </span>
            {entry.content}
          </li>
        ))}
      </ol>
    </div>
  );
}

/** How long a driver's note may be before it shows two lines and the rest on a press. */
const LONG_NOTE = 240;

/**
 * The driver's own sentence about the session — a refusal, a missing connector. A long one shows two
 * lines and the rest on a press (SESS1 S6): records written before the refusal named its call carry the
 * request's JSON, four lines each, and nothing reads that JSON to shorten it.
 *
 * A note with parts (CONVNOTE1: a refused sign-in since SIGNIN1b, a landing's line since LAND2b, the opening line since
 * UX7d-1) is worded from them as a session record's note is (`Note`, LANG1b): each coded line in the reader's language, and a
 * line the page cannot word as its record keeps it, marked. Its English is shown only where it has none, its backticks
 * drawn as code as a worded line's are (UX7d-1), never as backticks.
 */
function NoteLine({ text, parts }: { text: string; parts?: readonly NotePart[] }) {
  const { t, i18n } = useTranslation();
  const worded = parts?.length ? noteText(t, { note: text, parts }, i18n.language) : text;
  const long = worded.length > LONG_NOTE;
  const [open, setOpen] = useState(!long);
  return (
    <div className="text-small text-ink-soft">
      <p className={cn('m-0 wrap-anywhere', !open && 'line-clamp-2')}>
        <span className="mr-1.5 text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.conversation.driver')}</span>
        {parts?.length ? <Note note={text} parts={parts} compact /> : <Inline text={text} />}
      </p>
      {long && (
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
  );
}

/** Something the wire said that this version has no kind for — kept, and shown as itself. */
function RawLine({ block }: { block: Block }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  return (
    <div className="text-meta text-ink-faint">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex items-center gap-1 border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} />
        {t('work.conversation.raw', { title: block.title ?? '' })}
      </button>
      {open && (
        <pre className="m-0 mt-1 max-h-48 overflow-auto whitespace-pre-wrap wrap-anywhere rounded-control bg-page px-2.5 py-1.5 font-mono">
          {[block.text, block.raw].filter(Boolean).join('\n')}
        </pre>
      )}
    </div>
  );
}
