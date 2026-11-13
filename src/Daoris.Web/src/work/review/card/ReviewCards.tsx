import { useEffect, useState } from 'react';
import type { RunReviewAnswer } from '../run/runReviewWire';
import { useOverruleSupervisor } from '../../../bridge/sessions/supervision';
import { useRunNodeWord, useRunReview, useSessionRunReview } from '../../../bridge/shell';
import { sentence } from '../../../locales/format';
import { useViewedMarks } from '../run/viewedMarks';
import { ReviewCard } from './ReviewCard';

/** Each attempt's answer as its card first drew it, the newest replaced by the answer now: an earlier card is never redrawn. */
export function cardsOf(kept: readonly RunReviewAnswer[], now: RunReviewAnswer | undefined): RunReviewAnswer[] {
  const attempt = now?.record?.attempt;
  if (!now || attempt === undefined) return [...kept];
  const earlier = kept.filter((each) => (each.record?.attempt ?? 0) < attempt);
  return [...earlier, now];
}

/**
 * **The session's Accept cards**, under its conversation: the card of the run its quest serves, one per attempt the reader saw, the
 * newest live and each earlier one superseded by the next. A session no run governs, or a run with no review, draws none.
 */
export function ReviewCards({ session, onOpenOutputs }: { session: string; onOpenOutputs?: (run: string, attempt: number | null) => void }) {
  const bySession = useSessionRunReview(session);
  const run = bySession.data && !bySession.data.refused ? bySession.data.run : null;
  const review = useRunReview(run, 0);
  const word = useRunNodeWord();
  const viewed = useViewedMarks();
  const overrule = useOverruleSupervisor(review.data?.supervisor?.session ?? '');
  const [overruleSaid, setOverruleSaid] = useState<string | null>(null);
  const [kept, setKept] = useState<RunReviewAnswer[]>([]);
  useEffect(() => setKept([]), [run]);
  const now = review.data && !review.data.refused ? review.data : undefined;
  useEffect(() => { if (now?.record) setKept((was) => cardsOf(was, now)); }, [now]);

  if (!run || !now?.record) return null;
  const cards = cardsOf(kept, now);
  return (
    <div className="mt-3 grid gap-2">
      {cards.map((answer, at) => (
        <ReviewCard
          key={answer.record!.attempt}
          answer={answer}
          viewed={viewed}
          supersededBy={at < cards.length - 1 ? cards[at + 1]!.record!.attempt : null}
          onWord={(ask) => word.mutateAsync(ask)}
          onOpenOutputs={onOpenOutputs ? (attempt) => onOpenOutputs(run, attempt) : undefined}
          onOverrule={answer.supervisor ? (decision) => overrule.mutate(decision, {
            onSuccess: (answered) => setOverruleSaid(answered.done ? null : answered.state),
            onError: (error) => setOverruleSaid(sentence(error)),
          }) : undefined}
          overruling={overrule.isPending}
          overruleSaid={overruleSaid}
        />
      ))}
    </div>
  );
}
