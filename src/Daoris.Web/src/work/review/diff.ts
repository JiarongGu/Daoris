// The shape of a review (SURF6), in its own module for the reason `caption.ts` is: the components
// that RENDER a diff are presentational and may not import `./shell`, and the hook that FETCHES one
// lives there. A type-only import would still cross that line — the presentational check reads
// imports, not their erasure — and it would be right to, because a molecule that knows where its
// data comes from is one refactor away from fetching it.

import type { Session } from '../../data/api';
import { sessionOrigin, treeName } from '../identity';
import { readEvidence } from '../head/timeline';

/**
 * How long the page waits for a review, in minutes (REVIEW4): the bridge's own 30 seconds stopped waiting for a diff the
 * host took most of a minute to read, and the host bounds no git call of a review, so this is the page's patience, not a
 * twin of a host bound. The review's sentence for a wait that ran out names it.
 */
export const REVIEW_BOUND_MINUTES = 3;

/**
 * What the page knows of a session before its review answers (REVIEW4): read from the record, never from git, so the
 * review's frame stands at once while git reads the range — which took most of a minute on a session of seventy files.
 */
export type ReviewKnown = {
  /** The session's title, or null where something above the review already names it. */
  title: string | null;
  repository: string | null;
  /**
   * Its own branch, where it holds a tree of its own here: `daoris/` and the tree's folder, as the trees name it. The record's guess,
   * which {@link reviewBranch} replaces once the review answers with the branch the tree stands on (REVIEWBASE4).
   */
  branch: string | null;
  /** The commit the record says its tree stood at when it began. */
  base: string | null;
  /** How many commits its evidence lists, or null where it has listed none yet. */
  commits: number | null;
  /** The machine it ran on, where that is not this one. */
  machine: string | null;
};

/**
 * The review's frame from a session's record (REVIEW4). The branch only where the tree is its own: a session in the
 * repository's checkout has no branch of its own to name. The commits are the evidence's `git log` lines, read by shape
 * as the timeline reads them; none listed is left unsaid, since *nothing landed* is the review's answer to give.
 */
export function reviewKnown(session: Session, { title, ownTree }: { title: string | null; ownTree: boolean }): ReviewKnown {
  const folder = ownTree ? treeName(session.tree) : null;
  const commits = session.evidence ? readEvidence(session.evidence).commits.length : 0;
  return {
    title,
    repository: session.repository,
    branch: folder ? `daoris/${folder}` : null,
    base: session.baseCommit || null,
    commits: commits > 0 ? commits : null,
    machine: sessionOrigin(session),
  };
}

/**
 * The branch the review's head names (REVIEWBASE4, D164's note): the record's guess until the review answers with the branch its
 * tree's HEAD stands on, since the agent may have switched it (TREEFOLLOW1), or deleted its `daoris/` branch and detached. Said only
 * where the record names a tree of its own, and only from the tree's own answer: a landed branch's review and a host older than
 * REVIEWBASE4 name none, so the guess stands. A detached tree names no branch, and says so.
 */
export function reviewBranch(
  known: Pick<ReviewKnown, 'branch'>,
  answer: Pick<SessionDiff, 'source' | 'treeBranch'> | undefined,
): { branch: string | null; detached: boolean } {
  if (!known.branch || !answer || (answer.source ?? 'tree') !== 'tree' || answer.treeBranch === undefined) {
    return { branch: known.branch, detached: false };
  }
  return answer.treeBranch === null ? { branch: null, detached: true } : { branch: answer.treeBranch, detached: false };
}

/** One file in a session's landed work, exactly as git described it. */
export type DiffFile = {
  path: string;
  /** `added` · `modified` · `deleted` · `renamed` · `copied`. */
  status: string;
  /** Null for a binary file: "not counted" is not "counted nothing". */
  added: number | null;
  removed: number | null;
  /** Null when the bound dropped this file's patch — the file itself is still listed. */
  patch: string | null;
};

/**
 * Where a landed session's work went (REVIEW2, D113), from this machine's landing record: the branch its landing
 * made, when, the pull request a plugin opened, and where that branch stands now.
 */
export type LandedWork = {
  branch: string;
  repository: string;
  /** The line it grew from, where the record names one. */
  line: string | null;
  /** When it landed, or null where the record did not say. */
  landedAt: string | null;
  /** The plugin that last pushed it, and whether it answered that it pushed. */
  plugin: string | null;
  pushed: boolean;
  pullRequest: string | null;
  /** `standing` · `gone` · `not-ours` (rebased or replaced since) · `no-checkout` (no repository here to read it in). */
  state: string;
  /**
   * Whether the review reads as landed: its branch stands, or its tree is gone. The host decides it, and the acts
   * follow it — no accepting again, no sending back; the hand-off where one applies.
   */
  asLanded: boolean;
  /** A branch gone since: whether its work reads on the line, by the clean-up's proof by content. */
  reads: { kind: string; where: string | null; files: string[]; detail: string | null } | null;
  /** What the clean-up proved when it removed the branch, where it did. */
  removed: { kind: string; where: string | null; at: string | null } | null;
  /** Why a standing branch's changes could not be read, in git's terms. */
  detail: string | null;
};

export type SessionDiff = {
  session: string;
  /**
   * The commit the range is measured from: the newest the work shares with its line, or the one its record says it began at where
   * that is newer (REVIEWBASE1). Empty where no range was read.
   */
  base: string;
  /** What the bound dropped, in the host's own words, or null when nothing was. */
  truncated: string | null;
  files: DiffFile[];
  /**
   * Where the files were read from (REVIEW2): the session's own tree, or its landed branch in the repository's
   * checkout once the tree cannot be read. A host older than REVIEW2 names none, which is the tree.
   */
  source?: 'tree' | 'branch';
  /** Where this session's work landed, where this machine's landing record holds it. */
  landed?: LandedWork | null;
  /**
   * What the session's own tree holds that no commit does (REVIEWDIRTY1, D164 point 4): the modified and the untracked,
   * each file as the committed ones are (status `added` for an untracked one, its patch empty where large or not text).
   * Absent or null where nothing is uncommitted or the files were read from a branch; a host older than REVIEWDIRTY1 names none.
   */
  uncommitted?: { files: DiffFile[]; truncated: boolean } | null;
  /**
   * How the session's own tree stands against the latest its line is known on this machine, the branch and its copy on origin
   * together, as the last fetch left them (REVIEWBASE1, D164's note). Absent or null where the files were read from a branch or the
   * line could not be read; a host older than REVIEWBASE1 names none.
   */
  behind?: LineBehind | null;
  /**
   * The branch the session's own tree stands on, as its HEAD names it (REVIEWBASE4, D164's note): null where HEAD is detached, and
   * where the files were read from a branch. Absent from a host older than REVIEWBASE4, whose head says the record's guess.
   */
  treeBranch?: string | null;
  /**
   * What the driver would land of the session's own tree (ACCEPTFOOT1, D164's note): LAND4's offer as `SESSION_GROUPS` says it, by
   * D88's proof and SQUASHTIDY1b's content hold, read where the work is beyond the line and nothing landed it. Null where it offers
   * none; absent from a host before it.
   */
  offer?: { branch: string; tree: string; commits: number; uncommitted: number | null } | null;
};

/** The line's name, the commits it has that the tree does not, and the commits the tree has beyond it (REVIEWBASE1). */
export type LineBehind = { line: string; commits: number; ahead: number };
