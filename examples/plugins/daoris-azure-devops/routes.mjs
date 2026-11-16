// The place's route table: which release definition a landed branch releases through, read from each definition's continuous-deployment
// trigger at the moment it is asked, never kept and never from a label. The branch the work merged into routes to the definitions whose
// trigger takes it; an act on a definition the route does not reach is refused `target-changed` at the act, listing the route
// as the choice, so a feature branch's build is never released through the main line's definition.
import { heads, releases } from './azure.mjs';
import { triggeredBy } from './triggers.mjs';

const numeric = (value) => (/^\d+$/.test(String(value ?? '').trim()) ? Number(value) : null);

/** Whether `definition` takes `pipeline`'s builds: an artifact of type Build naming it. */
const takes = (definition, pipeline) => (definition?.artifacts ?? [])
  .some((artifact) => artifact.type === 'Build' && numeric(artifact.definitionReference?.definition?.id) === pipeline);

/** Whether `definition` releases by itself on any branch: a continuous-deployment trigger. */
export const triggered = (definition) => (definition?.triggers ?? []).some((each) => each.triggerType === 'artifactSource');

/** The release definitions taking `pipeline`'s builds, as Azure DevOps holds them now. */
export async function definitionsTaking(repo, pipeline) {
  const all = (await releases(repo, 'GET', '/definitions?$expand=artifacts,triggers,environments')).json?.value ?? [];
  return all.filter((each) => takes(each, pipeline));
}

/** Of `definitions`, the route of a build of `pipeline` from `branch`: those whose trigger takes that branch. */
export const routed = (definitions, pipeline, branch) => triggeredBy(definitions, { sourceBranch: heads(branch), definition: { id: pipeline } });

const named = (each) => `#${each.id} ${each.name}`;

/** The route in words: the one definition, several for the release manager's line to choose between, or none. */
export function routeWords(route, branch) {
  if (route.length === 0) return `No release definition's trigger takes \`${branch}\`.`;
  if (route.length === 1) return `The route for \`${branch}\`: ${named(route[0])}.`;
  return `Several definitions' triggers take \`${branch}\` (${route.map(named).join(', ')}): the release manager's line chooses.`;
}

/**
 * Why acting on `definition` with a build from `branch` leaves the route, or null where it keeps to it: the route reaches it, or the definition
 * releases by no trigger at all (one released only by hand is a choice its declaration made, whatever another's trigger takes). A definition
 * whose own trigger leaves the branch out is another line's. `{ route, why }`.
 */
export function offRoute(definition, definitions, pipeline, branch) {
  const route = routed(definitions, pipeline, branch);
  if (route.some((each) => each.id === definition.id) || !triggered(definition)) return null;
  return {
    route,
    why: route.length > 0
      ? `a build from \`${branch}\` releases through ${route.map(named).join(', ')} by its trigger, not ${named(definition)}`
      : `${named(definition)}'s trigger does not take \`${branch}\``,
  };
}
