import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

// D75 §4: one word for the scope, the CLI's own. The interface said *circle* beside *workspace*, and
// so did the sentences the CLI, the driver and the service print. The web's catalogues are held by
// its own i18n test; this holds the other doors. *Circle* stays prose in the design documents.

const cliRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(dirname(cliRoot));

/** Source files whose string literals a person or an agent reads: never tests, never build output. */
function sources(folder: string, extension: string): string[] {
  return (readdirSync(folder, { recursive: true, withFileTypes: true }))
    .filter((entry) => entry.isFile() && entry.name.endsWith(extension))
    .map((entry) => join(entry.parentPath, entry.name))
    .filter((path) => !/[\\/](bin|obj|test|[^\\/]*Tests)[\\/]/.test(path));
}

/**
 * Every string literal on a line that is not a comment, with its interpolations removed: `{circle}`
 * names a variable, and nobody reads that. What is left is what the program prints.
 */
export function circlesSaid(file: string, text: string): string[] {
  const hits: string[] = [];
  text.split('\n').forEach((line, index) => {
    if (/^\s*(\/\/|\*|\/\*|--)/.test(line)) return;
    for (const match of line.matchAll(/(["'`])((?:\\.|(?!\1).)*)\1/g)) {
      const said = match[2]!.replace(/\$?\{[^}]*\}/g, '');
      if (/\bcircles?\b/i.test(said)) hits.push(`${file}:${index + 1}: ${match[0]}`);
    }
  });
  return hits;
}

test('the check catches a printed circle and passes a variable or a comment', () => {
  assert.equal(circlesSaid('x.ts', "write('an ask is made in a circle');").length, 1);
  assert.equal(circlesSaid('x.cs', 'throw new X($"a {circle} scope needs its name.");').length, 0);
  assert.equal(circlesSaid('x.cs', '    // the circle this machine left').length, 0);
});

test('no door prints circle for a workspace', () => {
  const files = [
    ...sources(join(cliRoot, 'src'), '.ts'),
    ...sources(join(repoRoot, 'src', 'Daoris.Desktop'), '.cs'),
    ...sources(join(repoRoot, 'src', 'Daoris.Service'), '.cs'),
  ];
  assert.ok(files.length > 100, `found only ${files.length} source files: the scan is not reading`);
  const hits = files.flatMap((file) => circlesSaid(relative(repoRoot, file), readFileSync(file, 'utf8')));
  assert.deepEqual(hits, []);
});
