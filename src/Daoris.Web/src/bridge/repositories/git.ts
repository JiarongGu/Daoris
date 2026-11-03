import { useState } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { call } from '../call';
import { askHeld } from '../state/held';

// Git's reads (GIT1d-b, D147 §2, D150): the screen's door to `daoris git`, the same reads over the same checkouts, for
// Repositories' Branches and History tabs. Shell-only: a checkout is this machine's, and no machine path leaves it (D47 §4).
// Each read answers {value, refusal, problem, commands, remembered}: a refusal is a code the page words (`errors.{code}`, as
// `sentence` does) and `commands` are the git a person types for the same answer.

/** A read's answer: its value, or the code of why not, with the sentence beside it and the git that answers it. */
export type GitAnswer<T> = {
  value: T | null;
  /** `GIT_NOT_TOP` · `GIT_NOT_A_COMMIT` · `GIT_NOT_A_PATH` · `GIT_NOT_A_PAGE` · `GIT_TOO_MANY_TIPS` · `GIT_NOT_ANSWERED` · `GIT_NO_CHECKOUT`, or null. */
  refusal: string | null;
  problem: string | null;
  commands: string[];
  /** Whether the host answered from memory, with no git started. */
  remembered: boolean;
};

/** The refusal an answer carries, as the code the page words and the driver's sentence, or null where it answered. */
export const gitRefusal = (answer: GitAnswer<unknown> | undefined | null): { code: string; problem: string | null } | null =>
  answer?.refusal ? { code: answer.refusal, problem: answer.problem } : null;

export type GitLine = {
  branch: string | null;
  /** What set it: the driver's `LineSource`. */
  source: string;
  commit: string | null;
  origin: string | null;
  ahead: number | null;
  behind: number | null;
};

export type GitSessionBranch = {
  session: string | null;
  state: string | null;
  quest: string | null;
  grewFrom: string | null;
  from: string | null;
};

/**
 * What a plugin last answered about a landed branch's pull request (PLUGHOOK1c, D148 point 6): the driver's `PullRequestState`
 * as the landing record keeps it, read and never asked.
 */
export type GitPullRequestState = {
  /** `open` · `completed` · `abandoned` · `unknown`. */
  state: string;
  /** The pull request it answered for, a web address. */
  pullRequest: string | null;
  mergeCommit: string | null;
  sourceCommit: string | null;
  target: string | null;
  how: string | null;
  /** When it completed or was abandoned. */
  at: string | null;
  /** The plugin's own sentence: content. */
  message: string | null;
  plugin: string | null;
  /** When Daoris asked; the year 1 where a record did not keep it. */
  askedAt: string;
};

export type GitLandedBranch = {
  session: string;
  landedAt: string;
  /** One of the driver's `GitOrigin`: `in-step` · `ahead` · `behind` · `diverged` · `gone` · `not-pushed` · `unknown`. */
  origin: string;
  quest: string | null;
  title: string | null;
  plugin: string | null;
  pushed: boolean;
  pullRequest: string | null;
  pushedTip: string | null;
  originCommit: string | null;
  originAhead: number | null;
  originBehind: number | null;
  pullRequestState: GitPullRequestState | null;
  pullRequestAskFailed: { code: string; plugin: string; at: string } | null;
};

export type GitBranch = {
  /** `topic`, or `origin/release` for one only origin holds. */
  name: string;
  kind: string;
  commit: string;
  at: string | null;
  subject: string;
  ahead: number | null;
  behind: number | null;
  /** The working tree's folder name, never its path (D47 §4); null where none. */
  tree: string | null;
  session: GitSessionBranch | null;
  landed: GitLandedBranch | null;
};

export type GitRepositoryBranches = {
  repository: string;
  workspace: string;
  line: GitLine;
  fetch: { at: string; heard: boolean } | null;
  holds: boolean;
  missing: string[];
  commands: string[];
  problem: string | null;
  branches: GitBranch[];
};

export type GitBranchList = {
  repositories: GitRepositoryBranches[];
  /** The repositories the scope left out. */
  apart: { repository: string; workspace: string; holds: boolean }[];
  /** True where the session records could not be read, so no branch is named a session's. */
  sessionsUnread: boolean;
};

export type GitLogCommit = {
  commit: string;
  /** Whole ids, the first parent first. */
  parents: string[];
  author: string;
  authorEmail: string;
  subject: string;
  authoredAt: string | null;
  committedAt: string | null;
  /** In a two-sided walk, which side holds it; null for a walk from tips. */
  side: string | null;
};

export type GitGraphPage = { tips: string[]; sinceParted: boolean; page: number; commits: GitLogCommit[]; more: boolean };

/** The review's changes (REVIEW3), bounded: the list is whole, a patch may be dropped. */
export type GitChanges = {
  files: { path: string; status: string; added: number | null; removed: number | null; patch: string | null }[];
  truncated: string | null;
  base: string;
};

