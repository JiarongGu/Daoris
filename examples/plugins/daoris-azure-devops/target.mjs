// The release step's target as its options declare it, by stable id (D288): the build pipeline, the release definition (`none` for a
// pipeline run's own stages), the exact stage, the action and what counts as success. What the forge holds for a pipeline is only ever
// listed for the person as candidates: a label, a trigger or the releases already made describe the forge's behaviour, not the intent.
import { project, releases, withoutHeads } from './azure.mjs';

export const ACTIONS = ['observe', 'start-stage', 'create-and-start'];

/** The stage results each `success` takes as reached. */
export const SUCCESS = { succeeded: ['succeeded'], 'partially-succeeded': ['succeeded', 'partiallySucceeded'] };

const numeric = (value) => (/^\d+$/.test(String(value ?? '').trim()) ? Number(value) : null);
const text = (value) => (typeof value === 'string' && value.trim() !== '' ? value.trim() : null);
const joined = (names) => (names.length < 2 ? names.join('') : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`);

/** The step's options read: the target, the names it lacks or holds in no known shape (`missing`), and how long and how often to ask. */
export function declared(options) {
  const given = options ?? {};
  const seconds = (value, otherwise) => (Number(value) > 0 ? Number(value) : otherwise);
  const pipeline = numeric(given.pipeline);
  const definition = String(given.releaseDefinition ?? '').trim().toLowerCase() === 'none' ? 'none' : numeric(given.releaseDefinition);
  const stage = definition === 'none' ? text(given.stage) : numeric(given.stage);
  // A pipeline run's own stage is started by its own trigger and made by its run: it is only observed.
  const action = ACTIONS.includes(given.action) && (definition !== 'none' || given.action === 'observe') ? given.action : null;
  const success = given.success === undefined ? 'succeeded' : SUCCESS[given.success] ? given.success : null;
  const missing = [['pipeline', pipeline], ['releaseDefinition', definition], ['stage', stage], ['action', action], ['success', success]]
    .filter(([, value]) => value === null).map(([name]) => name);
  return { pipeline, definition, stage, action, success, missing, find: seconds(given.findSeconds, 600) * 1000, every: seconds(given.everySeconds, 30) * 1000 };
}

/** Why the step holds for a declaration it cannot act on. */
export const undeclared = (missing) => (missing.length >= 4
  ? `No release target is declared: ${joined(missing.slice(0, 4))}.`
  : `The release target is declared without ${joined(missing)}.`);

/** The artifact alias under which `definition` takes `pipeline`'s builds, else null. */
export function aliasOf(definition, pipeline) {
  const found = (definition?.artifacts ?? []).find((artifact) => artifact.type === 'Build' && numeric(artifact.definitionReference?.definition?.id) === pipeline);
  return found ? (typeof found.alias === 'string' ? found.alias : '') : null;
}

/** Why a declared classic target does not resolve, or null: the definition read, taking the pipeline's builds, holding the stage. */
export function unresolved(definition, target) {
  if (!definition) return `release definition #${target.definition} is not found`;
  if (aliasOf(definition, target.pipeline) === null) return `release definition #${definition.id} ${definition.name} takes no build of pipeline ${target.pipeline}`;
  if (!(definition.environments ?? []).some((each) => each.id === target.stage)) return `release definition #${definition.id} ${definition.name} has no stage ${target.stage}`;
  return null;
}

/** The branches a definition's continuous-deployment trigger takes, in words. */
function deployment(definition) {
  const triggers = (definition.triggers ?? []).filter((each) => each.triggerType === 'artifactSource');
  if (triggers.length === 0) return 'no CD';
  const branches = triggers.flatMap((each) => (each.triggerConditions ?? []).map((condition) => withoutHeads(condition.sourceBranch) ?? 'its build\'s branch'));
  return branches.length === 0 ? 'CD on every branch' : `CD on ${branches.join(', ')}`;
}

const described = (definition) => {
  const stages = [...(definition.environments ?? [])].sort((a, b) => (a.rank ?? 0) - (b.rank ?? 0)).map((each) => `${each.id} ${each.name}`);
  return `#${definition.id} ${definition.name} (${stages.length > 0 ? stages.join(', ') : 'no stages'}; ${deployment(definition)})`;
};

/** The pipelines a hold lists candidates for: the declared one, else those that built the merged commit, else the repository's. */
async function pipelinesOf(repo, repositoryId, commit, target, declaredPipeline) {
  const query = `repositoryId=${encodeURIComponent(repositoryId)}&repositoryType=TfsGit&branchName=${encodeURIComponent(target)}&queryOrder=queueTimeDescending&$top=50`
    + (declaredPipeline !== null ? `&definitions=${declaredPipeline}` : '');
  const ran = ((await project(repo, 'GET', `/build/builds?${query}`)).json?.value ?? []).filter((build) => build.sourceVersion === commit);
  const found = new Map();
  for (const build of ran) if (!found.has(build.definition?.id)) found.set(build.definition?.id, { id: build.definition?.id, project: build.project?.id });
  if (found.size > 0 || declaredPipeline !== null) {
    const declaredOne = found.get(declaredPipeline);
    if (declaredPipeline !== null && !declaredOne) found.set(declaredPipeline, { id: declaredPipeline, project: null });
    return [...found.values()];
  }
  const listed = (await project(repo, 'GET', `/build/definitions?repositoryId=${encodeURIComponent(repositoryId)}&repositoryType=TfsGit`)).json?.value ?? [];
  return listed.map((each) => ({ id: each.id, project: each.project?.id ?? null }));
}

/** The release definitions taking `pipeline`'s builds. */
async function takingPipeline(repo, pipeline) {
  const source = pipeline.project ? `${pipeline.project}:${pipeline.id}` : null;
  const query = source ? `artifactType=Build&artifactSourceId=${encodeURIComponent(source)}&` : '';
  const all = (await releases(repo, 'GET', `/definitions?${query}$expand=artifacts,triggers,environments`)).json?.value ?? [];
  return all.filter((each) => aliasOf(each, pipeline.id) !== null);
}

/**
 * The candidates a hold lists, each by id with its stages and trigger: `{ words, url }`, the words what was found for each pipeline, the
 * url the project's release hub.
 */
export async function candidates({ repo, repositoryId, commit, target, pipeline }) {
  const parts = [];
  for (const each of await pipelinesOf(repo, repositoryId, commit, target, pipeline)) {
    const definitions = await takingPipeline(repo, each);
    parts.push(definitions.length > 0
      ? `for pipeline ${each.id}: ${definitions.map(described).join('; ')}`
      : `for pipeline ${each.id}: no release definition takes its builds; its run's own stages are named by identifier, with releaseDefinition none`);
  }
  return {
    words: parts.length > 0 ? `Found ${parts.join('. Found ')}.` : 'No pipeline of the repository was found to release from.',
    url: `${repo.base}/${encodeURIComponent(repo.project)}/_release`,
  };
}
