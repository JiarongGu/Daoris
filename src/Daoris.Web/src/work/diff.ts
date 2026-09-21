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

export type SessionDiff = {
  session: string;
  /** The commit the range is measured from — recorded at spawn, so it is a fact. */
  base: string;
  /** What the bound dropped, in the host's own words, or null when nothing was. */
  truncated: string | null;
  files: DiffFile[];
};