export type GitCommitDetail = {
  commit: string;
  parents: string[];
  subject: string;
  message: string;
  author: string;
  authorEmail: string;
  authoredAt: string | null;
  committer: string;
  committerEmail: string;
  committedAt: string | null;
  against: string | null;
  merge: boolean;
  changes: GitChanges | null;
};

export type GitFileChange = { commit: GitLogCommit; status: string; path: string; from: string | null };
export type GitFileHistory = { commit: string; path: string; page: number; changes: GitFileChange[]; more: boolean };

export type GitBlameLine = { original: number; text: string };
export type GitBlameRun = { commit: string; line: number; path: string; previous: string | null; previousPath: string | null; lines: GitBlameLine[] };
export type GitBlameCommit = { commit: string; author: string; authorEmail: string; summary: string; authoredAt: string | null; boundary: boolean };
export type GitBlame = {
  commit: string;
  path: string;
  binary: boolean;
  runs: GitBlameRun[];
  commits: Record<string, GitBlameCommit>;
  stoppedAt: number | null;
};

export type GitCompare = {
  left: string;
  right: string;
  everything: boolean;
  commits: GitLogCommit[];
  more: boolean;
  changes: GitChanges | null;
};

/** The scope a branch list is read for (D112): the held repositories by default, every one, or one by name. */
export type GitBranchScope = { all?: boolean; repository?: string; workspace?: string; enabled?: boolean };

/** The branches of every repository in a scope, with the repositories apart from it. */
export const useGitBranches = (scope: GitBranchScope = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.gitBranches(Boolean(scope.all), scope.repository ?? '', scope.workspace ?? ''),
    queryFn: () => call<GitBranchList>('GIT_BRANCHES', {
      ...(scope.all ? { all: true } : {}),
      ...(scope.repository ? { repository: scope.repository } : {}),
      ...(scope.workspace ? { workspace: scope.workspace } : {}),
    }),
    enabled: isAvailable && scope.enabled !== false,
  });
  return { available: isAvailable, query };
};

/**
 * A repository's git remotes with what serves each (GITAUTH1e, the git-auth design §7.3), or every repository of a workspace: the list's
 * `remotes` beside its branches. Keyed under the tools' root, so a change of identity or choice (`GIT_IDENTITY`, `GIT_USE`) asks again.
 */
export const useGitRemotes = ({ repository, workspace, enabled = true }: { repository?: string; workspace?: string; enabled?: boolean }) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ['tools', 'git-remotes', repository ?? '', workspace ?? ''] as const,
    queryFn: async () => {
      const list = await call<{ repositories: { repository: string; workspace: string; remotes?: GitRemoteRow[] | null }[] }>(
        'GIT_BRANCHES', { remotes: true, ...(repository ? { repository } : {}), ...(workspace ? { workspace } : {}) });
      return list.repositories.map((each) => ({ repository: each.repository, workspace: each.workspace, remotes: each.remotes ?? [] }));
    },
    enabled: isAvailable && enabled && Boolean(repository || workspace),
  });
};

/** A remote's row as `GIT_BRANCHES` answers it with `remotes`: the settings page's `GitRemoteShown`. */
export type GitRemoteRow = import('../../settings/git/GitRemotes').GitRemoteShown;

/** What `CHECKOUT_BRANCH` answers: the checkout's branch by its short name, or null where HEAD is detached. */
export type CheckoutBranch = { branch: string | null };

/**
 * The branch a repository's checkout stands on here (NEW1e-a), for the composer's tree chip. Shell-only; asks nothing without a
 * repository. Always stale, so a window focus asks again and a branch switched elsewhere shows; no polling. A repository with no
 * checkout here is the driver's refusal, which the query holds as its error.
 */
export const useCheckoutBranch = ({ repository, workspace }: { repository: string | null; workspace?: string }) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.checkoutBranch(repository ?? '', workspace ?? ''),
    queryFn: () => call<CheckoutBranch>('CHECKOUT_BRANCH', {
      repository: repository!,
      ...(workspace ? { workspace } : {}),
    }),
    enabled: isAvailable && Boolean(repository),
    staleTime: 0,
  });
  return { available: isAvailable, query };
};

/**
 * The person's *Try again* on a remote held because it could not authenticate (GITAUTH1a, D198; the git auth design §5.2): the holds of
 * these repositories lifted, in the row's workspace where it has one, so the next act that skipped them connects once. It answers how
 * many it lifted; the attention slice wakes itself, so the row goes as .NET judges it again, and this hook asks nothing again.
 */
export const useGitTryAgain = () =>
  useMutation({
    mutationFn: ({ repositories, workspace }: { repositories: readonly string[]; workspace?: string }) =>
      call<{ lifted: number }>('GIT_TRY_AGAIN', { repositories: [...repositories], ...(workspace ? { workspace } : {}) }),
  });

