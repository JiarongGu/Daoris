// Watching the declared stage of a classic release or a pipeline run to its end. Azure DevOps' approvals and checks decide: a pending one
// is said and waited on.
import { Refusal, TRANSIENT, project, releases } from './azure.mjs';
import { SUCCESS } from './target.mjs';

const pause = (ms) => new Promise((done) => setTimeout(done, ms));

/** `read()` asked every `every` ms until it answers, or until `by` passes (undefined); a platform that cannot be asked now is asked again. */
export async function until(read, by, every, say) {
  for (;;) {
    try {
      const got = await read();
      if (got !== undefined) return got;
    } catch (error) {
      if (!(error instanceof Refusal) || !TRANSIENT.includes(error.code)) throw error;
      say(`${error.message}; asked again.`);
    }
    if (Date.now() + every > by) return undefined;
    await pause(every);
  }
}

const WELL = { succeeded: 'succeeded', partiallySucceeded: 'partly succeeded' };
const FAILED = ['rejected', 'canceled'];
const NOT_STARTED = ['notStarted', 'undefined'];
const UNDER_WAY = { inProgress: 'deploying', queued: 'queued', scheduled: 'scheduled' };

const approvalsOf = (environment) => [...(environment.preDeployApprovals ?? []), ...(environment.postDeployApprovals ?? [])];
const pendingOf = (environment) => approvalsOf(environment).find((approval) => approval.status === 'pending');
const whoOf = (approval) => approval?.approvedBy?.displayName ?? approval?.approver?.displayName ?? 'its approvers';
const sinceOf = (iso) => (Number.isFinite(Date.parse(iso ?? '')) ? ` since ${new Date(iso).toISOString().slice(0, 16).replace('T', ' ')} UTC` : '');

/** The actor whose act the stage waits on, in Azure DevOps and never here (an approval its approver's, an observed stage nothing started its deployer's), with the act; else null. */
export function waitOf(environment, target) {
  const name = environment.name;
  const pending = pendingOf(environment);
  if (pending) return { actor: 'approver', environment: name, act: `Approve ${name} in Azure DevOps: asked of ${whoOf(pending)}${sinceOf(pending.createdOn)}` };
  if (!NOT_STARTED.includes(environment.status) || target.action !== 'observe') return null;
  return { actor: 'deployer', environment: name, act: `Deploy ${name} in Azure DevOps: nothing started it, and the step observes it` };
}

/** The declared stage of a classic release in words: how it ended, or where it stands. */
function environmentWords(environment, seen, target) {
  const name = environment.name;
  if (WELL[environment.status]) {
    const after = seen.approval ? ` after an approval by ${seen.approval}` : '';
    const taken = SUCCESS[target.success].includes(environment.status) ? '' : `, and the step's success asks ${target.success}`;
    return `${seen.started ? `${name} started here,` : name} ${WELL[environment.status]}${after}${taken}`;
  }
  if (environment.status === 'rejected') {
    const refused = approvalsOf(environment).find((approval) => approval.status === 'rejected');
    return refused ? `${name} was rejected by ${whoOf(refused)}${refused.comments ? `: ${refused.comments}` : ''}` : `${name} failed its deployment`;
  }
  if (environment.status === 'canceled') return `${name} was canceled`;
  const pending = pendingOf(environment);
  if (pending) return `${name} waits on an approval by ${whoOf(pending)}`;
  if (NOT_STARTED.includes(environment.status)) return target.action === 'observe' ? `${name} not started: the step observes it, and start-stage would start it` : `${name} not started`;
  return `${name} ${UNDER_WAY[environment.status] ?? environment.status}`;
}

/**
 * The declared stage of release `id` watched until it ends or `ends` passes: `{ verdict: 'done' | 'failed' | 'timeout', words }`, or
 * `{ verdict: 'missing' }` where the release holds no such stage. Where the action starts it and nothing has, it is started here once, by
 * the token, for Azure DevOps' approvals and gates to decide. `tell` hears its first look and each change of what it waits on before it ends, once.
 */
