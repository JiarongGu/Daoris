import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { CLI_HOME } from './launchers.mjs';
import { STAGED } from './staging.mjs';
import { HOME } from './lay.mjs';

/**
 * The version a publish stamps on its build: the patch above every version the release and the install have carried, so
 * two publishes in a row say two versions, in the assemblies (the window's System page and the machine log), the doctrine tool's
 * package (`daoris --version`) and a stage's manifest (`daoris update`).
 *
 * It reads the install, never a counter of its own: a counter kept in the workspace would be a second record to disagree with the
 * install, and the install is what the launcher's refusal of an older build is held against. The launcher refuses a build below the
 * newest one that ran (`HomeLastBuild`), so the next version must clear every one of these, a rolled-back build's included.
 */

const numbered = (version) => {
  if (typeof version !== 'string') return null;
  const match = /^v?(\d+)\.(\d+)\.(\d+)/.exec(version.trim());
  return match ? [Number(match[1]), Number(match[2]), Number(match[3])] : null;
};

const compare = (a, b) => a[0] - b[0] || a[1] - b[1] || a[2] - b[2];

/** The `version` a JSON file at `path` names, or null where there is no such file or it does not read. */
function versionIn(path) {
  if (!existsSync(path)) return null;
  try {
    const version = JSON.parse(readFileSync(path, 'utf8'))?.version;
    return typeof version === 'string' ? version : null;
  } catch {
    return null;
  }
}

/** The patch above the highest of `known`, each read for its numbered core only (a prerelease or build metadata is no version of its own). */
export function nextVersion(known) {
  const highest = known.map(numbered).filter(Boolean).sort(compare).at(-1) ?? [0, 0, 0];
  return `${highest[0]}.${highest[1]}.${highest[2] + 1}`;
}

/**
 * Every version the install at `install` has carried, staged, tried or run: its doctrine tool, the build staged beside it, the last
 * swap's (a rolled-back build is in it) and the newest that ran on its home. A folder that is no install says none.
 */
export function seenVersions(install) {
  return [
    versionIn(join(install, ...CLI_HOME, 'package.json')),
    versionIn(join(install, ...STAGED, 'build.json')),
    versionIn(join(install, 'update', 'swap.json')),
    versionIn(join(install, HOME, 'last-build.json')),
  ].filter(Boolean);
}

/** The version the build of the checkout at `repoRoot` takes when published to `install`: above the release's and everything the install has seen. */
export function buildVersion(repoRoot, install) {
  const release = versionIn(join(repoRoot, 'canon', 'canon.json'));
  return nextVersion([release, ...seenVersions(install)]);
}
