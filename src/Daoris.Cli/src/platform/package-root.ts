import { basename, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The file an install's bundled doctrine tool is, and which only a bundle's folder holds. */
export const BUNDLE_FILE = 'daoris.mjs';

/**
 * The folder holding the package's manifest and the canon beside it, found from a module's own URL: the package's folder where
 * the module is in its `src/` or its built `dist/`, and the module's own folder where the module is the bundle an install
 * carries (`app/cli/daoris.mjs`, its canon and manifest beside it).
 */
export function packageRootOf(moduleUrl: string): string {
  const here = dirname(fileURLToPath(moduleUrl));
  return ['src', 'dist'].includes(basename(here)) ? dirname(here) : here;
}
