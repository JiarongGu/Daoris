import { useCallback } from 'react';
import { type QueryClient, type UseMutationResult, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { AccessChange, RepositoryAccess } from '../../settings/repository/Access';
import type { GitAccessChange, RepositoryGitAccess } from '../../settings/repository/GitAccess';
import type { CommitChange, RepositoryCommits } from '../../settings/repository/Commits';
import type { HelperChange, RepositoryHelpers } from '../../settings/repository/Helpers';
import type { HandOverChange, RepositoryHandOver } from '../../settings/repository/HandOver';
import type { LineChange, RepositoryLine } from '../../settings/repository/Lines';
import type { RepositoryLanguage } from '../../settings/repository/Languages';
import type { OpinionChange, RepositoryOpinion } from '../../settings/repository/Opinions';
import type { RepositoryReview, ReviewChange } from '../../settings/repository/Reviews';
import type { BranchDiscard, LandedBranch, LandedWithdrawal, SweepBranch } from '../../settings/Sweep';
import type { LinePull, RebaseBranch, SyncInclude, SyncPlan, SyncRepository } from '../../settings/Sync';
import { type HeldResolution, readWorkState, type RecoveryDropGroup, type RecoveryDropResult, type WorkState, workOf } from '../../work/head/milestone';
import { call, lookBound, pressBound } from '../call';
import { type DriverState, readSlice, useDriverChange } from '../driver';
import { pushedOf, readPushed, type SliceSpec } from '../state/slices';

// Whether a look or a press ended because the page stopped waiting: the section says so where it asked.
export { stoppedWaiting } from '../call';

// Each repository's line, how work lands in it, the clean-up of the branches sessions left, and bringing it up
// to date after a pull request merged: the Workspace domain's machine half.

/**
 * Every repository's line here and what said so. Desktop-only: the guess is read off a
 * checkout, and only this machine holds one.
 */
export type LinesAnswer = {
  lines: RepositoryLine[]; languages?: RepositoryLanguage[];
  // Where each repository's work is reviewed before it lands, as the driver resolves it.
  reviews?: RepositoryReview[];
  // Which other agent reads each one's work before it lands, as the driver resolves it.
  opinions?: RepositoryOpinion[];
  // How each one's work is committed and what a helper its agent asks for does, as the driver resolves them: each
  // never null, `default` where nothing is set.
  commits?: RepositoryCommits[];
  helpers?: RepositoryHelpers[];
  // What an agent's ask to hand its session over does, as the driver resolves it: never null, `default` where
  // nothing is set.
  handOver?: RepositoryHandOver[];
  // The access setup each one's sessions start under, every pause with its value and where it is set:
  // never null, `default` where nothing is set.
  access?: RepositoryAccess[];
  // What each one's sessions may do on a git remote, each class with its value and where it is set;
  // never null, `default` where nothing is set (Write then `as-today`).
  gitAccess?: RepositoryGitAccess[];
};

// The shell's `LinesStateSlice.Fields`, in its order: a field left out here is pushed and never composed.
const LINES_FIELDS = ['lines', 'languages', 'reviews', 'opinions', 'helpers', 'handOver', 'commits', 'access', 'gitAccess'] as const;

/**
 * The lines as the shell pushes them: a partition per repository, named by it, each holding that repository's rows
 * of what `LINES` answers, composed into the one entry the pull answers, repositories in name order. A repository not judged yet
 * is a partition whose line row says `notReadYet`; one the registry dropped is gone and leaves with its rows. Nothing judged yet
 * composes nothing, which is waited for until the first push.
 */
export const linesSlice: SliceSpec = {
  name: 'lines',
  key: keys.lines,
  read: () => readSlice('lines'),
  compose: (partitions) => {
    const names = [...partitions.keys()].sort();
    const parts = names.map((name) => partitions.get(name));
    if (parts.length === 0 || !parts.every((part) => typeof part === 'object' && part !== null && !Array.isArray(part))) return undefined;
    const composed: Record<string, unknown[]> = {};
    for (const field of LINES_FIELDS) {
      composed[field] = parts.flatMap((part) => {
        const rows = (part as Record<string, unknown>)[field];
        return Array.isArray(rows) ? rows : [];
      });
    }
    return composed as unknown as LinesAnswer;
  },
};

const NO_CHECKOUT = '<no checkout>';
const HELD = '<held>';

/**
 * The work as the shell pushes it: a partition per repository with a checkout here, then `<no checkout>`, each
 * `{units, recovery, unread}`, and `<held>`, `{held, ends}`, apart. Composed into the one entry `WORK_STATE` answers, repositories in name
 * order with `<no checkout>` last (a name sorts after `<`, so it is placed, not sorted). Not composed until the owner has judged every
 * part, which is waited for; a part of another shape is too.
 */
export const workSlice: SliceSpec = {
  name: 'work',
  key: keys.workState(null),
  read: () => readSlice('work'),
  compose: (partitions, judged) => {
    if (!judged) return undefined;
    const names = [...partitions.keys()].filter((name) => name !== HELD && name !== NO_CHECKOUT).sort();
    if (partitions.has(NO_CHECKOUT)) names.push(NO_CHECKOUT);
    const object = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);
    const parts = names.map((name) => partitions.get(name));
    const held = partitions.get(HELD);
    if (!parts.every(object) || (held !== undefined && !object(held))) return undefined;
    const rows = (field: string) => parts.flatMap((part) => (Array.isArray(part[field]) ? part[field] as unknown[] : []));
    // The closure graphs' ends ride in `<held>` too.
    return readWorkState({
      units: rows('units'), recovery: rows('recovery'), unread: rows('unread'),
      held: held && Array.isArray(held.held) ? held.held : [], ends: held && Array.isArray(held.ends) ? held.ends : [],
      // The node records that do not read, pushed beside the held attempts.
      unreadable: held && Array.isArray(held.unreadable) ? held.unreadable : [],
    });
  },
};