/** What `GIT_REF_CREATE`, `GIT_REF_DELETE`, `GIT_REF_RENAME`, `GIT_REF_FINISH` and `GIT_REF_UNDO` answer (GITAPP2c to e): the journal's act and its outcome, or a refusal as data with the driver's sentence. */
export type GitRefAnswer = { act?: string | null; outcome?: string | null; refused: boolean; said: string; code?: string | null };

/**
 * The person's *New branch* and *New tag* on a repository, and the undo of the ref one made (GITAPP2c, D204): the driver runs each
 * as an act of its journal and answers it as data. The Git reads sit under no state scope, so a made or undone ref asks the branch
 * list and the graph again here.
 */
export const useGitRefActs = (repository: string, workspace: string) => {
  const client = useQueryClient();
  const base = { repository, ...(workspace ? { workspace } : {}) };
  const refresh = () => client.invalidateQueries({ queryKey: ['git'] });
  const make = useMutation({
    mutationFn: (ask: { kind: 'branch' | 'tag'; name: string; at: string }) => call<GitRefAnswer>('GIT_REF_CREATE', { ...base, ...ask }),
    onSuccess: refresh,
  });
  const undo = useMutation({
    mutationFn: (act: string) => call<GitRefAnswer>('GIT_REF_UNDO', { ...base, act }),
    onSuccess: refresh,
  });
  // GITAPP2d: a branch deleted at the commit the list showed; the driver refuses it where it stands elsewhere.
  const remove = useMutation({
    mutationFn: (ask: { kind: 'branch' | 'tag'; name: string; at: string }) => call<GitRefAnswer>('GIT_REF_DELETE', { ...base, ...ask }),
    onSuccess: refresh,
  });
  // GITAPP2e: a branch renamed at the commit the list showed, and a rename that stopped before its settings moved finished.
  const rename = useMutation({
    mutationFn: (ask: { kind: 'branch' | 'tag'; name: string; to: string; at: string }) => call<GitRefAnswer>('GIT_REF_RENAME', { ...base, ...ask }),
    onSuccess: refresh,
  });
  const finish = useMutation({
    mutationFn: (act: string) => call<GitRefAnswer>('GIT_REF_FINISH', { ...base, act }),
    onSuccess: refresh,
  });
  // GITAPP2f: origin's branches fetched into the tracking refs alone; the answer's `notes` name what was made, moved and removed.
  const fetchOrigin = useMutation({
    mutationFn: () => call<GitFetchAnswer>('GIT_FETCH', base),
    onSuccess: refresh,
  });
  return {
    make: make.mutateAsync, undo: undo.mutateAsync, remove: remove.mutateAsync, rename: rename.mutateAsync, finish: finish.mutateAsync,
    fetchOrigin: fetchOrigin.mutateAsync,
  };
};

/** What `GIT_FETCH` answers (GITAPP2f): a ref act's answer, and in `notes` the refs the fetch made, moved and removed. */
export type GitFetchAnswer = GitRefAnswer & { notes?: string | null };

/**
 * Whether *Fetch origin* is offered on a repository's Branches (GITAPP2f, D204, D198): the driver's git doors installed, an origin, none of its
 * settings that run another program. `reason` says why not in a word (`doors`, `no-origin`, `widened`, `unread`); `address` is origin's as the
 * driver redacts it; `command` is what a person types.
 */
export type GitFetchPlan = { offered: boolean; reason: string | null; why: string | null; address: string | null; command: string };


/** A history edit asked of a branch (GITAPP2m): the commit or `A..B` range to pick or revert onto it, or the commit to rebase it onto. */
export type GitHistoryAsk = { edit: 'pick' | 'revert' | 'rebase'; branch: string; target: string };

/** The ids a shown plan pinned, which the edit's press carries back. */
export type GitHistoryPins = { tip: string; onto: string; commits: string[] };

/** What `GIT_HISTORY_EDIT`'s plan form answers: the branch's tip, the commit it builds on and the commits it takes, or a refusal in words. */
export type GitHistoryPlanAnswer = {
  refused: boolean;
  said: string;
  plan?: { edit: string; branch: string; tip: string; onto: string; commits: { id: string; subject: string }[]; commands: string[] } | null;
  code?: string | null;
};

/** What `GIT_HISTORY_EDIT`'s press answers: the journal's act and its outcome, the branch's new tip and its old one's recovery ref, or a refusal. */
export type GitHistoryAnswer = {
  act?: string | null; outcome?: string | null; refused: boolean; said: string; tip?: string | null; kept?: string | null; code?: string | null;
};

/**
 * A branch's *Edit history* (GITAPP2m, D204): the plan asked first (`plan: true`, which changes nothing, through a mutation since the
 * route declares the press's change), then the edit carrying the ids the plan pinned, which the driver runs as an act of its journal.
 * The Git reads sit under no state scope, so an edit asks the branch list and the graph again here.
 */
