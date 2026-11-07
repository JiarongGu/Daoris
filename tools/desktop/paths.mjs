import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const repoRoot = dirname(dirname(dirname(fileURLToPath(import.meta.url))));

/** Where a scratch run keeps everything it owns. Under `_fixtures/`, which is gitignored. */
export const scratchRoot = join(repoRoot, '_fixtures', 'desktop');

/**
 * The scratch run's one Daoris folder: `DAORIS_HOME` and `--app-root` both
 * name it, as an install's folder is both, so the readers that look in the home's Daoris folder for `app/` (the launchers,
 * the list built in, the plugin offers) and the window's memory (`profiles/window/`) are where an install has them.
 */
export const scratchDaoris = join(scratchRoot, 'daoris');
