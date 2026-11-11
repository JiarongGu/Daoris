// Watching a release to its end: a classic release's stages (its environments) or a pipeline run's own stages, asked every so often until
// each awaited stage ends or the watch's time runs out. Azure DevOps' approvals and checks decide: a pending one is said and waited on.
import { Refusal, TRANSIENT, project, releases } from './azure.mjs';

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

export const same = (a, b) => String(a ?? '').trim().toLowerCase() === String(b ?? '').trim().toLowerCase();

/** The stages awaited, in the order they stand: those `named`, else all; and each name no stage answers to. */
function awaited(stages, named, namesOf) {
  if (named.length === 0) return { list: stages, missing: [] };
  const answers = (stage, name) => namesOf(stage).some((each) => same(each, name));
  return { list: stages.filter((stage) => named.some((name) => answers(stage, name))), missing: named.filter((name) => !stages.some((stage) => answers(stage, name))) };
}

const WELL = { succeeded: 'succeeded', partiallySucceeded: 'partly succeeded' };
const FAILED = ['rejected', 'canceled'];
const NOT_STARTED = ['notStarted', 'undefined'];
const UNDER_WAY = { inProgress: 'deploying', queued: 'queued', scheduled: 'scheduled' };

const approvalsOf = (environment) => [...(environment.preDeployApprovals ?? []), ...(environment.postDeployApprovals ?? [])];
const pendingOf = (environment) => approvalsOf(environment).find((approval) => approval.status === 'pending');
const whoOf = (approval) => approval?.approvedBy?.displayName ?? approval?.approver?.displayName ?? 'its approvers';

/** A classic release's stage in words: how it ended, or where it stands. */
function environmentWords(environment, all, seen, deploy) {
  const name = environment.name;
  if (WELL[environment.status]) {
    const after = seen.approvals.has(name) ? ` after an approval by ${seen.approvals.get(name)}` : '';
    return `${seen.started.has(name) ? `${name} started here,` : name} ${WELL[environment.status]}${after}`;
  }
  if (environment.status === 'rejected') {
    const refused = approvalsOf(environment).find((approval) => approval.status === 'rejected');
    return refused ? `${name} was rejected by ${whoOf(refused)}${refused.comments ? `: ${refused.comments}` : ''}` : `${name} failed its deployment`;
  }
  if (environment.status === 'canceled') return `${name} was canceled`;
  const pending = pendingOf(environment);
  if (pending) return `${name} waits on an approval by ${whoOf(pending)}`;
  if (NOT_STARTED.includes(environment.status)) {
    const idle = all.filter((other) => (other.rank ?? 0) < (environment.rank ?? 0)).every((other) => WELL[other.status]);
    return idle && !deploy.some((each) => same(each, name)) ? `${name} not started: nothing started it, and the step's deploy option starts it` : `${name} not started`;
  }
  return `${name} ${UNDER_WAY[environment.status] ?? environment.status}`;
}

/**
 * Release `id` watched until each awaited stage ends or `ends` passes: `{ verdict: 'done' | 'failed' | 'timeout', words }`, or
 * `{ verdict: 'missing', missing }` where a stage asked for is not the release's. A stage `deploy` names that nothing started is
 * started here once, by the token, for Azure DevOps' approvals and gates to decide.
 */
