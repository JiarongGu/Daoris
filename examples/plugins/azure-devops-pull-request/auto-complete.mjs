// Auto-complete on the pull request the `pull-request` step opened or found: set where the step's options ask for it, by the token's own
// identity, so Azure DevOps merges it once its policies pass. Whatever happens, it is a sentence of the step's evidence: a refusal by a
// policy or by the token's permissions never fails the step, and only a platform that cannot be asked now throws, so the step is tried again.
import { Refusal, call, organization } from './azure.mjs';

const STRATEGIES = ['squash', 'noFastForward', 'rebase', 'rebaseMerge'];
const STRATEGY_WORDS = { noFastForward: 'merge', squash: 'squash', rebase: 'rebase', rebaseMerge: 'rebase-merge' };

const yes = (value) => value === true || value === 'true';

/** What the options ask: null where auto-complete is off, else its completion options or the problem that stops it. */
export function asked(options) {
  if (!yes(options?.autoComplete)) return null;
  const strategy = options.mergeStrategy ?? 'noFastForward';
  if (!STRATEGIES.includes(strategy)) return { problem: `merge strategy \`${String(strategy).slice(0, 40)}\` is none of ${STRATEGIES.join(', ')}` };
  return {
    mergeStrategy: strategy,
    deleteSourceBranch: yes(options.deleteSourceBranch),
    transitionWorkItems: yes(options.transitionWorkItems),
    ...(typeof options.mergeCommitMessage === 'string' && options.mergeCommitMessage.trim() ? { mergeCommitMessage: options.mergeCommitMessage } : {}),
  };
}

/** Auto-complete set on `pr` where `options` ask for it, said as the sentence the step's evidence ends with. */
export async function autoComplete(repo, pr, options) {
  const completion = asked(options);
  if (!completion) return 'Auto-complete off.';
  if (completion.problem) return `Auto-complete not set: ${completion.problem}.`;
  try {
    const me = (await organization(repo, 'GET', '/connectionData')).json?.authenticatedUser?.id;
    if (typeof me !== 'string') return 'Auto-complete not set: Azure DevOps named no identity for the token.';
    const set = await call(repo, 'PATCH', `/pullrequests/${pr.pullRequestId}`, { autoCompleteSetBy: { id: me }, completionOptions: completion });
    if (set.status !== 200) return `Auto-complete not set: Azure DevOps answered ${set.status}: ${set.json?.message ?? 'no reason given'}`;
    if (!set.json?.autoCompleteSetBy) return 'Auto-complete not set: Azure DevOps took the request, and the pull request shows none.';
    const how = [STRATEGY_WORDS[completion.mergeStrategy], ...(completion.deleteSourceBranch ? ['source branch deleted'] : [])].join(', ');
    return `Auto-complete set (${how}); its policies still decide when it merges.`;
  } catch (error) {
    if (error instanceof Refusal && error.code === 'unauthorized') return `Auto-complete not set: ${error.message}`;
    throw error;
  }
}
