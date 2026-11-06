import { useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { type LineSupervision, NO_SUPERVISION, readSupervision } from '../../work/head/lineSupervision';
import { call } from '../call';

/**
 * A line's counts and the acts the person's rules made on it (CONTEXTDRIVE1h): the supervisor read `SESSION_SUPERVISOR` answers beside
 * its decisions, for the head's chip and *Details*. This machine's own record only; nothing in a browser, and nothing from a driver before
 * the counts, which answers neither field.
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