export const useLines = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  // Pushed: read from memory, written by the pushes, so neither a tick nor a focus asks the shell again.
  return useQuery({
    queryKey: keys.lines,
    queryFn: () => readPushed<LinesAnswer>(client, linesSlice.name),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
};

/** What `SWEEP_PLAN` answers: the session branches, and the landings' branches. */
export type SweepAnswer = { branches: SweepBranch[]; landed?: LandedBranch[] };

/**
 * The clean-up's list as the shell pushes it: a partition per repository with a checkout here, each
 * `{branches, landed}` as `SWEEP_PLAN`'s own builder writes it, composed into the one entry the pull answers, repositories in name order
 * by UTF-16 unit, as the door orders its checkouts (`Ordinal`). Nothing composed until the owner judged the list whole, which is
 * waited for; a part of another shape is too. A list judged with no repository is empty.
 */
export const sweepSlice: SliceSpec = {
  name: 'sweep',
  key: keys.sweep,
  read: () => readSlice('sweep'),
  compose: (partitions, judged) => {
    if (!judged) return undefined;
    const object = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);
    const parts = [...partitions.keys()].sort().map((name) => partitions.get(name));
    if (!parts.every(object)) return undefined;
    const rows = (field: string) => parts.flatMap((part) => (Array.isArray(part[field]) ? part[field] as unknown[] : []));
    return { branches: rows('branches'), landed: rows('landed') } as SweepAnswer;
  },
};

/**
 * Every session branch here with what it holds — the clean-up's list — and every branch a
 * landing made, judged against the line. Desktop-only: it is read off this machine's checkouts.
 *
 * As the shell pushes it: read from memory, written by the pushes, so neither a tick, a focus nor a declared change reads
 * `SWEEP_PLAN`, each of whose reads asks the pull-request plugin; the person's *Look* does (`useSweepLook`).
 */
export const useSweepPlan = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.sweep,
    queryFn: () => readPushed<SweepAnswer>(client, sweepSlice.name),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
};

/**
 * The clean-up's *Look*: the person's press of `SWEEP_PLAN`, the occasion its pull-request asks are made at,
 * and never a read the page makes on its own. Where the pushes compose the list, its answer is not written: the shell judges again
 * each repository whose kept answers the look moved (`SweepStateSlice.Asked`), and its push brings them. Where the owner has not
 * judged the list yet, the answer is the list, written where it is read.
 */
export const useSweepLook = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => call('SWEEP_PLAN', {}) as Promise<SweepAnswer>,
    onSuccess: (answer) => {
      if (pushedOf(client).data(sweepSlice.name) === undefined) client.setQueryData(keys.sweep, answer);
    },
  });
};

/**
 * Each unit of work's milestone: whether its end is reached, the result kept, its
 * cleanup pending or completed and the recovery retained, as `daoris work state` says it. With `unit`, the units that name
 * picks (its id, its session or its branch). A read of this machine's records and git: shell-only, and stored nowhere.
 */
