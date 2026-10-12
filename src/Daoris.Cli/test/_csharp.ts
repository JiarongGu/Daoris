import assert from 'node:assert/strict';
import { readdirSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * The driver's `[InlineData(…)]` rows, read as data (TOOLS2, TOOLS3): how a CLI test holds a twin's table to
 * its own, cell for cell and in order, so a row changed on one side alone fails `npm run verify`. The C#
 * side keeps each row on one line, its cells literals.
 *
 * And the file each row is read from, found by its name wherever it lies under its desktop project (STRUCT1, D166): the
 * driver's sources and tests move into module folders one module per branch, so a reader names the file and never its
 * folder. `driver-names.test.ts` holds every reader to it.
 */

/** The desktop's projects' folder, `src/Daoris.Desktop/`. */
export const DESKTOP = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop');

/** The one path of the name among those found under a project, or why the name names none: absent, or found twice. */
export function oneNamed(project: string, name: string, found: readonly string[]): string {
  if (found.length === 1) return found[0]!;
  if (found.length === 0) throw new Error(`no ${name} under ${project}: a source read by name must be there, at any depth`);
  throw new Error(`${name} is under ${project} ${found.length} times, so its name does not say which: ${found.join(', ')}`);
}

const listed = new Map<string, string[]>();

/** Every file under a desktop project's folder, at any depth, the build's output (`bin/`, `obj/`) aside: walked once a run. */
export function projectFiles(project: string): string[] {
  const known = listed.get(project);
  if (known) return known;
  const files: string[] = [];
  const walk = (folder: string, top: boolean): void => {
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
      if (!entry.isDirectory()) files.push(join(folder, entry.name));
      else if (!top || (entry.name !== 'bin' && entry.name !== 'obj')) walk(join(folder, entry.name), false);
    }
  };
  walk(join(DESKTOP, project), true);
  files.sort();
  listed.set(project, files);
  return files;
}

/** The one file of this name under a desktop project's folder, at any depth. */
export const desktopFile = (project: string, name: string): string =>
  oneNamed(project, name, projectFiles(project).filter((path) => basename(path) === name));

/** The one driver source of this name, at any depth of `Daoris.Desktop.Driver/`. */
export const driverFile = (name: string): string => desktopFile('Daoris.Desktop.Driver', name);

/** The one file of the driver's tests of this name, at any depth of `Daoris.Desktop.Driver.Tests/`. */
export const driverTestFile = (name: string): string => desktopFile('Daoris.Desktop.Driver.Tests', name);

export type Cell = string | number | boolean | null;

/**
 * One row's arguments: a raw `"""…"""`, verbatim `@"…"` or plain `"…"` string, `null`, `true`, `false`, a
 * whole number, or a named constant in `words` (the placeholder each side spells for itself).
 */
export function csharpRow(line: string, words: Record<string, Cell> = {}): Cell[] {
  const cells: Cell[] = [];
  let at = line.indexOf('[InlineData(') + '[InlineData('.length;
  while (!line.startsWith(')]', at)) {
    if (line[at] === ',' || line[at] === ' ') {
      at += 1;
    } else if (line.startsWith('"""', at)) {
      const end = line.indexOf('"""', at + 3);
      cells.push(line.slice(at + 3, end));
      at = end + 3;
    } else if (line.startsWith('@"', at)) {
      const end = line.indexOf('"', at + 2);
      cells.push(line.slice(at + 2, end));
      at = end + 1;
    } else if (line[at] === '"') {
      let text = '';
      at += 1;
      while (line[at] !== '"') {
        // `\uXXXX` is the letter it names (CASEFOLD1: a row spells a combining dot above by its escape, so it can be seen).
        const unit = line[at] === '\\' && line[at + 1] === 'u' ? /^[0-9A-Fa-f]{4}/.exec(line.slice(at + 2))?.[0] : undefined;
        text += unit !== undefined ? String.fromCharCode(parseInt(unit, 16)) : line[at] === '\\' ? line[at + 1] : line[at];
        at += unit !== undefined ? 6 : line[at] === '\\' ? 2 : 1;
      }
      cells.push(text);
      at += 1;
    } else {
      const word = /^(null|true|false|-?[0-9]+|[A-Z]\w*)\b/.exec(line.slice(at))?.[1];
      assert.ok(word !== undefined && (!/^[A-Z]/.test(word) || word in words), `a cell this reader does not know, at ${at}: ${line}`);
      cells.push(word === 'null' ? null
        : word === 'true' ? true
          : word === 'false' ? false
            : /^-?[0-9]/.test(word) ? Number(word)
              : words[word]!);
      at += word.length;
    }
  }
  return cells;
}

/** The rows of one theory in a C# test class's source, in its order; the theory may be `void` or `async Task` (TOOLS4). */
export function driverRows(source: string, method: string, words: Record<string, Cell> = {}, owner = 'the driver’s tests'): Cell[][] {
  const end = Math.max(source.indexOf(`public void ${method}(`), source.indexOf(`public async Task ${method}(`));
  assert.ok(end > 0, `${owner} have no ${method}`);
  const start = source.lastIndexOf('[Theory]', end);
  return source.slice(start, end).split('\n').filter((line) => line.trim().startsWith('[InlineData(')).map((line) => csharpRow(line, words));
}
