/**
 * What a typecheck gate's compiler reads, and the files of it the gate's paths leave out. A gate's paths decide when its
 * verdict stands and when a merge runs it; a file its compiler reads that no path names changes with no typecheck run
 * (the page's `scripts/e2e-port.d.mts` is read through a test's import).
 */
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { join, relative, sep } from 'node:path';
import { laneMatcher } from './merge/lanes-file.ts';

/** The typecheck gates: each gate's name and the folder of the project its command compiles (`tsc --noEmit` there). */
export const TYPECHECK_GATES = Object.freeze([{ gate: 'web-types', project: 'src/Daoris.Web' }]);

/** The tracked-looking files the project's compiler reads, repository-relative with forward slashes; `node_modules` and files outside the repository are left out. */
export function typecheckReads(root, project) {
  const folder = join(root, project);
  const tsc = createRequire(join(folder, 'package.json')).resolve('typescript/bin/tsc');
  const run = spawnSync(process.execPath, [tsc, '--listFilesOnly', '-p', folder], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, windowsHide: true });
  if (run.status !== 0) throw new Error(`tsc --listFilesOnly failed in ${project}: ${run.stdout}${run.stderr}`);
  return run.stdout.split(/\r?\n/).filter(Boolean)
    .map((file) => relative(root, file).split(sep).join('/'))
    .filter((file) => !file.startsWith('..') && !/(^|\/)node_modules\//.test(file));
}

/** Of `reads`, the files no entry of a gate's declared paths names: an entry is a file, or a folder written with its trailing slash. */
export const outsideDeclared = (reads, paths) => reads.filter((file) => !paths.some((entry) => (entry.endsWith('/') ? file.startsWith(entry) : file === entry)));

/** Of `reads`, the files no glob of a path check's paths matches. */
export function outsideMatched(reads, globs) {
  const matches = laneMatcher([...globs]);
  return reads.filter((file) => !matches(file));
}
