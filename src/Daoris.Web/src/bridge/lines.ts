import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { LineChange, RepositoryLine } from '../settings/Lines';
import type { LandingChange, RepositoryLanding } from '../settings/Landings';
import type { LanguageChange, RepositoryLanguage } from '../settings/Languages';
import type { OpinionChange, RepositoryOpinion } from '../settings/Opinions';
import type { RepositoryReview, ReviewChange } from '../settings/Reviews';
import type { BranchDiscard, LandedBranch, SweepBranch } from '../settings/Sweep';
import type { LinePull, RebaseBranch, SyncInclude, SyncPlan, SyncRepository } from '../settings/Sync';
import { call, lookBound, pressBound } from './call';
import type { DriverState } from './driver';

// Whether a look or a press ended because the page stopped waiting (WSR7): the section says so where it was asked.
export { stoppedWaiting } from './call';

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
    queryFn: () => call<{
      lines: RepositoryLine[]; landings?: RepositoryLanding[]; languages?: RepositoryLanguage[];
      // Where each repository's work is reviewed before it lands, as the driver resolves it (REVIEWENV1a); absent on an older shell.
      reviews?: RepositoryReview[];
      // Which other agent reads each one's work before it lands, as the driver resolves it (XAGENT1a); absent on an older shell.
      opinions?: RepositoryOpinion[];
    }>('LINES'),
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
      // A tree removed: an ended session's review is kept until something moves its tree (REVIEW4), and this did.
      void client.invalidateQueries({ queryKey: keys.allDiffs });
      // A session branch gone may leave its repository holding nothing of Daoris's (D112).
      void client.invalidateQueries({ queryKey: treesSyncScopeKey });
    },
  });
};

/**
 * Discard a failed or superseded attempt's session branch, its commits with it and its tree where it is still here (LAND3b,
 * D102's LAND3 note): the screen's half of `daoris-driver trees remove <branch> --repository <name> --force`. Always
 * forced, since the page asks once, naming the branch and its commits, before it sends; a branch the driver kept is an
 * answer with its sentence. The list is asked again either way, since a branch gone already is a list out of date.
 */
export const useDiscardSessionBranch = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ repository, branch }: { repository: string; branch: string }) =>
      call<BranchDiscard>('DISCARD_SESSION_BRANCH', { repository, branch, force: true }),
    onSuccess: (result) => {
      void client.invalidateQueries({ queryKey: keys.sweep });
      if (!result.done) return;
      void client.invalidateQueries({ queryKey: keys.allSessions });
      // A tree removed with its branch: a kept review is read again (REVIEW4), as after the clean-up.
      void client.invalidateQueries({ queryKey: keys.allDiffs });
      // A session branch gone may leave its repository holding nothing of Daoris's (D112).
      void client.invalidateQueries({ queryKey: treesSyncScopeKey });
    },
  });
};

/**
 * Where the last look at bringing repositories up to date is kept (WSR6): one per workspace, so a workspace's page never
 * shows another's look (BRSCOPE1, WSP5's rule that a query's scope rides its cache key). Null is the machine's.
 */
const treesSyncKey = (workspace: string | null) => ['driver', 'trees-sync', workspace] as const;

/** What the last look was asked to take beyond the repositories holding Daoris's branches (WSR7, D112), per workspace. */
const treesSyncAskedKey = (workspace: string | null) => ['driver', 'trees-sync-asked', workspace] as const;

/**
 * Which repositories hold Daoris's branches, and which have a checkout and hold none (D112): the machine's reading, each
 * row naming its workspace, which a workspace's page filters to its own and Ask Daoris's sync card counts whole.
 */
const treesSyncScopeKey = ['driver', 'trees-sync-scope'] as const;

/** What a look was asked to take beyond the repositories holding Daoris's branches, and how many it takes in all where known. */
export type SyncAsked = { include: SyncInclude; count: number | null };

/** How many repositories a look's bound allows for when the machine's reading of how many it takes is not in yet. */
const UNKNOWN_COUNT = 32;

/** Whether a row is `workspace`'s, matched without case as the driver matches a workspace's name; every row with none. */
const inWorkspace = (row: { workspace: string }, workspace: string | null) =>
  workspace === null || row.workspace.toLowerCase() === workspace.toLowerCase();

/**
 * How many repositories a look takes (D112): those holding Daoris's branches and those `include` names, `workspace`'s
 * alone where one is named (BRSCOPE1), from the machine's last reading; null where that reading is not in yet.
 */
const lookCount = (client: QueryClient, include: SyncInclude, workspace: string | null = null): number | null => {
  const scope = client.getQueryData<{ repositories?: SyncRepository[] }>(treesSyncScopeKey)?.repositories;
  return Array.isArray(scope)
    ? scope.filter((each) => inWorkspace(each, workspace)
      && (each.holds || include === 'all' || include.includes(each.repository))).length
    : null;
};

/**
 * How long a look may take (WSR7): a few repositories at a time, each within a fetch's bound, for as many as it takes —
 * Ask Daoris's sync card's look as well as the screen's (`bridge/help.ts`).
 */
export const syncLookBound = (client: QueryClient, include: SyncInclude = []) =>
  lookBound(lookCount(client, include) ?? UNKNOWN_COUNT);

/**
 * The payload that asks a look to take `include` beside the default (D112), every one or the ones named, and that names
 * the workspace it is asked for, whose checkouts alone it then takes (BRSCOPE1).
 */
