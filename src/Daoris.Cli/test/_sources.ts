import { readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { oneNamed } from './_csharp.ts';

/**
 * The CLI's sources, found by name wherever they lie under `src/` (STRUCT5d, D166; the code-structure design §6): the
 * sources sit in module folders (`structure.test.ts`), so a test that reads a source by path names the file and never its
 * folder, and a move between folders edits no reader. The command rows under `cli/` are the exception, named by their path
 * (`cli/agent.ts`): each row shares its verb's name with the module that handles it.
 */

/** The package's folder, `src/Daoris.Cli/`. */
export const cliRoot = dirname(dirname(fileURLToPath(import.meta.url)));

/** The sources' folder, `src/Daoris.Cli/src/`. */
export const SOURCES = join(cliRoot, 'src');

/** The command rows' folder under `src/`, whose files are named by their path. */
export const ROWS = 'cli/';

let walked: string[] | undefined;

/** Every file under `src/`, by its path there with `/` between its parts, in order: walked once a run. */
export function sourcePaths(): string[] {
  if (walked) return walked;
  const files: string[] = [];
  const walk = (folder: string, prefix: string): void => {
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
      if (entry.isDirectory()) walk(join(folder, entry.name), `${prefix}${entry.name}/`);
      else files.push(`${prefix}${entry.name}`);
    }
  };
  walk(SOURCES, '');
  walked = files.sort();
  return walked;
}

/**
 * The one source of this name under `src/`, by its path there, the command rows aside; a path that names its folder
 * (`cli/agent.ts`) is taken as written, and must be there.
 */
export function cliSource(name: string, paths: readonly string[] = sourcePaths()): string {
  if (name.includes('/')) return oneNamed('src', name, paths.filter((path) => path === name));
  return oneNamed('src', name, paths.filter((path) => !path.startsWith(ROWS) && path.split('/').at(-1) === name));
}
