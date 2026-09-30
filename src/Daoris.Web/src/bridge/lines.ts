import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { LineChange, RepositoryLine } from '../settings/Lines';
import type { LandingChange, RepositoryLanding } from '../settings/Landings';
import type { LandedBranch, SweepBranch } from '../settings/Sweep';
import { call } from './call';
import type { DriverState } from './driver';

// Each repository's line, how work lands in it, and the clean-up of the branches sessions left (MOD3):
// the Workspace domain's machine half (WSR1, WSR2, WSR3).

/**
 * Every repository's line here and what said so (WSR2). Desktop-only: the guess is read off a
 * checkout, and only this machine holds one.
 */
export const useLines = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.lines,
    queryFn: () => call<{ lines: RepositoryLine[]; landings?: RepositoryLanding[] }>('LINES'),
    enabled: isAvailable,
  });
};

/**
 * Set how work lands in a repository or a workspace, or clear it with no form — the file `daoris driver
 * landing` edits (D50). Every repository's answer moves with it, so that is asked again.
 */
export const useSetLanding = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: LandingChange) => call<DriverState>('SET_LANDING', change),
    onSuccess: (state) => {
      client.setQueryData(keys.driver, state);
      void client.invalidateQueries({ queryKey: keys.lines });
      void client.invalidateQueries({ queryKey: keys.allLandings });
    },
  });
};

/**
 * Every session branch here with what it holds (WSR3, D88) — the clean-up's list — and every branch a
 * landing made, judged against the line (WSR5). Desktop-only: it is read off this machine's checkouts.
 * A shell older than WSR5 answers no `landed`.
 */
export const useSweepPlan = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.sweep,
    queryFn: () => call<{ branches: SweepBranch[]; landed?: LandedBranch[] }>('SWEEP_PLAN'),
    enabled: isAvailable,
    // It asks git in every repository with a checkout here: a minute is fresh enough to read a session by.
    staleTime: 60_000,
  });
};

/** The clean-up's press: only the branches the list showed to go, each judged again by the driver. */
export const useSweep = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (only: string[]) =>
      call<{
        results: { branch: SweepBranch; removed: boolean; message: string }[];
        landed?: { branch: LandedBranch; removed: boolean; message: string }[];
        removed: number;
      }>('SWEEP', { only }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.sweep });
      void client.invalidateQueries({ queryKey: keys.allSessions });
    },
  });
};

/**
 * Set a repository's line or a workspace's, or clear it with no branch — the file `daoris driver
 * line` edits (D50). What every repository's line is moves with it, so that answer is asked again.
 */
export const useSetLine = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: LineChange) => call<DriverState>('SET_LINE', change),
    onSuccess: (state) => {
      client.setQueryData(keys.driver, state);
      void client.invalidateQueries({ queryKey: keys.lines });
    },
  });
};
