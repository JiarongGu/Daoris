import type { TFunction } from 'i18next';
import type { ListedBranch, ListedLanded } from './BranchList';

// How a branch stands, in the words a repository's Branches says it (UX6h2a) and its branch page says it again (UX6h3b): one
// set of sentences, so the list and the page never say the same fact two ways (the built-in Git design §2.2, §2.3).

const KINDS = new Set(['line', 'session', 'landed', 'origin']);

/** A branch's kind: a kind the page has no word for is the person's, as a branch the record no longer matches is (D113). */
export const kindOf = (branch: Pick<ListedBranch, 'kind'>) => (KINDS.has(branch.kind) ? branch.kind : 'yours');

/** How a branch stands to `than` by the commits each holds that the other lacks; null where git did not count. */
export function standing(t: TFunction, than: string, ahead: number | null, behind: number | null): string | null {
  if (ahead === null || behind === null) return null;
  if (ahead > 0 && behind > 0) return t('projects.git.branches.standing.diverged', { count: ahead, behind, than });
  if (ahead > 0) return t('projects.git.branches.standing.ahead', { count: ahead, than });
  if (behind > 0) return t('projects.git.branches.standing.behind', { count: behind, than });
  return t('projects.git.branches.standing.inStep', { than });
}

/** A landed branch to its own name on origin, as the driver's `GitOrigin` says it. */
export function landedStanding(t: TFunction, name: string, landed: ListedLanded): string {
  const than = `origin/${name}`;
  if (landed.origin === 'gone') return t('projects.git.branches.standing.gone');
  if (landed.origin === 'not-pushed') return t('projects.git.branches.standing.notPushed');
  if (landed.origin === 'in-step') return t('projects.git.branches.standing.inStep', { than });
  const counted = landed.origin === 'ahead' || landed.origin === 'behind' || landed.origin === 'diverged'
    ? standing(t, than, landed.originAhead, landed.originBehind)
    : null;
  return counted ?? t('projects.git.branches.standing.unknown', { than });
}

const PULL_REQUEST_SAID: Record<string, string> = {
  open: 'workflow.run.said.pull-request.merge.open',
  completed: 'workflow.run.said.pull-request.merged',
  abandoned: 'workflow.run.said.pull-request.abandoned',
};

/**
 * What a plugin last answered about the pull request, with when (§2.2, §2.4): an end's own time where it has one, else when
 * Daoris asked, never the year 1 a record that did not keep it reads as; then the ask that failed since, where one did.
 */
export function pullRequestSaid(t: TFunction, landed: ListedLanded): { sentence: string; at: string | null }[] {
  const said: { sentence: string; at: string | null }[] = [];
  const answer = landed.pullRequestState;
  if (answer) {
    const ended = answer.state === 'completed' || answer.state === 'abandoned' ? answer.at : null;
    said.push({
      sentence: t(PULL_REQUEST_SAID[answer.state] ?? 'workflow.run.said.pull-request.merge.unknown', { plugin: answer.plugin ?? '' }),
      at: ended ?? (Date.parse(answer.askedAt) > 0 ? answer.askedAt : null),
    });
  }
  const failed = landed.pullRequestAskFailed;
  if (failed) said.push({ sentence: t('workflow.run.said.pull-request.ask-failed', { plugin: failed.plugin, code: failed.code }), at: failed.at });
  return said;
}
