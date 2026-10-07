import { useTranslation } from 'react-i18next';
import { useAbandonWork, usePauseWork, useResumeWork } from '../shell';
import { sittingSentence } from '../signals';
import { failure, type Notify } from '../ui';
import {
  type AbandonAnswer, abandonNotice, type PauseAnswer, pauseNotices, type ResumeAnswer, resumeNotices, scopeName,
  type WorkNotice, type WorkTarget,
} from './pausing';

/**
 * **The one owner of a pause, a resume and an abandon** (PAUSE1e, D132 §7.1): the ask's page, the quest's page and a
 * session's header each call this, so each act has one implementation whichever door pressed it, as `sessionActs.ts` is
 * the one owner of a session's acts.
 *
 * @remarks
 * **An organism**: it holds the bridge's hooks, so the pages and the header, which each press it, hold none. What each act
 * says is `pausing.ts`'s rule, said here in the catalogue's words with the scope's own name: a pause in one line, and a
 * failure for each session it meant to stop and could not; a resume, and what still holds a quest of it by its hold's own
 * sentence, translated by its verdict (UX5 U27) and worded from the facts the host hands beside it (CARRY2d), the driver's
 * sentence where an older host hands none; an abandon as how many of the listed pieces went. A refusal is the catalogue's
 * sentence for its code.
 */
export function useWorkActs({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const pause = usePauseWork();
  const resume = useResumeWork();
  const abandon = useAbandonWork();

  const tell = (notices: readonly WorkNotice[], target: WorkTarget) => {
    for (const notice of notices) {
      // The hold whole, its facts with it (CARRY2d): the one rule that words a sitting quest words a resume's hold.
      const why = notice.hold ? { why: sittingSentence({ ...notice.hold, repository: '' }) } : {};
      notify(t(notice.key, { ...notice.values, ...why, what: scopeName(t, target) }), notice.tone);
    }
  };

  return {
    /** *Pause…*'s press, or its ask's second press; `done` once the driver has answered. */
    pause: (target: WorkTarget, done?: (answer: PauseAnswer) => void) => pause.mutate(target, {
      onSuccess: (answer) => {
        tell(pauseNotices(answer), target);
        done?.(answer);
      },
      onError: failure(notify),
    }),
    /** *Resume*'s press: it never asks, since it starts nothing itself. */
    resume: (target: WorkTarget, done?: (answer: ResumeAnswer) => void) => resume.mutate(target, {
      onSuccess: (answer) => {
        tell(resumeNotices(answer), target);
        done?.(answer);
      },
      onError: failure(notify),
    }),
    /** *Abandon…*'s second press: exactly the pieces its first press listed, with the person's reason. */
    abandon: (target: WorkTarget, reason: string, pieces: readonly string[], done?: (answer: AbandonAnswer) => void) =>
      abandon.mutate({ target, reason, pieces }, {
        onSuccess: (answer) => {
          tell([abandonNotice(answer)], target);
          done?.(answer);
        },
        onError: failure(notify),
      }),
    pausing: pause.isPending,
    resuming: resume.isPending,
    abandoning: abandon.isPending,
    /** Any of the three on its way: the presses wait for it. */
    busy: pause.isPending || resume.isPending || abandon.isPending,
  };
}
