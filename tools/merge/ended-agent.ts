/**
 * How long a harness-locked worktree must have had no write before the prune takes it. A worker may be in the tree with no
 * process naming its folder, so a worktree that shows no commit of its own waits the whole span; one whose agent has ended
 * (it made commits, its branch is merged into main, nothing runs inside it) waits only a short quiet.
 */
export const HARNESS_IDLE_MINUTES = 120;
export const ENDED_IDLE_MINUTES = 5;

/** The quiet a harness-locked worktree must show, by what is known of its agent. */
export const idleNeeded = (facts: { ended?: boolean }): number => (facts.ended ? ENDED_IDLE_MINUTES : HARNESS_IDLE_MINUTES);

/** Whether a worktree's HEAD reflog (its `%gs` subjects) holds a commit made in it: a dispatched agent that never committed holds none. */
export const madeCommit = (subjects: readonly string[]): boolean => subjects.some((subject) => /^commit\b/.test(subject));
