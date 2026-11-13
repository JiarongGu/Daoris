// The release step's target as its options declare it, by stable id: the deployable it releases, the build pipeline, the release definition
// (`none` for a pipeline run's own stages), the exact stage, the action, what counts as success and the earliest act that deploys into it.
// What the forge holds for a pipeline is only ever listed for the person as candidates (discover.mjs): a label, a trigger or the releases
// already made describe the forge's behaviour, not the intent.
import { discover, discoveryWords } from './discover.mjs';

export const ACTIONS = ['observe', 'start-stage', 'create-and-start'];

/** The stage results each `success` takes as reached. */
export const SUCCESS = { succeeded: ['succeeded'], 'partially-succeeded': ['succeeded', 'partiallySucceeded'] };

/** The acts that can deploy into a target, earliest first; one undeclared is read as `push`, so every exposing act waits. */
export const DEPLOYS_ON = ['push', 'pull-request', 'merge', 'start'];

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
  const deploysOn = given.deploysOn === undefined ? 'push' : DEPLOYS_ON.includes(given.deploysOn) ? given.deploysOn : null;
  const missing = [['pipeline', pipeline], ['releaseDefinition', definition], ['stage', stage], ['action', action], ['success', success], ['deploysOn', deploysOn]]
    .filter(([, value]) => value === null).map(([name]) => name);
  return {
    deployable: text(given.deployable), pipeline, definition, stage, action, success, deploysOn, missing,
    find: seconds(given.findSeconds, 600) * 1000, every: seconds(given.everySeconds, 30) * 1000,
  };
}

/** Whose target a sentence speaks of: the deployable the options name, else the step's. */
const whose = (deployable) => (deployable ? ` for deployable \`${deployable}\`` : '');

/** Why the step holds for a declaration it cannot act on. */
export const undeclared = (missing, deployable = null) => (missing.length >= 4
  ? `No release target is declared${whose(deployable)}: ${joined(missing.slice(0, 4))}.`
  : `The release target${whose(deployable).replace(' for', ' of')} is declared without ${joined(missing)}.`);

/** Why the step holds for a declared target that no longer resolves. */
export const changedWords = (why, deployable = null) =>
  `The declared release target${whose(deployable).replace(' for', ' of')} no longer resolves: ${why}.`;

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

/** The candidates a hold lists: `{ words, url }`, the words what discovery found for each pipeline, the url the project's release hub. */
export async function candidates({ repo, repositoryId, commit, target, pipeline }) {
  const found = await discover({ repo, repositoryId, commit, target, pipeline });
  return { words: discoveryWords(found), url: found.url };
}
