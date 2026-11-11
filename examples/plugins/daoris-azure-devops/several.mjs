// A build taken by several release definitions (a dev, a QA and a prod one, as a repository's pipeline usually feeds): the releases
// actually made from it are watched, those of the definitions whose CD trigger takes the build's branch awaited, a release whose stages all
// start by hand left alone. Approvals still decide, and nothing is started here but a stage `deploy` names.
import { releases, withoutHeads } from './azure.mjs';
import { until, watchRelease } from './stages.mjs';
import { startsByHand, triggeredBy } from './triggers.mjs';

const RANK = { failed: 0, timeout: 1, done: 2 };

/**
 * `{ verdict: 'done' | 'failed' | 'timeout' | 'missing', words, url }` for the releases of `definitions` made from `build`, or
 * `{ verdict: 'none', words }` where none is made that anything awaits. `by` is when the look for the releases the triggers make ends.
 */
export async function watchSeveral({ repo, build, definitions, asked, by, ends, say, commit, sourceId, buildNumber }) {
  const branch = withoutHeads(build.sourceBranch);
  const triggered = triggeredBy(definitions, build);
  const names = (list) => list.map((each) => each.name).join(', ');
  const taken = new Map(definitions.map((each) => [each.id, each]));
  let found = [];
  const read = async () => {
    const query = `artifactTypeId=Build&sourceId=${encodeURIComponent(sourceId)}&artifactVersionId=${build.id}&$expand=environments`;
    const newest = new Map();
    for (const each of (await releases(repo, 'GET', `/releases?${query}`)).json?.value ?? []) {
      const id = each.releaseDefinition?.id;
      if (taken.has(id) && !(newest.get(id)?.id > each.id)) newest.set(id, each);
    }
    found = [...newest.values()];
    const complete = triggered.length > 0 ? triggered.every((each) => newest.has(each.id)) : found.length > 0;
    return complete ? found : undefined;
  };
  await until(read, by, asked.every, say);
  const made = found.map((release) => ({ release, definition: taken.get(release.releaseDefinition.id) }));
  const awaiting = made.filter((each) => !startsByHand(each.release));
  const label = (each) => `${each.release.name} of ${each.definition.name} from build ${buildNumber} (${commit.slice(0, 8)})`;
  if (awaiting.length === 0) {
    const hands = made.length > 0 ? `; ${names(made.map((each) => each.definition))} made ${made.map((each) => each.release.name).join(', ')}, whose stages start by hand` : '';
    return { verdict: 'none', words: triggered.length > 0 || made.length > 0
      ? `no release that starts by itself was made from build ${buildNumber} within ${asked.find / 1000} s${hands}; name one in the step's releaseDefinition option`
      : `no release was made from build ${buildNumber} within ${asked.find / 1000} s: none of ${names(definitions)} has a trigger on \`${branch}\`` };
  }
  const watched = await Promise.all(awaiting.map(async (each) => ({ ...each, watched: await watchRelease(repo, each.release.id, asked, ends, say, commit) })));
  const kept = watched.filter((each) => each.watched.verdict !== 'missing');
  if (kept.length === 0) return { verdict: 'missing', missing: watched[0].watched.missing, words: '' };
  const ordered = [...kept].sort((a, b) => RANK[a.watched.verdict] - RANK[b.watched.verdict]);
  const hands = made.filter((each) => !awaiting.includes(each)).map((each) => `${each.definition.name} made ${each.release.name}, whose stages start by hand, not awaited`);
  const web = ordered[0].release._links?.web?.href;
  return { verdict: ordered[0].watched.verdict, url: /^https?:\/\//.test(web ?? '') ? web : undefined,
    words: [...ordered.map((each) => `${label(each)}: ${each.watched.words}`), ...hands].join(' | ') };
}
