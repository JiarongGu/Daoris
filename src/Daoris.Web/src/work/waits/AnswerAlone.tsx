import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button } from '../../design/ui';
import { cn } from '../../lib/cn';
import { type Answered, Refused } from '../InlineConfirm';
import { SOUGHT } from './WaitAnchor';

/**
 * A third answer in a row with nobody between, which the answers-alone bound holds for the person,
 * as `ANSWER_WAITS` answers it: the session whose line it would take on, the agent that answered
 * by its adapter (null where the driver named none), the answers' ids on its record, and since when it is held.
 */
export type AnswerWait = { session: string; agent: string | null; words: string[]; since: string; rule?: AnswerRule | null };

/** The person's rule that holds the answer: its place, the threshold, and what the count stands at; none for a hold marked before the rules. */
export type AnswerRule = { place: string; at: number; n: number; count: string; do: string };

/** A rule's place in the person's words: this machine as *This Daoris*, any other as its own name (`repository:daoris` is `daoris`). */
export const rulePlaceName = (t: TFunction, place: string): string => (place === 'machine' ? t('settings.supervisor.machine') : place.slice(place.indexOf(':') + 1));

/** The one sentence the head, the foot and *What needs you* say of a held answer's rule: what the count stood at and where the rule waits. */
export const answerRuleLine = (t: TFunction, rule: AnswerRule): string =>
  t('work.wait.answerRule', { n: rule.n, at: rule.at, place: rulePlaceName(t, rule.place) });

/** The person's answer as the driver took it (`SESSION_ANSWER_ALONE`): whether it stands, and where not, its code and sentence. */
export type AnswerAloneAnswered = { answered: boolean; why: string | null; message: string };

/** The person's answer to one held answer: *Go on with this answer* (`goOn`) or *Not now*, and how it ended. */
export type AnswerAloneAnswer = (wait: AnswerWait, goOn: boolean, answered: Answered) => void;

/**
 * What the frame hands the head and the foot's strip for the answers the bound holds: every wait the shell
 * read, of which the session's own is drawn, the agents' names, the answer's one door and its pending. Absent where nothing reads
 * them (a browser, a teammate's record).
 */
export type AnswerWaits = {
  waits: readonly AnswerWait[];
  /** An agent's name as the roster says it; its id where the roster does not hold it. */
  nameOf?: (agent: string) => string;
  onAnswer?: AnswerAloneAnswer;
  /** An answer is on its way, pressed in either place: every press waits for it. */
  busy?: boolean;
  /** *Change the rule…*: opens the place whose rule held the answer; absent, the door is not drawn. */
  onChangeRule?: (place: string) => void;
};

/** The wait a session's line holds for the person, or null: one per session, since the driver marks a session's answers at once. */
export const answerFor = (waits: readonly AnswerWait[], session: string) => waits.find((wait) => wait.session === session) ?? null;

/** The codes an answer is refused by (`AnswersAloneWord`), each said in the catalogue's words; a later code by the driver's own sentence. */
const REFUSALS = new Set(['unheld']);

/** An answer's refusal in the catalogue's words, by its code; a code a later driver adds is said in the driver's own sentence. */
export const answerAloneSaid = (t: TFunction, why: string | null, message: string) =>
  (why && REFUSALS.has(why) ? t(`work.wait.answer.refused.${why}`) : message || why || '');

/** What the held answer would do, said by whose it is: the agent by its name, or another agent where the driver named none. */
export const answerAloneLine = (t: TFunction, wait: AnswerWait, nameOf?: (agent: string) => string) =>
  (wait.agent ? t('work.wait.answerAlone', { agent: nameOf?.(wait.agent) ?? wait.agent }) : t('work.wait.answerAloneSomeone'));

/**
 * **A third answer alone, held for the person**: a line of work goes on
 * by answers alone (a helper's, a second opinion's findings) at most twice in a row, and the third waits here, saying whose answer it
 * is and what it would do. *Go on with this answer* lets it go on, as the person's word, so the next two go on alone again; *Not
 * now* keeps it for them, and it goes on only beside words of theirs.
 *
 * @remarks
 * **Props only**: the frame holds the answer and its pending, and hands the head and the strip the same. Without
 * `onAnswer` no press is drawn: a browser or a teammate's record still says what waits. A refusal of the answer is said beside it
 * Its words never say "waiting on you", which a parked session's are (`person-waits`).
 */
export function AnswerAlone({ wait, nameOf, onAnswer, busy = false, marked = false, onChangeRule }: {
  wait: AnswerWait;
  nameOf?: (agent: string) => string;
  onAnswer?: AnswerAloneAnswer;
  busy?: boolean;
  onChangeRule?: (place: string) => void;
  /** A press led here: the wait is outlined. */
  marked?: boolean;
}) {
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
  // Both presses share one pending and one refusal line: they answer the same wait.
  const press = (goOn: boolean) => {
    const mine = ++turn.current;
    setRefusal(null);
    setPending(true);
    onAnswer!(wait, goOn, {
      done: () => { if (mine === turn.current && alive.current) setPending(false); },
      refused: (sentence) => {
        if (mine !== turn.current || !alive.current) return;
        setPending(false);
        setRefusal(sentence);
      },
    });
  };

  return (
    <div
      tabIndex={-1}
      data-wait="answerAlone"
      className={cn('grid gap-1 rounded-control border border-line bg-raised px-3 py-2', marked ? SOUGHT : 'outline-none')}
    >
      <p className="m-0 text-body">{answerAloneLine(t, wait, nameOf)}</p>
      {wait.rule && <p className="m-0 text-small text-ink-soft">{answerRuleLine(t, wait.rule)}</p>}
      {wait.rule && onChangeRule && (
        <div>
          <Button variant="ghost" onClick={() => onChangeRule(wait.rule!.place)}>{t('work.wait.answer.changeRule')}</Button>
        </div>
      )}
      {onAnswer && (
        <div className="mt-1 flex flex-wrap items-center gap-2">
          <Button variant="primary" disabled={busy || pending} aria-describedby={refusal ? refusalId : undefined} onClick={() => press(true)}>
            {t('work.wait.goOnAnswer')}
          </Button>
          <Button variant="ghost" disabled={busy || pending} aria-describedby={refusal ? refusalId : undefined} onClick={() => press(false)}>
            {t('work.wait.answer.notNow')}
          </Button>
          {/* Drawn from the first, so it is a live region before it speaks. */}
          <span role="status" aria-live="polite" className={pending ? 'self-center text-small text-ink-faint' : 'sr-only'}>
            {pending ? t('work.confirm.pending') : null}
          </span>
          {refusal && <Refused id={refusalId} sentence={refusal} />}
        </div>
      )}
    </div>
  );
}