export const useWorkState = (unit: string | null = null, enabled = true) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  // Pushed: the widest entry is read from memory and a unit's variant selected from it, so neither a tick nor a focus
  // asks the shell again.
  const select = useCallback((all: WorkState) => workOf(all, unit), [unit]);
  return useQuery({
    queryKey: keys.workState(null),
    queryFn: () => readPushed<WorkState>(client, workSlice.name),
    select, enabled: isAvailable && enabled, staleTime: Infinity, refetchOnWindowFocus: false,
  });
};

/**
 * The clean-up's press: only the branches the list showed to go, each judged again by the driver. What it moved (the list, the
 * sessions, the reviews kept until something moves their tree, and which repositories hold Daoris's branches)
 * the state hub asks again, as the route declares it, and the hook nothing itself.
 */
export const useSweep = () =>
  useMutation({
    mutationFn: (only: string[]) =>
      call('SWEEP', { only }) as Promise<{
        results: { branch: SweepBranch; removed: boolean; message: string }[];
        landed?: { branch: LandedBranch; removed: boolean; message: string }[];
        removed: number;
      }>,
  });

/**
 * Discard a failed or superseded attempt's session branch, its commits with it and its tree where it is still here: the screen's half of `daoris tree remove <branch> --repository <name> --force`. Always
 * forced, since the page asks once, naming the branch and its commits, before it sends; a branch the driver kept is an
 * answer with its sentence. What it moved is asked again either way, since a branch gone already is a list out of date: the
 * state hub's, as the route declares it, as after the clean-up, and never the hook's own.
 */
export const useDiscardSessionBranch = () =>
  useMutation({
    mutationFn: ({ repository, branch }: { repository: string; branch: string }) =>
      call('DISCARD_SESSION_BRANCH', { repository, branch, force: true }) as Promise<BranchDiscard>,
  });

/**
 * Withdraw a branch a landing made whose work the line will never hold: the screen's half of
 * `daoris tree remove <branch> --repository <name> --why "…"`. The reason is the person's, kept with the record; the commits stay
 * on a recovery ref, which the answer names. A branch the driver kept is an answer with its sentence, never an error. What it moved is
 * asked again by the state hub, as the route declares it, and never the hook's own.
 */
export const useWithdrawLandingBranch = () =>
  useMutation({
    mutationFn: ({ repository, branch, why }: { repository: string; branch: string; why: string }) =>
      call('WITHDRAW_LANDING_BRANCH', { repository, branch, why }) as Promise<LandedWithdrawal>,
  });

/**
 * The person's word on a plugin step's attempt held for them: the screen's half of `daoris work
 * held <unit> <node> <attempt> <word>`. The driver records it only where it holds the home, and applies it at its next look; a refusal
 * is an answer with the driver's sentence. What it moved the state hub asks again, as the route declares it.
 */
export const useResolveHeld = () =>
  useMutation({
    mutationFn: ({ unit, node, attempt, resolution }: { unit: string; node: string; attempt: number; resolution: HeldResolution }) =>
      call('WORK_HELD', { unit, node, attempt, resolution }) as Promise<{ done: boolean; refused?: string | null; message: string }>,
  });

/**
 * Open the folder the units' node records are kept in, where the person mends one that does not read: the shell names
 * the folder, never the page, and answers only whether it opened; with no record kept, the driver's refusal.
 */
export const useOpenWorkRecords = () =>
  useMutation({
    mutationFn: () => call<{ opened: boolean }>('WORK_RECORDS_FOLDER', {}),
  });

type RecoveryDropAnswer = {
  repository: string;
  done: boolean;
  refused?: string | null;
  dropped: { ref: string; commit: string }[];
  skipped: { ref: string; why: string }[];
};

/**
 * The person's delete of the recovery refs nothing holds: the screen's half of `daoris
 * work recovery drop`. One call per repository, in turn, each naming the refs with the commits the page showed, which the driver
 * verifies in the delete itself; a ref a holder keeps or that moved is skipped and its reason is the answer's. What it moved is asked
 * again by the state hub, as the route declares it, and never by this hook.
 */
export const useDropRecoveries = () =>
  useMutation({
    mutationFn: async (groups: RecoveryDropGroup[]): Promise<RecoveryDropResult> => {
      const result: RecoveryDropResult = { dropped: [], skipped: [], refused: [] };
      for (const { repository, refs } of groups) {
        const answer = await call('RECOVERY_DROP', { repository, refs }) as RecoveryDropAnswer;
        if (answer.refused) result.refused.push(answer.refused);
        result.dropped.push(...(answer.dropped ?? []).map((each) => ({ repository, ...each })));
        result.skipped.push(...(answer.skipped ?? []).map((each) => ({ repository, ...each })));
      }

      return result;
    },
  });

