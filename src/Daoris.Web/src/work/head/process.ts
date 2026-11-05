import type { TFunction } from 'i18next';
import { COMMIT_DEFAULT, commitRowSays, type RepositoryCommits } from '../../settings/repository/Commits';

/**
 * The process a session's own tree runs under, as `SESSION_PROCESS` answers it: its repository's commit rule as this machine
 * resolves it now, with where it was set, in the rows `LINES` answers. How its work lands is its run's bound `land`, which the run's
 * view says. The shape lives beside the head that renders it, so the molecule names it without the bridge.
 */
export type SessionProcess = { commits: RepositoryCommits };

/** What `SESSION_PROCESS` answers: the session, and its process; absent or null where it has no tree of its own here, told none. */
export type SessionProcessAnswer = { session: string; process?: SessionProcess | null };

/** How its work is committed and where that was set, in its Setup row's sentences. */
export function processCommitsSays(t: TFunction, process: SessionProcess): string {
  return commitRowSays(t, process.commits.rule ?? COMMIT_DEFAULT, { source: process.commits.source, workspace: process.commits.workspace });
}
