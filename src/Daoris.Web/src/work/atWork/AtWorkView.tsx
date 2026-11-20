import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { SOUGHT } from '../waits/WaitAnchor';
import { ago } from '../../locales/format';
import { whoOf } from '../../asks/ChannelSection';
import type { ChannelItem } from '../channel';
import { Dot, SectionTitle } from '../../design/ui';
import { type Consideration, sittingBecause, sittingSentence } from '../../data/signals';
import type { AtWorkA, AtWorkQuest } from './types';
import { partStateKey, waitingOnLine, waitLine } from './words';

/**
 * The newest messages of an ask's channel, the lines the ask page's section reads: each placed on the line of work that posted it.
 * A session's own line has no such block: its messages are rows of its conversation (`work/exchanges/`).
 */
export type AtWorkChannel = {
  words: readonly ChannelItem[];
  /** *All messages*: opens the panel that holds the channel. */
  onOpen?: () => void;
};

export function Failed({ why, onRetry }: { why: string; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <p role="alert" className="m-0 flex flex-wrap items-baseline gap-2 p-3 text-small text-ink-soft">
      {t('work.atWork.failed', { why })}
      {onRetry && <button type="button" className="text-accent-ink underline" onClick={onRetry}>{t('work.atWork.retry')}</button>}
    </p>
  );
}

/**
 * The head's *Waiting on*: what the line waits on, read from its requests that hold, a quest as *Waiting on #q7 in core (docs lane)*
 * and a co-worker by whom and where it stands, with a door that scrolls the timeline to the first request's row. Nothing where the
 * line waits on nothing it asked.
 */
export function WaitingOnLine({ answer, nameOf, onOpen }: {
  answer: AtWorkA; nameOf?: (agent: string) => string; onOpen?: (exchange: string) => void;
}) {
  const { t } = useTranslation();
  const line = waitingOnLine(t, answer.exchanges ?? [], nameOf);
  const first = (answer.exchanges ?? []).find((exchange) => exchange.holds);
  if (!line || !first) return null;
  return (
    <p data-waiting-on className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 text-small text-ink">
      <span className="min-w-0 wrap-anywhere">{line}</span>
      {onOpen && <button type="button" className="text-accent-ink underline" onClick={() => onOpen(first.id)}>{t('work.atWork.toRow')}</button>}
    </p>
  );
}

/** One message of the channel's last words, as the channel draws it: who, to which part alone where it is to one, when, and the words. */
export function Word({ item }: { item: ChannelItem }) {
  const { t } = useTranslation();
  const first = (item.text ?? '').trim();
  return (
    <li className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small text-ink-soft" title={first}>
      <span className="font-semibold text-ink">{whoOf(t, item)}</span>
      {item.part && <span className="text-caption text-ink-faint">{t('asks.channel.toPart', { part: item.part })}</span>}
      <span className="text-caption text-ink-faint">{ago(item.at)}</span>
      <span className="line-clamp-2 min-w-0 flex-1 basis-40 [overflow-wrap:anywhere]">{first}</span>
    </li>
  );
}

/**
 * The quests an ask's lines of work asked: one button each, opening the quest, with who took it and what the taker waits on. An
 * untaken quest says why beside it from the driver's last look, none before the first. A chain's step is set in under the quest
 * before it; the one a press sought is outlined (`quest:<id>`).
 */
export function QuestRows({ quests, more, considered, onOpenQuest, sought }: {
  quests: readonly AtWorkQuest[] | undefined; more: number; considered: readonly Consideration[]; onOpenQuest?: (quest: string) => void;
  sought?: string | null;
}) {
  const { t } = useTranslation();
  if (!quests?.length) return null;
  return (
    <section className="grid min-w-0 gap-1">
      <SectionTitle level={3}>{t('work.atWork.group.quests')}</SectionTitle>
      <ol className="m-0 grid list-none gap-0.5 p-0">
        {quests.map((quest) => (
          <li
            key={quest.id}
            data-exchange={`quest:${quest.id}`}
            tabIndex={-1}
            className={sought === `quest:${quest.id}` ? SOUGHT : 'outline-none'}
            style={quest.step ? { paddingInlineStart: '12px' } : undefined}
          >
            <button
              type="button"
              onClick={() => onOpenQuest?.(quest.id)}
              className="flex w-full min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 rounded-control border-l-2 border-l-transparent px-2 py-1 text-left text-small text-ink-soft hover:bg-accent-soft/50"
            >
              <span className="font-mono text-caption text-ink-faint">#{quest.id}</span>
              <span className="min-w-0 text-ink [overflow-wrap:anywhere]">{quest.title}</span>
              {quest.of && <span className="text-caption text-ink-faint">{t('work.atWork.quest.of', { repository: quest.of })}</span>}
              {quest.lanes?.length ? (
                <span className="text-caption text-ink-faint">{t('work.atWork.quest.lanes', { lanes: quest.lanes.join(', '), count: quest.lanes.length })}</span>
              ) : null}
              <QuestTaker t={t} quest={quest} considered={considered} />
            </button>
          </li>
        ))}
      </ol>
      {more > 0 && <p className="m-0 text-caption text-ink-faint">{t('work.atWork.quest.more', { count: more })}</p>}
    </section>
  );
}

/** Who took a quest and how it stands: its own line under the quest's name. */
function QuestTaker({ t, quest, considered }: { t: TFunction; quest: AtWorkQuest; considered: readonly Consideration[] }) {
  const { taker } = quest;
  const sitting = taker?.untaken ? sittingBecause(considered, quest.id) : null;
  return (
    <span className="flex basis-full flex-wrap items-baseline gap-x-2 gap-y-0.5 text-caption text-ink-faint">
      {taker?.session !== undefined && (
        <>
          {taker.agent && <span className="font-mono">{taker.agent}</span>}
          {taker.state && <Dot tone={taker.state === 'working' ? 'live' : 'ended'} label={t(partStateKey(taker.state))} />}
        </>
      )}
      {taker?.conversation !== undefined && <span>{t('work.atWork.quest.takenBy', { conversation: taker.conversation })}</span>}
      {taker?.elsewhere && <span>{t('work.atWork.quest.elsewhere')}</span>}
      {taker?.unrecorded && <span>{t('work.atWork.quest.unrecorded')}</span>}
      {taker?.untaken && (
        <span>{sitting ? t('work.atWork.quest.untakenWhy', { why: sittingSentence(sitting) }) : t('work.atWork.quest.untaken')}</span>
      )}
      {!taker && quest.status === 'Declined' && <span>{t('work.atWork.quest.declined')}</span>}
      {quest.waits && <Dot tone="idle" label={waitLine(t, quest.waits)} className="whitespace-normal" />}
      {quest.step && <span>{t('work.atWork.quest.nextStep', { quest: quest.step })}</span>}
    </span>
  );
}
