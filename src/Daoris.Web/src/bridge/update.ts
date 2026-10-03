import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getBridge, useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from '../queries';
import type { UpdateMode, UpdateState } from '../update/UpdateBanner';

// An install's update (UPDATE1, D139): where it stands, and the person's word on it, over `DAORIS.UPDATE`. The terminal's
// door is `daoris-driver update`; both write the same request under the home (D50).

/**
 * Where the install's update stands — what the banner renders, and the last swap (`last`) Settings' row says after the
 * banner's *Dismiss* (UPDATE1d). Desktop only: the update is the install's, and a browser over the service has no bridge to
 * ask (D47 §4). Kept current by the shell's `UPDATE_STATE`, each change once.
 */
export function useUpdateState() {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  useShenoraEvent<UpdateState>('DAORIS', 'UPDATE_STATE', (state) => {
    if (state) client.setQueryData(keys.update, state);
  });
  return useQuery({
    queryKey: keys.update,
    queryFn: () => getBridge().invoke<UpdateState>('DAORIS.UPDATE', 'STATE'),
    enabled: isAvailable,
    // The event keeps it current; a look now and then covers an event missed while the page was away.
    refetchInterval: 30_000,
  });
}

/** The person's word: *Update when idle*, *Update now* or *Not now*. Answers the state after it, which the cache takes. */
export function useSayUpdate() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (mode: UpdateMode) => getBridge().invoke<UpdateState>('DAORIS.UPDATE', 'SET', { payload: { mode } }),
    onSuccess: (state) => client.setQueryData(keys.update, state),
  });
}

/** Put away an outcome already said: the banner's *Dismiss* after an update installed or rolled back. */
export function useDismissUpdate() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => getBridge().invoke<UpdateState>('DAORIS.UPDATE', 'DISMISS'),
    onSuccess: (state) => client.setQueryData(keys.update, state),
  });
}
