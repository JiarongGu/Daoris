/**
 * The steps of the merge's `cli` gate (`npm run verify:checks`), each with what it reads: the one list `tools/verify-checks.mjs` runs,
 * and `npm run quick` runs with its own writers before it.
 *
 * A step's `reads` holds paths as a gate's `paths` do (an entry ending in `/` is a folder, one with `*` a glob, any other a file), or is
 * `'*'`, every path. `node tools/test-map.mjs --check` holds each to what its command's code reads (`tools/verify-checks/reads.mjs`): a
 * step whose tool lists the tree names the listing here, as a census names its reads. `writes` runs first and alone. `aside` names the
 * gate that runs the same check in every merge, with the private patterns, so the step stands aside where the merge's paths are given.
 */
import { globToRegExp } from '../merge/lanes-file.ts';

/** What every tool reads through the shared helpers it imports. */
const NODE = ['tools/fsx.mjs', 'src/Daoris.Cli/src/platform/', 'src/Daoris.Cli/src/tools/gitforwindows.ts'];
/** Every hand-written source file, as the comment gate and the size ratchet list them. */
const SOURCES = ['**/*.{cs,ts,tsx,mjs,js}', '**/*.{cs,ts,tsx,mjs,js}.template'];
/** The prose and the configuration the documents' checks read, wherever it sits. */
const DOCUMENTS = ['**/*.md', 'docs/', 'canon/', '.claude/', 'examples/', 'daoris.json', 'daoris.lock', 'daoris.gates.json', 'llms.txt', 'context7.json'];
const DRIVER = 'src/Daoris.Desktop/Daoris.Desktop.Driver/';
/** The test trees the tests' censuses walk. */
const TESTS = ['src/Daoris.Cli/', 'tools/'];
const PARTIALS = ['tools/partials.mjs', 'tools/partials.json'];

const CONTRACTS = [
  ...NODE, 'tools/contracts.mjs', 'tools/contracts/', 'tools/run-lock.mjs', 'src/Daoris.Contracts/', 'src/Daoris.Desktop/Daoris.Desktop.Modules/',
  `${DRIVER}Settings/List/`, `${DRIVER}Workflows/Kinds/`, `${DRIVER}Agents/Providers/`, `${DRIVER}Agents/Registry/`,
  'src/Daoris.Web/src/data/generated/', 'src/Daoris.Web/src/bridge/generated/',
];

const step = (run, reads, more = {}) => Object.freeze({ run, reads: reads === '*' ? '*' : Object.freeze([...new Set(reads)]), ...more });

