import { useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { TraceAnswer, TraceKind } from '../work/trace';
import { call } from './call';

// How a session or a quest came to be (TRACE1b, D143, D50): the screen's door to `daoris-driver trace`, the same read over
// the same stores. Shell-only: it reads this machine's records of its sessions, its rules files and its landings (D47 §4).

/**
 * The chain from a session or a quest back to its ask, asked only once `id` names one: a page folds the section by default and
 * names its id when the person opens it, since the read takes every session record and quest. Null asks nothing.
 */
export const useTrace = (kind: TraceKind, id: string | null) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.trace(kind, id ?? ''),
    queryFn: () => call<TraceAnswer>('TRACE', { kind, id }),
    enabled: isAvailable && Boolean(id),
  });
  return { available: isAvailable, query };
};
