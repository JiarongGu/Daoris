import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { compact, span } from '../format';
import { cn } from '../lib/cn';
import { Button, Dot, Icon, Tip } from '../ui';
import type { Ask, Block, PlanEntry, Turn } from './conversation';
import { Markdown } from './Markdown';
import { ToolCard } from './ToolCard';

/**
 * A session's conversation (D76, CONV2): what was asked, what the agent said and did, and how each
 * turn ended — drawn from the typed events the driver keeps, never parsed from the console.
 *
 * @remarks
 * - **A finished turn folds its work.** Its tool calls, thoughts and earlier messages collapse into
 *   one row that counts them, and its last message stays open: what the agent concluded is what a
 *   reader came back for, and how it got there is one press away. A running turn is all open.
 * - **The agent's words are Markdown**; the person's are shown as written. Both are content, never
 *   translated (translation-parity).
 * - **A session whose door carries only text** has no conversation to draw, and says so, pointing at
 *   the console below — D76 §1's "stays text, and the page says so".
 *
 * A molecule: turns in, a press out. The organism above it holds the record.
 */
export function ConversationView({
  turns, tree, structured, chat = false, live = false, loaded = true, earlier = false, onLoadEarlier,
}: {
  turns: Turn[];
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
    return <p className="m-0 mt-2 max-w-prose text-small text-ink-faint">{sentence}</p>;
  }

  return (
    // `minmax(0,1fr)`: a code block's long line scrolls inside its own box, and never widens the
    // conversation past the centre (seen on the window with a real session, CONV3). No measure of its
    // own: the agent's words are content, shown at the width they are given (UX5 U16, the owner: a
    // 768px cap was half a maximized window).
    <section aria-label={t('work.conversation.label')} className="grid w-full grid-cols-[minmax(0,1fr)] gap-3">
      {earlier && onLoadEarlier && (
        <Button variant="ghost" onClick={onLoadEarlier} className="justify-self-center text-small">
          {t('work.conversation.earlier')}
        </Button>
      )}
      {turns.map((turn, index) => (
        <TurnView key={turn.key} turn={turn} tree={tree} running={live && index === turns.length - 1 && !turn.ended} />
      ))}
    </section>
  );
}

function TurnView({ turn, tree, running }: { turn: Turn; tree?: string | null; running: boolean }) {
  const { t } = useTranslation();
  const [unfolded, setUnfolded] = useState(false);

  // A finished turn with more than its answer folds everything but its last message.
  const last = turn.items[turn.items.length - 1];
  const answer = turn.ended && last?.kind === 'message' ? last : undefined;
  const work = answer ? turn.items.slice(0, -1) : turn.items;
  const folds = Boolean(turn.ended) && work.length > 0 && !unfolded;

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5">
      {turn.ask && <AskView ask={turn.ask} />}
      {folds
        ? <FoldRow items={work} onOpen={() => setUnfolded(true)} />
        : work.map((item) => <BlockView key={item.key} block={item} tree={tree} />)}
      {turn.ended && unfolded && work.length > 0 && (
        <button
          type="button"
          onClick={() => setUnfolded(false)}
          className="justify-self-start border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
        >
          {t('work.conversation.fold')}
        </button>
      )}
      {answer && <BlockView block={answer} />}
      {running && <Dot tone="live" label={t('work.conversation.working')} className="mt-1" />}
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

  // Input is what the harness read this turn, however it arrived: new, from its cache, or into it.
  const inputs = [tokens?.input, tokens?.cacheRead, tokens?.cacheWrite].filter((n): n is number => typeof n === 'number');
  const input = inputs.length > 0 ? inputs.reduce((sum, n) => sum + n, 0) : undefined;
  const line = [
    took !== undefined ? span(took) : null,
    input !== undefined ? t('work.meter.in', { tokens: compact(input) }) : null,
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

/** What the person asked, or the target the driver composed — the latter folded, since it is long. */
function AskView({ ask }: { ask: Ask }) {
  const { t } = useTranslation();
  const target = ask.origin === 'target';
  const [open, setOpen] = useState(!target);

  return (
    <div className="rounded-card border border-line border-l-[3px] border-l-accent bg-raised px-3 py-2">
      <div className="flex items-center justify-between gap-2">
        <span className="text-meta text-ink-faint">
          {target ? t('work.conversation.target') : t('work.conversation.you')}
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
      {ask.text && (
        <p className={cn('m-0 mt-0.5 whitespace-pre-wrap text-body text-ink', !open && 'line-clamp-2')}>{ask.text}</p>
      )}
      {/* What the person attached (CONV4c): names, as the record keeps them. */}
      {ask.files && ask.files.length > 0 && (
        <ul aria-label={t('work.conversation.attached')} className="m-0 mt-1.5 flex list-none flex-wrap gap-1.5 p-0">
          {ask.files.map((name, index) => (
            <li
              key={`${index}:${name}`}
              className="inline-flex min-w-0 items-center gap-1 rounded-control border border-line bg-page px-2 py-0.5 text-meta text-ink-soft"
            >
              <Icon name="attach" size={11} className="shrink-0 text-ink-faint" />
              <span className="min-w-0 truncate">{name}</span>
            </li>
          ))}
        </ul>
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
      return <NoteLine text={block.text ?? ''} />;
    default:
      return <RawLine block={block} />;
  }
}

/** A finished turn's work, counted on one line; a press opens it. */
function FoldRow({ items, onOpen }: { items: Block[]; onOpen: () => void }) {
  const { t } = useTranslation();
  const tools = items.filter((b) => b.kind === 'tool').length;
  const messages = items.filter((b) => b.kind === 'message').length;
  const thought = items.some((b) => b.kind === 'thought');
  const parts = [
    tools > 0 && t('work.conversation.tools', { count: tools }),
    messages > 0 && t('work.conversation.messages', { count: messages }),
    thought && t('work.conversation.thought'),
  ].filter(Boolean);

  return (
    <button
      type="button"
      onClick={onOpen}
      className="flex items-center gap-1.5 justify-self-start border-0 bg-transparent p-0 text-small text-ink-faint hover:text-ink"
    >
      <Icon name="chevronRight" size={13} />
      {parts.length > 0 ? parts.join(' · ') : t('work.conversation.worked')}
    </button>
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

/** The driver's own sentence about the session — a refusal, a missing connector. */
function NoteLine({ text }: { text: string }) {
  const { t } = useTranslation();
  return (
    <p className="m-0 text-small text-ink-soft">
      <span className="mr-1.5 text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.conversation.driver')}</span>
      {text}
    </p>
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
