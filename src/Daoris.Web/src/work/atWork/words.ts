import type { TFunction } from 'i18next';
import type { AtWorkAsker, AtWorkChildState, AtWorkExchange, AtWorkHint, AtWorkJoined, AtWorkPart, AtWorkWait } from './types';

/** The hint's words, by the standing the driver's judge answered. */
export const HINT_KEY: Record<AtWorkHint, string> = {
  'not-driven': 'work.atWork.hint.notDriven',
  'no-lanes': 'work.atWork.hint.noLanes',
  'one-at-a-time': 'work.atWork.hint.oneAtATime',
};

/** The guide's lanes section, which the hint links to. */
export const LANES_GUIDE = 'https://github.com/JiarongGu/Daoris/blob/main/docs/guide/README.md#lanes';

/** A part's wait in the rail's words (the strip places it; the view words it alike): *waits on #q, asked of core*, *awaiting helper*. */
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

/** The kind of request an exchange is, by its id's prefix (COMMS1i): `helper`, `piece`, `quest` or `hand-over`. */
export const exchangeSource = (exchange: AtWorkExchange): string => exchange.id.slice(0, Math.max(0, exchange.id.indexOf(':')));

/** The words a request names before anyone took it up: no agent's name. */
const UNNAMED = new Set(['helper', 'partner', 'successor']);

/**
 * Whom or what a request asked (COMMS1i, the comms design §7), the *Waiting on* line's and the co-workers' one wording: a quest as the
 * strip words its wait, a co-worker by its agent and as what. `nameOf` names an agent as the person calls it.
 */
export function exchangeWhat(t: TFunction, exchange: AtWorkExchange, nameOf: (agent: string) => string = (agent) => agent): string {
  const source = exchangeSource(exchange);
  if (source === 'quest') return waitWhat(t, { quest: exchange.id.slice('quest:'.length), of: exchange.to, lanes: exchange.lanes });
  const named = exchange.agent ?? (UNNAMED.has(exchange.to) ? undefined : exchange.to);
  const agent = named ? nameOf(named) : undefined;
  if (source === 'piece') return agent ? t('work.atWork.coworker.partner', { agent }) : t('work.atWork.coworker.anyPartner');
  if (source === 'hand-over') return agent ? t('work.atWork.coworker.successor', { agent }) : t('work.atWork.coworker.anySuccessor');
  return agent ? t('work.atWork.coworker.helper', { agent }) : t('work.atWork.coworker.anyHelper');
}

const OUTCOMES = ['asked', 'approved', 'answered', 'taken-in', 'refused', 'done', 'declined'] as const;

/**
 * Where a request stands (COMMS1i): its protocol's own word, and what Daoris saw of its delivery where that says more; never a
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

/** What the line waits on, in one line (COMMS1i): *At work*'s and the head's alike. Null where nothing it asked holds it. */
export function waitingOnLine(t: TFunction, exchanges: readonly AtWorkExchange[], nameOf?: (agent: string) => string): string | null {
  const holds = exchanges.filter((exchange) => exchange.holds);
  if (holds.length === 0) return null;
  const what = holds.map((exchange) => (exchangeSource(exchange) === 'quest'
    ? exchangeWhat(t, exchange, nameOf)
    : `${exchangeWhat(t, exchange, nameOf)} · ${exchangeState(t, exchange)}`));
  return t('work.atWork.waitingOn', { what: what.join('; ') });
}

/**
 * How a part joined, in the design's words (§3.4), by what its record and quest say; the strip's own words where it only began
 * (retried, resumed, carried on, handed over, moved). Null for a part that simply opened.
 */
