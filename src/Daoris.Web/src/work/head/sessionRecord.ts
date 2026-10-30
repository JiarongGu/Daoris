/**
 * The record of the finished tree a session ran in (KEEPSIZE1), as `SESSION_RECORD` passes the service's answer through: what is kept
 * once the tree is let go. A shape the page reads, never builds.
 */
export type SessionRecord = {
  at: string;
  post: {
    branch: string;
    line?: string | null;
    tip: string;
    grewFrom?: string | null;
    /** The recovery ref holding the tip, where one was written. */
    ref?: string | null;
    hold: { kind: string; where?: string | null };
    landing?: { branch: string; line?: string | null; at: string; pullRequest?: string | null } | null;
    landingWhy?: string | null;
    /** Oldest first; null where the tip no longer read. */
    commits: { id: string; subject: string; files: { path: string; added: number; removed: number }[] }[] | null;
    commitsUnread?: string | null;
    left: { names: string[]; count: number; cut: boolean };
  };
};
