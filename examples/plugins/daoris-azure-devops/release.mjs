// The `release` step, placed after the merge: the merged commit from the pull request the work merged by; the build that ran for it on
// the target branch; then the classic release made from that build (a release definition taking its artifact) or, where no definition
// takes it, the pipeline run's own stages; watched until each awaited stage ends, each stage's outcome its evidence.
import { Refusal, call, choose, fit, fromBranch, idOf, located, project, releases, withoutHeads } from './azure.mjs';
import { watchSeveral } from './several.mjs';
import { until, watchRelease, watchRun } from './stages.mjs';
import { builtNothing } from './triggers.mjs';
import { unreleased } from './unreleased.mjs';

const EVIDENCE = 'azure-devops-pull-request/release@1';
// The watch ends this long before the attempt's deadline, so its outcome reaches Daoris before Daoris asks after it.
const MARGIN = 5000;

/** The step's options: the pipeline and release definition named, the stages awaited and started, and how long and how often to ask. */
function settings(options) {
  const given = options ?? {};
  const named = (value) => (value === undefined || value === null || String(value).trim() === '' ? null : value);
  const list = (value) => (Array.isArray(value) ? value : typeof value === 'string' ? value.split(',') : []).map((each) => String(each).trim()).filter(Boolean);
  const seconds = (value, otherwise) => (Number(value) > 0 ? Number(value) : otherwise);
  return {
    pipeline: named(given.pipeline), definition: named(given.releaseDefinition), stages: list(given.stages), deploy: list(given.deploy),
    create: given.createRelease === true || String(given.createRelease).toLowerCase() === 'true',
    find: seconds(given.findSeconds, 600) * 1000, every: seconds(given.everySeconds, 30) * 1000,
  };
}

const numeric = (value) => (/^\d+$/.test(String(value).trim()) ? Number(value) : null);
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

async function pipelineOf(repo, named) {
  if (numeric(named) !== null) return numeric(named);
  const found = (await project(repo, 'GET', `/build/definitions?name=${encodeURIComponent(named)}`)).json?.value?.[0];
  if (!found) throw new Refusal('no-release', `no pipeline is named \`${named}\``);
  return found.id;
}

async function definitionOf(repo, named) {
  const id = numeric(named);
  const found = id !== null
    ? await releases(repo, 'GET', `/definitions/${id}`).then((answer) => (answer.status === 200 ? answer.json : null))
    : (await releases(repo, 'GET', `/definitions?searchText=${encodeURIComponent(named)}&isExactNameMatch=true&$expand=artifacts,triggers`)).json?.value?.[0];
  if (!found) throw new Refusal('no-release', `no release definition is named \`${named}\``);
  return found;
}

/** The pipeline a release definition's build artifact comes from, its primary one first. */
const pipelineTaken = (definition) => {
  const builds = (definition.artifacts ?? []).filter((artifact) => artifact.type === 'Build');
  return numeric((builds.find((artifact) => artifact.isPrimary) ?? builds[0])?.definitionReference?.definition?.id);
};

const taking = async (repo, build) =>
  (await releases(repo, 'GET', `/definitions?artifactType=Build&artifactSourceId=${encodeURIComponent(sourceOf(build))}&$expand=artifacts,triggers`)).json?.value ?? [];

/** The builds of `commit` on `target`, newest first, of `pipeline` where one is named; undefined while there are none. */
async function buildsOf(repo, repositoryId, commit, target, pipeline) {
  const query = `repositoryId=${encodeURIComponent(repositoryId)}&repositoryType=TfsGit&branchName=${encodeURIComponent(target)}`
    + `&queryOrder=queueTimeDescending&$top=50${pipeline ? `&definitions=${pipeline}` : ''}`;
  const ran = ((await project(repo, 'GET', `/build/builds?${query}`)).json?.value ?? []).filter((build) => build.sourceVersion === commit);
  return ran.length > 0 ? ran : undefined;
}

/** Which build is the release's: the newest of the one pipeline that ran, else of the one pipeline a release definition takes. */
async function chosen(repo, ran, named) {
  const newest = new Map();
  for (const build of ran) if (!newest.has(build.definition?.id)) newest.set(build.definition?.id, build);
  if (newest.size === 1 || named) return { build: [...newest.values()][0], definitions: named ? [named] : await taking(repo, [...newest.values()][0]) };
  const taken = [];
  for (const build of newest.values()) {
    const definitions = await taking(repo, build);
    if (definitions.length > 0) taken.push({ build, definitions });
  }
  if (taken.length === 1) return taken[0];
  const names = [...newest.values()].map((build) => build.definition?.name ?? build.definition?.id).join(', ');
  throw new Refusal('ambiguous', `builds of several pipelines ran for this commit (${names}); name one in the step's pipeline option`);
}

/**
 * The step for `unit`: reached once every awaited stage succeeded, else failed with the words of the stage that did not, of what is
 * not found (`no-release`), or of where each stage stood at the deadline (`timeout`).
 */
