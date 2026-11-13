// The `release` step after the merge, acting only on the declared target (target.mjs): anything unresolved holds before any write, and a
// release is made only where the action says so, never twice, since a retry reads before it makes one.
import { Refusal, call, choose, fit, fromBranch, idOf, located, project, releases, withoutHeads } from './azure.mjs';
import { until, watchRelease, watchRun } from './stages.mjs';
import { aliasOf, candidates, declared, undeclared, unresolved } from './target.mjs';
import { builtNothing, selfStarting, triggeredBy } from './triggers.mjs';

const EVIDENCE = 'azure-devops-pull-request/release@1';
// The watch ends this long before the attempt's deadline, so its outcome reaches Daoris before Daoris asks after it.
const MARGIN = 5000;

const sourceOf = (build) => `${build.project?.id}:${build.definition?.id}`;
const numberOf = (build) => build.buildNumber ?? build.id;

/** The pull request the unit's work merged by, and the commit and branch it merged into; else a `not-merged` refusal. */
async function mergedOf(repo, unit) {
  const id = idOf(unit.pullRequest) ?? choose(await fromBranch(repo, unit.branch), unit.line)?.pullRequestId;
  if (id === undefined) throw new Refusal('not-merged', `no pull request from \`${unit.branch}\` was found, so nothing is released`);
  const pr = (await call(repo, 'GET', `/pullrequests/${id}`)).json;
  const commit = pr?.lastMergeCommit?.commitId;
  if (pr?.status !== 'completed' || typeof commit !== 'string') throw new Refusal('not-merged', `pull request ${id} is ${pr?.status ?? 'unread'}, not merged, so nothing is released`);
  return { commit, target: pr.targetRefName };
}

/** The declared pipeline's builds of `commit` on `target`, newest first; undefined while there are none. */
async function buildsOf(repo, repositoryId, commit, target, pipeline) {
  const query = `repositoryId=${encodeURIComponent(repositoryId)}&repositoryType=TfsGit&branchName=${encodeURIComponent(target)}`
    + `&queryOrder=queueTimeDescending&$top=50&definitions=${pipeline}`;
  const ran = ((await project(repo, 'GET', `/build/builds?${query}`)).json?.value ?? [])
    .filter((build) => build.sourceVersion === commit && build.definition?.id === pipeline);
  return ran.length > 0 ? ran : undefined;
}

/** The releases of `definition` made from exactly `build`: its artifact under the definition's alias for the pipeline is that build. */
async function releasesOf(repo, definition, build, alias) {
  const query = `definitionId=${definition.id}&artifactTypeId=Build&sourceId=${encodeURIComponent(sourceOf(build))}&artifactVersionId=${build.id}`;
  return ((await releases(repo, 'GET', `/releases?${query}`)).json?.value ?? []).filter((each) => each.releaseDefinition?.id === definition.id
    && (each.artifacts ?? []).some((artifact) => artifact.type === 'Build' && (alias === '' || artifact.alias === undefined || artifact.alias === alias)
      && artifact.sourceId === sourceOf(build) && String(artifact.definitionReference?.version?.id) === String(build.id)));
}

