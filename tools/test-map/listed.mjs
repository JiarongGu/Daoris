/**
 * The units of the census of test units (`tools/test-map.mjs`) a gate's command names one by one: each `tools/**.test.mjs` file a
 * declared gate runs with `node --test`, its `npm run` scripts followed as the tool-test census follows them
 * (`tools/tool-test-census.mjs`), a unit of that gate's suite run from where its gate runs it. The CLI's suites and the merge tool's
 * are globbed (`TS_SUITES`); these are listed, the `cli-tests` gate's (suite `cli`) and the `layers` gate's among them, so a merge
 * reaches them as it reaches the rest.
 */
import { posix } from 'node:path';
import { coverage, expand } from '../tool-test-census.mjs';

const LISTED = /^tools\/.+\.test\.mjs$/;

/** The suite a gate's units are counted in: the census names the CLI's verify run `cli`, as `TS_SUITES` does, though `cli-tests` runs it. */
const SUITE_OF = Object.freeze({ 'cli-tests': 'cli' });

/** A JSON file of the tree, or an empty object where it holds none readable. */
const json = (read, path) => {
  try {
    return JSON.parse(read(path) || '{}');
  } catch {
    return {};
  }
};

/** Each listed tool test of `gates` over the tree read by `read`: its suite (its gate's), its file and the folder its gate runs it from. */
export function listedNodeTests(gates, read) {
  const scripts = { root: json(read, 'package.json').scripts ?? {}, cli: json(read, 'src/Daoris.Cli/package.json').scripts ?? {} };
  const found = new Map();
  for (const gate of gates) {
    for (const piece of expand(gate.run, scripts)) {
      for (const file of coverage([piece]).listed) {
        if (LISTED.test(file) && !found.has(file)) found.set(file, { suite: SUITE_OF[gate.name] ?? gate.name, file, cwd: piece.base || '.' });
      }
    }
  }
  return [...found.values()];
}

/**
 * The listed tool tests of the tree as census units: `reachOf` gives a file's reach, `wanted` says whether a suite is asked, and
 * `within` the declared paths that bound a suite's units (an end-to-end tier's), or null.
 */
export function listedUnits({ gates, read, files, reachOf, wanted = () => true, within = () => null }) {
  const tracked = new Set(files);
  return listedNodeTests(gates, read).filter((each) => wanted(each.suite) && tracked.has(each.file)).map((each) => {
    const reach = reachOf(each.file);
    const bound = within(each.suite);
    return {
      suite: each.suite, kind: 'node', unit: each.file, name: posix.basename(each.file), file: each.file, cwd: each.cwd,
      subject: each.file.replace(/\.test\.mjs$/, '.mjs'), reach: bound ? { ...reach, within: bound } : reach,
    };
  });
}
