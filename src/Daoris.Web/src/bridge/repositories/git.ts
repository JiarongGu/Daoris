import { useMutation, useQueries, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { call } from '../call';

// Git's reads (GIT1d-b, D147 §2, D150): the screen's door to `daoris-driver git`, the same reads over the same checkouts, for
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
