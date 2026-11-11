import { join } from 'node:path';
import { readJsonObject, readText, writeTextAtomic } from './fsx.ts';

/**
 * The file beside an installed package's `package.json` that says which build laid it there: the desktop's publish writes it when it
 * lays the package into an install, with the build's id and its commit. The release's own package carries none,
 * since its version alone names it; an install's carries the version every build of a release shares, so the stamp tells them apart.
 */
export const BUILD_STAMP = 'build.json';

/** A build as its stamp names it: its id, and its commit where the publish could read one. */
export interface BuildStamp {
  id: string;
  commit: string | null;
}

/** Write the stamp into the package at `packageRoot`. */
export function writeBuildStamp(packageRoot: string, stamp: BuildStamp): void {
  writeTextAtomic(join(packageRoot, BUILD_STAMP), `${JSON.stringify({ id: stamp.id, commit: stamp.commit }, null, 2)}\n`);
}

/** The stamp of the package at `packageRoot`, or null where it carries none, or one that names no build. */
export function readBuildStamp(packageRoot: string): BuildStamp | null {
  const { value } = readJsonObject(join(packageRoot, BUILD_STAMP));
  if (typeof value?.id !== 'string' || value.id.length === 0) return null;
  return { id: value.id, commit: typeof value.commit === 'string' && value.commit.length > 0 ? value.commit : null };
}

/** What `daoris --version` prints: the package's version, and the build that laid it where it carries a stamp. */
export function versionLine(packageRoot: string): string {
  const version = JSON.parse(readText(join(packageRoot, 'package.json'))).version as string;
  const stamp = readBuildStamp(packageRoot);
  if (stamp === null) return version;
  return `${version} (build ${stamp.id}${stamp.commit ? `, commit ${stamp.commit}` : ''})`;
}
