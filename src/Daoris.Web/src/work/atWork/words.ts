import type { TFunction } from 'i18next';
import type { AtWorkExchange, AtWorkWait } from './types';

/** A part's wait in the rail's words (the strip places it; a quest row words it alike): *waits on #q, asked of core*, *awaiting helper*. */
export function waitLine(t: TFunction, wait: AtWorkWait): string {
  if (wait.helper) return t('work.shown.awaitingHelper');
  const quest = wait.quest ?? '';
  return wait.of ? t('work.rail.awaitsOf', { quest, repository: wait.of }) : t('work.rail.awaits', { quest });
}

/** What the *Waiting on* line says a quest it waits on is: *#q7 in core (docs lane)*. */
export function waitWhat(t: TFunction, wait: AtWorkWait): string {
  const values = { quest: wait.quest ?? '', repository: wait.of ?? '', lanes: wait.lanes?.join(', ') ?? '', count: wait.lanes?.length ?? 0 };
  if (wait.lanes?.length) return wait.of ? t('work.atWork.what.questOfLanes', values) : t('work.atWork.what.questLanes', values);
  return wait.of ? t('work.atWork.what.questOf', values) : t('work.atWork.what.quest', values);
}

/** The kind of request an exchange is, by its id's prefix: `helper`, `piece`, `quest`, `hand-over` or `opinion`. */
export const exchangeSource = (exchange: AtWorkExchange): string => exchange.id.slice(0, Math.max(0, exchange.id.indexOf(':')));

/** The words a request names before anyone took it up: no agent's name. */
const UNNAMED = new Set(['helper', 'partner', 'successor', 'reviewer']);

/**
 * Whom or what a request asked, the *Waiting on* line's and the co-workers' one wording: a quest as the
 * strip words its wait, a co-worker by its agent and as what. `nameOf` names an agent as the person calls it.
 */
export function exchangeWhat(t: TFunction, exchange: AtWorkExchange, nameOf: (agent: string) => string = (agent) => agent): string {
  const source = exchangeSource(exchange);
  if (source === 'quest') return waitWhat(t, { quest: exchange.id.slice('quest:'.length), of: exchange.to, lanes: exchange.lanes });
  const named = exchange.agent ?? (UNNAMED.has(exchange.to) ? undefined : exchange.to);
  const agent = named ? nameOf(named) : undefined;
  if (source === 'piece') return agent ? t('work.atWork.coworker.partner', { agent }) : t('work.atWork.coworker.anyPartner');
  if (source === 'hand-over') return agent ? t('work.atWork.coworker.successor', { agent }) : t('work.atWork.coworker.anySuccessor');
  if (source === 'opinion') return agent ? t('work.atWork.coworker.reviewer', { agent }) : t('work.atWork.coworker.anyReviewer');
  return agent ? t('work.atWork.coworker.helper', { agent }) : t('work.atWork.coworker.anyHelper');
}

const OUTCOMES = ['asked', 'approved', 'answered', 'taken-in', 'refused', 'done', 'declined', 'ended'] as const;

/**
 * Where a request stands: its protocol's own word, and what Daoris saw of its delivery where that says more; never a
 * turn's end read as an answer. A word this page does not know is said as recorded.
 */
export function exchangeState(t: TFunction, exchange: AtWorkExchange): string {
  const outcome = (OUTCOMES as readonly string[]).includes(exchange.outcome)
    ? t(`work.atWork.outcome.${exchange.outcome}`)
    : t('work.atWork.outcome.other', { word: exchange.outcome });
  const delivery = exchange.delivery === 'uncertain' ? t('work.atWork.delivery.uncertain')
    : exchange.delivery === 'not-reached' && exchange.outcome !== 'refused' ? t('work.atWork.delivery.notReached')
      : exchange.delivery === 'elsewhere' ? t('work.atWork.delivery.elsewhere') : null;
  return delivery ? `${outcome} · ${delivery}` : outcome;
}

/** What the line waits on, in one line: *At work*'s and the head's alike. Null where nothing it asked holds it. */
export function waitingOnLine(t: TFunction, exchanges: readonly AtWorkExchange[], nameOf?: (agent: string) => string): string | null {
  const holds = exchanges.filter((exchange) => exchange.holds);
  if (holds.length === 0) return null;
  const what = holds.map((exchange) => (exchangeSource(exchange) === 'quest'
    ? exchangeWhat(t, exchange, nameOf)
    : `${exchangeWhat(t, exchange, nameOf)} · ${exchangeState(t, exchange)}`));
  return t('work.atWork.waitingOn', { what: what.join('; ') });
}

/** A part's state: its record's own, worded as a session's is, never the strip's shown word. */
export const partStateKey = (state: string): string => `sessionState.${state}`;
