import { useMutation, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { ReadChange, RepositoryAcross, WriteChange } from '../../settings/repository/Across';
import { call } from '../call';
import type { DriverState } from '../driver';

// Reading and writing across repositories: how each repository's checkout stands, and the
// screen's half of `daoris place across`, over the same `driver.json`.

/**
 * Every repository here, whether agents outside it read its checkout and what said so, and what its sessions
 * may also write into. Desktop-only: the workspaces are this machine's registry, and the file is its own.
 * `enabled` holds it back until a surface shows it: a repository's Setup asks only while it is open.
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
 * Set a repository's reading or a workspace's, or clear it with no `read` — the file `daoris place across …
 * read` edits. The answer is the driver's state, which reaches every window by its push; every repository's answer moves
 * with it, under the driver's key, which the route publishes and the state hub asks again in every window.
 */
export const useSetReadAcross = () =>
  useMutation({ mutationFn: (change: ReadChange) => call('SET_READ_ACROSS', change) as Promise<DriverState> });

/** Declare a relationship, or take it back — `daoris place across <repository> write-to <other> [--clear]`. */
export const useSetWriteAcross = () =>
  useMutation({ mutationFn: (change: WriteChange) => call('SET_WRITE_ACROSS', change) as Promise<DriverState> });
