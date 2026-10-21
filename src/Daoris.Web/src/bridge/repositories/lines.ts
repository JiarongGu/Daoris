import { useCallback } from 'react';
import { type QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import type { AccessChange, RepositoryAccess } from '../../settings/repository/Access';
import type { CommitChange, RepositoryCommits } from '../../settings/repository/Commits';
import type { HelperChange, RepositoryHelpers } from '../../settings/repository/Helpers';
import type { HandOverChange, RepositoryHandOver } from '../../settings/repository/HandOver';
import type { LineChange, RepositoryLine } from '../../settings/repository/Lines';
import type { LandingChange, RepositoryLanding } from '../../settings/repository/Landings';
import type { LanguageChange, RepositoryLanguage } from '../../settings/repository/Languages';
import type { OpinionChange, RepositoryOpinion } from '../../settings/repository/Opinions';
import type { RepositoryReview, ReviewChange } from '../../settings/repository/Reviews';
import type { BranchDiscard, LandedBranch, SweepBranch } from '../../settings/Sweep';
import type { LinePull, RebaseBranch, SyncInclude, SyncPlan, SyncRepository } from '../../settings/Sync';
import { type HeldResolution, readWorkState, type RecoveryDropGroup, type RecoveryDropResult, type WorkState, workOf } from '../../work/head/milestone';
import { call, lookBound, pressBound } from '../call';
import { type DriverState, readSlice, useDriverChange, writeAnswered } from '../driver';
import { pushedOf, readPushed, type SliceSpec, usePushedLive } from '../state/slices';

// Whether a look or a press ended because the page stopped waiting (WSR7): the section says so where it was asked.
export { stoppedWaiting } from '../call';

// Each repository's line, how work lands in it, the clean-up of the branches sessions left, and bringing it up
// to date after a pull request merged (MOD3): the Workspace domain's machine half (WSR1, WSR2, WSR3, WSR6).

/**
 * Every repository's line here and what said so (WSR2). Desktop-only: the guess is read off a
 * checkout, and only this machine holds one.
 */
export type LinesAnswer = {
  lines: RepositoryLine[]; landings?: RepositoryLanding[]; languages?: RepositoryLanguage[];
  // Where each repository's work is reviewed before it lands, as the driver resolves it (REVIEWENV1a); absent on an older shell.
  reviews?: RepositoryReview[];
  // Which other agent reads each one's work before it lands, as the driver resolves it (XAGENT1a); absent on an older shell.
  opinions?: RepositoryOpinion[];
  // How each one's work is committed and what a helper its agent asks for does, as the driver resolves them (DOORS1b): each
  // never null, `default` where nothing is set; absent on an older shell, which offers neither row.
  commits?: RepositoryCommits[];
  helpers?: RepositoryHelpers[];
  // What an agent's ask to hand its session over does, as the driver resolves it (MIXAGENT1c): never null, `default` where
  // nothing is set; absent on an older shell, which offers no hand-over row.
  handOver?: RepositoryHandOver[];
  // The access setup each one's sessions start under, every pause with its value and where it was set (AUTONOMY1a, D184 point 1):
  // never null, `default` where nothing is set; absent on an older shell, which offers no access section.
  access?: RepositoryAccess[];
};

// The shell's `LinesStateSlice.Fields`, in its order: a field left out here is pushed and never composed (AUTONOMY1d).
const LINES_FIELDS = ['lines', 'landings', 'languages', 'reviews', 'opinions', 'helpers', 'handOver', 'commits', 'access'] as const;

/**
 * The lines as the shell pushes them (PUSH1b2, D182): a partition per repository, named by it, each holding that repository's rows
 * of what `LINES` answers, composed into the one entry the pull answers, repositories in name order. A repository not judged yet
 * is a partition whose line row says `notReadYet`; one the registry dropped is gone and leaves with its rows. Nothing judged yet
 * composes nothing, which keeps the pull until the first push.
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
 * The work as the shell pushes it (PUSH1c2b, D182): a partition per repository with a checkout here, then `<no checkout>`, each
 * `{units, recovery, unread}`, and `<held>`, `{held, ends}`, apart. Composed into the one entry `WORK_STATE` answers, repositories in name
 * order with `<no checkout>` last (a name sorts after `<`, so it is placed, not sorted). Not composed until the owner has judged every
 * part, which keeps the pull; a part of another shape keeps it too.
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
    // FABRIC-CLOSE1j: the closure graphs' ends ride in `<held>` too, absent from a shell before them.
    return readWorkState({
      units: rows('units'), recovery: rows('recovery'), unread: rows('unread'),
      held: held && Array.isArray(held.held) ? held.held : [], ends: held && Array.isArray(held.ends) ? held.ends : [],
      // FABRIC-PERFORM1j: the node records that do not read, pushed beside the held attempts.
      unreadable: held && Array.isArray(held.unreadable) ? held.unreadable : [],
    });
  },
};

export const useLines = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  // Pushed (PUSH1b2): read from memory, written by the pushes, so neither a tick nor a focus asks the shell again.
  const pushed = usePushedLive(linesSlice.name);
  return useQuery({
    queryKey: keys.lines,
    queryFn: () => readPushed(client, linesSlice.name, () => call<LinesAnswer>('LINES')),
    enabled: isAvailable,
    ...(pushed ? { staleTime: Infinity, refetchOnWindowFocus: false } : {}),
  });
};

/**
 * Set how work lands in a repository or a workspace, or clear it with no form — the file `daoris driver
 * landing` edits (D50). Its answer is the driver's state, written where it is read; every repository's rules and every plan
 * move with it, which the state hub asks again, as the route declares them (STATEHUB1b, D169).
 */
export const useSetLanding = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: LandingChange) => call<DriverState>('SET_LANDING', change),
    onSuccess: (state) => writeAnswered(client, state),
  });
};

