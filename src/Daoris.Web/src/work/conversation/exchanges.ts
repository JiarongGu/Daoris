import type { HelperAskRow } from '../waits/HelperAsks';
import { helperKey } from '../waits/anchors';
import { type AnswerHead, answerHeadsOf } from './answerHeads';
import type { Turn } from './conversation';

/**
 * One request the session's agent made, drawn whole once as its card at the call that asked it: an
 * ask for a helper, joined to its `helper_ask` call as the wait's anchor joins it. Its answer folds into the card where
 * the page holds the turn that took it, and that turn holds a line that opens the card.
 */
export type ExchangeCard = {
  /** The request's id as the exchanges' read names it: `helper:<ask>`. */
  id: string;
  /** The ask's own id, which the wait's anchor names. */
  ask: string;
  /** The key of the call that asked it: where the card is drawn. */
  call: string;
  question: string;
  /** The agent's name as the person calls it: the one the ask named, else the one it started on; null where neither is known yet. */
  agent: string | null;
  /** Where the request stands, the helper's own word (`HelperAskRow.state`), or null before the driver's look read it. */
  state: string | null;
  at: string;
  /** The session that answers it, once started: the card's *Open its conversation*. */
  helper: string | null;
  /** The answer, where the page holds the turn that took it: that turn's ask, its words and when. */
  answer?: { key: string; text: string; at: string };
};

/**
 * The cards a conversation draws, by the key of the call each is at, and each answer's turn by its ask's key, naming its card's call;
 * and, by the taking turn's ask key, the head of each helper's answer no card folds.
 */
export type Exchanges = {
  cards: Readonly<Record<string, ExchangeCard>>;
  takenAt: Readonly<Record<string, string>>;
  answers: Readonly<Record<string, AnswerHead>>;
};

export const NO_EXCHANGES: Exchanges = { cards: {}, takenAt: {}, answers: {} };

/**
 * The exchange cards of `turns`: each of `asks` whose call the page holds, with its answer where the turn that took it is
 * held. An answer is joined by the helper's session its words carry (`Ask.helper`), never by its text; an ask whose
 * call is not held draws no card, and its answer stays drawn whole where it is taken.
 */
export function exchangesOf(
  turns: readonly Turn[], asks: readonly HelperAskRow[], nameOf: (agent: string) => string = (agent) => agent,
): Exchanges {
  const cards: Record<string, ExchangeCard> = {};
  const takenAt: Record<string, string> = {};
  for (const ask of asks) {
    if (ask.askedBy !== 'agent') continue;
    const call = helperKey(turns, ask);
    if (!call || cards[call]) continue;
    const named = ask.agent ?? ask.startedOn;
    const card: ExchangeCard = {
      id: `helper:${ask.id}`, ask: ask.id, call, question: ask.question, agent: named ? nameOf(named) : null, state: ask.state, at: ask.at,
      helper: ask.helper,
    };
    const taking = ask.helper ? turns.find((turn) => turn.ask?.from === 'helper' && turn.ask.helper === ask.helper) : undefined;
    if (taking?.ask) {
      card.answer = { key: taking.ask.key, text: taking.ask.text, at: taking.ask.at };
      takenAt[taking.ask.key] = call;
    }
    cards[call] = card;
  }
  return { cards, takenAt, answers: answerHeadsOf(turns, asks, takenAt, nameOf) };
}
