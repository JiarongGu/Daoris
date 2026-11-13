// What Azure DevOps holds for a repository's pipelines, read only: each pipeline, the release definitions taking its builds, their stages
// with the conditions that start them, their continuous-deployment triggers and their links, and for each stage the earliest act that
// deploys into it (deploysOn). It proposes; it never chooses: a release step's hold and the `discover-releases` command both say it.
import { project, releases, withoutHeads } from './azure.mjs';
import { selfStarting } from './triggers.mjs';

const numeric = (value) => (/^\d+$/.test(String(value ?? '').trim()) ? Number(value) : null);

/** Whether `definition` takes `pipeline`'s builds: an artifact of type Build naming it. */
const takes = (definition, pipeline) => (definition?.artifacts ?? [])
  .some((artifact) => artifact.type === 'Build' && numeric(artifact.definitionReference?.definition?.id) === pipeline);

/** The branches a definition's continuous-deployment trigger takes: `*` for every branch, none where it has no trigger. */
function triggersOf(definition) {
  const cd = (definition.triggers ?? []).filter((each) => each.triggerType === 'artifactSource');
  const branches = cd.flatMap((each) => ((each.triggerConditions ?? []).length === 0 ? ['*']
    : each.triggerConditions.map((condition) => withoutHeads(condition.sourceBranch) ?? '*')));
  return [...new Set(branches)];
}

/** One definition as discovery lists it: by id, its link, its CD branches, and each stage in rank with what starts it and deploysOn. */
function definitionOf(repo, definition) {
  const triggers = triggersOf(definition);
  const ordered = [...(definition.environments ?? [])].sort((a, b) => (a.rank ?? 0) - (b.rank ?? 0));
  const itself = new Set(selfStarting(ordered));
  // A stage a release's creation starts deploys at the act that makes the release: a push where CD takes every branch, a merge into the
  // branches it names; any other stage deploys only when started.
  const deploysOn = (stage) => (!itself.has(stage.name) || triggers.length === 0 ? 'start' : triggers.includes('*') ? 'push' : 'merge');
  return {
    id: definition.id,
    name: definition.name,
    url: definition._links?.web?.href ?? `${repo.base}/${encodeURIComponent(repo.project)}/_release?definitionId=${definition.id}`,
    triggers,
    stages: ordered.map((stage) => ({
      id: stage.id,
      name: stage.name,
      after: (Array.isArray(stage.conditions) ? stage.conditions : []).filter((each) => each.conditionType === 'environmentState').map((each) => each.name),
      deploysOn: deploysOn(stage),
    })),
  };
}

/** The pipelines discovery reads: the declared one, else those that built `commit` on `target`, else every pipeline of the repository. */
async function pipelinesOf(repo, repositoryId, commit, target, declaredPipeline) {
  const found = new Map();
  if (commit && target) {
    const query = `repositoryId=${encodeURIComponent(repositoryId)}&repositoryType=TfsGit&branchName=${encodeURIComponent(target)}&queryOrder=queueTimeDescending&$top=50`
      + (declaredPipeline !== null ? `&definitions=${declaredPipeline}` : '');
    const ran = ((await project(repo, 'GET', `/build/builds?${query}`)).json?.value ?? []).filter((build) => build.sourceVersion === commit);
    for (const build of ran) if (!found.has(build.definition?.id)) found.set(build.definition?.id, { id: build.definition?.id, project: build.project?.id, url: null });
  }
  if (declaredPipeline !== null) return [found.get(declaredPipeline) ?? { id: declaredPipeline, project: null, url: null }];
  if (found.size > 0) return [...found.values()];
  const listed = (await project(repo, 'GET', `/build/definitions?repositoryId=${encodeURIComponent(repositoryId)}&repositoryType=TfsGit`)).json?.value ?? [];
  return listed.map((each) => ({ id: each.id, project: each.project?.id ?? null, url: each._links?.web?.href ?? null }));
}

/** The release definitions taking `pipeline`'s builds, in the order Azure DevOps lists them. */
async function takingPipeline(repo, pipeline) {
  const source = pipeline.project ? `${pipeline.project}:${pipeline.id}` : null;
  const query = source ? `artifactType=Build&artifactSourceId=${encodeURIComponent(source)}&` : '';
  const all = (await releases(repo, 'GET', `/definitions?${query}$expand=artifacts,triggers,environments`)).json?.value ?? [];
  return all.filter((each) => takes(each, pipeline.id));
}

/**
 * What the repository's pipelines release through, as candidates: `{ pipelines: [{ id, url, definitions }], url }`, the url the project's
 * release hub. `commit` and `target` narrow it to the pipelines that built a merge; `pipeline` to the one declared.
 */
export async function discover({ repo, repositoryId, commit = null, target = null, pipeline = null }) {
  const pipelines = [];
  for (const each of await pipelinesOf(repo, repositoryId, commit, target, pipeline)) {
    pipelines.push({
      id: each.id,
      url: each.url ?? `${repo.base}/${encodeURIComponent(repo.project)}/_build?definitionId=${each.id}`,
      definitions: (await takingPipeline(repo, each)).map((definition) => definitionOf(repo, definition)),
    });
  }
  return { pipelines, url: `${repo.base}/${encodeURIComponent(repo.project)}/_release` };
}

/** One definition in words: *#42 Portal (401 Dev, 402 Prod; CD on dev)*. */
const described = (definition) => {
  const stages = definition.stages.map((each) => `${each.id} ${each.name}`);
  const cd = definition.triggers.length === 0 ? 'no CD' : definition.triggers.includes('*') ? 'CD on every branch' : `CD on ${definition.triggers.join(', ')}`;
  return `#${definition.id} ${definition.name} (${stages.length > 0 ? stages.join(', ') : 'no stages'}; ${cd})`;
};

/** What discovery found, in one sentence per pipeline. */
export function discoveryWords(found) {
  const parts = found.pipelines.map((each) => (each.definitions.length > 0
    ? `for pipeline ${each.id}: ${each.definitions.map(described).join('; ')}`
    : `for pipeline ${each.id}: no release definition takes its builds; its run's own stages are named by identifier, with releaseDefinition none`));
  return parts.length > 0 ? `Found ${parts.join('. Found ')}.` : 'No pipeline of the repository was found to release from.';
}
