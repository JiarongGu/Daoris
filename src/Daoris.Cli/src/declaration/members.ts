/**
 * The converter's data: where each member of `daoris.gates.json` goes. One test sorts every member: must every clone
 * hold the same value? Then it is the repository's, in the `daoris/` folder. Is it tuned to one machine, a private binding or a grant
 * of power? Then it is the home's. Is it the same for every repository? Then it is the product's, its documentation. A member this
 * table does not name goes to neither side: the conversion stops and names it, so a part nobody sorted is never placed by luck.
 */

/** Where a member goes: the repository's folder, the machine's home. */
export type Side = 'repository' | 'home';

/** A gate's members. `after` is the folder's own: where a gate sits among the others. */
export const GATE_MEMBERS: Readonly<Record<string, Side>> = {
  name: 'repository',
  run: 'repository',
  cwd: 'repository',
  regenerate: 'repository',
  handback: 'repository',
  reuse: 'repository',
  clock: 'repository',
  paths: 'repository',
  timeoutMinutes: 'home',
  timeoutWhy: 'home',
  memoryGb: 'home',
  alone: 'home',
};

/** A test set's members, `gate` aside, which the set's place (its gate's file) carries. */
export const SET_MEMBERS: Readonly<Record<string, Side>> = {
  name: 'repository',
  suite: 'repository',
  runner: 'repository',
  cwd: 'repository',
  config: 'repository',
  project: 'repository',
  files: 'repository',
  whole: 'repository',
  setup: 'repository',
  bounded: 'repository',
  last: 'repository',
  of: 'repository',
  args: 'repository',
  timeoutMs: 'home',
};

/** The members of `tests` besides its sets: `_why` documents the format, which is the product's, so it is written nowhere. */
export const TESTS_PRODUCT = ['_why'] as const;

/** The members that are one small file each in the folder, the file's name, and whether the member is an array its file wraps by name. */
export const CONCERNS: readonly { member: string; file: string; wrap?: true }[] = [
  { member: 'tiers', file: 'tiers.json' },
  { member: 'generated', file: 'generated.json', wrap: true },
  { member: 'reached', file: 'reached.json' },
  { member: 'admission', file: 'admission.json' },
  { member: 'doctrine', file: 'doctrine.json' },
  { member: 'docs', file: 'docs.json' },
  { member: 'integration', file: 'merge.json' },
  { member: 'version', file: 'version.json' },
];

/** The members that go whole to the home. */
export const HOME_WHOLE = ['devkit', 'treeBaseline', 'localInputs', 'unasked'] as const;

/**
 * The sections that split between the two sides, each member named on one: the home's go to the home and the repository's stay with
 * the section in its file. `verifyBudget`'s `budgetMinutes` and `fullRunReasons` are the repository's own rule, the same on every machine; only its
 * journal is the machine's. An explanation stays with the section.
 */
export const SPLIT_SECTIONS: Readonly<Record<string, { file: string; home: readonly string[]; repository: readonly string[] }>> = {
  sensitive: { file: 'sensitive.json', home: ['patternsFile'], repository: ['reviewedObjects'] },
  verifyBudget: { file: 'verify-budget.json', home: ['overruns'], repository: ['budgetMinutes', 'fullRunReasons'] },
};

/** The members of `integration`, the folder's `merge.json` holding what is under `merge`. */
export const INTEGRATION_MEMBERS = ['merge'] as const;

/** The top-level members besides the ones above: the gates and tests, which the gates' files carry. */
export const TOP_GATES = 'gates';
export const TOP_TESTS = 'tests';

/** The order a gate's members are written in, so a file reads the same each time. */
export const GATE_ORDER = ['name', 'run', 'cwd', 'regenerate', 'handback', 'reuse', 'clock', 'paths', 'after', 'tests'] as const;

/** The order a set's members are written in. */
export const SET_ORDER = ['name', 'suite', 'runner', 'cwd', 'config', 'project', 'files', 'whole', 'setup', 'bounded', 'last', 'of', 'args'] as const;

/** An explanation, a member whose name begins `_`: it stays with the member it explains, on that member's side. */
export const isExplanation = (name: string): boolean => name.startsWith('_');
