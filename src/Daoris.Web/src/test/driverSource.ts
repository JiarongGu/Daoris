import { readdirSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';

/**
 * A driver source a twin vitest parses, read by the file's name wherever it lies under the driver's folder (STRUCT1, D166;
 * the code-structure design §6): the driver's files move into module folders one module per branch, so a twin names the
 * file and never its folder. A name no file has, or two files have, is refused. The CLI's `driver-names.test.ts` holds
 * each name a twin reads by to one file.
 */

/** The driver's folder, from where vitest runs, the page's folder, as the twins found it before; the test environment's module URL is no file's. */
export const DRIVER_FOLDER = join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver');

let listed: string[] | undefined;

/** Every file under the driver's folder, at any depth, the build's output (`bin/`, `obj/`) aside: walked once a run. */
export function driverFiles(): string[] {
  if (listed) return listed;
  const files: string[] = [];
  const walk = (folder: string, top: boolean): void => {
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
      if (!entry.isDirectory()) files.push(join(folder, entry.name));
      else if (!top || (entry.name !== 'bin' && entry.name !== 'obj')) walk(join(folder, entry.name), false);
    }
  };
  walk(DRIVER_FOLDER, true);
  listed = files;
  return files;
}

/** The one path of the name among the driver's files, or why the name names none: absent, or found twice. */
export function oneNamed(name: string, files: readonly string[]): string {
  const found = files.filter((path) => basename(path) === name);
  if (found.length === 0) throw new Error(`no ${name} under Daoris.Desktop.Driver: a source read by name must be there, at any depth`);
  if (found.length > 1) throw new Error(`${name} is under Daoris.Desktop.Driver ${found.length} times, so its name does not say which: ${found.join(', ')}`);
  return found[0]!;
}

/** The text of the one driver source of this name. */
export const driverSource = (name: string): string => readFileSync(oneNamed(name, driverFiles()), 'utf8');
