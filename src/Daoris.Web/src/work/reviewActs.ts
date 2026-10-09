import { useShenora } from '@shenora/react';
import { useTranslation } from 'react-i18next';
import type { Quest, QuestAction, SetUpRef } from '../api';
import { sentence } from '../format';
import { usePublishSetUpStep, useReviewVerdict } from '../queries';
import { useNudge, useSay, useShowReviewAgain } from '../shell';
import { failure, type Notify } from '../ui';
import type { Answered } from './InlineConfirm';
import { type ReviewState, skipOn } from './review';
import type { ReviewGateActs } from './ReviewGate';

/** What one gate's presses act on: the set-up step, by its record where the frame holds one, and the chain's work. */
export type ReviewTarget = {
  state: ReviewState;
  /** The set-up step's id: what a verdict and *Show it again* name. */
  step?: string | null;
  /** The set-up step's record, whose set-up a *not yet* finds its session in. */
  record?: Quest | null;
  /** The chain's work quest: where *Set it up* follows, and a skip goes once the step was reviewed. */
  work?: string | null;
  /** The step's own page, where the gate is drawn elsewhere. */
  open?: () => void;
};

/**
 * **The one owner of a review's presses** (REVIEWENV1g; D154 point 8, the review environment design §3.3–§3.4, §3.6): the
 * session's head, a set-up step's page and *What needs you* each press this, so a verdict has one implementation whichever
 * door pressed it, as `historyActs.ts` is the one owner of a clear.
 *
 * @remarks
 * **An organism**: it holds the service's and the bridge's hooks, so the gate and the rows, which press them, hold none.
 *
 * **The verdict goes to the service's door** (`api.reviewQuest`, through the person's seam), naming the set-up the view drew. A
 * refusal, a stale verdict's above all, is the host's sentence, said inside the gate that was pressed (UXFIX2). Once kept, the
 * driver is asked to look now, which lets the tab go and lands work *Accept automatically* holds.
 *
 * **A *not yet*'s words go to the set-up step's session as its next turn** (design §3.4, D137), through the same door the
 * terminal's `sessions say` takes (`SESSION_INPUT`), once the verdict is kept. A browser has no shell to reach a session, so
 * there *Not yet…* is not offered; the person's own set-up has no session, and its words are kept on the quest alone.
 */
export function useReviewActs({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const { isAvailable } = useShenora();
  const verdict = useReviewVerdict();
  const setUpStep = usePublishSetUpStep();
  const say = useSay();
  const showAgain = useShowReviewAgain();
  const nudge = useNudge();

  /** A press told back to the gate that made it: the host's sentence once kept, its refusal inside the gate. */
  const told = (answered: Answered, then?: (result: QuestAction) => void) => ({
    onSuccess: (result: QuestAction) => {
      notify(result.message);
      answered.done();
      nudge();
      then?.(result);
    },
    onError: (error: unknown) => answered.refused(sentence(error)),
  });

  /** The set-up a *not yet* answered, found by the reference the view drew. */
  const sessionOf = (record: Quest | null | undefined, ref: SetUpRef) =>
    record?.setUps?.find((setUp) => setUp.machine === ref.machine && setUp.sequence === ref.sequence)?.session ?? null;

  const actsFor = ({ state, step = null, record = null, work = null, open }: ReviewTarget): ReviewGateActs => {
    const skipTo = skipOn(state, step, work);
    return {
      ...(step ? {
        reviewed: (setUp: SetUpRef, answered: Answered) =>
          verdict.mutate({ id: step, verdict: 'reviewed', setUp }, told(answered)),
      } : {}),
      ...(step && isAvailable ? {
        notYet: (setUp: SetUpRef, words: string, answered: Answered) =>
          verdict.mutate({ id: step, verdict: 'not-yet', words, setUp }, told(answered, () => {
            const session = sessionOf(record, setUp);
            if (!session) return;
            say.mutate({ id: session, text: words }, {
              onSuccess: () => notify(t('review.said.notYet', { session })),
              onError: () => notify(t('review.said.notYetUnheard', { session }), 'error'),
            });
          })),
      } : {}),
      ...(skipTo ? {
        skip: (words: string | null, answered: Answered) =>
          verdict.mutate({ id: skipTo, verdict: 'skipped', words }, told(answered)),
      } : {}),
      ...(work ? {
        setUp: (environment: string, answered: Answered) => setUpStep.mutate({ id: work, environment }, told(answered)),
      } : {}),
      ...(step && isAvailable ? {
        showAgain: () => showAgain.mutate({ quest: step }, {
          onSuccess: () => notify(t('browser.review.shownAgain', { quest: step })),
          onError: failure(notify),
        }),
      } : {}),
      ...(open ? { open } : {}),
    };
  };

  return {
    /** The presses for one gate. */
    actsFor,
    /** A press on its way: the gate's presses wait for it. */
    busy: verdict.isPending || setUpStep.isPending,
  };
}