export async function release(attempt, unit, say) {
  const failed = (code, message, url) => ({ state: 'failed', code, evidence: { ref: `${EVIDENCE}:${attempt.invocation}`, ...(url ? { url } : {}), message: fit(message) } });
  if (!unit) return failed('no-unit', `no unit awaits this step at ${attempt.subjectRevision}`);
  const asked = settings(attempt.options);
  const deadline = Date.parse(attempt.deadlineAt);
  const ends = Number.isFinite(deadline) ? deadline - MARGIN : Date.now() + 3600 * 1000;
  const finding = () => Math.min(ends, Date.now() + asked.find);
  try {
    const repo = located(unit);
    const web = (thing, path) => (/^https?:\/\//.test(thing?._links?.web?.href ?? '') ? thing._links.web.href : `${repo.base}/${encodeURIComponent(repo.project)}/${path}`);
    const { commit, target } = await mergedOf(repo, unit);
    const short = commit.slice(0, 8);
    const named = asked.definition !== null ? await definitionOf(repo, asked.definition) : null;
    const pipeline = asked.pipeline !== null ? await pipelineOf(repo, asked.pipeline) : named ? pipelineTaken(named) : null;
    const repositoryId = (await call(repo, 'GET', '')).json?.id ?? repo.repository;
    // A merge no pipeline takes (every CI trigger off, or its branch or path filters leaving it out) builds nothing: said once, at the first look.
    let looked = pipeline !== null;
    const ran = await until(async () => {
      const found = await buildsOf(repo, repositoryId, commit, target, pipeline);
      if (found || looked) return found;
      looked = true;
      const nothing = await builtNothing(repo, repositoryId, commit, target);
      return nothing ? { nothing } : undefined;
    }, finding(), asked.every, say);
    if (!ran) {
      return failed('no-release', `no build ran for ${short} on \`${withoutHeads(target)}\`${pipeline ? ` in pipeline ${pipeline}` : ''} within ${asked.find / 1000} s.`);
    }
    if (ran.nothing) {
      return { state: 'reached', evidence: { ref: `${EVIDENCE}:${attempt.invocation}`, url: web(null, `_git/${encodeURIComponent(repo.repository)}/commit/${commit}`),
        message: fit(`Nothing to release: ${ran.nothing}.`) } };
    }
    const { build, definitions } = await chosen(repo, ran, named);
    const buildUrl = web(build, `_build/results?buildId=${build.id}`);

    const outcome = (header, watched, url) => {
      if (watched.verdict === 'missing') return failed('no-release', `${header}no stage is named ${watched.missing.join(', ')}.`, url);
      if (watched.verdict === 'done') return { state: 'reached', evidence: { ref: `${EVIDENCE}:${attempt.invocation}`, url, message: fit(`${header}${watched.words}.`) } };
      if (watched.verdict === 'failed') return failed('stage-failed', `${header}${watched.words}.`, url);
      return failed('timeout', `${header}stopped watching at the step's deadline: ${watched.words}.`, url);
    };

    if (definitions.length === 0) {
      const watched = await watchRun(repo, build, asked, ends, say);
      if (watched.verdict === 'none') return failed('no-release', `build ${numberOf(build)} has no stages, and no release definition takes its artifact.`, buildUrl);
      return outcome(`${build.definition?.name ?? 'pipeline'} run ${numberOf(build)} (${short}): `, watched, buildUrl);
    }

    const [definition] = definitions;
    const built = await until(async () => {
      const read = (await project(repo, 'GET', `/build/builds/${build.id}`)).json;
      return read?.status === 'completed' ? read : undefined;
    }, ends, asked.every, say);
    if (!built) return failed('timeout', `build ${numberOf(build)} of ${short} had not completed by the step's deadline.`, buildUrl);
    if (!['succeeded', 'partiallySucceeded'].includes(built.result)) {
      return failed('build-failed', `build ${numberOf(build)} of ${short} ${built.result ?? 'ended'}, so no release takes it.`, buildUrl);
    }
    const none = await unreleased({ repo, build, definitions, asked, sourceId: sourceOf(build), buildNumber: numberOf(build), commit });
    if (none?.words) return failed('no-release', none.words, buildUrl);
    if (definitions.length > 1) {
      const watched = await watchSeveral({ repo, build, definitions, asked, by: finding(), ends, say, commit, sourceId: sourceOf(build), buildNumber: numberOf(build) });
      if (watched.verdict === 'none') return failed('no-release', watched.words, buildUrl);
      return outcome('', watched, watched.url ?? buildUrl);
    }
    const made = none?.made ?? await until(async () => {
      const query = `definitionId=${definition.id}&artifactTypeId=Build&sourceId=${encodeURIComponent(sourceOf(build))}&artifactVersionId=${build.id}`;
      const found = (await releases(repo, 'GET', `/releases?${query}`)).json?.value ?? [];
      return found.length > 0 ? found.reduce((a, b) => (b.id > a.id ? b : a)) : undefined;
    }, finding(), asked.every, say);
    if (!made) return failed('no-release', `no release of ${definition.name} was made from build ${numberOf(build)} within ${asked.find / 1000} s.`, buildUrl);
    const watched = await watchRelease(repo, made.id, asked, ends, say, commit);
    return outcome(`${made.name} of ${definition.name} from build ${numberOf(build)} (${short})${none?.made ? ', made here as no trigger makes one' : ''}: `, watched,
      web(made, `_releaseProgress?releaseId=${made.id}&_a=release-pipeline-progress`));
  } catch (error) {
    if (error instanceof Refusal) return failed(error.code, error.message);
    throw error;
  }
}