export const useGitHistoryEdits = (repository: string, workspace: string) => {
  const client = useQueryClient();
  const base = { repository, ...(workspace ? { workspace } : {}) };
  const plan = useMutation({
    mutationFn: (ask: GitHistoryAsk) => call<GitHistoryPlanAnswer>('GIT_HISTORY_EDIT', { ...base, ...ask, plan: true }),
  });
  const edit = useMutation({
    mutationFn: ({ pins, ...ask }: GitHistoryAsk & { pins: GitHistoryPins }) => call<GitHistoryAnswer>('GIT_HISTORY_EDIT', { ...base, ...ask, pins }),
    onSuccess: () => client.invalidateQueries({ queryKey: ['git'] }),
  });
  return { plan: plan.mutateAsync, edit: edit.mutateAsync };
};

/** A page of the graph from tips named by their whole ids (the list's `commit`); asks nothing with no tip. */
export const useGitGraph = (repository: string | null, tips: readonly string[], options: { page?: number; sinceParted?: boolean; workspace?: string } = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery(graphPage(isAvailable, repository, tips, options.page ?? 0, options));
  return { available: isAvailable, query };
};

/**
 * Pages `0..count-1` of the graph from the same tips, each its own cached read, in order (UX6h3a): a person reading further back
 * asks one more, and the pages already read are kept. The holder accumulates their commits.
 */
export const useGitGraphPages = (repository: string | null, tips: readonly string[], count: number, options: { sinceParted?: boolean; workspace?: string } = {}) => {
  const { isAvailable } = useShenora();
  const queries = useQueries({ queries: Array.from({ length: Math.max(1, count) }, (_, page) => graphPage(isAvailable, repository, tips, page, options)) });
  return { available: isAvailable, queries };
};

const graphPage = (isAvailable: boolean, repository: string | null, tips: readonly string[], page: number, options: { sinceParted?: boolean; workspace?: string }) => {
  const sinceParted = Boolean(options.sinceParted);
  const workspace = options.workspace ?? '';
  return {
    queryKey: keys.gitGraph(repository ?? '', tips, page, sinceParted, workspace),
    queryFn: () => call<GitAnswer<GitGraphPage>>('GIT_GRAPH', {
      repository, tips, ...(page ? { page } : {}), ...(sinceParted ? { sinceParted: true } : {}), ...(workspace ? { workspace } : {}),
    }),
    enabled: isAvailable && Boolean(repository) && tips.length > 0,
  };
};

/** A commit's page by its whole id. Its answer cannot change, so it is kept. */
export const useGitCommit = (repository: string | null, commit: string | null, workspace = '') => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.gitCommit(repository ?? '', commit ?? '', workspace),
    queryFn: () => call<GitAnswer<GitCommitDetail>>('GIT_COMMIT', { repository, commit, ...(workspace ? { workspace } : {}) }),
    enabled: isAvailable && Boolean(repository) && Boolean(commit),
    staleTime: Infinity,
  });
  return { available: isAvailable, query };
};

/** A file's history at a commit, following renames, a page at a time. */
export const useGitHistory = (repository: string | null, commit: string | null, path: string | null, page = 0, workspace = '') => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.gitHistory(repository ?? '', commit ?? '', path ?? '', page, workspace),
    queryFn: () => call<GitAnswer<GitFileHistory>>('GIT_HISTORY', {
      repository, commit, path, ...(page ? { page } : {}), ...(workspace ? { workspace } : {}),
    }),
    enabled: isAvailable && Boolean(repository) && Boolean(commit) && Boolean(path),
    staleTime: Infinity,
  });
  return { available: isAvailable, query };
};

/** A file as a commit holds it, each line with the commit that last changed it. */
export const useGitBlame = (repository: string | null, commit: string | null, path: string | null, workspace = '') => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.gitBlame(repository ?? '', commit ?? '', path ?? '', workspace),
    queryFn: () => call<GitAnswer<GitBlame>>('GIT_BLAME', { repository, commit, path, ...(workspace ? { workspace } : {}) }),
    enabled: isAvailable && Boolean(repository) && Boolean(commit) && Boolean(path),
    staleTime: Infinity,
  });
  return { available: isAvailable, query };
};

/** One file as a diff names it (GITAPP2b): `A` · `M` · `D` · `T`, with the mode and blob id of each side (all zeros where a side holds none). */
export type ChangeEntry = { path: string; status: string; oldMode: string; newMode: string; oldBlob: string; newBlob: string };

/** A file the index holds in conflict: status's two letters, and the mode and blob id of stages 1, 2 and 3. */
export type UnmergedEntry = { path: string; code: string; modes: string[]; blobs: string[] };

/**
 * What a checkout's changes are (GITAPP2b), three observations made at `readAt`: HEAD → index, index → worktree (the index not
 * refreshed), and status's untracked and unmerged entries. A staged addition whose file was then removed is in both lists.
 */
export type CheckoutChanges = {
  head: string | null;
  staged: ChangeEntry[];
  unstaged: ChangeEntry[];
  untracked: string[];
  unmerged: UnmergedEntry[];
  readAt: string;
};

