import type { HelperAskRow } from '../waits/HelperAsks';
import type { Turn } from './conversation';

/**
 * What heads a helper's answer drawn as that agent's block where no card holds it: the person
 * asked the helper from the session's page, so no call in the conversation asked it, or the page does not hold the call that did.
 */
export type AnswerHead = {
  /** The agent as the person calls it: the one the ask named, else the one it started on; null where neither is known. */
  agent: string | null;
  question: string;
  /** When the question came. */
  asked: string;
  /** The person asked it, from the session's page; else the session's agent did. */
  byPerson: boolean;
};

/**
 * The heads of the helpers' answers `turns` took that no card folds (`folded`, each taking turn's ask key): each joined to the ask it
 * answers by the helper's session its words carry, never by their text. A partner's answer and an answer whose ask
 * the page does not hold get none, and stay under their sender's words.
 */
export function answerHeadsOf(
  turns: readonly Turn[], asks: readonly HelperAskRow[], folded: Readonly<Record<string, string>>,
  nameOf: (agent: string) => string = (agent) => agent,
): Record<string, AnswerHead> {
  const heads: Record<string, AnswerHead> = {};
  for (const turn of turns) {
    const ask = turn.ask;
    if (!ask?.helper || ask.from !== 'helper' || ask.key in folded) continue;
    const row = asks.find((each) => each.helper === ask.helper);
    if (!row) continue;
    const named = row.agent ?? row.startedOn;
    heads[ask.key] = { agent: named ? nameOf(named) : null, question: row.question, asked: row.at, byPerson: row.askedBy === 'person' };
  }
  return heads;
}
