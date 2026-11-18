// The release as the run's own steps, one per fact Azure DevOps holds: target, create release, start <stage>, approve <stage>,
// watch <stage>. Each step's act reads its fact first, whoever made it (a trigger, a person in Azure DevOps, Daoris), so a fact made while the
// step is only ready settles it at once, credited to the identity Azure DevOps names (`outside: <name>`). A step makes its fact itself only
// where its action says so, keyed by its effect and read again before it is made, so a retry or a restart between intent and effect finds
// what the first made. A wait says the role it waits on, with its page, until the step's deadline: no fixed give-up. A refused key or
// sign-in waits on the credentials keeper with the page that fixes it, never failing the step. No step approves by itself:
// an approval is its approver's (`approve-release` is the person's press alone, never an agent's tool).
import { createHash } from 'node:crypto';
import { Refusal, TRANSIENT, call, choose, fit, fromBranch, idOf, located, project, releases, withoutHeads } from './azure.mjs';
import { SUCCESS, aliasOf, changedWords, declared, undeclared, unresolved } from './target.mjs';
import { definitionsTaking, offRoute, routeWords, routed } from './routes.mjs';

const EVIDENCE = 'azure-devops-pull-request/release@1';
const MARGIN = 5000;
const ACCESS = ['unauthorized', 'no-token', 'signed-out'];
const pause = (ms, signal) => new Promise((done) => {
  const timer = setTimeout(done, ms);
  signal?.addEventListener('abort', () => { clearTimeout(timer); done(); }, { once: true });
});
const numberOf = (build) => build.buildNumber ?? build.id;
const at = (ms) => new Date(ms).toISOString().slice(11, 16);

/** The step ids, in their order on a run. */
export const STEPS = ['release-target', 'release-create', 'release-start', 'release-approve', 'release-watch'];

/** The effect a release is made for: its definition and the landed commit, so every attempt on that target reads for the same release. */
export const effectOf = (definition, commit) => createHash('sha256').update(`${definition}:${commit}`).digest('hex').slice(0, 16);

/** The commit a release's build artifact carries: its source version, else that of the build it names among `builds`. */
function commitOf(release, builds) {
  for (const artifact of (release.artifacts ?? []).filter((each) => each.type === 'Build')) {
    const source = artifact.definitionReference?.sourceVersion?.id;
    if (typeof source === 'string') return source;
    const build = builds.find((each) => String(each.id) === String(artifact.definitionReference?.version?.id));
    if (build) return build.sourceVersion;
  }
  return null;
}

/** Who Azure DevOps credits with an act: Daoris where its own words say so, else `outside: <name>`. */
const credit = (who, ours) => (ours ? 'Daoris' : `outside: ${who ?? 'someone in Azure DevOps'}`);

/**
 * One release step's act. `say` is the console; `progress` tells Daoris what the step waits on as it changes. Answers reached or failed,
 * or, at the step's deadline with the fact still not made, `timeout` naming what it waited on.
 */