// Told by event (NOBLOCK1): a pending answer is none yet, which the query holds as null and the tab says as reading.
const readChanges = (raw: unknown): GitAnswer<CheckoutChanges> | null => {
  const answer = raw as Partial<GitAnswer<CheckoutChanges>> | null;
  if (!answer || typeof answer !== 'object' || !('value' in answer)) return null;
  const value = answer.value;
  return {
    value: value && Array.isArray(value.staged) && Array.isArray(value.unstaged)
      ? { ...value, untracked: value.untracked ?? [], unmerged: value.unmerged ?? [] }
      : null,
    refusal: answer.refusal ?? null,
    problem: answer.problem ?? null,
    commands: answer.commands ?? [],
    remembered: answer.remembered === true,
  };
};

/**
 * A checkout's changes, read again at every open, window focus and *Refresh* (GITAPP2b, the git-writes design §1): never fresh
 * enough to keep, so a mount asks again whatever it held, and what the shell holds until its fresh read lands carries its own `readAt`.
 * `askedAt` is stamped as each ask goes out, before its answer can be read, so a read older than it is the held one and never the
 * fresh read itself.
 */
export const useGitChanges = (repository: string | null, workspace = '', enabled = true) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const [askedAt, setAskedAt] = useState(() => Date.now());
  const queryKey = keys.gitChanges(repository ?? '', workspace);
  const query = useQuery({
    queryKey,
    queryFn: () => {
      setAskedAt(Date.now());
      return askHeld('GIT_CHANGES', client, queryKey, { repository, ...(workspace ? { workspace } : {}) }, readChanges);
    },
    enabled: isAvailable && Boolean(repository) && enabled,
    staleTime: 0,
    refetchOnMount: 'always',
    refetchOnWindowFocus: 'always',
  });
  return { available: isAvailable, query, askedAt };
};

/** One line of a hunk: `' '` context, `'+'` added, `'-'` removed, `'\\'` git's no-newline marker. */
export type GitHunkLine = { kind: string; text: string };

/** A hunk as git prints it; its `id` names its text, so a press can say which hunk it meant. */
export type GitHunk = { id: string; header: string; lines: GitHunkLine[] };

/**
 * One file's hunks on a side (GITAPP2h): `unstaged` is the index → worktree, `staged` HEAD → index. `head`, `index` (`mode blob`, or
 * empty) and `source` are what a press must find unchanged; the page sends them back unread.
 */
export type GitFileHunks = { path: string; side: 'unstaged' | 'staged'; head: string | null; index: string; source: string; binary: boolean; hunks: GitHunk[] };

const readHunks = (raw: unknown): GitAnswer<GitFileHunks> | null => {
  const answer = raw as Partial<GitAnswer<GitFileHunks>> | null;
  if (!answer || typeof answer !== 'object' || !('value' in answer)) return null;
  const value = answer.value;
  return {
    value: value && Array.isArray(value.hunks) ? { ...value, head: value.head ?? null, index: value.index ?? '', source: value.source ?? '' } : null,
    refusal: answer.refusal ?? null,
    problem: answer.problem ?? null,
    commands: answer.commands ?? [],
    remembered: answer.remembered === true,
  };
};

const hunksKey = (repository: string, workspace: string, path: string, side: string) => [...keys.gitChanges(repository, workspace), 'hunks', path, side] as const;

/** A file's hunks on a side, read when asked for and read again with the Changes tab (they sit under its key). */
export const useGitHunks = (repository: string | null, workspace: string, path: string, side: 'unstaged' | 'staged', enabled = true) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = hunksKey(repository ?? '', workspace, path, side);
  return useQuery({
    queryKey,
    queryFn: () => askHeld('GIT_HUNKS', client, queryKey, { repository, path, side, ...(workspace ? { workspace } : {}) }, readHunks),
    enabled: isAvailable && Boolean(repository) && enabled,
    staleTime: 0,
    refetchOnMount: 'always',
  });
};

/** One conflict of a file in conflict (GITAPP2j): what ours and theirs hold there, and a line or two around it; `id` names it for a press. */
export type GitConflictHunk = { id: string; before: string[]; ours: string[]; theirs: string[]; after: string[] };

/**
 * A file the index holds in conflict, read by its stages (GITAPP2j): `base`, `ours` and `theirs` are `mode blob` (empty where a stage is
 * missing) and `worktree` the file's id, which a press sends back unread. `why` is set, with no hunks, where it cannot be taken by hunk.
 */
export type GitFileConflict = {
  path: string; head: string | null; base: string; ours: string; theirs: string; worktree: string; why: string | null; hunks: GitConflictHunk[];
};

const readConflict = (raw: unknown): GitAnswer<GitFileConflict> | null => {
  const answer = raw as Partial<GitAnswer<GitFileConflict>> | null;
  if (!answer || typeof answer !== 'object' || !('value' in answer)) return null;
  const value = answer.value;
  return {
    value: value && Array.isArray(value.hunks) ? { ...value, head: value.head ?? null, why: value.why ?? null } : null,
    refusal: answer.refusal ?? null,
    problem: answer.problem ?? null,
    commands: answer.commands ?? [],
    remembered: answer.remembered === true,
  };
};

