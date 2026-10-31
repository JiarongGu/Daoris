import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { call } from '../call';
import { askHeld } from '../state/held';

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

/** What `GIT_REF_CREATE`, `GIT_REF_DELETE` and `GIT_REF_UNDO` answer (GITAPP2c, d): the journal's act and its outcome, or a refusal as data with the driver's sentence. */
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
  return { make: make.mutateAsync, undo: undo.mutateAsync, remove: remove.mutateAsync };
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
 */
export const useGitChanges = (repository: string | null, workspace = '', enabled = true) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = keys.gitChanges(repository ?? '', workspace);
  const query = useQuery({
    queryKey,
    queryFn: () => askHeld('GIT_CHANGES', client, queryKey, { repository, ...(workspace ? { workspace } : {}) }, readChanges),
    enabled: isAvailable && Boolean(repository) && enabled,
    staleTime: 0,
    refetchOnMount: 'always',
    refetchOnWindowFocus: 'always',
  });
  return { available: isAvailable, query };
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

/** What a hunk or a file pressed: whole file with no pick, else the hunks (and lines of them, by index) chosen. */
export type GitActPick = { id: string; lines?: number[] };

/** What the driver answered an act (GITAPP2h): done, or refused in its sentence; `act` is what Undo takes. */
export type GitActAnswer = { done: boolean; refused: boolean; act: string | null; outcome: string | null; said: string | null };

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
  };
};

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
  return { press, undo };
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
