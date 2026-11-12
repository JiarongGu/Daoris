// A merged build with no release of its own, whose release definitions make none by themselves (no continuous-deployment trigger of
// theirs takes its branch): said at the first look rather than after the find, so the step holds for the person, or, where the step's
// createRelease option asks and one definition is the release's, the release is made here from that build. A release made by then, or a
// trigger that may make one, or triggers not read, leaves the step to look for it as before.
import { releases, withoutHeads } from './azure.mjs';
import { triggeredBy } from './triggers.mjs';

const numeric = (value) => (/^\d+$/.test(String(value ?? '').trim()) ? Number(value) : null);

/** The alias `definition` takes `build`'s artifact under: the artifact of its pipeline, else its primary build artifact. */
function aliasOf(definition, build) {
  const builds = (definition.artifacts ?? []).filter((artifact) => artifact.type === 'Build');
  const own = builds.find((artifact) => numeric(artifact.definitionReference?.definition?.id) === build.definition?.id)
    ?? builds.find((artifact) => artifact.isPrimary) ?? builds[0];
  return typeof own?.alias === 'string' && own.alias !== '' ? own.alias : null;
}

/**
 * Null where the step goes on to look for the release; `{ made }`, the release made here; or `{ words }`, why no release exists and none
 * comes, which the step fails `no-release` with.
 */
export async function unreleased({ repo, build, definitions, asked, sourceId, buildNumber, commit }) {
  if (definitions.length === 0 || definitions.some((each) => !Array.isArray(each.triggers))) return null;
  if (triggeredBy(definitions, build).length > 0) return null;
  const ids = new Set(definitions.map((each) => each.id));
  const query = `artifactTypeId=Build&sourceId=${encodeURIComponent(sourceId)}&artifactVersionId=${build.id}`;
  const found = ((await releases(repo, 'GET', `/releases?${query}`)).json?.value ?? []).filter((each) => ids.has(each.releaseDefinition?.id));
  if (found.length > 0) return null;

  const branch = withoutHeads(build.sourceBranch);
  const of = `build ${buildNumber} (${commit.slice(0, 8)})`;
  if (definitions.length > 1) {
    return { words: `No release exists for ${of}: none of ${definitions.map((each) => each.name).join(', ')} has a continuous-deployment trigger on \`${branch}\`, `
      + 'so none is made by itself. Make one and try the step again, or name one in releaseDefinition and set createRelease.' };
  }

  const [definition] = definitions;
  const alias = aliasOf(definition, build);
  if (asked.create && alias) {
    const made = await releases(repo, 'POST', '/releases', {
      definitionId: definition.id, isDraft: false, description: `Made by Daoris's release step for ${of}.`,
      artifacts: [{ alias, instanceReference: { id: String(build.id), name: String(buildNumber) } }],
    });
    if (numeric(made.json?.id) !== null) return { made: made.json };
    const why = typeof made.json?.message === 'string' ? `: ${made.json.message.slice(0, 120)}` : '';
    return { words: `No release of ${definition.name} exists for ${of}, and Azure DevOps refused to make one (${made.status})${why}.` };
  }

  const off = definition.triggers.some((trigger) => trigger.triggerType === 'artifactSource') ? `does not take \`${branch}\`` : 'is off';
  return { words: `No release of ${definition.name} exists for ${of}: its continuous-deployment trigger ${off}, so none is made by itself. `
    + (asked.create ? `Its build artifact has no alias to make one from; make one and try the step again.`
      : `Make one and try the step again, or set the step's createRelease option.`) };
}