/** What `SWEEP_PLAN` answers: the session branches, and the landings' branches from a shell since WSR5. */
export type SweepAnswer = { branches: SweepBranch[]; landed?: LandedBranch[] };

/**
 * The clean-up's list as the shell pushes it (PUSH1c4b, D182's PUSH1c4a note): a partition per repository with a checkout here, each
 * `{branches, landed}` as `SWEEP_PLAN`'s own builder writes it, composed into the one entry the pull answers, repositories in name order
 * by UTF-16 unit, as the door orders its checkouts (`Ordinal`). Nothing composed until the owner judged the list whole, which keeps the
 * pull; a part of another shape keeps it too. A list judged with no repository is empty.
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
 * Every session branch here with what it holds (WSR3, D88) — the clean-up's list — and every branch a
 * landing made, judged against the line (WSR5). Desktop-only: it is read off this machine's checkouts.
 * A shell older than WSR5 answers no `landed`.
 *
 * Pushed where the shell pushes it (PUSH1c4b): read from memory, written by the pushes, so neither a tick, a focus nor a declared
 * change reads `SWEEP_PLAN`, each of whose reads asks the pull-request plugin (D148 point 2); the person's *Look* does (`useSweepLook`).
 * Elsewhere (a plain browser, an older shell) it is the pull it always was, after the first snapshot where one is on its way.
 */
export const useSweepPlan = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const pushed = usePushedLive(sweepSlice.name);
  return useQuery({
    queryKey: keys.sweep,
    queryFn: () => readPushed(client, sweepSlice.name, () => call<SweepAnswer>('SWEEP_PLAN')),
    enabled: isAvailable,
    // Pulled, it asks git in every repository with a checkout here: a minute is fresh enough to read a session by.
    ...(pushed ? { staleTime: Infinity, refetchOnWindowFocus: false } : { staleTime: 60_000 }),
  });
};

/**
 * The clean-up's *Look* (PUSH1c4b): the person's press of `SWEEP_PLAN`, the occasion its pull-request asks are made at (D148 point 2),
 * and never a read the page makes on its own. Where the pushes compose the list, its answer is not written: the shell judges again
 * each repository whose kept answers the look moved (`SweepStateSlice.Asked`), and its push brings them. Where the list is pulled, or
 * the owner has not judged it yet, the answer is the list, written where it is read, as the read again was.
 */
export const useSweepLook = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => call<SweepAnswer>('SWEEP_PLAN'),
    onSuccess: (answer) => {
      if (pushedOf(client).data(sweepSlice.name) === undefined) client.setQueryData(keys.sweep, answer);
    },
  });
};

/**
 * Each unit of work's milestone (FABRIC-MILESTONE1b, the fabric events design §D0): whether its end was reached, the result kept, its
 * cleanup pending or completed and the recovery retained, as `daoris-driver work state` says it. With `unit`, the units that name
 * picks (its id, its session or its branch). A read of this machine's records and git: shell-only, and stored nowhere. A shell older
 * than the route answers nothing of the kind, which reads as no unit. The state hub asks it again with the landings, the sweep and
 * the sessions (`landing`, `sweep`, `sessions`), which are what move it.
 */
