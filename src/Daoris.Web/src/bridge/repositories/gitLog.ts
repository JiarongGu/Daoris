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
