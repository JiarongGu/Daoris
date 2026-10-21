import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { ago } from '../locales/format';
import { cn } from '../lib/cn';
import {
  type Channel, type ChannelItem, type ChannelPost, type ChannelReceipt, forEntry, forwardable, lineWords, newestAt, RECEIPT_STATES,
  repliesTo, repositoriesOf, sinceMark,
} from '../work/channel';
import { Refused } from '../work/InlineConfirm';
import { Button, Icon, Pill, SelectField } from '../design/ui';

/** How a post ended, as the composer hears it: `done` once the service kept it, `refused` with the service's code, which this page words. */
export type ChannelAnswered = { done: () => void; refused: (code: string) => void };

/** Everyone, and no message: the one value of each choice that names nothing (a repository's name has no `*`). */
const NONE = '*';

/** The codes the service refuses a post with, which this page has words for; another is said as the service wrote it. */
const REFUSALS = new Set(['empty', 'too-long', 'bad-to', 'bad-re', 'not-found']);

/** The codes *Send to its session* is refused with, which this page has words for (COWORK1d); another is said as the shell wrote it. */
const FORWARD_REFUSALS = new Set(['no-part', 'gone', 'held']);

/** The person's *Send to its session* (COWORK1d): a message, the repository whose session it goes to, and how it ended. */
export type ChannelForward = (message: string, repository: string, answered: ChannelAnswered) => void;

/** A door, in the accent, as the page's other doors are. */
const DOOR = 'cursor-pointer border-0 bg-transparent p-0 text-left text-accent underline-offset-2 hover:underline';

/** Who wrote a message: the person, or a session by its repository and agent (the agent's adapter, never a model), else a session. */
const whoOf = (t: TFunction, item: ChannelItem) => item.person
  ? t('asks.channel.who.you')
  : [item.repository, item.agent].filter(Boolean).join(' · ') || t('asks.channel.who.session');

/** A message cut to the one line a choice and a quote have room for. */
const firstWords = (text: string | null | undefined, limit = 60) => {
  const line = (text ?? '').split('\n', 1)[0].trim();
  return line.length > limit ? `${line.slice(0, limit - 1)}…` : line;
};

/**
 * **An ask's channel** (SESSONE2c, D167 points 6 to 8; the co-working design §2.4): where its sessions and the person talk,
 * messages and Daoris's own lines in time order, newest at the foot, and the person's composer under them. The ask's page
 * draws all of it; a session's panel (`ChannelPanel`) draws what concerns its line of work and starts *To* on its repository.
 *
 * @remarks
 * **A molecule**: the channel arrives as the shell answered it, and a post leaves as a call with how it ended; nothing is asked
 * here. Daoris's lines are coded and worded in the reader's language (D142); a message is shown as written, and a session's
 * is that agent's claim, so it names its repository and agent and never says *You*.
 *
 * **What is unread** is what came after the person's mark when the page was opened: the items are marked *new*, counted in the
 * heading, and the mark moves through the newest once (`onRead`), so the count falls in the rows and the list. The marks stay
 * until the page is left, so what was new is seen. A panel reads a part of the channel and moves no mark.
 *
 * **A teammate's ask's channel is read-only** (SESSONE2h): `readOnly` draws the messages and the unread count and no composer, no
 * *Reply*, and a sentence saying the ask is a teammate's, held on their machine. The unread count is drawn as the heading's, and the
 * mark moves where `onRead` is handed: the service keeps the person's mark on a teammate's ask its mirror holds (NOTIFY2g).
 *
 * **A refusal is the service's code** (`empty`, `too-long`, `bad-to`, `bad-re`, `not-found`), said in the composer in the
 * reader's language, the words kept.
 *
 * **What a message reached** (COWORK1d, D188; the message-urgency design §7, §8.4): an urgent message names its level in a word, and
 * each says, per repository it addresses, what reached that line of work, never more than Daoris observed. Where it waits there, is
 * held for a turn's end, was handed only partly or never reached it, *Send to its session* sends it on (`onForward`), the person's own
 * press; a read-only channel offers none.
 */