export const useWorkState = (unit: string | null = null, enabled = true) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  // Pushed (PUSH1c2b): the widest entry is read live from memory and a unit's variant selected from it, so neither a tick nor a focus
  // asks the shell again. Elsewhere each variant keeps its own pull, after the first snapshot where one is on its way.
  const pushed = usePushedLive(workSlice.name);
  const select = useCallback((all: WorkState) => workOf(all, unit), [unit]);
  const pull = async () => readWorkState(await call<unknown>('WORK_STATE', unit ? { unit } : {}));
  return useQuery(pushed
    ? {
      queryKey: keys.workState(null),
      queryFn: () => readPushed(client, workSlice.name, async () => readWorkState(await call<unknown>('WORK_STATE', {}))),
      select, enabled: isAvailable && enabled, staleTime: Infinity, refetchOnWindowFocus: false,
    }
    : {
      queryKey: keys.workState(unit),
      queryFn: async () => {
        const all = await readPushed<WorkState | null>(client, workSlice.name, async () => null);
        return all === null ? pull() : workOf(all, unit);
      },
      enabled: isAvailable && enabled,
      // It asks git in each repository with a checkout here: a minute is fresh enough to say where work stands by.
      staleTime: 60_000,
    });
};

/**
 * The clean-up's press: only the branches the list showed to go, each judged again by the driver. What it moved (the list, the
 * sessions, the reviews kept until something moves their tree (REVIEW4), and which repositories hold Daoris's branches, D112)
 * the state hub asks again, as the route declares it (STATEHUB1b, D169), and the hook nothing itself (STATEHUB1f).
 */
export const useSweep = () =>
  useMutation({
    mutationFn: (only: string[]) =>
      call<{
        results: { branch: SweepBranch; removed: boolean; message: string }[];
        landed?: { branch: LandedBranch; removed: boolean; message: string }[];
        removed: number;
      }>('SWEEP', { only }),
  });

/**
 * Discard a failed or superseded attempt's session branch, its commits with it and its tree where it is still here (LAND3b,
 * D102's LAND3 note): the screen's half of `daoris-driver trees remove <branch> --repository <name> --force`. Always
 * forced, since the page asks once, naming the branch and its commits, before it sends; a branch the driver kept is an
 * answer with its sentence. What it moved is asked again either way, since a branch gone already is a list out of date: the
 * state hub's, as the route declares it, as after the clean-up (STATEHUB1b, D169), and never the hook's own (STATEHUB1f).
 */
export const useDiscardSessionBranch = () =>
  useMutation({
    mutationFn: ({ repository, branch }: { repository: string; branch: string }) =>
      call<BranchDiscard>('DISCARD_SESSION_BRANCH', { repository, branch, force: true }),
  });

/**
 * The person's word on a plugin step's attempt held for them (FABRIC-PERFORM1g, D173's note): the screen's half of `daoris-driver work
 * held <unit> <node> <attempt> <word>`. The driver records it only where it holds the home, and applies it at its next look; a refusal
 * is an answer with the driver's sentence. What it moved the state hub asks again, as the route declares it (STATEHUB1f).
 */
export const useResolveHeld = () =>
  useMutation({
    mutationFn: ({ unit, node, attempt, resolution }: { unit: string; node: string; attempt: number; resolution: HeldResolution }) =>
      call<{ done: boolean; refused?: string | null; message: string }>('WORK_HELD', { unit, node, attempt, resolution }),
  });

/**
 * Open the folder the units' node records are kept in (FABRIC-PERFORM1j), where the person mends one that does not read: the shell names
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
 * The person's delete of the recovery refs nothing holds (RECOVERYDROP1, D102's CLEANUP1 note): the screen's half of `daoris-driver
 * work recovery drop`. One call per repository, in turn, each naming the refs with the commits the page showed, which the driver
 * verifies in the delete itself; a ref a holder keeps or that moved is skipped and its reason is the answer's. What it moved is asked
 * again by the state hub, as the route declares it, and never by this hook (STATEHUB1f).
 */
