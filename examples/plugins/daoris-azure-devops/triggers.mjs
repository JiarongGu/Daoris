// What starts what: whether a pipeline builds a merge commit (its CI trigger's branch and path filters, a YAML pipeline's read from the
// file at that commit), which release definitions an artifact's build triggers, and which stages a release's creation starts by itself. Anything not read in a shape known here answers "unknown", so a doubt keeps the step waiting rather than ending it.
import { call, project, withoutHeads } from './azure.mjs';

const escaped = (text) => text.replace(/[.+?^${}()|[\]\\]/g, '\\$&');
const globbed = (pattern) => new RegExp(`^${pattern.split('*').map(escaped).join('.*')}$`);
const branchName = (text) => String(text).trim().replace(/^[+-]/, '').replace(/^refs\/heads\//, '');
const pathName = (text) => String(text).trim().replace(/^[+-]/, '').replace(/^\/+/, '').replace(/\/+$/, '');

const branchHit = (pattern, name) => globbed(branchName(pattern)).test(name);
const pathHit = (pattern, path) => globbed(pathName(pattern)).test(path) || globbed(`${pathName(pattern)}/*`).test(path);

/** `{ include, exclude }` patterns: a value is selected when it is included (all are, where none is named) and not excluded. */
const EVERYTHING = { include: [], exclude: [] };
const selects = (filter, value, hit) => (filter.include.length === 0 || filter.include.some((each) => hit(each, value))) && !filter.exclude.some((each) => hit(each, value));

/** A classic trigger's filter strings (`+refs/heads/main`, `-/docs`) as a filter. */
const filterOf = (strings) => ({ include: (strings ?? []).filter((each) => !String(each).startsWith('-')), exclude: (strings ?? []).filter((each) => String(each).startsWith('-')) });

const unquoted = (text) => text.trim().replace(/^(['"])(.*)\1$/, '$2');
const scalars = (text) => {
  const inner = /^\[(.*)\]$/.exec(text.trim());
  return (inner ? inner[1].split(',') : [text]).map(unquoted).filter(Boolean);
};

/** The include and exclude lists of a trigger's `branches` or `paths`: a list alone is its includes; null where not in a known shape. */
function filterBlock(lines) {
  const filter = { include: [], exclude: [] };
  let into = filter.include;
  for (const line of lines) {
    const item = /^-\s*(.+)$/.exec(line);
    const key = /^(include|exclude)\s*:\s*(.*)$/.exec(line);
    if (item) into.push(unquoted(item[1]));
    else if (key) {
      into = filter[key[1]];
      if (key[2].trim() !== '') into.push(...scalars(key[2]));
    } else return null;
  }
  return filter;
}

/**
 * The `trigger:` of a pipeline's YAML: undefined where it names none (every branch, every path), 'none' where it is off, else
 * `{ branches, paths }`; null where it is written in a shape not read here.
 */
export function yamlTrigger(text) {
  const rows = text.split(/\r?\n/).map((row) => row.replace(/(^|\s)#.*$/, '')).filter((row) => row.trim() !== '');
  const at = rows.findIndex((row) => /^trigger\s*:/.test(row));
  if (at < 0) return undefined;
  const inline = rows[at].replace(/^trigger\s*:/, '').trim();
  const body = [];
  for (const row of rows.slice(at + 1)) {
    if (!/^\s/.test(row) && !(inline === '' && row.startsWith('- '))) break;
    if (row.includes('\t')) return null;
    body.push(row);
  }
  if (inline === 'none') return body.length === 0 ? 'none' : null;
  if (inline !== '') return body.length === 0 ? { branches: { include: scalars(inline), exclude: [] }, paths: EVERYTHING } : null;
  if (body.length === 0) return null;
  const indent = (row) => row.length - row.trimStart().length;
  if (body.every((row) => row.trim().startsWith('- '))) return { branches: filterBlock(body.map((row) => row.trim())), paths: EVERYTHING };
  const top = indent(body[0]);
  const parts = new Map();
  let key = null;
  for (const row of body) {
    const named = indent(row) === top ? /^(\w+)\s*:\s*(.*)$/.exec(row.trim()) : null;
    if (indent(row) === top && !named) return null;
    if (named) {
      key = named[1];
      parts.set(key, named[2].trim() === '' ? [] : [`include: ${named[2]}`]);
    } else if (key) parts.get(key).push(row.trim());
  }
  const read = (name) => (parts.has(name) ? filterBlock(parts.get(name)) : EVERYTHING);
  const branches = read('branches');
  const paths = read('paths');
  if (!branches || !paths || [...parts.keys()].some((each) => !['branches', 'paths', 'batch', 'tags'].includes(each))) return null;
  return { branches, paths };
}

/** The YAML of a definition's file at `commit`, else null. */
async function yamlAt(repo, definition, commit) {
  const file = String(definition.process?.yamlFilename ?? '');
  if (file === '') return null;
  const read = await call(repo, 'GET', `/items?path=${encodeURIComponent(`/${file.replace(/^\/+/, '')}`)}&versionDescriptor.version=${commit}&versionDescriptor.versionType=commit&includeContent=true`);
  return read.status === 200 && typeof read.json?.content === 'string' ? read.json.content : null;
}

/** A build definition's CI trigger: 'none', `{ branches, paths }` (everything where it sets none), or null where it cannot be told. */
async function ciOf(repo, definition, commit) {
  if (!Array.isArray(definition.triggers)) return null;
  const ci = definition.triggers.find((each) => each.triggerType === 'continuousIntegration');
  if (ci?.settingsSourceType === 1 || (definition.process?.type !== 2 && ci)) return { branches: filterOf(ci.branchFilters), paths: filterOf(ci.pathFilters) };
  if (definition.process?.type !== 2) return 'none';
  const text = await yamlAt(repo, definition, commit);
  if (text === null) return null;
  const read = yamlTrigger(text);
  return read === undefined ? { branches: EVERYTHING, paths: EVERYTHING } : read;
}

/** The paths the merge commit changed, else null where they are not all known. */
async function changesOf(repo, commit) {
  const json = (await call(repo, 'GET', `/commits/${commit}/changes?$top=1000`)).json;
  const listed = (json?.changes ?? []).map((each) => each?.item?.path).filter((each) => typeof each === 'string');
  const counted = Object.values(json?.changeCounts ?? {}).reduce((sum, each) => sum + Number(each), 0);
  return listed.length === 0 || counted > listed.length ? null : listed;
}

/** Why `pipeline` does not build `commit` merged into `target` (its CI trigger off, or leaving out the branch or every path), else null. */
export async function builtNothing(repo, repositoryId, commit, target, pipeline) {
  const definitions = (await project(repo, 'GET', `/build/definitions?repositoryId=${encodeURIComponent(repositoryId)}&repositoryType=TfsGit&includeAllProperties=true`)).json?.value;
  const definition = Array.isArray(definitions) ? definitions.find((each) => each.id === pipeline) : undefined;
  if (!definition) return null;
  const changed = await changesOf(repo, commit);
  if (!changed) return null;
  const branch = withoutHeads(target);
  const ci = await ciOf(repo, definition, commit);
  if (ci === null) return null;
  if (ci !== 'none' && selects(ci.branches, branch, branchHit) && changed.some((path) => selects(ci.paths, pathName(path), pathHit))) return null;
  const shown = changed.length > 3 ? `${changed.slice(0, 3).join(', ')} and ${changed.length - 3} more` : changed.join(', ');
  return `pipeline ${pipeline} does not build ${commit.slice(0, 8)} on \`${branch}\`, which changed ${shown}`;
}

const numeric = (value) => (/^\d+$/.test(String(value ?? '').trim()) ? Number(value) : null);

/** The release definitions, of those that take `build`'s pipeline, with a CD trigger on the branch the build ran on. */
export function triggeredBy(definitions, build) {
  const branch = withoutHeads(build.sourceBranch);
  return definitions.filter((definition) => {
    const aliases = (definition.artifacts ?? []).filter((artifact) => artifact.type === 'Build' && numeric(artifact.definitionReference?.definition?.id) === build.definition?.id)
      .map((artifact) => artifact.alias);
    return (definition.triggers ?? []).some((trigger) => trigger.triggerType === 'artifactSource'
      && (aliases.length === 0 || !trigger.artifactAlias || aliases.includes(trigger.artifactAlias))
      && ((trigger.triggerConditions ?? []).length === 0
        || trigger.triggerConditions.some((each) => each.useBuildDefinitionBranch || !each.sourceBranch || branchHit(each.sourceBranch, branch))));
  });
}

/** The stages a release's creation starts by itself, on its start event or chained after one; one not read counts, so a doubt is said. */
export function selfStarting(stages) {
  const byName = new Map(stages.map((stage) => [stage.name, stage]));
  const itself = (stage, seen = new Set()) => !Array.isArray(stage.conditions) || (!seen.has(stage.name) && stage.conditions.some((each) =>
    each.conditionType === 'event' || (each.conditionType === 'environmentState' && byName.has(each.name) && itself(byName.get(each.name), seen.add(stage.name)))));
  return stages.filter((stage) => itself(stage)).map((stage) => stage.name);
}