export function ChannelSection({
  channel, onPost, onRead, onOpenQuest, onForward, repositories = [], only, readOnly = false,
}: {
  /** Null while it is being read. */
  channel: Channel | null;
  onPost: (post: ChannelPost, answered: ChannelAnswered) => void;
  /** *Send to its session* (COWORK1d). Absent where there is nowhere to send one from. */
  onForward?: ChannelForward;
  /** A teammate's ask's channel (SESSONE2h): read here and posted nowhere from here, so no composer and no *Reply*. */
  readOnly?: boolean;
  /** The person's mark moved through the newest item shown, once what was unread is on the page. Absent in a panel. */
  onRead?: (through: string) => void;
  /** A line opens its quest. Absent where there is nowhere to open it. */
  onOpenQuest?: (quest: string) => void;
  /** Repositories the page knows the ask works in, offered as *To* beside those the channel names. */
  repositories?: readonly string[];
  /** A session's panel: only what concerns this line of work, and *To* starting at its repository. */
  only?: { entry: string; repository: string };
}) {
  const { t } = useTranslation();
  if (!channel) return <p className="m-0 text-small text-ink-faint">{t('asks.channel.loading')}</p>;
  return (
    <ChannelBody
      channel={channel} onPost={onPost} onRead={onRead} onOpenQuest={onOpenQuest} repositories={repositories} only={only} readOnly={readOnly}
      onForward={readOnly ? undefined : onForward}
    />
  );
}

function ChannelBody({ channel, onPost, onRead, onOpenQuest, onForward, repositories, only, readOnly }: {
  readOnly: boolean;
  channel: Channel;
  onPost: (post: ChannelPost, answered: ChannelAnswered) => void;
  onForward?: ChannelForward;
  onRead?: (through: string) => void;
  onOpenQuest?: (quest: string) => void;
  repositories: readonly string[];
  only?: { entry: string; repository: string };
}) {
  const { t } = useTranslation();
  const place = useId();
  const items = only ? forEntry(channel.items, only) : channel.items;
  const newest = newestAt(channel.items);
  // What was unread when the page opened, held until it is left: the mark moves at once, and what was new is still seen.
  const [opened] = useState(() => ({ mark: channel.mark ?? null, unread: channel.unread }));
  // A read-only channel counts and marks what is new as the ask's page does, though no mark moves from it.
  const counted = onRead !== undefined || readOnly;
  const fresh = counted ? sinceMark(items, opened.mark) : [];
  const [to, setTo] = useState(only?.repository ?? NONE);
  const [reply, setReply] = useState(NONE);
  const [words, setWords] = useState('');
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const turn = useRef(0);
  const alive = useRef(true);
  const read = useRef(onRead);
  read.current = onRead;

  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);

  // The mark moves once what was unread is on the page, and again only if more arrives and is shown.
  useEffect(() => {
    if (channel.unread > 0 && newest) read.current?.(newest);
  }, [channel.unread, newest]);

  const recipients = repositoriesOf(channel.items, [...(only ? [only.repository] : []), ...repositories]);
  const messages = items.filter((item) => item.kind === 'message' && item.id);
  const unread = counted ? opened.unread : 0;

  const answer = (item: ChannelItem) => {
    setReply(item.id!);
    // Answering a session is saying it to the repository that wrote it, which the person may still change.
    if (!item.person && item.repository) setTo(item.repository);
  };

  const send = () => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    onPost({ text: words.trim(), to: to === NONE ? null : to, re: reply === NONE ? null : reply }, {
      done: () => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setWords('');
        setReply(NONE);
      },
      refused: (code) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(code);
      },
    });
  };

  const refusalWords = refusal === null ? null : REFUSALS.has(refusal) ? t(`asks.channel.refused.${refusal}`) : t('asks.channel.refused.other', { code: refusal });

  return (
    <div className="grid gap-3">
      <p className="m-0 flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 text-small text-ink-soft">
        <span>{only ? t('asks.channel.panel.lead', { repository: only.repository }) : t('asks.channel.lead')}</span>
        {unread > 0 && <Pill tone="open">{t('asks.channel.unread', { count: unread })}</Pill>}
      </p>

      {items.length === 0
        ? <p className="m-0 text-small text-ink-faint">{only ? t('asks.channel.panel.empty', { repository: only.repository }) : t('asks.channel.empty')}</p>
        : (
          <ol aria-label={t('asks.channel.title')} className="m-0 grid list-none gap-2 p-0">
            {items.map((item, index) => {
              const isNew = fresh.includes(item);
              return item.kind === 'line'
                ? <LineRow key={`line-${index}`} item={item} isNew={isNew} onOpenQuest={onOpenQuest} />
                : (
                  <MessageRow
                    key={item.id ?? `message-${index}`}
                    item={item}
                    anchor={`${place}-${item.id}`}
                    quoted={item.re ? repliesTo(channel.items, item.re) : null}
                    anchorOf={(id) => `${place}-${id}`}
                    isNew={isNew}
                    onReply={readOnly ? undefined : () => answer(item)}
                    onForward={onForward && item.id ? (repository, answered) => onForward(item.id!, repository, answered) : undefined}
                  />
                );
            })}
          </ol>
        )}

      {readOnly ? <p className="m-0 text-small text-ink-faint">{t('asks.elsewhere.readOnly')}</p> : <div className="grid gap-2">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
          <label className="flex items-center gap-2 text-small text-ink-soft">
            {t('asks.channel.compose.to')}
            <SelectField
              ariaLabel={t('asks.channel.compose.to')}
              value={to}
              onChange={setTo}
              options={[{ value: NONE, label: t('asks.channel.everyone') }, ...recipients.map((name) => ({ value: name, label: name }))]}
            />
          </label>
          <label className="flex min-w-0 items-center gap-2 text-small text-ink-soft">
            {t('asks.channel.compose.reply')}
            <SelectField
              ariaLabel={t('asks.channel.compose.reply')}
              value={reply}
              onChange={setReply}
              options={[
                { value: NONE, label: t('asks.channel.compose.replyNone') },
                ...messages.map((item) => ({ value: item.id!, label: `${whoOf(t, item)}: ${firstWords(item.text)}` })),
              ]}
            />
          </label>
        </div>
        <textarea
          aria-label={t('asks.channel.compose.words')}
          placeholder={t('asks.channel.compose.words')}
          rows={3}
          value={words}
          onChange={(e) => setWords(e.target.value)}
          className="min-w-0 resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
        />
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <Button variant="primary" disabled={pending || words.trim() === ''} onClick={send}>{t('asks.channel.compose.send')}</Button>
          <span role="status" aria-live="polite" className={pending ? 'text-small text-ink-faint' : 'sr-only'}>
            {pending ? t('work.confirm.pending') : null}
          </span>
          <span className="text-small text-ink-faint">
            {to === NONE ? t('asks.channel.compose.note') : t('asks.channel.compose.noteTo', { repository: to })}
          </span>
        </div>
        {refusalWords && <Refused sentence={refusalWords} />}
      </div>}
    </div>
  );
}