export async function releaseStep(id, attempt, unit, say, progress = () => {}, once = false) {
  const asked = declared(attempt.options);
  // `by` is the identity Azure DevOps credits with the fact (`outside: <name>`, or Daoris), kept beside the words, not only in them.
  const answered = (state, code, message, url, by) => ({ state, ...(code ? { code } : {}),
    evidence: { ref: `${EVIDENCE}:${attempt.invocation}`, ...(url ? { url } : {}), ...(by ? { by } : {}), message: fit(message) } });
  if (!unit) return answered('failed', 'no-unit', `no unit awaits this step at ${attempt.subjectRevision}`);
  const deadline = Date.parse(attempt.deadlineAt);
  const ends = Number.isFinite(deadline) ? deadline - MARGIN : Date.now() + 3600 * 1000;
  let told = null;
  const tell = (role, message, url) => {
    if (told === `${role}|${message}`) return;
    told = `${role}|${message}`;
    progress({ ...(role ? { actor: role } : {}), url, message: fit(message), environment: undefined });
  };
  /** What a wait came to with its fact still not made: read once (a check), not yet so; else its deadline passed. */
  const late = (words, url) => (once ? answered('failed', 'not-yet', words, url) : answered('failed', 'timeout', `${words} The step's deadline passed.`, url));

  const repo = unit.repo ?? located(unit);
  let reread = false;
  /** Refused once Daoris stood the attempt down (`attempt.signal`, the kit's): read before every write and in every wait, so nothing is made after it. */
  const standing = (what) => {
    if (attempt.signal?.aborted) throw new Refusal('stood-down', `stood down (${attempt.signal.reason ?? 'by Daoris'}) before ${what}, so nothing was made`);
  };
  /** Asks `read` until it answers or the step's deadline passes; a refused key waits on the credentials keeper, never failing. */
  const until = async (read) => {
    for (;;) {
      standing('its next read');
      try {
        const got = await read();
        if (got !== undefined) return got;
      } catch (error) {
        if (!(error instanceof Refusal)) throw error;
        if (ACCESS.includes(error.code)) tell('credentials-keeper', `Waiting on the credentials keeper: ${error.message}.`, `${repo.base}/_usersSettings/tokens`);
        else if (!TRANSIENT.includes(error.code)) throw error;
        else say(`${error.message}; asked again.`);
      }
      if ((once && !reread) || Date.now() + asked.every > ends) return undefined;
      reread = false;
      await pause(asked.every, attempt.signal);
    }
  };

  try {
    const context = await until(async () => {
      const pr = idOf(unit.pullRequest) ?? choose(await fromBranch(repo, unit.branch), unit.line)?.pullRequestId;
      if (pr === undefined) throw new Refusal('not-merged', `no pull request from \`${unit.branch}\` was found, so nothing is released`);
      const read = (await call(repo, 'GET', `/pullrequests/${pr}`)).json;
      if (read?.status !== 'completed' || typeof read?.lastMergeCommit?.commitId !== 'string') throw new Refusal('not-merged', `pull request ${pr} is ${read?.status ?? 'unread'}, not merged`);
      // The target, read again at every step's act: a step never acts on a definition its route leaves out.
      const all = asked.pipeline === null ? [] : await definitionsTaking(repo, asked.pipeline);
      const definition = typeof asked.definition !== 'number' ? null
        : await releases(repo, 'GET', `/definitions/${asked.definition}`).then((answer) => (answer.status === 200 ? answer.json : null));
      return { commit: read.lastMergeCommit.commitId, target: read.targetRefName, repositoryId: (await call(repo, 'GET', '')).json?.id ?? repo.repository, all, definition };
    });
    if (!context) return late(told ? told.split('|')[1] : 'The merged pull request was not read.');
    const { commit, target, repositoryId, all, definition } = context;
    const branch = withoutHeads(target);
    const short = commit.slice(0, 8);

    if (asked.missing.length > 0 || asked.definition === 'none') {
      return answered('failed', 'no-target', `${undeclared(asked.missing.length > 0 ? asked.missing : ['releaseDefinition'])} ${routeWords(routed(all, asked.pipeline, branch), branch)}`);
    }
    const changed = unresolved(definition, asked);
    if (changed) return answered('failed', 'target-changed', changedWords(changed));
    const off = offRoute(definition, all, asked.pipeline, branch);
    if (off) return answered('failed', 'target-changed', `${changedWords(off.why)} ${routeWords(off.route, branch)}`);
    const of = `${definition.name} (#${definition.id})`;
    const stage = (definition.environments ?? []).find((each) => each.id === asked.stage);
    if (id === 'release-target') return answered('reached', null, `Target: ${of}, stage ${stage.id} ${stage.name}, for builds of pipeline ${asked.pipeline} from \`${branch}\`. ${routeWords(routed(all, asked.pipeline, branch), branch)}`);

    /** Every build of the declared pipeline for the landed commit on its branch, newest first. */
    const builds = async () => ((await project(repo, 'GET', `/build/builds?repositoryId=${encodeURIComponent(repositoryId)}&repositoryType=TfsGit&branchName=${encodeURIComponent(target)}&queryOrder=queueTimeDescending&$top=50&definitions=${asked.pipeline}`)).json?.value ?? [])
      .filter((build) => build.sourceVersion === commit && build.definition?.id === asked.pipeline);
    /** The releases of the definition whose artifact carries the landed commit, from any build of it. */
    const made = async (known) => ((await releases(repo, 'GET', `/releases?definitionId=${definition.id}&$top=50&$expand=artifacts`)).json?.value ?? [])
      .filter((each) => each.releaseDefinition?.id === definition.id && commitOf(each, known) === commit);
    const linkOf = (release) => release._links?.web?.href ?? `${repo.base}/${encodeURIComponent(repo.project)}/_releaseProgress?releaseId=${release.id}&_a=release-pipeline-progress`;

    // A release the holder named (done outside Daoris, with its link) is read and taken where it carries the landed commit or one after it.
    const named = /^\d+$/.test(String(attempt.options?.release ?? '')) ? Number(attempt.options.release) : Number(/releaseId=(\d+)/.exec(String(attempt.options?.release ?? ''))?.[1] ?? NaN);
    // A link that names no release settles nothing: the step reads the release it names, never one it would find in its place.
    if (String(attempt.options?.release ?? '').trim() !== '' && !Number.isFinite(named)) {
      return answered('failed', 'no-release', `the link names no release (no releaseId), so nothing of ${of} is read for it`);
    }
    /** The commit a release carries, reading its artifact's build where it is none of the landed commit's. */
    const carriedBy = async (held, known) => {
      const read = commitOf(held, known);
      if (read) return read;
      const build = (held.artifacts ?? []).find((each) => each.type === 'Build')?.definitionReference?.version?.id;
      return /^\d+$/.test(String(build ?? '')) ? (await project(repo, 'GET', `/build/builds/${build}`)).json?.sourceVersion ?? null : null;
    };
    /** Whether `carries` descends from the landed commit: the landed commit is their merge base. */
    const after = async (carries) => ((await call(repo, 'GET', `/commits/${carries}/mergebases?otherCommitId=${commit}`)).json?.value ?? [])
      .some((base) => base.commitId === commit);

    const release = await until(async () => {
      const known = await builds();
      if (Number.isFinite(named)) {
        const held = (await releases(repo, 'GET', `/releases/${named}`)).json;
        if (!/^\d+$/.test(String(held?.id ?? ''))) throw new Refusal('no-release', `release ${named} was not found in ${repo.project}`);
        if (Number(held.releaseDefinition?.id) !== definition.id) {
          throw new Refusal('target-changed', `release ${named} is of definition #${held.releaseDefinition?.id ?? '?'}, not the step's #${definition.id}`);
        }
        const carries = await carriedBy(held, known);
        if (carries === commit) return held;
        if (carries && await after(carries)) return { ...held, after: carries };
        throw new Refusal('target-changed', `release ${named} carries ${carries ? carries.slice(0, 8) : 'no commit read'}, which is neither the landed ${short} nor after it`);
      }
      const found = await made(known);
      if (found.length > 0) return found.sort((a, b) => a.id - b.id)[0];
      if (id !== 'release-create') {
        tell('release-manager', `Waiting on the release manager: no release of ${of} carries ${short} yet.`, `${repo.base}/${encodeURIComponent(repo.project)}/_release?definitionId=${definition.id}`);
        return undefined;
      }
      const [build] = known;
      const buildLink = build?._links?.web?.href ?? `${repo.base}/${encodeURIComponent(repo.project)}/_build/results?buildId=${build?.id}`;
      if (!build || build.status !== 'completed') {
        tell('release-manager', `Waiting on the release manager: ${build ? `build ${numberOf(build)} running` : `no build of pipeline ${asked.pipeline} for ${short} yet`}; until ${at(ends)} UTC.`, build ? buildLink : undefined);
        return undefined;
      }
      if (!['succeeded', 'partiallySucceeded'].includes(build.result)) throw new Refusal('build-failed', `build ${numberOf(build)} of ${short} ${build.result ?? 'ended'}, so no release takes it`);
      // CD has its time from the build's end; only `create-and-start` makes the release here, else CD (or, observed, its holder) makes it.
      const finished = Date.parse(build.finishTime ?? '') || Date.now();
      const cdUntil = Math.min(ends, (Number.isFinite(finished) ? finished : Date.now()) + asked.find);
      if (asked.action !== 'create-and-start' || Date.now() < cdUntil) {
        tell('release-manager', asked.action === 'observe'
          ? `Waiting on the release manager: create a release of ${of} from build ${numberOf(build)} in Azure DevOps; until ${at(ends)} UTC.`
          : asked.action === 'start-stage'
            ? `Waiting on the release manager: build ${numberOf(build)} / CD, the release of ${of} its continuous deployment makes; until ${at(ends)} UTC.`
            : `Waiting on the release manager: build ${numberOf(build)} succeeded, its continuous deployment has until ${at(cdUntil)} UTC to make the release.`, buildLink);
        return undefined;
      }
      standing(`it made a release of ${of}`);
      const effect = effectOf(definition.id, commit);
      say(`creating a release of ${of} from build ${numberOf(build)} (effect ${effect}).`);
      const alias = aliasOf(definition, asked.pipeline);
      const posted = await releases(repo, 'POST', '/releases', { definitionId: definition.id, isDraft: false,
        description: `Made by Daoris's release step for build ${numberOf(build)} (${short}); effect ${effect}.`,
        artifacts: [{ alias, instanceReference: { id: String(build.id), name: String(numberOf(build)) } }] });
      if (!/^\d+$/.test(String(posted.json?.id ?? ''))) throw new Refusal('rejected', `Azure DevOps refused to make a release of ${of} (${posted.status})`);
      return { ...posted.json, made: true };
    });
    if (!release) return late(told ? told.split('|')[1] : `nothing of ${of} was read`);
    const link = linkOf(release);
    const by = release.made ? 'Daoris' : credit(release.createdBy?.displayName, /^Made by Daoris/.test(release.description ?? ''));
    const carried = release.after ? `${release.after.slice(0, 8)}, after the landed ${short}` : short;
    if (id === 'release-create') return answered('reached', null, `${release.name} of ${of} carries ${carried}, created by ${by}.`, link, by);

    // The stage's facts, read from the release as Azure DevOps holds it now.
    let started = false;
    const result = await until(async () => {
      const read = (await releases(repo, 'GET', `/releases/${release.id}`)).json;
      const environment = (read?.environments ?? []).find((each) => Number(each.definitionEnvironmentId) === asked.stage);
      if (!environment) throw new Refusal('target-changed', `${read?.name ?? 'the release'} holds no stage ${asked.stage}`);
      const name = environment.name;
      const approvals = [...(environment.preDeployApprovals ?? [])].filter((each) => !each.isAutomated);
      const step = (environment.deploySteps ?? []).at(-1);
      const notStarted = ['notStarted', 'undefined'].includes(environment.status);
      if (id === 'release-start') {
        if (!notStarted) {
          const starter = started ? 'Daoris' : credit(step?.requestedBy?.displayName, /Daoris/.test(environment.comment ?? ''));
          return answered('reached', null, `${name} of ${read.name} started by ${starter}.`, link, starter);
        }
        if (asked.action === 'observe') {
          tell('deployer', `Waiting on the deployer: start ${name} of ${read.name} in Azure DevOps.`, link);
          return undefined;
        }
        // Started here already, and Azure DevOps not showing it yet: waited for, never started a second time.
        if (started) {
          tell(null, `${name} of ${read.name} was started by Daoris; Azure DevOps does not show it started yet.`, link);
          return undefined;
        }
        standing(`it started ${name} of ${read.name}`);
        const set = await releases(repo, 'PATCH', `/releases/${release.id}/environments/${environment.id}`, { status: 'inProgress', comment: `Started by Daoris for ${short}.`, scheduledDeploymentTime: null });
        if (set.status === 200) {
          started = true;
          reread = true;
          return undefined;
        }
        const again = (await releases(repo, 'GET', `/releases/${release.id}`)).json;
        const shown = (again?.environments ?? []).find((each) => Number(each.definitionEnvironmentId) === asked.stage);
        if (!shown || ['notStarted', 'undefined'].includes(shown.status)) throw new Refusal('rejected', `Azure DevOps did not start ${name} (${set.status})`);
        return undefined;
      }
      if (id === 'release-approve') {
        if (notStarted) {
          tell('deployer', `Waiting on the deployer: ${name} of ${read.name} is not started, so nothing asks its approval yet.`, link);
          return undefined;
        }
        if (approvals.length === 0) return answered('reached', null, `${name} of ${read.name} asks no approval.`, link);
        const rejected = approvals.find((each) => each.status === 'rejected');
        const approver = (each) => `outside: ${each?.approvedBy?.displayName ?? each?.approver?.displayName ?? 'its approver'}`;
        if (rejected) return answered('failed', 'rejected', `${name} of ${read.name} was rejected by ${approver(rejected)}.`, link, approver(rejected));
        const pending = approvals.find((each) => each.status === 'pending');
        if (!pending) return answered('reached', null, `${name} of ${read.name} approved by ${approver(approvals.at(-1))}.`, link, approver(approvals.at(-1)));
        // The card names the target, the build and its branch; its Approve and Reject are the holder's own press (approve-release).
        tell('approver', `Waiting on the approver: approve ${name} of ${read.name}, ${of}, carrying ${short} from \`${branch}\`; asked of ${pending.approver?.displayName ?? 'its approvers'}. Approve or reject it here or in Azure DevOps.`, link);
        return undefined;
      }
      if (SUCCESS[asked.success].includes(environment.status)) return answered('reached', null, `${name} of ${read.name} ${environment.status}.`, link);
      if (['rejected', 'canceled', 'succeeded', 'partiallySucceeded'].includes(environment.status)) return answered('failed', 'stage-failed', `${name} of ${read.name} ${environment.status}.`, link);
      tell(null, `${name} of ${read.name} ${environment.status}.`, link);
      return undefined;
    });
    return result ?? late(told ? told.split('|')[1] : 'its stage was not read', link);
  } catch (error) {
    if (error instanceof Refusal) return answered('failed', error.code, error.message);
    throw error;
  }
}

/**
 * A holder's Done outside Daoris link on a release step, read once by the kit's `check` (`attempt.link`): the step read as it would act, the
 * release the link names in place of one it would find and nothing made, started or approved (`observe`). Done where the fact is there, the
 * link's release carrying the landed commit or one after it; failed where it is refused (`target-changed`, naming the commit it carries) or
 * not yet so (`not-yet`, with what the step would wait on). The answer names the link it read.
 */
export async function linkCheck(id, attempt, unit, say) {
  const read = await releaseStep(id, { ...attempt, options: { ...attempt.options, release: attempt.link, action: 'observe' } }, unit, say, () => {}, true);
  return { state: read.state === 'reached' ? 'done' : 'failed', outcome: read, link: attempt.link };
}
