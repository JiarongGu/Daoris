import { existsSync, readdirSync, readFileSync, rmdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { repositoryFiles as listFiles, writeAtomic } from '../fsx.mjs';
import { LANES_FILE, laneMatcher, readLanes } from '../merge-branch.mjs';
import { registeredPage } from '../orient-registered.mjs';
import { declaredPath, largeFiles, outlinePage, readManifest } from './records.mjs';
import { GENERATED, INDEX, LARGE, compare, kb, reader } from './common.mjs';
import { routesPage } from './routes.mjs';
import { verbsPage } from './verbs.mjs';
import { cataloguesPage, fixturesPage } from './catalogues.mjs';
import { decisionsPage } from './decisions.mjs';
import { outlineOf } from './outline.mjs';

/** The code map: the projects, each with its path, written and checked by the devkit's `map`. */
const CODE_MAP = 'docs/code-map.json';

/**
 * The indexes kept elsewhere, each named only when the tree holds it (the canon's `templates/index.md`).
 * `byHand` are the README's *Kept by hand* rows, listed before the generated files because nothing below restates
 * theirs: the router, then the lanes map, whose lanes group the large files below. `generated` are the indexes
 * another tool writes, named in the opening as the template names the knowledge's: the doctrine's `INDEX.md` and
 * the code map.
 */
function keptElsewhere(ctx) {
  const manifest = readManifest(ctx.root);
  const router = declaredPath(manifest, 'router');
  const byHand = [
    router && ctx.has(router) && [router, 'every document, its kind, what it is for and where it stands'],
    ctx.has(LANES_FILE) && [LANES_FILE, `each lane's id, title and summary, and the paths it owns: which part of the code a path is in, and how the files over ${LARGE / 1024} KB below are grouped`],
  ].filter(Boolean);
  const knowledge = `${(typeof manifest.target === 'string' ? manifest.target : '.claude').replace(/\\/g, '/').replace(/\/+$/, '')}/INDEX.md`;
  const generated = [
    ctx.has(knowledge) && `the knowledge and the skills in \`${knowledge}\`, written by \`daoris sync\``,
    ctx.has(CODE_MAP) && `the projects in \`${CODE_MAP}\`, written by the devkit's \`map\``,
  ].filter(Boolean);
  return { byHand, generated };
}

function readmePage(ctx, large) {
  const { byHand, generated } = keptElsewhere(ctx);
  const out = [
    '# The orientation index',
    '',
    `${GENERATED} Read here before searching: each row names a file and line to open, and a range to read with offset and limit. It is never committed: each checkout writes its own.`,
  ];
  if (generated.length) {
    out.push('', `Listed elsewhere, by tools of their own: ${generated.join('; ')}. Open those for what they list: nothing here restates ${generated.length === 1 ? 'it' : 'them'}.`);
  }
  out.push('', '## Kept by hand', '');
  if (byHand.length) {
    out.push(
      'Listed first, because nothing below restates their rows: open one of these for what it answers.',
      '',
      '| Index | Answers |',
      '|---|---|',
      ...byHand.map(([path, answers]) => `| \`${path}\` | ${answers} |`),
    );
  } else {
    out.push('None: this tree keeps no index by hand.');
  }
  out.push(
    '',
    '## Generated',
    '',
    '| File | Answers |',
    '|---|---|',
    '| `routes.md` | which module answers a bridge route, and which bridge function sends it; the service\'s HTTP routes |',
    '| `verbs.md` | where each terminal verb is routed and handled: `daoris`, `daoris-driver`, the devkit, `npm run desktop`, the npm scripts |',
    '| `catalogues.md` | which catalogue file holds a key, by area; the glossary\'s sections, kinds and terms by line |',
    '| `fixtures.md` | the test fixtures, stand-ins and kits by name, with file and line |',
    '| `decisions.md` | each decision\'s title with its entry\'s lines, then each dated note with its lines |',
    '| `registered.md` | each root\'s parts by seam, in the order it runs them, with each mark\'s file and line |',
    `| \`outlines/<path>.md\` | the declarations or headings of a file over ${LARGE / 1024} KB, each with its line range |`,
    '',
    `## Files over ${LARGE / 1024} KB (${large.length})`,
    '',
    'Each has its outline at `outlines/<its path>.md`: open the outline, then read the range you need, not the file.',
  );
  let lanes = null;
  try {
    lanes = readLanes(ctx.root)?.lanes ?? null;
  } catch {
    lanes = null;
  }
  const groups = new Map();
  for (const file of large) {
    const steward = lanes?.find((lane) => lane.steward && laneMatcher(lane.paths)(file.path));
    const lane = steward ?? lanes?.find((candidate) => laneMatcher(candidate.paths)(file.path));
    const name = lane ? `${lane.title} (\`${lane.id}\`)` : lanes ? 'No lane' : 'Files';
    if (!groups.has(name)) groups.set(name, []);
    groups.get(name).push(file);
  }
  const order = [...(lanes ?? []).map((lane) => `${lane.title} (\`${lane.id}\`)`), 'No lane', 'Files'];
  for (const name of order.filter((group) => groups.has(group))) {
    out.push('', `### ${name}`, '', '| File | KB | Lines |', '|---|---|---|');
    for (const file of groups.get(name)) out.push(`| \`${file.path}\` | ${kb(file.bytes)} | ${file.lines} |`);
  }
  return `${out.join('\n')}\n`;
}

// ---------------------------------------------------------------------------------------------------
// Plan, check, apply

/** The index the tree at `root` says, as `docs/index/…` path to content. Writes nothing. */
export function planIndex(root, files = listFiles(root)) {
  const ctx = reader(root, files.filter((path) => !path.startsWith(`${INDEX}/`)));
  const large = largeFiles(ctx);
  const planned = new Map([
    [`${INDEX}/README.md`, readmePage(ctx, large)],
    [`${INDEX}/routes.md`, routesPage(ctx)],
    [`${INDEX}/verbs.md`, verbsPage(ctx)],
    [`${INDEX}/catalogues.md`, cataloguesPage(ctx)],
    [`${INDEX}/fixtures.md`, fixturesPage(ctx)],
    [`${INDEX}/decisions.md`, decisionsPage(ctx)],
    [`${INDEX}/registered.md`, registeredPage(ctx, GENERATED)],
  ]);
  for (const file of large) planned.set(`${INDEX}/outlines/${file.path}.md`, outlinePage(file, outlineOf(file.path, ctx.read(file.path)) ?? []));
  return planned;
}

function filesUnder(root, at) {
  const folder = join(root, at);
  if (!existsSync(folder)) return [];
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = `${at}/${entry.name}`;
    return entry.isDirectory() ? filesUnder(root, path) : [path];
  });
}

