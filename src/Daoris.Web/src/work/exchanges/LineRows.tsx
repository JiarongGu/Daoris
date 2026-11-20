import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { type ChannelAnswered, type ChannelForward, MessageRow } from '../../asks/ChannelSection';
import { Icon } from '../../design/ui';
import type { ChannelPost } from '../channel';
import { dockKeyOfExchange } from '../waits/dock';
import { ChannelReply } from './ChannelReply';
import { actorOf, cameBack, coworkerName, type LineRow as Row } from './rows';

export type LineRowHome = {
  /** The part attended: a request that judged its work reads as this session's. */
  attended: string;
  /** The keys of the dock's lines, so a request waiting on the person leads to its own. */
  docked: readonly string[];
  onToDock: (key: string) => void;
  /** Open the session that took a request up (a helper's, a piece's partner's, a reviewer's), or the one that published its quest. */
  onOpenSession?: (session: string) => void;
  onOpenQuest?: (quest: string) => void;
  /** The agent's name as the person calls it. */
  nameOf?: (agent: string) => string;
  /** A reviewer's findings by its exchange's id: its verdict, said beside what it judged. */
  findings?: Readonly<Record<string, number>>;
  /** The channel's presses: a message answered in place, and *Send to its session*; absent, its messages are read only. */
  channel?: { onPost: (post: ChannelPost, answered: ChannelAnswered) => void; onForward?: ChannelForward };
};

const PRESS = 'shrink-0 cursor-pointer border-0 bg-transparent p-0 text-small text-accent underline-offset-2 hover:underline';

/** The protocol's word for where it stands, in the reader's language; one the page has no word for is said as recorded. */
const outcomeOf = (t: TFunction, word: string): string =>
  t(`work.atWork.outcome.${word}`, { defaultValue: t('work.atWork.outcome.other', { word }) });

/**
 * **One of a line's exchanges or channel messages as a row**: a request at its asking or at its coming back, its actor named, with
 * the session that took it up a press away (a piece opens its own session); an opinion's verdict beside what it judged; a request
 * that waits on the person leads to its line in the dock and offers no press of its own; a quest its session published, at its
 * filing or at its close; the answered question its session resumed after; or a channel message naming its sender, answered in
 * place and sent on to its session where it may be.
 *
 * Presentational: the row (`lineRowsOf`), the dock's keys and the presses arrive as props; its one state is whether its message is
 * being answered.
 */
export function LineRow({ row, attended, docked, onToDock, onOpenSession, onOpenQuest, nameOf = (agent) => agent, findings, channel }: LineRowHome & {
  row: Row;
}) {
  const { t } = useTranslation();
  const [answering, setAnswering] = useState(false);
  const line = 'm-0 flex min-w-0 items-center gap-2 text-small text-ink-soft';
  switch (row.kind) {
    case 'exchange': {
      const { exchange } = row;
      const who = coworkerName(t, exchange, nameOf);
      const dock = exchange.holds ? dockKeyOfExchange(exchange.id) : null;
      const waitsOnYou = dock !== null && docked.includes(dock);
      const verdict = row.side === 'back' && actorOf(exchange) === 'reviewer' ? [
        findings?.[exchange.id] !== undefined ? t('work.exchange.findings', { count: findings[exchange.id] }) : null,
        exchange.to === attended ? t('work.exchange.judged.here') : t('work.exchange.judged.part'),
      ] : [];
      const said = [
        row.side === 'asked' ? t('work.exchange.asked', { who }) : t('work.exchange.back', { who, outcome: outcomeOf(t, exchange.outcome) }),
        ...verdict,
      ].filter(Boolean).join(' · ');
      // Its taker's session a press away on its newest row: its coming back, else its asking.
      const opens = !waitsOnYou && exchange.session && onOpenSession && (row.side === 'back' || !cameBack(exchange));
      return (
        <div data-exchange-row={exchange.id} data-side={row.side} className={line}>
          <Icon name="atWork" size={14} className="shrink-0 text-ink-faint" />
          <span className="min-w-0 truncate">{said}</span>
          {waitsOnYou && <button type="button" onClick={() => onToDock(dock)} className={PRESS}>{t('work.exchange.toDock')}</button>}
          {opens && <button type="button" onClick={() => onOpenSession!(exchange.session!)} className={PRESS}>{t('work.exchange.open')}</button>}
        </div>
      );
    }
    case 'quest': {
      const { quest } = row;
      const said = [
        row.side === 'asked'
          ? t('work.exchange.quest.asked', { quest: quest.id, repository: quest.to })
          : t('work.exchange.quest.back', { quest: quest.id, outcome: outcomeOf(t, quest.status === 'Done' ? 'done' : 'declined') }),
        row.question ? t('work.exchange.quest.question') : null,
      ].filter(Boolean).join(' · ');
      return (
        <div data-exchange-row={`quest:${quest.id}`} data-side={row.side} className={line}>
          <Icon name="quests" size={14} className="shrink-0 text-ink-faint" />
          <span className="min-w-0 truncate">{said}</span>
          {onOpenQuest && <button type="button" onClick={() => onOpenQuest(quest.id)} className={PRESS}>{t('work.exchange.quest.open')}</button>}
        </div>
      );
    }
    case 'resumed':
      return (
        <div data-exchange-row={row.key} className={line}>
          <Icon name="quests" size={14} className="shrink-0 text-ink-faint" />
          <span className="min-w-0 truncate">{t('work.exchange.resumed', { quest: row.quest.id })}</span>
          {onOpenQuest && <button type="button" onClick={() => onOpenQuest(row.quest.id)} className={PRESS}>{t('work.exchange.quest.open')}</button>}
        </div>
      );
    case 'message': {
      const { item } = row;
      const id = item.id!;
      return (
        <div data-channel-row={id} className="grid min-w-0 gap-1.5">
          <ul className="m-0 grid list-none p-0">
            <MessageRow
              item={item}
              anchor={`line-message-${id}`}
              anchorOf={(of) => `line-message-${of}`}
              quoted={row.quoted}
              isNew={false}
              onReply={channel ? () => setAnswering((was) => !was) : undefined}
              onForward={channel?.onForward ? (repository, answered) => channel.onForward!(id, repository, answered) : undefined}
            />
          </ul>
          {channel && answering && (
            <ChannelReply to={item.person ? null : item.repository ?? null} re={id} onPost={channel.onPost} onClose={() => setAnswering(false)} />
          )}
        </div>
      );
    }
    default:
      return (
        <div data-exchange-row={row.key} className={line}>
          <Icon name="quests" size={14} className="shrink-0 text-ink-faint" />
          <span className="min-w-0 truncate">{t('work.exchange.askedBy')}</span>
          {onOpenSession && <button type="button" onClick={() => onOpenSession(row.session)} className={PRESS}>{t('work.exchange.open')}</button>}
        </div>
      );
  }
}
