// What an install's driver folder must hold: the host the launchers run, and the git library's native file, which the `win-x64`
// publish lays beside the driver as it lays SQLite's. Without it every ref question is git's, which works and costs starts.
import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { DRIVER_EXE, DRIVER_HOME } from './launchers.mjs';

/** The native file LibGit2Sharp loads, as its binaries package names it: `git2-` and libgit2's short commit. */
export const GIT_LIBRARY_NATIVE = /^git2-[0-9a-f]+\.dll$/i;

/** What the driver folder under `install` lacks, each said as a problem; none where both are there. */
export function driverProblems(install) {
  const folder = join(install, ...DRIVER_HOME);
  const problems = [];
  if (!existsSync(join(folder, DRIVER_EXE))) problems.push(`${DRIVER_HOME.join('/')}/${DRIVER_EXE} is not there`);
  const files = existsSync(folder) ? readdirSync(folder) : [];
  if (!files.some((name) => GIT_LIBRARY_NATIVE.test(name))) problems.push(`${DRIVER_HOME.join('/')}/git2-*.dll, the git library's native file, is not there`);
  return problems;
}