export function joinedLine(t: TFunction, joined: AtWorkJoined): string | null {
  const part = (n: number | undefined) => (n === undefined ? t('work.atWork.routed.anotherSession') : t('work.parts.number', { n }));
  const quest = joined.quest ?? '';
  if (joined.took) return t('work.atWork.routed.took', { quest });
  if (joined.ask) return joined.began === 'asked' ? t('work.atWork.routed.askQuest', { quest }) : t('work.atWork.routed.ask', { ask: joined.ask });
  if (joined.parent) return t('work.atWork.quest.nextStep', { quest: joined.parent });
  if (joined.lanes?.length && joined.byPart !== undefined) {
    return t('work.atWork.routed.lane', { lane: joined.lanes.join(', '), by: part(joined.byPart) });
  }
  if (joined.byPart !== undefined && joined.of) return t('work.atWork.routed.repository', { repository: joined.of, by: part(joined.byPart) });
  const after = joined.after === undefined ? '' : t('work.parts.number', { n: joined.after });
  switch (joined.began) {
    case 'opened': return null;
    case 'asked': return joined.askedBy ? t('work.parts.began.asked', { repository: joined.askedBy }) : t('work.parts.began.askedUnnamed');
    case 'retried': return t('work.parts.began.retried', { after });
    case 'resumed': return t('work.parts.began.resumed', { after });
    case 'carried-on': return joined.after === undefined ? t('work.parts.began.carriedOnAlone') : t('work.parts.began.carriedOn', { after });
    case 'handed-over': return t('work.parts.began.handedOver', { after });
    case 'moved': return t('work.parts.began.moved', { after });
    default: return t('work.parts.began.recorded', { began: joined.began });
  }
}

/**
 * What asked the attended part's quest, in words (the design §3.1): the publishing session by its place in the strip, the step before,
 * the ask, or the person.
 */
export function askerLine(t: TFunction, asker: AtWorkAsker, parts: readonly AtWorkPart[]): string {
  const { quest, by } = asker;
  if (by.session !== undefined) {
    const number = parts.find((part) => part.key === by.session)?.number;
    return t('work.atWork.askedBy.session', {
      quest, by: number === undefined ? t('work.atWork.askedBy.anotherSession') : t('work.parts.number', { n: number }),
    });
  }
  if (by.step !== undefined) return t('work.atWork.askedBy.step', { quest, step: by.step });
  if (by.ask !== undefined) return t('work.atWork.askedBy.ask', { quest, ask: by.ask });
  return t('work.atWork.askedBy.person', { quest });
}

/**
 * The one key each child state draws (the cowork view design §5.4, *each state's words*): an end the wire said reads the panel's own
 * word, and a state Daoris's reading gave reads the view's; a key missing from a catalogue fails the mapping's test.
 */
export const CHILD_STATE_KEY: Readonly<Record<AtWorkChildState, string>> = {
  working: 'sessionState.working',
  unsaid: 'work.atWork.child.unsaid',
  completed: 'work.panel.stream.state.completed',
  failed: 'work.panel.stream.state.failed',
  cancelled: 'work.panel.stream.state.cancelled',
  disconnected: 'work.panel.stream.state.disconnected',
  stopped: 'work.panel.stream.state.stopped',
  finished: 'work.atWork.child.finished',
  ended: 'work.atWork.child.ended',
  lost: 'work.atWork.child.lost',
  agentEnded: 'work.atWork.child.agentEnded',
  unrecorded: 'work.atWork.child.unrecorded',
  uncertain: 'work.atWork.child.uncertain',
};

/** A part's state: its record's own, worded as a session's is, never the strip's shown word. */
export const partStateKey = (state: string): string => `sessionState.${state}`;

/** The record states a part can hold, the nine and `idle`. */
export const PART_STATES = [
  'queued', 'starting', 'working', 'awaiting-person', 'completed', 'declined', 'stood-down', 'failed', 'stopped', 'idle',
] as const;

/** States whose row says the child's last line rather than an end's moment. */
export const LAST_SEEN: ReadonlySet<AtWorkChildState> = new Set(['uncertain', 'unrecorded', 'agentEnded']);

/** How a state's word is toned: live where it works, warn where it was lost, neutral where nothing is proven. */
export function childTone(state: AtWorkChildState): 'live' | 'warn' | 'failed' | 'ended' | 'neutral' {
  switch (state) {
    case 'working':
      return 'live';
    case 'lost':
      return 'warn';
    case 'failed':
      return 'failed';
    case 'unsaid':
    case 'uncertain':
    case 'unrecorded':
    case 'agentEnded':
      return 'neutral';
    default:
      return 'ended';
  }
}