/** Where `docs/index/` differs from the plan: a file missing, stale, or not one the tool generates. */
export function staleness(root, planned) {
  const found = [];
  for (const [path, content] of planned) {
    const file = join(root, path);
    if (!existsSync(file)) found.push({ path, fact: 'missing' });
    else if (readFileSync(file, 'utf8') !== content) found.push({ path, fact: 'stale' });
  }
  for (const path of filesUnder(root, INDEX)) if (!planned.has(path)) found.push({ path, fact: 'not generated' });
  return found.sort((a, b) => compare(a.path, b.path));
}

function removeEmpty(folder) {
  if (!existsSync(folder)) return;
  for (const entry of readdirSync(folder, { withFileTypes: true })) if (entry.isDirectory()) removeEmpty(join(folder, entry.name));
  if (readdirSync(folder).length === 0) rmdirSync(folder);
}

/** Writes what the plan changes, each file beside and renamed, and removes what it does not generate. */
export function applyIndex(root, planned) {
  const changed = staleness(root, planned);
  for (const { path, fact } of changed) {
    const file = join(root, path);
    if (fact === 'not generated') {
      rmSync(file, { force: true });
      continue;
    }
    writeAtomic(file, planned.get(path));
  }
  removeEmpty(join(root, INDEX));
  return changed;
}