export const CHECK_STEPS = Object.freeze([
  step('node tools/orient-index.mjs', '*', { writes: true }),
  step('node tools/contracts.mjs --if-stale', CONTRACTS, { writes: true }),
  step('npm run typecheck -w daoris', ['src/Daoris.Cli/', 'tools/', 'package.json', 'package-lock.json']),
  step('npm run typecheck:tools', ['tools/', 'src/Daoris.Cli/', 'package.json', 'package-lock.json']),
  step('node src/Daoris.Cli/bin/daoris.mjs check', '*'),
  step('node tools/orient-index.mjs --check', '*'),
  step('node tools/contracts.mjs --check', CONTRACTS),
  step('node tools/doc-budgets.mjs', [...NODE, ...DOCUMENTS, 'tools/doc-budgets.mjs', 'tools/doc-budgets.json', 'tools/doc-budgets/']),
  // It holds what the present records say against every source file's names.
  step('node tools/doc-current.mjs', '*'),
  step('node tools/doc-shapes.mjs', [
    ...NODE, ...DOCUMENTS, 'tools/doc-shapes.mjs', 'tools/doc-shapes.json', 'tools/doc-budgets.mjs', 'tools/doc-budgets.json', 'tools/doc-budgets/',
    'tools/doc-duplicates.mjs',
  ]),
  step('node tools/doc-duplicates.mjs', [...NODE, ...DOCUMENTS, 'tools/doc-duplicates.mjs']),
  step('node tools/doc-system.mjs', [...NODE, ...DOCUMENTS, 'tools/doc-system.mjs', 'tools/doc-duplicates.mjs']),
  // A link breaks where the file it names is removed or renamed, wherever that file sits.
  step('node tools/doc-links.mjs', '*'),
  step('node tools/partials.mjs', [...NODE, ...PARTIALS, 'src/**/*.cs']),
  step('node tools/read-shares.mjs', [...NODE, ...PARTIALS, DRIVER, 'tools/read-shares.mjs']),
  step('node tools/optional-locks.mjs', [...NODE, ...PARTIALS, DRIVER, 'tools/']),
  // The layers' counts list every C# source and project file wherever it sits, and hold them to the list and the lanes.
  step('node tools/layers-census/counts.mjs', [
    ...NODE, ...PARTIALS, '**/*.{cs,csproj}', 'src/Daoris.Adapters/', '**/daoris.lanes.json', 'tools/layers-census.json', 'tools/layers-census/',
    'tools/merge/lanes-file.ts', 'tools/merge/packages.ts', 'daoris.packages.lock', 'src/Daoris.Assistant/', 'src/Daoris.Ports/',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/resources.json', 'src/Daoris.Desktop/Directory.Packages.props',
  ]),
  step('node tools/comment-gate.mjs', [...NODE, ...SOURCES, 'tools/']),
  step('node tools/comment-gate.mjs --all', [...NODE, ...SOURCES, 'tools/']),
  step('node tools/size-ratchet.mjs', [...NODE, ...SOURCES, 'tools/']),
  // The records it judges are the timing files this run's gates write, which no changed path names.
  step('node tools/time-ratchet.mjs', '*'),
  step('node tools/live-logins.mjs', [...NODE, 'src/', 'tools/']),
  step('node tools/tool-test-census.mjs', [...NODE, ...TESTS, 'daoris.gates.json', 'examples/game/daoris.gates.json', 'package.json', 'src/Daoris.Web/']),
  step('node tools/timing-census.mjs', [...NODE, 'src/', 'tools/', 'examples/', 'daoris.gates.json']),
  step('node tools/unit-pure.mjs', [...NODE, ...TESTS, 'daoris.gates.json', 'examples/game/daoris.gates.json', 'TASKS.md', 'tools/unit-pure.json', 'tools/unit-pure/', 'src/Daoris.Web/']),
  step('node tools/console-windows.mjs', [...NODE, 'tools/', 'src/Daoris.Cli/', 'src/Daoris.Web/', 'examples/']),
  step('node tools/test-patience.mjs', [...NODE, ...TESTS, ...DOCUMENTS, DRIVER, 'src/Daoris.Web/']),
  step('node tools/release-prep.mjs --check', [
    ...NODE, ...DOCUMENTS, 'tools/release-prep.mjs', 'src/Daoris.Cli/package.json', 'src/Daoris.Devkit/Daoris.Devkit.Cli/Repository.cs',
  ]),
  step('node tools/as-merged.mjs dotnet run --project src/Daoris.Devkit/Daoris.Devkit.Cli -- verify --universal-only --allow-builtins-only', '*', { aside: 'universal' }),
]);

/** Whether `reads` holds `path`. */
export const holds = (reads, path) => reads === '*' || reads.some((entry) => (entry.endsWith('/') ? path.startsWith(entry) : entry.includes('*') ? globOf(entry).test(path) : path === entry));

const GLOBS = new Map();
const globOf = (glob) => {
  if (!GLOBS.has(glob)) GLOBS.set(glob, globToRegExp(glob));
  return GLOBS.get(glob);
};

/**
 * The steps a run takes, and each one left out with why. With no `changed` every step runs; with the merge's changed paths, a step runs
 * where one of them is a path it reads, and a step whose `aside` gate the repository declares (`gates`) stands aside.
 */
export function selectSteps(steps, changed, { gates = [] } = {}) {
  if (!changed) return { run: [...steps], left: [] };
  const run = [];
  const left = [];
  for (const each of steps) {
    if (each.aside && gates.includes(each.aside)) left.push({ step: each, why: `the ${each.aside} gate runs it in every merge` });
    else if (each.reads === '*' || changed.some((path) => holds(each.reads, path))) run.push(each);
    else left.push({ step: each, why: 'no changed path is one it reads' });
  }
  return { run, left };
}