/** The step for `unit`: reached once the declared stage ended as its success asks, else failed in the words of what stands in the way. */
export async function release(attempt, unit, say) {
  const failed = (code, message, url) => ({ state: 'failed', code, evidence: { ref: `${EVIDENCE}:${attempt.invocation}`, ...(url ? { url } : {}), message: fit(message) } });
  if (!unit) return failed('no-unit', `no unit awaits this step at ${attempt.subjectRevision}`);
  const asked = declared(attempt.options);
  const deadline = Date.parse(attempt.deadlineAt);
  const ends = Number.isFinite(deadline) ? deadline - MARGIN : Date.now() + 3600 * 1000;
  const finding = () => Math.min(ends, Date.now() + asked.find);
  try {
    const repo = located(unit);
    const web = (thing, path) => (/^https?:\/\//.test(thing?._links?.web?.href ?? '') ? thing._links.web.href : `${repo.base}/${encodeURIComponent(repo.project)}/${path}`);
    const { commit, target } = await mergedOf(repo, unit);
    const short = commit.slice(0, 8);
    const repositoryId = (await call(repo, 'GET', '')).json?.id ?? repo.repository;
    const holding = async (code, why) => {
      const found = await candidates({ repo, repositoryId, commit, target, pipeline: asked.pipeline });
      say(`${why} ${found.words}`);
      return failed(code, `${why} ${found.words}`, found.url);
    };
    if (asked.missing.length > 0) return await holding('no-target', undeclared(asked.missing));

    const definition = asked.definition === 'none' ? null
      : await releases(repo, 'GET', `/definitions/${asked.definition}`).then((answer) => (answer.status === 200 ? answer.json : null));
    const changed = asked.definition === 'none' ? null : unresolved(definition, asked);
    if (changed) return await holding('target-changed', `The declared release target no longer resolves: ${changed}.`);

    // A merge the declared pipeline's CI trigger leaves out builds nothing: said once, at the first look.
    let looked = false;
    const ran = await until(async () => {
      const found = await buildsOf(repo, repositoryId, commit, target, asked.pipeline);
      if (found || looked) return found;
      looked = true;
      const nothing = await builtNothing(repo, repositoryId, commit, target, asked.pipeline);
      return nothing ? { nothing } : undefined;
    }, finding(), asked.every, say);
    if (!ran) return failed('no-release', `no build of pipeline ${asked.pipeline} ran for ${short} on \`${withoutHeads(target)}\` within ${asked.find / 1000} s.`);
    if (ran.nothing) {
      return { state: 'reached', evidence: { ref: `${EVIDENCE}:${attempt.invocation}`, url: web(null, `_git/${encodeURIComponent(repo.repository)}/commit/${commit}`),
        message: fit(`Nothing to release: ${ran.nothing}.`) } };
    }
    const [build] = ran;
    const buildUrl = web(build, `_build/results?buildId=${build.id}`);

    const outcome = (header, watched, url) => {
      if (watched.verdict === 'missing') return failed('target-changed', `${header}it holds no declared stage ${asked.stage}.`, url);
      if (watched.verdict === 'done') return { state: 'reached', evidence: { ref: `${EVIDENCE}:${attempt.invocation}`, url, message: fit(`${header}${watched.words}.`) } };
      if (watched.verdict === 'failed') return failed('stage-failed', `${header}${watched.words}.`, url);
      return failed('timeout', `${header}stopped watching at the step's deadline: ${watched.words}.`, url);
    };

    if (!definition) {
      const watched = await watchRun(repo, build, asked, ends, say);
      return outcome(`${build.definition?.name ?? 'pipeline'} run ${numberOf(build)} (${short}): `, watched, buildUrl);
    }

    const built = await until(async () => {
      const read = (await project(repo, 'GET', `/build/builds/${build.id}`)).json;
      return read?.status === 'completed' ? read : undefined;
    }, ends, asked.every, say);
    if (!built) return failed('timeout', `build ${numberOf(build)} of ${short} had not completed by the step's deadline.`, buildUrl);
    if (!['succeeded', 'partiallySucceeded'].includes(built.result)) {
      return failed('build-failed', `build ${numberOf(build)} of ${short} ${built.result ?? 'ended'}, so no release takes it.`, buildUrl);
    }

    const alias = aliasOf(definition, asked.pipeline);
    const of = `${definition.name} (#${definition.id})`;
    const from = `build ${numberOf(build)} (${short})`;
    // A release the trigger makes is waited for; one the action makes is made at once where no trigger takes the build's branch.
    const waits = asked.action !== 'create-and-start' || triggeredBy([definition], build).length > 0;
    let found = waits ? await until(async () => {
      const made = await releasesOf(repo, definition, build, alias);
      return made.length > 0 ? made : undefined;
    }, finding(), asked.every, say) : await releasesOf(repo, definition, build, alias);
    let starting = null;
    if ((found ?? []).length === 0) {
      if (asked.action !== 'create-and-start') {
        return failed('no-release', `No release of ${of} was made from ${from} within ${asked.find / 1000} s; the step's action does not create one.`, buildUrl);
      }
      starting = selfStarting(definition.environments ?? []);
      say(`creating a release of ${of} from build ${numberOf(build)}; ${starting.length > 0 ? `its creation starts ${starting.join(', ')} by itself` : 'no stage starts by itself'}.`);
      const made = await releases(repo, 'POST', '/releases', {
        definitionId: definition.id, isDraft: false,
        description: `Made by Daoris's release step for ${from}, attempt ${attempt.idempotencyKey ?? attempt.invocation}.`,
        artifacts: [{ alias, instanceReference: { id: String(build.id), name: String(numberOf(build)) } }],
      });
      if (!/^\d+$/.test(String(made.json?.id ?? ''))) {
        const why = typeof made.json?.message === 'string' ? `: ${made.json.message.slice(0, 120)}` : '';
        return failed('rejected', `Azure DevOps refused to make a release of ${of} from ${from} (${made.status})${why}.`, buildUrl);
      }
      found = [made.json];
    }
    if (found.length > 1) {
      const listed = found.map((each) => `${each.name} (#${each.id})`).join(', ');
      return failed('ambiguous', `Several releases of ${of} were made from ${from}: ${listed}. Abandon all but one and try the step again.`, buildUrl);
    }
    const [chosen] = found;
    const watched = await watchRelease(repo, chosen.id, asked, ends, say, commit);
    const made = starting === null ? '' : `, made here${starting.length > 0 ? `, its creation starting ${starting.join(', ')} by itself` : ''}`;
    return outcome(`${chosen.name} of ${definition.name} from ${from}${made}: `, watched,
      web(chosen, `_releaseProgress?releaseId=${chosen.id}&_a=release-pipeline-progress`));
  } catch (error) {
    if (error instanceof Refusal) return failed(error.code, error.message);
    throw error;
  }
}