/**
 * Where the last look at bringing repositories up to date is kept: one per workspace, so a workspace's page never
 * shows another's look (a query's scope rides its cache key). Null is the machine's.
 */
const treesSyncKey = (workspace: string | null) => ['driver', 'trees-sync', workspace] as const;

/** What the last look took beyond the repositories holding Daoris's branches, per workspace. */
const treesSyncAskedKey = (workspace: string | null) => ['driver', 'trees-sync-asked', workspace] as const;

/**
 * Which repositories hold Daoris's branches, and which have a checkout and hold none: the machine's reading, each
 * row naming its workspace, which a workspace's page filters to its own and Ask Daoris's sync card counts whole.
 */
export const treesSyncScopeKey = ['driver', 'trees-sync-scope'] as const;

/** What a look is asked to take beyond the repositories holding Daoris's branches, and how many it takes in all where known. */
export type SyncAsked = { include: SyncInclude; count: number | null };

/** How many repositories a look's bound allows for when the machine's reading of how many it takes is not in yet. */
const UNKNOWN_COUNT = 32;

/** Whether a row is `workspace`'s, matched without case as the driver matches a workspace's name; every row with none. */
const inWorkspace = (row: { workspace: string }, workspace: string | null) =>
  workspace === null || row.workspace.toLowerCase() === workspace.toLowerCase();

/**
 * How many repositories a look takes: those holding Daoris's branches and those `include` names, `workspace`'s
 * alone where one is named, from the machine's last reading; null where that reading is not in yet.
 */
const lookCount = (client: QueryClient, include: SyncInclude, workspace: string | null = null): number | null => {
  const scope = client.getQueryData<{ repositories?: SyncRepository[] }>(treesSyncScopeKey)?.repositories;
  return Array.isArray(scope)
    ? scope.filter((each) => inWorkspace(each, workspace)
      && (each.holds || include === 'all' || include.includes(each.repository))).length
    : null;
};

/**
 * The payload that asks a look to take `include` beside the default, every one or the ones named, and that names
 * the workspace it is asked for, whose checkouts alone it then takes.
 */
const scopePayload = (include: SyncInclude, workspace: string | null): Record<string, unknown> | undefined => {
  const payload = {
    ...(include === 'all' ? { all: true } : include.length > 0 ? { also: include } : {}),
    ...(workspace === null ? {} : { workspace }),
  };
  return Object.keys(payload).length > 0 ? payload : undefined;
};

/**
 * Every repository with a checkout here, and whether it holds a branch of Daoris's: what a look takes by
 * default, and the rest, listed apart for the person to include. Read on the machine, never fetched, so it is asked
 * when the section shows. Desktop-only, like every look at this machine's checkouts. **The machine's whole, each row
 * naming its workspace**: a workspace's page counts its own rows.
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
 * Bringing repositories up to date after a pull request merged: the driver fetches each line, then says what a
 * press would do. It takes the repositories holding Daoris's branches, and those `look` is told to include.
 * Asked for, never on its own — looking reaches the network, as the person — so it waits for `look`, which the
 * section's *Look for updates* is.
 *
 * @remarks
 * What the look asks to take is kept beside its answer, so looking again keeps what the person included, and a
 * section drawn again mid-look still says how many repositories it is looking at.
 *
 * **It waits as long as the host may fetch**: a few repositories at a time, each within a fetch's bound. The
 * bridge's default 30 seconds would give up on a look that runs for minutes, and the host's answer would reach nobody. Where the
 * machine's reading of how many it takes is not in yet, the bound is a workspace's worth.
 *
 * **A workspace's page asks for its own**: with `workspace`, the driver fetches that workspace's
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
      return call('TREES_SYNC_PLAN', scopePayload(asked?.include ?? [], scoped) ?? {}, {
        timeoutMs: lookBound(asked?.count ?? UNKNOWN_COUNT),
      }) as Promise<SyncPlan>;
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
 * branches it moved or deleted change the clean-up's list, the sessions' trees, what a kept review reads, every
 * plan judged against a line, and which repositories hold Daoris's branches, so the state hub asks those again, as the
 * route declares them. It waits for each row's replay, one after another. Asked for a workspace,
 * the driver judges that workspace's checkouts alone, as its look does.
 */