const conflictKey = (repository: string, workspace: string, path: string) => [...keys.gitChanges(repository, workspace), 'conflict', path] as const;

/** A conflicted file's conflicts, read when asked for and read again with the Changes tab (they sit under its key). */
export const useGitConflict = (repository: string | null, workspace: string, path: string, enabled = true) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = conflictKey(repository ?? '', workspace, path);
  return useQuery({
    queryKey,
    queryFn: () => askHeld('GIT_CONFLICT', client, queryKey, { repository, path, ...(workspace ? { workspace } : {}) }, readConflict),
    enabled: isAvailable && Boolean(repository) && enabled,
    staleTime: 0,
    refetchOnMount: 'always',
  });
};

/** A resolve or a mark as pressed: the file, the stages and worktree id it was read against, and (for a resolve) a take for every conflict. */
export type GitResolvePress = {
  verb: 'resolve' | 'mark';
  path: string;
  faces?: Pick<GitFileConflict, 'head' | 'base' | 'ours' | 'theirs' | 'worktree'>;
  picks?: { id: string; take: 'ours' | 'theirs' | 'both' }[];
};

/** What a hunk or a file pressed: whole file with no pick, else the hunks (and lines of them, by index) chosen. */
export type GitActPick = { id: string; lines?: number[] };

/** What the driver answered an act (GITAPP2h): done, or refused in its sentence; `act` is what Undo takes. */
export type GitActAnswer = { done: boolean; refused: boolean; act: string | null; outcome: string | null; said: string | null; notes?: string | null };

export type GitActPress = {
  verb: 'stage' | 'unstage';
  path: string;
  /** The index entry as the list showed it (`mode blob`, empty where none): a file pressed whole is refused where it no longer is. */
  shown: string;
  picks: GitActPick[];
  /** What the hunks were read against; absent, the file's are read at the press. */
  faces?: Pick<GitFileHunks, 'head' | 'index' | 'source'>;
};

const readAct = (raw: unknown): GitActAnswer => {
  const answer = (raw ?? {}) as Partial<GitActAnswer>;
  return {
    done: answer.done === true, refused: answer.refused === true, act: answer.act ?? null, outcome: answer.outcome ?? null, said: answer.said ?? null,
    notes: answer.notes ?? null,
  };
};

/**
 * What a commit or an amend would rest on (GITAPP2i): the tip and the index as a tree, which a press sends back, and what the page says
 * beside the box: the hooks the git runs, never skipped, and who commits. `tree` is null, with `why`, where a file in conflict stands in the index.
 */
export type GitCommitPlan = {
  head: string | null;
  branch: string | null;
  tree: string | null;
  why: string | null;
  empty: boolean;
  /** HEAD's message, which an amend starts from; null on a branch with no commit. */
  message: string | null;
  hooks: string[];
  identity: string | null;
};

const readPlan = (raw: unknown): GitAnswer<GitCommitPlan> | null => {
  const answer = raw as Partial<GitAnswer<GitCommitPlan>> | null;
  if (!answer || typeof answer !== 'object' || !('value' in answer)) return null;
  const value = answer.value;
  return {
    value: value && typeof value === 'object'
      ? { ...value, head: value.head ?? null, branch: value.branch ?? null, tree: value.tree ?? null, why: value.why ?? null, message: value.message ?? null, hooks: value.hooks ?? [], identity: value.identity ?? null }
      : null,
    refusal: answer.refusal ?? null,
    problem: answer.problem ?? null,
    commands: answer.commands ?? [],
    remembered: answer.remembered === true,
  };
};

const planKey = (repository: string, workspace: string, verb: string) => [...keys.gitChanges(repository, workspace), 'commit-plan', verb] as const;

/** The commit's plan for a verb, read again with the Changes tab (it sits under its key) and whenever the page asks. */
export const useGitCommitPlan = (repository: string | null, workspace: string, verb: 'commit' | 'amend') => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = planKey(repository ?? '', workspace, verb);
  return useQuery({
    queryKey,
    queryFn: () => askHeld('GIT_COMMIT_PLAN', client, queryKey, { repository, verb, ...(workspace ? { workspace } : {}) }, readPlan),
    enabled: isAvailable && Boolean(repository),
    staleTime: 0,
    refetchOnMount: 'always',
  });
};

/** A commit or an amend as pressed: the message, and the tip and tree of the plan the page showed. */
export type GitCommitPress = { verb: 'commit' | 'amend'; message: string; head: string | null; tree: string };

/** Commit and amend (GITAPP2i, the git-writes design §8): one act at a time, refusals and a hook's refusal as data in the answer. */
export const useGitCommitAct = (repository: string | null, workspace: string) =>
  useMutation({
    mutationFn: async (press: GitCommitPress): Promise<GitActAnswer> =>
      readAct(await call(press.verb === 'commit' ? 'GIT_MAKE_COMMIT' : 'GIT_AMEND', {
        repository, ...(workspace ? { workspace } : {}), message: press.message, head: press.head, tree: press.tree,
      })),
  });

