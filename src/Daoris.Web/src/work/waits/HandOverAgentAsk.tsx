import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button } from '../../design/ui';
import { type Answered, Refused } from '../InlineConfirm';

/**
 * A session's agent's own ask to hand it over (MIXAGENT1c, D163 point 3), as `SESSION_HAND_OVER_ASK` answers it: the agent's note as it
 * wrote it, when, and where the driver's look left it, its state (`waiting` on the person's word, `approved`, `taken`, `refused`; null
 * before the look read it), a refusal's code and the agent it goes to. A state a newer driver writes is read as written.
 */
export type HandOverAskRow = {
  id: string;
  note: string;
  at: string;
  state: string | null;
  code: string | null;
  /** The next agent in the person's order the look named; null before it read the ask. */
  agent: string | null;
};

/** The person's yes or no to the ask, told how it ended (UXFIX2): `done` once the driver's file holds it, `refused` with why not. */
export type HandOverAnswer = (ask: string, answered: Answered) => void;

/** What the frame hands the page for a session's agent's ask: the ask, the agents' names, and the presses it can make. */
export type HandOverAskWait = {
  ask: HandOverAskRow;
  nameOf: (agent: string) => string;
  onApprove?: HandOverAnswer;
  onDecline?: HandOverAnswer;
  busy?: boolean;
};

/** The codes of a refused word the page says itself (`HandOverWordWhy`, and `cannot` where the runner refused the take). */
const REFUSED = new Set(['gone', 'past', 'unread', 'approved', 'cannot']);

/** A refused yes or no in the page's words where it knows the code, else the driver's sentence as it came. */
export const handOverRefused = (t: TFunction, why: string | null, message: string): string =>
  why && REFUSED.has(why) ? t(`work.handOverAsk.refused.${why}`) : message || why || '';

/** Whether the driver's look holds the ask waiting on the person's word: the one state the yes and the no answer. */
export const waitsOnHandOverWord = (row: Pick<HandOverAskRow, 'state'>) => row.state === 'waiting';

/** Whether the ask is still on its way: not yet taken or refused, which the timeline's notes say where they settled. */
export const handOverAskOpen = (row: Pick<HandOverAskRow, 'state'>) => row.state === null || row.state === 'waiting' || row.state === 'approved';

/**
 * **Its agent asks to hand the session over** (MIXAGENT1c, D163 point 3; the mixed-agents design §2, §5): the agent's note, shown as
 * it wrote it and said to be its claim, then where the driver's look left the ask in the person's words. Where the look holds it
 * waiting on their word, *Hand over to {agent}*, their yes, and *Not now*, their no. Taken or refused, it is said in the timeline's
 * note and the session's `from` link, so nothing is drawn here.
 *
 * @remarks
 * **Props only** (components §2): the frame reads the ask and holds the presses. Without `onApprove` no yes is offered, without
 * `onDecline` no no: a browser, a story. Each waits in place and a refusal is said beside it (UXFIX2b3b), as a helper's ask's is.
 */
export function HandOverAgentAsk({ ask, nameOf, onApprove, onDecline, busy = false }: HandOverAskWait) {
  const { t } = useTranslation();
  const [pending, setPending] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const alive = useRef(true);
  const turn = useRef(0);
  const refusalId = useId();
  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; };
  }, []);
  if (!handOverAskOpen(ask)) return null;

  const agent = ask.agent ? nameOf(ask.agent) : null;
  const waiting = waitsOnHandOverWord(ask) && agent !== null;
  const yes = waiting && onApprove;
  const no = waiting && onDecline;
  const state = ask.state === 'approved' && agent
    ? t('work.handOverAsk.approved', { agent })
    : waiting
      ? t('work.handOverAsk.waits', { agent })
      : t('work.handOverAsk.unread');
  // The yes and the no share one pending and one refusal line: they answer the same ask.
  const press = (answer: HandOverAnswer) => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    answer(ask.id, {
      done: () => { if (mine === turn.current && alive.current) setPending(false); },
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };

  return (
    <div data-hand-over-ask={ask.id} className="grid gap-1 rounded-control border border-line bg-raised px-3 py-2">
      <p className="m-0 text-small text-ink-soft">{t('work.handOverAsk.claim')}</p>
      <p className="m-0 min-w-0 whitespace-pre-wrap break-words text-body">{ask.note}</p>
      <p className="m-0 text-small text-ink-soft">{state}</p>
      {(yes || no) && (
        <div className="mt-1 flex flex-wrap items-center gap-2">
          {yes && (
            <Button disabled={busy || pending} aria-describedby={refusal ? refusalId : undefined} onClick={() => press(onApprove!)}>
              {t('work.handOverAsk.approve', { agent })}
            </Button>
          )}
          {no && (
            <Button variant="ghost" disabled={busy || pending} aria-describedby={refusal ? refusalId : undefined} onClick={() => press(onDecline!)}>
              {t('work.handOverAsk.decline')}
            </Button>
          )}
          {/* Drawn from the first, so it is a live region before it speaks (UXFIX2c). */}
          <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
            {pending ? t('work.confirm.pending') : null}
          </span>
          {refusal && <Refused id={refusalId} sentence={refusal} />}
        </div>
      )}
    </div>
  );
}
