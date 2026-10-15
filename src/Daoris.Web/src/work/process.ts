import type { TFunction } from 'i18next';
import { COMMIT_DEFAULT, commitRowSays, type RepositoryCommits } from '../settings/Commits';
import { landingRuleSays, type RepositoryLanding } from '../settings/Landings';

/**
 * The process a session's own tree runs under (PROCESS1e, D164's amendment point 3), as `SESSION_PROCESS` answers it: its
 * repository's commit rule and landing rule as this machine resolves them now, each with where it was set, in the rows `LINES`
 * answers. The shape lives beside the head that renders it (SURF6), so the molecule names it without the bridge.
 */
export type SessionProcess = { commits: RepositoryCommits; landing: RepositoryLanding };

/** What `SESSION_PROCESS` answers: the session, and its process; absent or null where it has no tree of its own here, told none. */
export type SessionProcessAnswer = { session: string; process?: SessionProcess | null };

/** How its work is committed and where that was set, in its Setup row's sentences. */
export function processCommitsSays(t: TFunction, process: SessionProcess): string {
  return commitRowSays(t, process.commits.rule ?? COMMIT_DEFAULT, { source: process.commits.source, workspace: process.commits.workspace });
}

/** How its work lands and where that was set, in its Setup row's sentences, words alone. */
export function processLandingSays(t: TFunction, process: SessionProcess): string {
  return landingRuleSays(t, process.landing).join(t('projects.setup.sentenceJoin'));
}