/**
 * What a switch to a branch would rest on (GITAPP2k): where HEAD is, whether tracked files are changed, the folder of another tree that has the
 * branch out and, in the driver's sentence, why it would be refused. A press sends back the branch HEAD named and its commit.
 */
export type GitSwitchPlan = {
  from: string | null;
  head: string | null;
  to: string;
  toHead: string | null;
  dirty: boolean;
  elsewhere: string | null;
  why: string | null;
  hooks: string[];
};

/** What a stash Save would rest on (GITAPP2k): the changes, the tip of `refs/stash`, and how many other trees share that stash. */
export type GitStashPlan = {
  head: string | null;
  branch: string | null;
  stash: string | null;
  tracked: number;
  untracked: number;
  sharedWith: number;
  why: string | null;
  hooks: string[];
  identity: string | null;
  /** The shared stash newest first, each by its commit id (GITAPP2l); `collides` counts its paths already standing untracked here. */
  entries: { id: string; message: string; untracked: boolean; collides: number }[];
  /** Why no stash could be applied now, in the driver's sentence; null where one could. */
  applyWhy: string | null;
};

const readPlanOf = <T>(fill: (value: Partial<T>) => T) => (raw: unknown): GitAnswer<T> | null => {
  const answer = raw as Partial<GitAnswer<Partial<T>>> | null;
  if (!answer || typeof answer !== 'object' || !('value' in answer)) return null;
  return {
    value: answer.value && typeof answer.value === 'object' ? fill(answer.value) : null,
    refusal: answer.refusal ?? null,
    problem: answer.problem ?? null,
    commands: answer.commands ?? [],
    remembered: answer.remembered === true,
  };
};

const readSwitchPlan = readPlanOf<GitSwitchPlan>((value) => ({
  from: value.from ?? null, head: value.head ?? null, to: value.to ?? '', toHead: value.toHead ?? null, dirty: value.dirty === true,
  elsewhere: value.elsewhere ?? null, why: value.why ?? null, hooks: value.hooks ?? [],
}));

const readStashPlan = readPlanOf<GitStashPlan>((value) => ({
  head: value.head ?? null, branch: value.branch ?? null, stash: value.stash ?? null, tracked: value.tracked ?? 0, untracked: value.untracked ?? 0,
  sharedWith: value.sharedWith ?? 0, why: value.why ?? null, hooks: value.hooks ?? [], identity: value.identity ?? null,
  entries: value.entries ?? [], applyWhy: value.applyWhy ?? null,
}));

/** The switch's plan for a branch, read again with the Changes tab and whenever the page asks; not asked while no branch is chosen. */
export const useGitSwitchPlan = (repository: string | null, workspace: string, branch: string | null) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.gitChanges(repository ?? '', workspace), 'switch-plan', branch ?? ''] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('GIT_SWITCH_PLAN', client, queryKey, { repository, branch, ...(workspace ? { workspace } : {}) }, readSwitchPlan),
    enabled: isAvailable && Boolean(repository) && Boolean(branch),
    staleTime: 0,
    refetchOnMount: 'always',
  });
};

/** The stash's plan, read again with the Changes tab and whenever the page asks. */
export const useGitStashPlan = (repository: string | null, workspace: string) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.gitChanges(repository ?? '', workspace), 'stash-plan'] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('GIT_STASH_PLAN', client, queryKey, { repository, ...(workspace ? { workspace } : {}) }, readStashPlan),
    enabled: isAvailable && Boolean(repository),
    staleTime: 0,
    refetchOnMount: 'always',
  });
};

const readFetchPlan = readPlanOf<GitFetchPlan>((value) => ({
  offered: value.offered === true, reason: value.reason ?? null, why: value.why ?? null, address: value.address ?? null, command: value.command ?? '',
}));

/** The fetch's plan, read when the Branches tab opens and again after a fetch (GITAPP2f). */
export const useGitFetchPlan = (repository: string | null, workspace: string) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.gitChanges(repository ?? '', workspace), 'fetch-plan'] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('GIT_FETCH_PLAN', client, queryKey, { repository, ...(workspace ? { workspace } : {}) }, readFetchPlan),
    enabled: isAvailable && Boolean(repository),
    staleTime: 0,
    refetchOnMount: 'always',
  });
};

/** A switch as pressed: the branch, and the branch and commit HEAD stood on in the plan the page showed. */
export type GitSwitchPress = { branch: string; from: string | null; head: string | null };

/** *Switch* (GITAPP2k, the git-writes design §8): never forced; a refusal is data in the answer. Switching back is the same press on the branch it left. */
export const useGitSwitchAct = (repository: string | null, workspace: string) =>
  useMutation({
    mutationFn: async (press: GitSwitchPress): Promise<GitActAnswer> =>
      readAct(await call('GIT_SWITCH', { repository, ...(workspace ? { workspace } : {}), branch: press.branch, from: press.from, head: press.head })),
  });

