/**
 * What each step of the `cli` gate reads, from its command's code, held against what the step declares (`tools/verify-checks/steps.mjs`):
 * a step whose code reads a path its `reads` does not hold fails `node tools/test-map.mjs --check`.
 *
 * - `node <tool>`: the tool's closure as test-map reads a TypeScript unit's (its imports followed, every file, folder and glob its strings
 *   name, its `// reaches:` lines). A closure that imports a module it computes is wide: the step declares `'*'`.
 * - `npm run <script>` (`-w <workspace>` in that workspace): each command of the script, read in turn.
 * - `tsc`: the files its tsconfig's `include` holds and what they import, the tsconfig, and the packages it types against.
 * - `node tools/as-merged.mjs <command>`: the tool, then the command it runs.
 * - Any other command (a `dotnet` build) is read by no census here: the step declares `'*'`.
 *
 * What a tool lists from the tree by a filter (every source file, a folder walked) no string names, so its step names the listing in its
 * `reads`; this holds what the code names, as test-map holds a unit's reach.
 */
import { posix } from 'node:path';
import { resolveImport } from '../test-map/ts-facts.mjs';
import { holds } from './steps.mjs';

const CODE = /\.(ts|tsx|mts|mjs|js)$/;
const WORDS = /"[^"]*"|'[^']*'|\S+/g;

/** A tsconfig's text as JSON: its comments and trailing commas read away. */
const tsconfigOf = (text) => JSON.parse(text.replace(/\/\*[\s\S]*?\*\/|("(?:[^"\\]|\\.)*")|\/\/[^\n]*/g, (match, string) => string ?? '').replace(/,(\s*[}\]])/g, '$1'));

/**
 * What `command` reads, run at `cwd` of a tree of `files` read by `read`, with test-map's TypeScript `reader`:
 * `{ files, folders, globs, wide }`, the same shape as a unit's reach.
 */
export function commandReach(command, { files, read, reader, cwd = '' }) {
  const reach = { files: new Set(), folders: new Set(), globs: new Set(), wide: [] };
  const join = (other) => {
    for (const each of other.files) reach.files.add(each);
    for (const each of other.folders) reach.folders.add(each);
    for (const each of other.globs) reach.globs.add(each);
    reach.wide.push(...other.wide);
  };
  for (const part of command.split(/&&|\|\||;/).map((each) => each.trim()).filter(Boolean)) {
    const words = (part.match(WORDS) ?? []).map((word) => word.replace(/^["']|["']$/g, ''));
    if (words[0] === 'node' && words[1] === 'tools/as-merged.mjs') {
      join(reader.reachOf('tools/as-merged.mjs'));
      join(commandReach(words.slice(2).join(' '), { files, read, reader, cwd }));
    } else if (words[0] === 'node' && words[1] && CODE.test(words[1])) {
      join(reader.reachOf(posix.join(cwd, words[1])));
    } else if (words[0] === 'npm' && words[1] === 'run' && words[2]) {
      join(scriptReach(words, { files, read, reader, cwd }));
    } else if (words[0] === 'tsc') {
      join(tscReach(words, { files, read, reader, cwd }));
    } else {
      reach.wide.push(`${part}: a command no census here reads`);
    }
  }
  return { files: [...reach.files].sort(), folders: [...reach.folders].sort(), globs: [...reach.globs].sort(), wide: reach.wide };
}

/** An `npm run <script> [-w <name>]`: the package.json it is read from, and each command of its script. */
function scriptReach(words, context) {
  const at = words.indexOf('-w');
  let folder = context.cwd;
  if (at >= 0) {
    const root = JSON.parse(context.read('package.json') || '{}');
    folder = (root.workspaces ?? []).find((each) => JSON.parse(context.read(`${each}/package.json`) || '{}').name === words[at + 1]) ?? null;
    if (folder === null) return { files: [], folders: [], globs: [], wide: [`npm run ${words[2]} -w ${words[at + 1]}: no workspace of that name`] };
  }
  const manifest = posix.join(folder, 'package.json');
  const script = JSON.parse(context.read(manifest) || '{}').scripts?.[words[2]];
  if (typeof script !== 'string') return { files: [], folders: [], globs: [], wide: [`npm run ${words[2]}: ${manifest} has no such script`] };
  const inner = commandReach(script, { ...context, cwd: folder });
  return { ...inner, files: [...inner.files, manifest, 'package.json', 'package-lock.json'] };
}

/** A `tsc [-p <tsconfig>]`: the tsconfig, every code file its `include` holds, and the closure of their imports. */
function tscReach(words, { files, read, reader, cwd }) {
  const at = words.indexOf('-p');
  const named = at >= 0 ? posix.join(cwd, words[at + 1]) : posix.join(cwd, 'tsconfig.json');
  const config = named.endsWith('.json') ? named : posix.join(named, 'tsconfig.json');
  const dir = posix.dirname(config);
  const include = tsconfigOf(read(config) || '{}').include ?? ['**/*'];
  const tracked = new Set(files);
  const roots = include.map((entry) => posix.normalize(posix.join(dir, entry)));
  const queue = files.filter((file) => CODE.test(file) && roots.some((each) => file === each || file.startsWith(`${each}/`)));
  const seen = new Set(queue);
  while (queue.length) {
    const file = queue.pop();
    for (const { spec } of reader.factsOf(file).edges) {
      const resolved = resolveImport(file, spec, tracked);
      const typed = resolved?.replace(/\.mjs$/, '.d.mts');
      for (const each of [resolved, typed && tracked.has(typed) ? typed : null]) {
        if (each && !seen.has(each)) {
          seen.add(each);
          if (CODE.test(each)) queue.push(each);
        }
      }
    }
  }
  return { files: [...seen, config], folders: [], globs: [], wide: [] };
}

/**
 * The paths a step's code reads that its `reads` does not hold, each said once: a file, a folder (held where a declared folder holds
 * it), a glob (held where it is declared, or a declared folder holds what it can match), and a wide reach where the step is not `'*'`.
 */
export function undeclaredReads(step, reach) {
  if (step.reads === '*') return [];
  const folderHeld = (path) => step.reads.some((entry) => entry.endsWith('/') && `${path}/`.startsWith(entry));
  const fixed = (glob) => glob.slice(0, glob.search(/[*?{[]/) < 0 ? glob.length : glob.search(/[*?{[]/));
  return [
    ...reach.wide.map((why) => `${why}, so it declares '*'`),
    ...reach.files.filter((file) => !holds(step.reads, file)),
    ...reach.folders.filter((folder) => !folderHeld(folder)).map((folder) => `${folder}/`),
    ...reach.globs.filter((glob) => !step.reads.includes(glob) && !step.reads.some((entry) => entry.endsWith('/') && fixed(glob).startsWith(entry))),
  ];
}

/** The census's failures: for each step, the reads its command's code has that it does not declare. */
export function stepReadFailures(steps, context) {
  const failures = [];
  for (const each of steps) {
    const missing = undeclaredReads(each, commandReach(each.run, context));
    if (missing.length) failures.push(`check step ${each.run} reads ${missing.length} path(s) it does not declare: ${missing.slice(0, 6).join(', ')}${missing.length > 6 ? ', …' : ''}`);
  }
  return failures;
}
