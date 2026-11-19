/**
 * The merge tool's tests' scratch folders: each under the checkout's `_fixtures/tool-tests/`, which git ignores and
 * `tools/fixtures-keep.mjs` sweeps, never the system's temporary folder or the working directory.
 */
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const FIXTURE_HOME = join(fileURLToPath(new URL('../..', import.meta.url)), '_fixtures', 'tool-tests');

/** `FIXTURE_HOME`, made: the folder a test that names its own scratch with `mkdtempSync` puts it in, never the system's temporary one. */
export function scratchRoot(): string {
  mkdirSync(FIXTURE_HOME, { recursive: true });
  return FIXTURE_HOME;
}

/** A new empty folder named for what uses it, and the call that removes it. */
export function fixtureFolder(name: string): { dir: string; remove: () => void } {
  mkdirSync(FIXTURE_HOME, { recursive: true });
  const dir = mkdtempSync(join(FIXTURE_HOME, `${name}-`));
  return { dir, remove: () => rmSync(dir, { recursive: true, force: true }) };
}

const untilExit: string[] = [];
process.on('exit', () => {
  for (const dir of untilExit) rmSync(dir, { recursive: true, force: true });
});

/** A new empty folder under `FIXTURE_HOME` that is removed when the test file's process ends. */
export function scratchFolder(name = 'scratch'): string {
  const { dir } = fixtureFolder(name);
  untilExit.push(dir);
  return dir;
}