/** One of Daoris's lines: quiet and neutral, worded from its code, and a door to its quest where there is one. */
function LineRow({ item, isNew, onOpenQuest }: { item: ChannelItem; isNew: boolean; onOpenQuest?: (quest: string) => void }) {
  const { t } = useTranslation();
  const words = lineWords(t, item);
  return (
    <li className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 px-1 text-small text-ink-soft">
      <span className="font-semibold">{t('asks.channel.who.daoris')}</span>
      {onOpenQuest && item.quest
        ? <button type="button" onClick={() => onOpenQuest(item.quest!)} className={cn(DOOR, 'min-w-0 wrap-anywhere text-small')}>{words}</button>
        : <span className="min-w-0 wrap-anywhere">{words}</span>}
      <span className="text-meta text-ink-faint">{ago(item.at)}</span>
      {isNew && <Pill tone="neutral">{t('asks.channel.new')}</Pill>}
    </li>
  );
}

/** One message as written, with who, to whom, when, what it answers, what it reached, and a press to answer it. */
function MessageRow({ item, anchor, anchorOf, quoted, isNew, onReply, onForward }: {
  item: ChannelItem;
  anchor: string;
  anchorOf: (id: string) => string;
  quoted: ChannelItem | null;
  isNew: boolean;
  /** Absent in a read-only channel (SESSONE2h): there is nothing to answer from here. */
  onReply?: () => void;
  /** *Send to its session* for one repository it addresses (COWORK1d); absent where nothing may be sent on from here. */
  onForward?: (repository: string, answered: ChannelAnswered) => void;
}) {
  const { t } = useTranslation();
  const toWords = item.to ? t('asks.channel.to', { to: item.to }) : t('asks.channel.toEveryone');
  return (
    <li
      id={anchor}
      tabIndex={-1}
      className={cn(
        'grid gap-1 rounded-control border border-line px-3 py-2 outline-none',
        item.person ? 'border-l-[3px] border-l-accent bg-raised' : 'bg-page',
      )}
    >
      <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1 text-meta text-ink-faint">
        <span className="text-small font-semibold text-ink">{whoOf(t, item)}</span>
        <span>{toWords}</span>
        <span>{ago(item.at)}</span>
        {item.question && <Pill tone={item.open ? 'open' : 'neutral'}>{item.open ? t('asks.channel.questionOpen') : t('asks.channel.question')}</Pill>}
        {/* COWORK1d (the design §8.2): a level is named only where it is not quiet, in a word and a neutral emphasis. */}
        {item.urgent === true && <Pill tone="neutral">{t('work.level.urgent')}</Pill>}
        {isNew && <Pill tone="neutral">{t('asks.channel.new')}</Pill>}
      </p>
      {item.re && (quoted
        ? (
          <p className="m-0 min-w-0 truncate text-small text-ink-faint">
            <button
              type="button"
              onClick={() => document.getElementById(anchorOf(quoted.id!))?.focus()}
              className={cn(DOOR, 'max-w-full truncate text-small')}
            >
              {t('asks.channel.inReply', { text: firstWords(quoted.text) })}
            </button>
          </p>
        )
        : <p className="m-0 text-small text-ink-faint">{t('asks.channel.inReplyGone')}</p>)}
      {/* A word wider than the column, a pasted URL, breaks inside it rather than scrolling the region sideways. */}
      <p className="m-0 whitespace-pre-wrap wrap-anywhere text-body text-ink">{item.text}</p>
      {item.receipts && item.receipts.length > 0 && (
        <ul aria-label={t('asks.channel.receipts')} className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-soft">
          {item.receipts.map((receipt) => (
            <ReceiptRow
              key={receipt.repository}
              receipt={receipt}
              onForward={onForward && forwardable(receipt) ? (answered) => onForward(receipt.repository, answered) : undefined}
            />
          ))}
        </ul>
      )}
      {onReply && (
        <div>
          <Button variant="ghost" className="px-1.5 py-0 text-small" onClick={onReply}>{t('asks.channel.reply')}</Button>
        </div>
      )}
    </li>
  );
}

