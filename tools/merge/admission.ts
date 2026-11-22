/**
 * The line's admission, read from `daoris.gates.json`'s `admission` key: the gates every change to the line passes, the suites
 * it always runs, and the checks its paths reach. The merge tool selects its blocking gates from it (`reach.ts`) and the driver's
 * check step, which judges a run's work as merged before its landing moves the line, reads the same key (`AdmissionGates`), so
 * the two never hold a list of their own. A gates file with none, or one mistyped, is an error said at once: a
 * landing never runs a shorter list than the declaration meant.
 */
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/** A light check a change is held to when one of its paths reaches what it reads: the gate, the paths that reach it and why. */
export type PathCheck = { gate: string; paths: readonly string[]; why: string };

/** The admission as declared: `always` runs at every change, `suites` too but not as a blocking check, `pathChecks` where a path reaches them. */
export type Admission = { always: readonly string[]; suites: readonly string[]; pathChecks: readonly PathCheck[] };

const GATES_FILE = 'daoris.gates.json';

const fail = (why: string): never => {
  throw new Error(`${GATES_FILE}'s admission ${why}: the merge tool and the landing's check read it as their one list`);
};

const names = (value: unknown, key: string): readonly string[] => {
  if (!Array.isArray(value) || value.some((each) => typeof each !== 'string' || each.length === 0)) return fail(`has no \`${key}\` (a list of gate names)`);
  return Object.freeze([...(value as string[])]);
};

/** The admission a gates file's text declares. */
export function declaredAdmission(gatesText: string): Admission {
  let declared: { admission?: { always?: unknown; suites?: unknown; byPaths?: unknown } } | null;
  try {
    declared = JSON.parse(gatesText) as typeof declared;
  } catch {
    return fail('cannot be read: the file is not JSON');
  }
  const admission = declared?.admission;
  if (!admission || typeof admission !== 'object') return fail('is not declared');
  const always = names(admission.always, 'always');
  const suites = names(admission.suites, 'suites');
  const byPaths: unknown = admission.byPaths;
  if (!Array.isArray(byPaths)) return fail('has no `byPaths` (a list of checks, each with its gate, paths and why)');
  const pathChecks = byPaths.map((entry: { gate?: unknown; paths?: unknown; why?: unknown } | null): PathCheck => {
    const gate = entry?.gate;
    const paths = entry?.paths;
    const why = entry?.why;
    if (typeof gate !== 'string' || !gate) return fail('has a `byPaths` entry with no gate');
    if (!Array.isArray(paths) || !paths.length || paths.some((path) => typeof path !== 'string' || !path)) return fail(`has the check ${gate} with no \`paths\``);
    if (typeof why !== 'string' || !why) return fail(`has the check ${gate} with no \`why\``);
    return Object.freeze({ gate, paths: Object.freeze([...(paths as string[])]), why });
  });
  const seen = new Set<string>();
  for (const gate of [...always, ...suites, ...pathChecks.map((check) => check.gate)]) {
    if (seen.has(gate)) return fail(`names ${gate} twice`);
    seen.add(gate);
  }
  return Object.freeze({ always, suites, pathChecks: Object.freeze(pathChecks) });
}

/** The admission of the repository at `root`. */
export function readAdmission(root: string): Admission {
  const file = join(root, GATES_FILE);
  return existsSync(file) ? declaredAdmission(readFileSync(file, 'utf8')) : fail('is not declared: the file is missing');
}

/** The admission of the tree at `root` where it declares one, else `fallback`: a tree that predates the key is judged by the tool's own list; one that declares it badly is an error. */
export function readAdmissionOr(root: string, fallback: Admission): Admission {
  const file = join(root, GATES_FILE);
  if (!existsSync(file)) return fallback;
  const text = readFileSync(file, 'utf8');
  try {
    if (!Object.hasOwn(JSON.parse(text) as object, 'admission')) return fallback;
  } catch {
    // not JSON: the reader below says so
  }
  return declaredAdmission(text);
}
