// The shape of a review (SURF6), in its own module for the reason `caption.ts` is: the components
// that RENDER a diff are presentational and may not import `./shell`, and the hook that FETCHES one
// lives there. A type-only import would still cross that line — the presentational check reads
// imports, not their erasure — and it would be right to, because a molecule that knows where its
// data comes from is one refactor away from fetching it.

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
  /** The commit the range is measured from — recorded at spawn, so it is a fact. Empty where no range was read. */
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
};