/**
 * What a message reached in one repository it addresses (COWORK1d, the design §7, §8.4), worded from the shell's state, and *Send to
 * its session* where it may be sent on: pressed once, it says it was sent, or why not by the shell's code.
 */
function ReceiptRow({ receipt, onForward }: { receipt: ChannelReceipt; onForward?: (answered: ChannelAnswered) => void }) {
  const { t } = useTranslation();
  const [pending, setPending] = useState(false);
  const [sent, setSent] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const { repository, state } = receipt;
  const words = state === 'not-reached'
    ? t('asks.channel.notReached', { repository })
    : RECEIPT_STATES.has(state) ? t(`asks.channel.receipt.${state}`, { repository }) : t('asks.channel.receipt.other', { repository, state });
  const refused = refusal === null
    ? null
    : FORWARD_REFUSALS.has(refusal) ? t(`asks.channel.forwardRefused.${refusal}`, { repository }) : t('asks.channel.forwardRefused.other', { code: refusal });

  const forward = () => {
    setPending(true);
    setRefusal(null);
    onForward?.({
      done: () => { setPending(false); setSent(true); },
      refused: (code) => { setPending(false); setRefusal(code); },
    });
  };

  return (
    <li className="grid min-w-0 gap-0.5">
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5">
        <span className="min-w-0 wrap-anywhere">{words}</span>
        {onForward && !sent && (
          <Button variant="ghost" className="px-1.5 py-0 text-small" disabled={pending} onClick={forward}>{t('asks.channel.forward')}</Button>
        )}
        {sent && <span role="status" className="text-ink-faint">{t('asks.channel.forwarded')}</span>}
      </span>
      {refused && <Refused sentence={refused} />}
    </li>
  );
}

/**
 * **A session's Channel** (SESSONE2c, the co-working design §2.4): the ask's channel kept to its line of work, under its head,
 * folded to its title and a count. Open, it lists what was said to everyone, to its repository and by it, with the lines of the
 * quests that concern it, and the composer with *To* starting at its repository. The whole channel is the ask's page's.
 *
 * @remarks
 * A molecule over `ChannelSection`: it counts nothing unread and moves no mark, which the ask's page owns, since this reads a
 * part of the channel and a mark is for all of it.
 */
export function ChannelPanel({ channel, only, onPost, onForward, onOpenQuest, onOpenAsk, repositories }: {
  channel: Channel | null;
  only: { entry: string; repository: string };
  onPost: (post: ChannelPost, answered: ChannelAnswered) => void;
  /** *Send to its session* (COWORK1d), as the ask's page offers it. */
  onForward?: ChannelForward;
  onOpenQuest?: (quest: string) => void;
  /** Open the ask's own page, where the whole channel is. Absent where there is nowhere to open it. */
  onOpenAsk?: () => void;
  repositories?: readonly string[];
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const count = channel ? forEntry(channel.items, only).length : 0;
  return (
    <section aria-label={t('asks.channel.title')} className="min-w-0">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-faint hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} className="mt-1 shrink-0" />
        <span className="min-w-0 truncate font-semibold">{count > 0 ? `${t('asks.channel.title')} · ${count}` : t('asks.channel.title')}</span>
      </button>
      {open && (
        <div className="mt-2 grid gap-2">
          <ChannelSection
            channel={channel} only={only} onPost={onPost} onForward={onForward} onOpenQuest={onOpenQuest} repositories={repositories}
          />
          {onOpenAsk && <div><Button variant="ghost" className="px-1.5 py-0 text-small" onClick={onOpenAsk}>{t('asks.channel.panel.open')}</Button></div>}
        </div>
      )}
    </section>
  );
}
