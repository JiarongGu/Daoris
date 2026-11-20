import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { repositoryFiles } from '../../../tools/fsx.mjs';
import { FIXTURE_HOME } from '../../../tools/merge/fixture-home.ts';

// reaches: src/Daoris.Cli/**, tools/**, src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/**

/**
 * No test or tool leaves a folder in the system's temporary folder: scratch goes under the checkout's ignored `_fixtures/`
 * (`tools/merge/fixture-home.ts`). A `tmpdir()` / `os.tmpdir` in a `.ts` or `.mjs` under `src/Daoris.Cli` or `tools`, and a
 * `Path.GetTempPath()` in the C# test projects, fails here naming file and line, unless the file is a declared exception with
 * its why. The C# list is the driver census's own (`TestHomesCensusTests.Kept`), read from its source, never copied.
 */

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const CSHARP_CENSUS = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/TestHomesCensusTests.cs';
const CSHARP_PROJECTS = ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/', 'src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/'];

/** Node files that name the temporary folder on purpose, each with why; each creates nothing there. */
export const KEPT: Record<string, string> = {
  'src/Daoris.Cli/tool-test/temp-folder-census.test.ts': 'names the call it forbids, creating nothing',
  'tools/time-why.mjs': 'classifies a file by the call in its text (a pattern), creating nothing',
};

const node = /\btmpdir\s*\(|\bos\.tmpdir\b|\benv\.(?:TEMP|TMP|TMPDIR)\b/;
const csharp = /\bGetTemp(?:Path|FileName)\s*\(/;

/** Whether a node source (`.ts`, `.mjs`, `.js`) of the repository is read: all of them but dependencies and scratch. */
export function inNodeScope(path: string): boolean {
  return /\.(?:ts|tsx|mjs|js)$/.test(path) && !/(^|\/)(node_modules|_fixtures)\//.test(path);
}

/** The source with its comments blanked, line for line, so a line number still names the line. */
export function uncommented(text: string): string {
  return text
    .replace(/\/\*[\s\S]*?\*\//g, (block) => block.replace(/[^\n]/g, ' '))
    .replace(/(^|[^:'"`])\/\/[^\n]*/g, '$1');
}

export function sitesOf(text: string, pattern: RegExp): number[] {
  const lines = uncommented(text.replace(/\r\n/g, '\n')).split('\n');
  return lines.flatMap((line, index) => (pattern.test(line) ? [index + 1] : []));
}

function csharpKept(): Set<string> {
  const text = readFileSync(join(root, CSHARP_CENSUS), 'utf8');
  const block = /Kept\s*=\s*new[^{]*\{([\s\S]*?)\n\s*\};/.exec(text);
  assert.ok(block, `${CSHARP_CENSUS} no longer declares its Kept list where this census reads it`);
  return new Set([...(block[1] ?? '').matchAll(/\["([^"]+)"\]\s*=/g)].map((m) => m[1] ?? ''));
}

test('the census reads every node source of the repository, the plugin examples too, and a scratch folder lies under the checkout', () => {
  for (const path of ['tools/a.mjs', 'src/Daoris.Cli/test/a.test.ts', 'examples/plugins/local-release/storage.test.mjs', 'src/Daoris.Web/src/a.ts', 'examples/engine/tools/a.mjs']) {
    assert.equal(inNodeScope(path), true, path);
  }
  for (const path of ['node_modules/x/a.js', 'src/Daoris.Web/node_modules/x/a.ts', '_fixtures/a.mjs', 'docs/a.md']) assert.equal(inNodeScope(path), false, path);
  assert.equal(FIXTURE_HOME.startsWith(join(root, '_fixtures')), true, "the writers' scratch is the checkout's ignored _fixtures");
  assert.deepEqual(sitesOf('const d = process.env.TEMP;\nconst e = process.env.TMPDIR ?? 1;', node), [1, 2], 'the folder asked for by variable too');
});

test('the pattern finds a call and not a comment, and names its line', () => {
  assert.deepEqual(sitesOf("a\nconst d = tmpdir();\n// tmpdir() is gone\nx", node), [2]);
  assert.deepEqual(sitesOf('a\n/* os.tmpdir\n */\nb os.tmpdir', node), [4]);
  assert.deepEqual(sitesOf('var h = Path.GetTempPath();', csharp), [1]);
  assert.deepEqual(sitesOf('var h = TestHomes.Root;', csharp), []);
});

test('no node test or tool asks for the temporary folder but the declared ones', () => {
  const found: string[] = [];
  for (const file of repositoryFiles(root)) {
    const path = file.replace(/\\/g, '/');
    if (!inNodeScope(path) || path in KEPT) continue;
    for (const line of sitesOf(readFileSync(join(root, path), 'utf8'), node)) found.push(`${path}:${line}`);
  }
  assert.deepEqual(found, [], `use scratchFolder / FIXTURE_HOME from tools/merge/fixture-home.ts, or declare the file in KEPT with why:\n${found.join('\n')}`);
});

test('no C# test asks for the temporary folder but those its own census declares', () => {
  const kept = csharpKept();
  const found: string[] = [];
  for (const file of repositoryFiles(root)) {
    const path = file.replace(/\\/g, '/');
    const project = CSHARP_PROJECTS.find((p) => path.startsWith(p));
    if (!project || !path.endsWith('.cs') || path.endsWith('/TestHomesCensusTests.cs') || kept.has(path.slice(project.length))) continue;
    for (const line of sitesOf(readFileSync(join(root, path), 'utf8'), csharp)) found.push(`${path}:${line}`);
  }
  assert.deepEqual(found, [], `use TestHomes, or declare the file in ${CSHARP_CENSUS} with why:\n${found.join('\n')}`);
  assert.ok(kept.size > 0 && relative(root, join(root, CSHARP_CENSUS)).length > 0);
});