export async function watchRelease(repo, id, asked, ends, say, commit) {
  const seen = { started: new Set(), approvals: new Map() };
  let last = null;
  const verdict = await until(async () => {
    const release = (await releases(repo, 'GET', `/releases/${id}`)).json;
    if (!release) return undefined;
    last = release;
    const { list, missing } = awaited([...(release.environments ?? [])].sort((a, b) => (a.rank ?? 0) - (b.rank ?? 0)), asked.stages, (each) => [each.name]);
    if (missing.length > 0) return { verdict: 'missing', missing };
    for (const environment of list) {
      const pending = pendingOf(environment);
      if (pending && !seen.approvals.has(environment.name)) say(`${environment.name} waits on an approval by ${whoOf(pending)}.`);
      if (pending) seen.approvals.set(environment.name, whoOf(pending));
      if (!NOT_STARTED.includes(environment.status) || seen.started.has(environment.name) || !asked.deploy.some((each) => same(each, environment.name))) continue;
      const set = await releases(repo, 'PATCH', `/releases/${id}/environments/${environment.id}`,
        { status: 'inProgress', comment: `Started by Daoris for ${commit.slice(0, 8)}.`, scheduledDeploymentTime: null });
      if (set.status !== 200) throw new Refusal('rejected', `Azure DevOps did not start ${environment.name} (${set.status}): ${set.json?.message ?? 'no reason given'}`);
      seen.started.add(environment.name);
      say(`started ${environment.name} of ${release.name}.`);
    }
    if (list.some((environment) => FAILED.includes(environment.status))) return { verdict: 'failed' };
    return list.every((environment) => WELL[environment.status]) ? { verdict: 'done' } : undefined;
  }, ends, asked.every, say);
  if (verdict?.verdict === 'missing') return verdict;
  const all = [...(last?.environments ?? [])].sort((a, b) => (a.rank ?? 0) - (b.rank ?? 0));
  const { list } = awaited(all, asked.stages, (each) => [each.name]);
  return { verdict: verdict?.verdict ?? 'timeout', words: list.map((environment) => environmentWords(environment, all, seen, asked.deploy)).join('; ') || 'its stages had not begun' };
}

const RESULTS = { succeeded: 'succeeded', succeededWithIssues: 'succeeded with issues', failed: 'failed', canceled: 'was canceled', abandoned: 'was abandoned', skipped: 'was skipped' };
const RAN = ['succeeded', 'succeededWithIssues'];
const STOPPED = ['failed', 'canceled', 'abandoned'];

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
  if (stage.state === 'completed') return `${stage.name} ${RESULTS[stage.result] ?? stage.result ?? 'ended'}${RAN.includes(stage.result) && waited.has(stage.id) ? ' after an approval' : ''}`;
  if (awaitingApproval(records, stage)) return `${stage.name} waits on an approval`;
  return `${stage.name} ${stage.state === 'inProgress' ? 'running' : 'not started'}`;
}

/**
 * Pipeline run `build` watched until each awaited stage of its timeline ends, or `ends` passes: as `watchRelease` answers, and
 * `{ verdict: 'none' }` for a run that ended with no stages of its own. A stage named and skipped fails it; one skipped unnamed does not.
 */
export async function watchRun(repo, build, asked, ends, say) {
  if (asked.deploy.length > 0) say(`deploy starts a classic release's stage; ${asked.deploy.join(', ')} of a pipeline run is started by its own trigger.`);
  const waited = new Set();
  let records = [];
  const stagesOf = () => records.filter((record) => record.type === 'Stage' && record.identifier !== '__default').sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  const verdict = await until(async () => {
    const run = (await project(repo, 'GET', `/build/builds/${build.id}`)).json ?? build;
    records = (await project(repo, 'GET', `/build/builds/${build.id}/timeline`)).json?.records ?? [];
    const ended = run.status === 'completed';
    const all = stagesOf();
    if (all.length === 0) return ended ? { verdict: 'none' } : undefined;
    const { list, missing } = awaited(all, asked.stages, (each) => [each.name, each.identifier]);
    if (missing.length > 0) return ended ? { verdict: 'missing', missing } : undefined;
    for (const stage of list) {
      if (!awaitingApproval(records, stage) || waited.has(stage.id)) continue;
      waited.add(stage.id);
      say(`${stage.name} waits on an approval.`);
    }
    const named = asked.stages.length > 0;
    if (list.some((stage) => stage.state === 'completed' && (STOPPED.includes(stage.result) || (named && stage.result === 'skipped')))) return { verdict: 'failed' };
    if (list.every((stage) => stage.state === 'completed')) return { verdict: 'done' };
    return ended ? { verdict: 'failed' } : undefined;
  }, ends, asked.every, say);
  if (verdict?.verdict === 'none' || verdict?.verdict === 'missing') return verdict;
  const { list } = awaited(stagesOf(), asked.stages, (each) => [each.name, each.identifier]);
  return { verdict: verdict?.verdict ?? 'timeout', words: list.map((stage) => stageWords(stage, records, waited)).join('; ') || 'its stages had not begun' };
}
