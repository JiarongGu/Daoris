import { createContext, useContext, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { clockOf } from '../../locales/format';
import { cn } from '../../lib/cn';
import { Chip, Icon } from '../../design/ui';
import { CHANNEL_CLAIM_OPENING } from '../channel';
import type { Ask, Receipts } from './conversation';
import type { Author } from './sender';
import type { AnswerHead } from './answerHeads';
import { type ChannelHome, type ChannelPoster, NO_CHANNEL_HOME } from './channelHome';
import { HandedAccount } from './HandedAccount';
import { ChannelDoor, SenderChip } from './SenderChip';

/** What each author's words are headed with: never *You* for words that are not the person's. */
export const AUTHOR_KEYS = {
  daoris: 'work.conversation.daoris',
  supervisor: 'work.conversation.supervisor',
  unattributed: 'work.conversation.unattributed',
  helper: 'work.conversation.helper',
  partner: 'work.conversation.partner',
} as const satisfies Record<Author, string>;

/** The home of the channel messages the conversation's turns took: who posted each, and the door to the channel. */
export const ChannelHomes = createContext<ChannelHome>(NO_CHANNEL_HOME);

/** Another session's claim, named by its poster where the page holds the channel's message, else as a claim in Daoris's words. */
const claimLabel = (t: TFunction, poster: ChannelPoster | undefined) => {
  // An agent outside Daoris is its own, named as outside, never as the person or a session.
  if (poster?.outside) {
    const who = [poster.repository, poster.agent].filter(Boolean).join(' · ');
    return who ? t('work.conversation.claimFromOutside', { who }) : t('work.conversation.claimFromOutsideAgent');
  }
  if (!poster?.repository) return t('work.conversation.claim');
  return poster.agent
    ? t('work.conversation.claimFrom', { repository: poster.repository, agent: poster.agent })
    : t('work.conversation.claimFromRepository', { repository: poster.repository });
};

/**
 * What the person asked, or the target the driver composed — the latter folded, since it is long, with what it is composed
 * of beneath it. Another agent's findings handed as the session's next turn
 * are a block from that agent, open, under Daoris's fixed words that name it and its maker, never the person's. Daoris's own words,
 * a reopen told or a step's go-on, are named as Daoris's by their mark, open, and never as the person's.
 *
 * Each is headed by its sender chip: who, by the one rule, and when. A helper's answer no card folds is that agent's block,
 * headed by the agent and the question it answers. Another session's claim names its poster where the page holds
 * the channel's message, and where the page can open the channel, its home, the words that carried it fold here.
 */
export function AskView({ ask, answer }: { ask: Ask; answer?: AnswerHead }) {
  const { t, i18n } = useTranslation();
  const { posterOf, onOpen } = useContext(ChannelHomes);
  const findings = Boolean(ask.opinion);
  // Words by an author's mark: Daoris's, the supervisor's on the owner's behalf, or not attributed.
  const marked = Boolean(ask.from);
  // Another session's claim on the ask's channel: Daoris's words around it, kept as the driver's own, short enough to
  // read whole and never the target Daoris composed. Told by its channel mark, and a record from before by its opening.
  const claim = ask.origin === 'target' && !findings && !marked
    && (Boolean(ask.channel) || (ask.text ?? '').startsWith(CHANNEL_CLAIM_OPENING));
  const target = ask.origin === 'target' && !claim && !marked;
  // A channel message's home is the channel: a door to it, and another session's claim folded where the door is.
  const home = ask.channel && onOpen ? ask.channel : undefined;
  const folds = target || (claim && Boolean(home));
  const [open, setOpen] = useState(!folds || findings);
  const helperAnswer = ask.from === 'helper' ? answer : undefined;

  const label = findings ? t('work.conversation.opinion')
    : helperAnswer ? (helperAnswer.agent
      ? t('work.conversation.asked.answer', { agent: helperAnswer.agent })
      : t('work.conversation.asked.answerAny'))
      : ask.from ? t(AUTHOR_KEYS[ask.from])
        : claim ? claimLabel(t, ask.channel ? posterOf?.(ask.channel) : undefined)
          // A new part's first prompt is Daoris's brief of the line it continues, folded as a target is.
          : target && ask.continues ? t('work.conversation.brief')
            : target ? t('work.conversation.target') : t('work.conversation.you');

  return (
    <div
      data-word={ask.key}
      className={cn(
        'rounded-card border border-line border-l-[3px] bg-raised px-3 py-2',
        findings || marked ? 'border-l-line-strong' : 'border-l-accent',
      )}
    >
      <SenderChip label={label} at={ask.at}>
        {home && <ChannelDoor message={home} onOpen={onOpen!} />}
        {folds && (
          <button
            type="button"
            aria-expanded={open}
            onClick={() => setOpen((was) => !was)}
            className="border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
          >
            {open ? t('work.conversation.hide') : t('work.conversation.show')}
          </button>
        )}
      </SenderChip>
      {/* The question the helper's answer answers, whose and when, quiet above the answer. */}
      {helperAnswer && (
        <p data-answers className="m-0 mt-0.5 line-clamp-2 wrap-anywhere text-meta text-ink-soft">
          {t(helperAnswer.byPerson ? 'work.conversation.answers.yours' : 'work.conversation.answers.session', {
            time: clockOf(helperAnswer.asked, new Date(), i18n.language),
          })}
          {' '}
          <q>{helperAnswer.question}</q>
        </p>
      )}
      {/* A word wider than the column, a pasted URL, breaks inside it rather than scrolling the region sideways. */}
      {ask.choice
        ? <ChoiceWords choice={ask.choice} text={ask.text ?? ''} className="text-ink" />
        : ask.text && (
          <p className={cn('m-0 mt-0.5 whitespace-pre-wrap wrap-anywhere text-body text-ink', !open && 'line-clamp-2')}>{ask.text}</p>
        )}
      <Attached files={ask.files} />
      {ask.cutIn && <p className="m-0 mt-0.5 text-meta text-ink-faint">{t('work.conversation.cutIn')}</p>}
      {ask.receipts && <ReceiptsLine receipts={ask.receipts} />}
      {/* What the target is composed of, section by section, beside the words it composes. */}
      {target && !findings && <HandedAccount account={ask.account} />}
    </div>
  );
}

/**
 * What the door observed of these words: *sent 14:02 · taken 14:03 · turn ended 14:09*,
 * each only where it is observed, folded under them as a quiet line. A turn's end is what Daoris saw, never an answer.
 */
function ReceiptsLine({ receipts }: { receipts: Receipts }) {
  const { t, i18n } = useTranslation();
  const seen = (['sent', 'taken', 'ended'] as const)
    .filter((receipt) => receipts[receipt])
    .map((receipt) => `${t(`work.receipt.${receipt}`)} ${clockOf(receipts[receipt]!, new Date(), i18n.language)}`);
  if (seen.length === 0) return null;
  return <p data-receipts className="m-0 mt-1 text-meta text-ink-faint">{seen.join(' · ')}</p>;
}

/** What the person attached: names, as the record keeps them. */
export function Attached({ files }: { files?: string[] }) {
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
 * Words the person pressed as an answer of its park's question: *Chose* and the answer as its question
 * offered it, then the person's own words, so a press reads as a choice and never as words they typed. The words begin with the label,
 * as the page composes a press; where a record says otherwise they are drawn whole beneath the mark.
 */
export function ChoiceWords({ choice, text, className }: { choice: string; text: string; className: string }) {
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
