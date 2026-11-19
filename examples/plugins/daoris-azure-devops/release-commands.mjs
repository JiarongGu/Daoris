// The release's read-only command, which a person or a planner calls to propose release targets: `discover-releases` lists, for a
// repository named by its origin's address, what each pipeline releases through (discover.mjs). It writes nothing and chooses nothing.
import { within } from './browser.mjs';
import { Refusal, azureOf, call, releases } from './azure.mjs';
import { discover, discoveryWords } from './discover.mjs';
import { ways } from './pull-request-commands.mjs';

export const DISCOVER = 'discover-releases';

const TELL = 'These are candidates only: declare the one each deployable releases on, by id, in its release node\'s options '
  + '(deployable, pipeline, releaseDefinition, stage, action, deploysOn); none is chosen here.';

async function discovered(args) {
  const repo = azureOf(args.origin);
  if (!repo) throw new Refusal('no-origin', `\`${args.origin}\` names no Azure DevOps repository`);
  const repositoryId = (await call(repo, 'GET', '')).json?.id ?? repo.repository;
  const pipeline = /^\d+$/.test(String(args.pipeline ?? '').trim()) ? Number(args.pipeline) : null;
  const found = await discover({ repo, repositoryId, pipeline });
  return { said: [discoveryWords(found), TELL], exit: 0, data: found };
}

export const APPROVE = 'approve-release';

/** The approver's act's fact, which its receipt names and the approval step its holder's act settles maps (D325). */
export const APPROVED = 'approved';

/** A round of the approval with its receipt as its data: reached where the stage's approval reads given, else failed with its code. */
const receipted = (said, exit, receipt) => ({ said: [said], exit, data: { receipt: { fact: APPROVED, ...receipt, message: said } } });

/** The stage's approval, read first and again after the act: who gave it, from the approvals Azure DevOps keeps. */
const givenBy = (approval) => `outside: ${approval?.approvedBy?.displayName ?? approval?.approver?.displayName ?? 'its approver'}`;

async function approved(args) {
  const repo = azureOf(args.origin);
  if (!repo) throw new Refusal('no-origin', `\`${args.origin}\` names no Azure DevOps repository`);
  const decision = args.decision === 'reject' ? 'rejected' : args.decision === 'approve' ? 'approved' : null;
  if (!decision) throw new Refusal('bad-args', 'the decision is `approve` or `reject`');
  const read = await releases(repo, 'GET', `/releases/${Number(args.release)}`);
  if (read.status !== 200 || read.json?.id === undefined) {
    const why = typeof read.json?.message === 'string' ? `: ${read.json.message.slice(0, 160)}` : '';
    return receipted(`Release ${args.release} was not found in Azure DevOps (${read.status})${why}; the id is the page's releaseId, not its name: nothing is changed.`, 1,
      { state: 'failed', code: 'no-release' });
  }
  const release = read.json;
  const page = release._links?.web?.href;
  const of = { release: release.id, ...(page ? { url: page } : {}) };
  const environment = (release?.environments ?? []).find((each) => String(each.definitionEnvironmentId) === String(args.stage) || each.name === args.stage);
  const approvals = environment?.preDeployApprovals ?? [];
  const pending = approvals.find((each) => each.status === 'pending');
  const given = approvals.findLast((each) => each.status === 'approved');
  // An approval already given, by anyone, is the fact read first: nothing is changed and the receipt says who gave it.
  if (!pending && given && decision === 'approved') {
    return receipted(`${environment.name} of ${release.name} approved by ${givenBy(given)}.`, 0, { state: 'reached', ...of, by: givenBy(given) });
  }
  if (!pending) {
    return receipted(`${release.name ?? `Release ${args.release}`} has no approval of ${args.stage} pending: nothing is changed.`, 1, { state: 'failed', code: 'not-pending' });
  }
  const set = await releases(repo, 'PATCH', `/approvals/${pending.id}`, { status: decision, comments: String(args.comment ?? '') });
  if (set.status !== 200) throw new Refusal('rejected', `Azure DevOps did not take the ${args.decision} (${set.status})`);
  const now = set.json?.status ?? decision;
  return now === 'approved'
    ? receipted(`${environment.name} of ${release.name} approved by Daoris.`, 0, { state: 'reached', ...of, by: 'Daoris' })
    : receipted(`${environment.name} of ${release.name} ${now}.`, 0, { state: 'failed', code: now, ...of });
}

export function approveCommand(args = {}) {
  return within(async () => {
    try {
      return ways(await approved(args));
    } catch (error) {
      if (error instanceof Refusal) return ways(receipted(error.message, 1, { state: 'failed', code: error.code }));
      throw error;
    }
  });
}

/** One round of `discover-releases`; a refusal said as its line, exit 1. */
export function discoverCommand(args = {}) {
  return within(async () => {
    try {
      return ways(await discovered(args));
    } catch (error) {
      if (error instanceof Refusal) return ways({ said: [error.message], exit: 1 });
      throw error;
    }
  });
}
