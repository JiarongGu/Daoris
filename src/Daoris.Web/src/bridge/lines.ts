import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { LineChange, RepositoryLine } from '../settings/Lines';
import type { LandingChange, RepositoryLanding } from '../settings/Landings';
import type { LandedBranch, SweepBranch } from '../settings/Sweep';
import type { LinePull, RebaseBranch, SyncInclude, SyncPlan, SyncRepository } from '../settings/Sync';
import { call } from './call';
import type { DriverState } from './driver';

// Each repository's line, how work lands in it, the clean-up of the branches sessions left, and bringing it up
// to date after a pull request merged (MOD3): the Workspace domain's machine half (WSR1, WSR2, WSR3, WSR6).

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
      // A session branch gone may leave its repository holding nothing of Daoris's (D112).
      void client.invalidateQueries({ queryKey: treesSyncScopeKey });
    },
  });
};

/** Where the last look at bringing repositories up to date is kept (WSR6). */
const treesSyncKey = ['driver', 'trees-sync'] as const;

/** What the last look was asked to take beyond the repositories holding Daoris's branches (WSR7, D112). */
const treesSyncAskedKey = ['driver', 'trees-sync-asked'] as const;

/** Which repositories hold Daoris's branches, and which have a checkout and hold none (D112). */
const treesSyncScopeKey = ['driver', 'trees-sync-scope'] as const;

/** What a look was asked to take beyond the repositories holding Daoris's branches, and how many it takes in all where known. */
export type SyncAsked = { include: SyncInclude; count: number | null };

/** The payload that asks a look to take `include` beside the default (D112): every one, or the ones named. */
const scopePayload = (include: SyncInclude): Record<string, unknown> | undefined =>
  include === 'all' ? { all: true } : include.length > 0 ? { also: include } : undefined;

/**
 * Every repository with a checkout here, and whether it holds a branch of Daoris's (WSR7, D112): what a look takes by
 * default, and the rest, listed apart for the person to include. Read on the machine, never fetched, so it is asked
 * when the section shows. Desktop-only, like every look at this machine's checkouts.
 */
export const useTreesSyncScope = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: treesSyncScopeKey,
    queryFn: () => call<{ repositories: SyncRepository[] }>('TREES_SYNC_SCOPE'),
    enabled: isAvailable,
    // It asks git once in every repository with a checkout here, as the clean-up's list does.
    staleTime: 60_000,
  });
};

/**
 * Bringing repositories up to date after a pull request merged (WSR6): the driver fetches each line, then says what a
 * press would do. It takes the repositories holding Daoris's branches, and those `look` is told to include (D112).
 * Asked for, never on its own — looking reaches the network, as the person — so it waits for `look`, which the
 * section's *Look for updates* is.
 *
 * @remarks
 * What the look was asked to take is kept beside its answer, so looking again keeps what the person included, and a
 * section drawn again mid-look still says how many repositories it is looking at.
 */
export const useTreesSyncPlan = () => {
  const client = useQueryClient();
  const plan = useQuery({
    queryKey: treesSyncKey,
    queryFn: () => {
      const asked = client.getQueryData<SyncAsked>(treesSyncAskedKey);
      return call<SyncPlan>('TREES_SYNC_PLAN', scopePayload(asked?.include ?? []));
    },
    enabled: false,
    staleTime: Infinity,
  });
  const asked = useQuery({
    queryKey: treesSyncAskedKey,
    queryFn: () => client.getQueryData<SyncAsked>(treesSyncAskedKey) ?? null,
    enabled: false,
    staleTime: Infinity,
  });
  return {
    ...plan,
    asked: asked.data ?? undefined,
    /** Look: the default, and what `include` names beside it. */
    look: (include: SyncInclude) => {
      const scope = client.getQueryData<{ repositories?: SyncRepository[] }>(treesSyncScopeKey)?.repositories;
      const count = Array.isArray(scope)
        ? scope.filter((each) => each.holds || include === 'all' || include.includes(each.repository)).length
        : null;
      client.setQueryData<SyncAsked>(treesSyncAskedKey, { include, count });
      return plan.refetch();
    },
  };
};

/**
 * The press: only the rows the list showed, each judged again by the driver, which does not fetch again. The
 * branches it moved or deleted change the clean-up's list, the sessions' trees and which repositories hold Daoris's
 * branches, so those are asked again.
 */
export const useTreesSync = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (only: string[]) =>
      call<{
        lines: { line: LinePull; moved: boolean; message: string }[];
        rebases: { branch: RebaseBranch; replayed: boolean; message: string }[];
        deletes: { branch: LandedBranch; removed: boolean; message: string }[];
        changed: number;
      }>('TREES_SYNC', { only }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.sweep });
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: treesSyncScopeKey });
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