/** A stash Save as pressed: untracked files too or not, an optional message, and the tip and stash the plan showed. */
export type GitStashPress = { untracked: boolean; message: string; head: string | null; stash: string | null };

/** *Save to the stash* (GITAPP2k): tracked files only unless `untracked`; refused where the shared stash moved since the plan. */
export const useGitStashAct = (repository: string | null, workspace: string) =>
  useMutation({
    mutationFn: async (press: GitStashPress): Promise<GitActAnswer> =>
      readAct(await call('GIT_STASH', {
        repository, ...(workspace ? { workspace } : {}), untracked: press.untracked, message: press.message, head: press.head, stash: press.stash,
      })),
  });

/** A stash Apply as pressed: the stash's commit id (never its place on the list) and the commit HEAD stood on in the plan the page showed. */
export type GitStashApplyPress = { id: string; head: string | null };

/** *Apply a stash* (GITAPP2l): by commit id, never `pop`; the stash is kept and a conflict stays in the index for Resolve. */
export const useGitStashApplyAct = (repository: string | null, workspace: string) =>
  useMutation({
    mutationFn: async (press: GitStashApplyPress): Promise<GitActAnswer> =>
      readAct(await call('GIT_STASH_APPLY', { repository, ...(workspace ? { workspace } : {}), id: press.id, head: press.head })),
  });

/**
 * Stage and unstage by file, hunk or line, and Undo (GITAPP2h, the git-writes design §8). One act at a time: the driver's lock refuses a
 * second, and says so. A refusal is data in the answer, never an error; the state hub reads the Changes tab again.
 */
export const useGitActs = (repository: string | null, workspace: string) => {
  const client = useQueryClient();
  const scope = { repository, ...(workspace ? { workspace } : {}) };
  const press = useMutation({
    mutationFn: async (act: GitActPress): Promise<GitActAnswer & { stale?: boolean }> => {
      const side = act.verb === 'stage' ? 'unstaged' : 'staged';
      let faces = act.faces;
      if (!faces) {
        const key = hunksKey(repository ?? '', workspace, act.path, side);
        const read = await client.fetchQuery({
          queryKey: key, staleTime: 0,
          queryFn: () => askHeld('GIT_HUNKS', client, key, { ...scope, path: act.path, side }, readHunks),
        });
        faces = read?.value ?? undefined;
        if (!faces) return { done: false, refused: true, act: null, outcome: null, said: read?.problem ?? null };
        if (faces.index !== act.shown) return { done: false, refused: true, act: null, outcome: null, said: null, stale: true };
      }
      const payload = { ...scope, path: act.path, head: faces.head, index: faces.index, source: faces.source, picks: act.picks };
      return readAct(act.verb === 'stage' ? await call('GIT_STAGE', payload) : await call('GIT_UNSTAGE', payload));
    },
  });
  const undo = useMutation({ mutationFn: async (act: string) => readAct(await call('GIT_UNDO', { ...scope, act })) });
  // GITAPP2j: Resolve and Mark resolved of a file in conflict. A mark reads the file's stages and id when it is pressed, as the person marks it
  // once it stands as they want it; a resolve sends the faces its conflicts were read against.
  const resolve = useMutation({
    mutationFn: async (act: GitResolvePress): Promise<GitActAnswer> => {
      let faces = act.faces;
      if (!faces) {
        const key = conflictKey(repository ?? '', workspace, act.path);
        const read = await client.fetchQuery({
          queryKey: key, staleTime: 0,
          queryFn: () => askHeld('GIT_CONFLICT', client, key, { ...scope, path: act.path }, readConflict),
        });
        faces = read?.value ?? undefined;
        if (!faces) return { done: false, refused: true, act: null, outcome: null, said: read?.problem ?? null };
      }
      const payload = { ...scope, path: act.path, head: faces.head, base: faces.base, ours: faces.ours, theirs: faces.theirs, worktree: faces.worktree };
      return readAct(act.verb === 'resolve' ? await call('GIT_RESOLVE', { ...payload, picks: act.picks ?? [] }) : await call('GIT_MARK_RESOLVED', payload));
    },
  });
  return { press, undo, resolve };
};

/** Two commits compared: the commits only one side holds, and the changes (`everything` reads A..B, not A...B). */
export const useGitCompare = (repository: string | null, left: string | null, right: string | null, everything = false, workspace = '') => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: keys.gitCompare(repository ?? '', left ?? '', right ?? '', everything, workspace),
    queryFn: () => call<GitAnswer<GitCompare>>('GIT_COMPARE', {
      repository, left, right, ...(everything ? { everything: true } : {}), ...(workspace ? { workspace } : {}),
    }),
    enabled: isAvailable && Boolean(repository) && Boolean(left) && Boolean(right),
    staleTime: Infinity,
  });
  return { available: isAvailable, query };
};