export const useTreesSync = (workspace?: string) =>
  useMutation({
    mutationFn: (only: string[]) =>
      call('TREES_SYNC', workspace === undefined ? { only } : { only, workspace }, { timeoutMs: pressBound(only.length) }) as Promise<{
        lines: { line: LinePull; moved: boolean; message: string }[];
        rebases: { branch: RebaseBranch; replayed: boolean; message: string }[];
        deletes: { branch: LandedBranch; removed: boolean; message: string }[];
        changed: number;
      }>,
  });

// The standing choices over a repository's or a workspace's rules are the driver's own shape (`useDriverChange`): the answer is
// the driver's state, written where it is read; what each repository resolves to, and every plan and gate that reads it, move
// with it, which the route publishes and the state hub asks again in every window. A rule edit's nested object
// is the shared table's to judge, so its generated payload leaves it unknown; each hook takes the page's own shape of the edit.

type Change<T> = UseMutationResult<DriverState, Error, T>;

/** Set a repository's line or a workspace's, or clear it with no branch — the file `daoris place line` edits. */
export const useSetLine = () => useDriverChange('SET_LINE') as Change<LineChange>;

/**
 * Set the language a repository's sessions write to the person in, or a workspace's, or clear it with no language —
 * the file `daoris place language` edits. A code the driver's table does not hold is its refusal, its sentence verbatim.
 */
export const useSetLanguage = () => useDriverChange('SET_LANGUAGE');

/**
 * Change where a repository's work is reviewed before it lands, or a workspace's: the twins' edit,
 * over the file `daoris place review` edits. An environment put has its procedure looked for in the checkouts it
 * reaches, and the answer's `reviewed` says what it found. A refusal is the driver's sentence, verbatim. The landing's gate
 * reads the rule.
 */
export const useSetReview = () => useDriverChange('SET_REVIEW') as Change<ReviewChange>;

/**
 * Change which other agent reads a repository's work before it lands, or a workspace's: the twins'
 * edit, over the file `daoris place opinion` edits. A refusal is the driver's sentence, verbatim.
 */
export const useSetOpinion = () => useDriverChange('SET_OPINION') as Change<OpinionChange>;

/**
 * Change how a repository's work is committed, or a workspace's: the twins' edit, over
 * the file `daoris place commits` edits. A refusal is the driver's sentence, verbatim. Driven sessions and conversations
 * are told the rule at their start.
 */
export const useSetCommits = () => useDriverChange('SET_COMMITS') as Change<CommitChange>;

/**
 * Change what a helper a repository's agent asks for does, or a workspace's: the twins' edit, over the
 * file `daoris place helpers` edits. A refusal is the driver's sentence, verbatim.
 */
export const useSetHelpers = () => useDriverChange('SET_HELPERS') as Change<HelperChange>;

/**
 * Change what an agent's ask to hand its session over does in a repository, or a workspace: the twins'
 * edit, over the file `daoris settings handover` edits. A refusal is the driver's sentence, verbatim.
 */
export const useSetHandOver = () => useDriverChange('SET_HAND_OVER') as Change<HandOverChange>;

/**
 * Change the access setup a repository's sessions start under, a workspace's or this machine's: full
 * access, a pause set or taken back to the level above, or the level cleared, over the file `daoris place access` edits. A
 * refusal is the driver's sentence, verbatim. Full access is the person's press alone: no proposal names it.
 */
export const useSetAccess = () => useDriverChange('SET_ACCESS') as Change<AccessChange>;

/**
 * Change git access for a repository, a workspace or this machine: the classes it sets (a null
 * taking one back to the level above), one value for Read and Write, or the level cleared, over the file `daoris git access`
 * edits. A separate route from `SET_ACCESS`. A refusal is the driver's sentence, verbatim; a widening is confirmed by the page first.
 */
export const useSetGitAccess = () => useDriverChange('SET_GIT_ACCESS') as Change<GitAccessChange>;

/**
 * Tighten what one running session holds of git access: the classes to narrow, each to a value tighter than it holds,
 * over the record `daoris git access --session` edits. It only narrows, so nothing is confirmed; a refusal is the driver's sentence.
 */
export const useTightenSessionGitAccess = () => useDriverChange('TIGHTEN_SESSION_GIT_ACCESS') as Change<{ id: string; access: Record<string, string> }>;