export const useDropRecoveries = () =>
  useMutation({
    mutationFn: async (groups: RecoveryDropGroup[]): Promise<RecoveryDropResult> => {
      const result: RecoveryDropResult = { dropped: [], skipped: [], refused: [] };
      for (const { repository, refs } of groups) {
        const answer = await call<RecoveryDropAnswer>('RECOVERY_DROP', { repository, refs });
        if (answer.refused) result.refused.push(answer.refused);
        result.dropped.push(...(answer.dropped ?? []).map((each) => ({ repository, ...each })));
        result.skipped.push(...(answer.skipped ?? []).map((each) => ({ repository, ...each })));
      }

      return result;
    },
  });

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
export const treesSyncScopeKey = ['driver', 'trees-sync-scope'] as const;

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
 * branches it moved or deleted change the clean-up's list, the sessions' trees, what a kept review reads (REVIEW4), every
 * plan judged against a line, and which repositories hold Daoris's branches, so the state hub asks those again, as the
 * route declares them (STATEHUB1b, D169). It waits for each row's replay, one after another (WSR7). Asked for a workspace,
 * the driver judges that workspace's checkouts alone (BRSCOPE1), as its look did.
 */
export const useTreesSync = (workspace?: string) =>
  useMutation({
    mutationFn: (only: string[]) =>
      call<{
        lines: { line: LinePull; moved: boolean; message: string }[];
        rebases: { branch: RebaseBranch; replayed: boolean; message: string }[];
        deletes: { branch: LandedBranch; removed: boolean; message: string }[];
        changed: number;
      }>('TREES_SYNC', workspace === undefined ? { only } : { only, workspace }, { timeoutMs: pressBound(only.length) }),
  });

// The standing choices over a repository's or a workspace's rules are the driver's own shape (`useDriverChange`): the answer is
// the driver's state, written where it is read; what each repository resolves to, and every plan and gate that reads it, move
// with it, which the route publishes and the state hub asks again in every window (STATEHUB1h, D169).

/** Set a repository's line or a workspace's, or clear it with no branch — the file `daoris driver line` edits (D50). */
export const useSetLine = () => useDriverChange<LineChange>('SET_LINE');

/**
 * Set the language a repository's sessions write to the person in, or a workspace's, or clear it with no language (LANG1c) —
 * the file `daoris driver language` edits (D50). A code the driver's table does not hold is its refusal, its sentence verbatim.
 */
export const useSetLanguage = () => useDriverChange<LanguageChange>('SET_LANGUAGE');

/**
 * Change where a repository's work is reviewed before it lands, or a workspace's (REVIEWENV1a, D154 point 2): the twins' edit,
 * over the file `daoris driver review` edits (D50). An environment put has its procedure looked for in the checkouts it
 * reaches, and the answer's `reviewed` says what was found. A refusal is the driver's sentence, verbatim. The landing's gate
 * reads the rule since REVIEWENV1c.
 */
export const useSetReview = () => useDriverChange<ReviewChange>('SET_REVIEW');

/**
 * Change which other agent reads a repository's work before it lands, or a workspace's (XAGENT1a, D155 point 3): the twins'
 * edit, over the file `daoris driver opinion` edits (D50). A refusal is the driver's sentence, verbatim.
 */
export const useSetOpinion = () => useDriverChange<OpinionChange>('SET_OPINION');

/**
 * Change how a repository's work is committed, or a workspace's (DOORS1b, D164's amendment points 1 and 3): the twins' edit, over
 * the file `daoris driver commits` edits (D50). A refusal is the driver's sentence, verbatim. Driven sessions and conversations
 * are told the rule at their start (PROCESS1d).
 */
export const useSetCommits = () => useDriverChange<CommitChange>('SET_COMMITS');

/**
 * Change what a helper a repository's agent asks for does, or a workspace's (DOORS1b, D163 point 3): the twins' edit, over the
 * file `daoris driver helpers` edits (D50). A refusal is the driver's sentence, verbatim.
 */
export const useSetHelpers = () => useDriverChange<HelperChange>('SET_HELPERS');

/**
 * Change what an agent's ask to hand its session over does in a repository, or a workspace (MIXAGENT1c, D163 point 3): the twins'
 * edit, over the file `daoris driver handover` edits (D50). A refusal is the driver's sentence, verbatim.
 */
export const useSetHandOver = () => useDriverChange<HandOverChange>('SET_HAND_OVER');

/**
 * Change the access setup a repository's sessions start under, a workspace's or this machine's (AUTONOMY1d, D184 points 1-3): full
 * access, a pause set or taken back to the level above, or the level cleared, over the file `daoris-driver access` edits (D50). A
 * refusal is the driver's sentence, verbatim. Full access is the person's press alone: no proposal names it (D184's rejected list).
 */
export const useSetAccess = () => useDriverChange<AccessChange>('SET_ACCESS');
