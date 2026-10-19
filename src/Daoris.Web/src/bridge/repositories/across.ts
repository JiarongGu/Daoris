import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { ReadChange, RepositoryAcross, WriteChange } from '../../settings/repository/Across';
import { call } from '../call';
import { type DriverState, writeAnswered } from '../driver';

// Reading and writing across repositories (READ1, D107): how each repository's checkout stands, and the
// screen's half of `daoris driver across`, over the same `driver.json`.

/**
 * Every repository here, whether agents outside it read its checkout and what said so, and what its sessions
 * may also write into. Desktop-only: the workspaces are this machine's registry, and the file is its own.
 * `enabled` holds it back until a surface shows it: a repository's Setup asks only while it is open (UX6f).
 */
export const useAcross = ({ enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.across,
    queryFn: () => call<{ repositories: RepositoryAcross[] }>('ACROSS'),
    enabled: isAvailable && enabled,
  });
};

/**
 * Set a repository's reading or a workspace's, or clear it with no `read` — the file `daoris driver across …
 * read` edits (D50). The answer is the driver's state, written where it is read; every repository's answer moves with it,
 * under the driver's key, which the route publishes and the state hub asks again in every window (STATEHUB1h, D169).
 */
export const useSetReadAcross = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: ReadChange) => call<DriverState>('SET_READ_ACROSS', change),
    onSuccess: (state) => writeAnswered(client, state),
  });
};

/** Declare a relationship, or take it back — `daoris driver across <repository> write-to <other> [--clear]`. */
export const useSetWriteAcross = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: WriteChange) => call<DriverState>('SET_WRITE_ACROSS', change),
    onSuccess: (state) => writeAnswered(client, state),
  });
};
