import { useMutation, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { TogetherView } from '../../work/head/TogetherDoor';
import { call } from '../call';

// LINESTOGETHER1d (the review-flow design §4): the operation bringing sessions together a session is in, and the head's presses on it.
// Each press declares what it moved, so the state hub asks the sessions again in every window (D169); the hooks ask nothing themselves.

/** What a press on the operation came to: done, or the sentence it was refused with. */
export type TogetherAnswer = { done: boolean; refused: string | null };

const answerOf = (answer: { operation?: unknown; done?: unknown; refused?: unknown } | null): TogetherAnswer => ({
  done: answer?.done === true || typeof answer?.operation === 'string',
  refused: typeof answer?.refused === 'string' ? answer.refused : null,
});

/** The operation `session` is in, head or member, under the sessions' key so a press's publish asks it again; null where none. */
export const useTogether = (session: string | null | undefined): TogetherView | null => {
  const { isAvailable } = useShenora();
  const answer = useQuery({
    queryKey: ['sessions', 'together', session ?? ''],
    queryFn: () => call<{ operation?: TogetherView | null }>('TOGETHER', { session }),
    enabled: isAvailable && Boolean(session),
    staleTime: 10_000,
  });
  return answer.data?.operation ?? null;
};

/** *Bring them together…*: the ticked sessions asked into the head. */
export const useBringTogether = () => useMutation({
  mutationKey: ['TOGETHER_PRESS'],
  mutationFn: async ({ session, members }: { session: string; members: readonly string[] }) =>
    answerOf(await call<{ operation?: unknown; refused?: unknown } | null>('TOGETHER_PRESS', { session, members: [...members] })),
});

/** *Ask again* after a failed check, or *Let them go* before the landing, on the head. */
export const useTogetherAct = () => useMutation({
  mutationFn: async ({ session, act }: { session: string; act: 'ask-again' | 'let-go' }) =>
    answerOf(await call<{ done?: unknown; refused?: unknown } | null>(act === 'ask-again' ? 'TOGETHER_ASK_AGAIN' : 'TOGETHER_LET_GO', { session })),
});