const scopePayload = (include: SyncInclude, workspace: string | null): Record<string, unknown> | undefined => {
  const payload = {
    ...(include === 'all' ? { all: true } : include.length > 0 ? { also: include } : {}),
    ...(workspace === null ? {} : { workspace }),
  };
  return Object.keys(payload).length > 0 ? payload : undefined;
};

/**
 * Every repository with a checkout here, and whether it holds a branch of Daoris's (WSR7, D112): what a look takes by
 * default, and the rest, listed apart for the person to include. Read on the machine, never fetched, so it is asked
 * when the section shows. Desktop-only, like every look at this machine's checkouts. **The machine's whole, each row
 * naming its workspace**: a workspace's page counts its own rows (BRSCOPE1).
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
 *
 * **It waits as long as the host may fetch** (WSR7): a few repositories at a time, each within a fetch's bound. The
 * bridge's default 30 seconds gave up on a look that ran for minutes, and the host's answer reached nobody. Where the
 * machine's reading of how many it takes is not in yet, the bound is a workspace's worth.
 *
 * **A workspace's page asks for its own** (BRSCOPE1, WSP5): with `workspace`, the driver fetches that workspace's
 * checkouts alone and leaves apart only its own, and the look is kept under its name. With none, the machine's.
 */
export const useTreesSyncPlan = (workspace?: string) => {
  const client = useQueryClient();
  const scoped = workspace ?? null;
  const askedKey = treesSyncAskedKey(scoped);
  const plan = useQuery({
    queryKey: treesSyncKey(scoped),
    queryFn: () => {
      const asked = client.getQueryData<SyncAsked>(askedKey);
      return call<SyncPlan>('TREES_SYNC_PLAN', scopePayload(asked?.include ?? [], scoped), {
        timeoutMs: lookBound(asked?.count ?? UNKNOWN_COUNT),
      });
    },
    // A look is asked by a press, and one the page stopped waiting for is said, not asked again behind the person.
    retry: false,
    enabled: false,
    staleTime: Infinity,
  });
  const asked = useQuery({
    queryKey: askedKey,
    queryFn: () => client.getQueryData<SyncAsked>(askedKey) ?? null,
    enabled: false,
    staleTime: Infinity,
  });
  return {
    ...plan,
    asked: asked.data ?? undefined,
    /** Look: the default, and what `include` names beside it. */
    look: (include: SyncInclude) => {
      client.setQueryData<SyncAsked>(askedKey, { include, count: lookCount(client, include, scoped) });
      return plan.refetch();
    },
  };
};

/**
 * The press: only the rows the list showed, each judged again by the driver, which does not fetch again. The
 * branches it moved or deleted change the clean-up's list, the sessions' trees and which repositories hold Daoris's
 * branches, so those are asked again. It waits for each row's replay, one after another (WSR7). Asked for a workspace,
 * the driver judges that workspace's checkouts alone (BRSCOPE1), as its look did.
 */
export const useTreesSync = (workspace?: string) => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (only: string[]) =>
      call<{
        lines: { line: LinePull; moved: boolean; message: string }[];
        rebases: { branch: RebaseBranch; replayed: boolean; message: string }[];
        deletes: { branch: LandedBranch; removed: boolean; message: string }[];
        changed: number;
      }>('TREES_SYNC', workspace === undefined ? { only } : { only, workspace }, { timeoutMs: pressBound(only.length) }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.sweep });
      void client.invalidateQueries({ queryKey: keys.allSessions });
      // A tree replayed or a branch deleted moves what a kept review reads (REVIEW4).
      void client.invalidateQueries({ queryKey: keys.allDiffs });
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

/**
 * Set the language a repository's sessions write to the person in, or a workspace's, or clear it with no language (LANG1c) —
 * the file `daoris driver language` edits (D50). What each repository resolves to moves with it, so the lines are asked again.
 * A code the driver's table does not hold is its refusal, its sentence verbatim.
 */
export const useSetLanguage = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: LanguageChange) => call<DriverState>('SET_LANGUAGE', change),
    onSuccess: (state) => {
      client.setQueryData(keys.driver, state);
      void client.invalidateQueries({ queryKey: keys.lines });
    },
  });
};

/**
 * Change where a repository's work is reviewed before it lands, or a workspace's (REVIEWENV1a, D154 point 2): the twins' edit,
 * over the file `daoris driver review` edits (D50). An environment put has its procedure looked for in the checkouts it
 * reaches, and the answer's `reviewed` says what was found. What each repository resolves to moves with it, so the lines are
 * asked again. A refusal is the driver's sentence, verbatim. Declared only: nothing reads it yet.
 */
export const useSetReview = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: ReviewChange) => call<DriverState>('SET_REVIEW', change),
    onSuccess: (state) => {
      client.setQueryData(keys.driver, state);
      void client.invalidateQueries({ queryKey: keys.lines });
    },
  });
};

/**
 * Change which other agent reads a repository's work before it lands, or a workspace's (XAGENT1a, D155 point 3): the twins'
 * edit, over the file `daoris driver opinion` edits (D50). What each repository resolves to moves with it, so the lines are
 * asked again. A refusal is the driver's sentence, verbatim. Declared only: nothing reads it yet.
 */
export const useSetOpinion = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: OpinionChange) => call<DriverState>('SET_OPINION', change),
    onSuccess: (state) => {
      client.setQueryData(keys.driver, state);
      void client.invalidateQueries({ queryKey: keys.lines });
    },
  });
};