export async function watchRelease(repo, id, target, ends, say, commit, tell = () => {}) {
  const seen = { started: false, approval: null };
  let last = null;
  let told;
  const verdict = await until(async () => {
    const release = (await releases(repo, 'GET', `/releases/${id}`)).json;
    if (!release) return undefined;
    const environment = (release.environments ?? []).find((each) => Number(each.definitionEnvironmentId) === target.stage);
    if (!environment) return { verdict: 'missing' };
    last = environment;
    const pending = pendingOf(environment);
    if (pending && !seen.approval) say(`${environment.name} waits on an approval by ${whoOf(pending)}.`);
    if (pending) seen.approval = whoOf(pending);
    const ended = WELL[environment.status] || FAILED.includes(environment.status);
    const waits = ended ? null : waitOf(environment, target);
    if (!ended && (told === undefined || (waits?.act ?? null) !== (told?.act ?? null))) {
      tell(waits ?? { environment: environment.name, act: `${environment.name} ${UNDER_WAY[environment.status] ?? environment.status}` });
      told = waits;
    }
    if (NOT_STARTED.includes(environment.status) && !seen.started && target.action !== 'observe') {
      const set = await releases(repo, 'PATCH', `/releases/${id}/environments/${environment.id}`,
        { status: 'inProgress', comment: `Started by Daoris for ${commit.slice(0, 8)}.`, scheduledDeploymentTime: null });
      if (set.status !== 200) throw new Refusal('rejected', `Azure DevOps did not start ${environment.name} (${set.status}): ${set.json?.message ?? 'no reason given'}`);
      seen.started = true;
      say(`started ${environment.name} of ${release.name}.`);
    }
    if (FAILED.includes(environment.status)) return { verdict: 'failed' };
    if (WELL[environment.status]) return { verdict: SUCCESS[target.success].includes(environment.status) ? 'done' : 'failed' };
    return undefined;
  }, ends, target.every, say);
  if (verdict?.verdict === 'missing') return verdict;
  return { verdict: verdict?.verdict ?? 'timeout', words: last ? environmentWords(last, seen, target) : 'its stage had not begun' };
}

const RESULTS = { succeeded: 'succeeded', succeededWithIssues: 'succeeded with issues', failed: 'failed', canceled: 'was canceled', abandoned: 'was abandoned', skipped: 'was skipped' };
const RAN = { succeeded: ['succeeded'], 'partially-succeeded': ['succeeded', 'succeededWithIssues'] };

/** Whether an approval not yet given stands under `stage` in a run's timeline. */
function awaitingApproval(records, stage) {
  const byId = new Map(records.map((record) => [record.id, record]));
  const under = (record) => {
    for (let at = record, hops = 0; at && hops < 8; at = byId.get(at.parentId), hops += 1) if (at.parentId === stage.id) return true;
    return false;
  };
  return records.some((record) => record.type === 'Checkpoint.Approval' && record.state !== 'completed' && under(record));
}

function stageWords(stage, records, waited) {
  if (stage.state === 'completed') return `${stage.name} ${RESULTS[stage.result] ?? stage.result ?? 'ended'}${RAN['partially-succeeded'].includes(stage.result) && waited ? ' after an approval' : ''}`;
  if (awaitingApproval(records, stage)) return `${stage.name} waits on an approval`;
  return `${stage.name} ${stage.state === 'inProgress' ? 'running' : 'not started'}`;
}

/** The declared stage of pipeline run `build` (its identifier, or its name) watched as `watchRelease` watches a release's. */
export async function watchRun(repo, build, target, ends, say) {
  let waited = false;
  let found = null;
  let records = [];
  const verdict = await until(async () => {
    const run = (await project(repo, 'GET', `/build/builds/${build.id}`)).json ?? build;
    records = (await project(repo, 'GET', `/build/builds/${build.id}/timeline`)).json?.records ?? [];
    found = records.find((record) => record.type === 'Stage' && (record.identifier === target.stage || record.name === target.stage)) ?? null;
    if (!found) return run.status === 'completed' ? { verdict: 'missing' } : undefined;
    if (!waited && awaitingApproval(records, found)) {
      waited = true;
      say(`${found.name} waits on an approval.`);
    }
    if (found.state === 'completed') return { verdict: RAN[target.success].includes(found.result) ? 'done' : 'failed' };
    return run.status === 'completed' ? { verdict: 'failed' } : undefined;
  }, ends, target.every, say);
  if (verdict?.verdict === 'missing') return verdict;
  return { verdict: verdict?.verdict ?? 'timeout', words: found ? stageWords(found, records, waited) : 'its stage had not begun' };
}
