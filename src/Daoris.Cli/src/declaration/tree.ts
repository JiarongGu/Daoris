import { readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * The folders never walked: version control's, dependencies', build output and the scratch folders, which hold no tracked file a
 * declaration's reach is about. The doctrine commands spawn nothing, so a repository's files are listed from the disk and not by git.
 */
const SKIPPED = new Set(['.git', 'node_modules', 'bin', 'obj', 'dist', '_fixtures', 'local', '.daoris']);

/** Every file under `root`, repository-relative with `/` between the parts, sorted. */
export function repositoryFiles(root: string): string[] {
  const out: string[] = [];
  const walk = (folder: string, prefix: string): void => {
    for (const entry of readdirSync(join(root, folder), { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (!SKIPPED.has(entry.name)) walk(join(folder, entry.name), `${prefix}${entry.name}/`);
      } else out.push(`${prefix}${entry.name}`);
    }
  };
  walk('', '');
  return out.sort();
}
