import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { type LineSupervision, NO_SUPERVISION, readSupervision } from '../../work/head/lineSupervision';
import { call } from '../call';

/**
 * A line's counts, the acts the person's rules made on it and the supervisor's course decisions (CONTEXTDRIVE1h, CONTEXTDRIVE1j): the
 * supervisor read `SESSION_SUPERVISOR` answers beside its decisions, for the head's chip, its card and *Details*. This machine's own
 * record only; nothing in a browser, and nothing from a driver before the counts, which answers none of the fields.
 */
export const useSessionSupervision = (id: string | null | undefined, enabled = true): LineSupervision => {
  const { isAvailable } = useShenora();
  const answer = useQuery({
    queryKey: keys.sessionSupervision(id ?? ''),
    queryFn: async () => readSupervision(await call('SESSION_SUPERVISOR', { id: id ?? '' })),
    enabled: isAvailable && enabled && Boolean(id),
    staleTime: 15_000,
    refetchOnWindowFocus: false,
  });
  return answer.data ?? NO_SUPERVISION;
};

/** How *Overrule* went: taken back (`state` `overruled`), or not, with the state the decision had already reached. */
export type OverruleAnswered = { done: boolean; state: string | null };

/**
 * The person's *Overrule* of a supervisor's decision (`SUPERVISOR_OVERRULE`, the person's alone): a course decision held for its window,
 * which keeps the point theirs. The line's supervision is read again whichever way it went, so the card goes with the decision.
 */
export const useOverruleSupervisor = (session: string) => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (decision: string): Promise<OverruleAnswered> => {
      const said = (await call('SUPERVISOR_OVERRULE', { id: session, decision })) as { done?: unknown; state?: unknown } | null;
      return { done: said?.done === true, state: typeof said?.state === 'string' ? said.state : null };
    },
    onSettled: () => client.invalidateQueries({ queryKey: keys.sessionSupervision(session) }),
  });
};
