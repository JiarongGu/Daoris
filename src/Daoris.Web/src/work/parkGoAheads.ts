import type { Ask, GoAhead } from '../api';
import { list } from '../format';
import { neverSentence } from './say';

// A park's go-aheads (KNOWUSE1a2, D135 §2): which go-aheads a parked session asked, read from its quest's ask, and what one
// press that answers one of them said. Pure, so every park a person could attend is an argument.

/**
 * The go-aheads a session asked on its quest's ask, in the ask's order: each one whose requests name the session, whether
 * it asked it first or asked it again (KNOWUSE1a joins a later request to the earlier). None where its quest was asked by
 * no ask, the ask is not among those read, or it asked none.
 */
export function goAheadsAsked(asks: readonly Ask[] | undefined, ask: string | null, session: string): GoAhead[] {
  if (!ask || !asks) return [];
  const held = asks.find((row) => row.id === ask);
  return (held?.goAheads ?? []).filter((goAhead) => goAhead.asked.some((request) => request.session === session));
}

/**
 * What the press said, as `SESSION_GO_AHEAD` answered: what became of the park, whether it still waits on the person
 * (GOAHEAD2b: a go-ahead it asked still open, or its own answer), and the go-ahead's sentence.
 */
export type GoAheadAnswer = { sent: boolean; reaches: string | null; why: string | null; waits: boolean; message: string };

type Translate = (key: string, options?: Record<string, unknown>) => string;

/**
 * The toast for a go-ahead answered on a park's page, in the page's own words: the yes or the no and that the same session
 * goes on with it, where the park took its answer; the yes or the no and which go-ahead it still waits on, where the door
 * kept it parked (GOAHEAD2b), named from `asked`, the go-aheads the park asked as the page held them at the press, or that
 * it waits on the person's own answer where none of them is open; the go-ahead alone where the park went on before the
 * press; and beside it, why the park was not answered, by the box's code or because nothing waited on the person any more.
 */
export function goAheadToast(
  t: Translate, number: number, approved: boolean, answer: GoAheadAnswer,
  { quest, asked = [] }: { quest?: string | null; asked?: readonly GoAhead[] },
): string {
  if (answer.sent && answer.reaches === 'resume') {
    return t(approved ? 'work.awaiting.goAheadApproved' : 'work.awaiting.goAheadRefused', { number });
  }
  if (answer.waits) {
    // Whether the park goes on was the service's to decide; which of its go-aheads are still open is only named here.
    const open = asked.filter((goAhead) => goAhead.state === 'asked' && goAhead.number !== number).map((goAhead) => `#${goAhead.number}`);
    return t('work.composer.twoSentences', {
      first: t(approved ? 'work.awaiting.goAheadWaits.approved' : 'work.awaiting.goAheadWaits.refused', { number }),
      second: open.length > 0
        ? t('work.awaiting.goAheadWaits.for', { count: open.length, open: list(open) })
        : t('work.awaiting.goAheadWaits.onYou'),
    });
  }
  const answered = t('work.awaiting.goAheadOnly', { number });
  if (answer.sent) return answered;
  const why = answer.why ? neverSentence(t, answer.why, { quest }) : t('work.awaiting.goAheadNotWaiting');
  return t('work.composer.twoSentences', { first: answered, second: why });
}
