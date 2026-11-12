import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const TOOLS = fileURLToPath(new URL('..', import.meta.url));

/** The names a declaration file exports. */
const declaredNames =(text: string): string[] =>
  [...new Set([...text.matchAll(/^export (?:declare )?(?:function|const|let|class) (\w+)/gm)].map((found) => found[1]!))].sort();

test('a declaration file names what it declares, each overload once', () => {
  assert.deepEqual(declaredNames('export declare function b(): void;\nexport function b(x: 1): void;\nexport const a: 1;\n// export declare function c\n'), ['a', 'b']);
});

test('each script a typed module imports has a declaration beside it that names exactly its exports', async () => {
  const declared = readdirSync(TOOLS).filter((name) => name.endsWith('.d.mts'));
  assert.ok(declared.includes('fsx.d.mts'), 'the file helpers are declared');
  for (const name of declared) {
    const script = join(TOOLS, name.replace(/\.d\.mts$/, '.mjs'));
    assert.ok(existsSync(script), `${name} declares a script that is not there`);
    const exported = Object.keys(await import(pathToFileURL(script).href) as object).sort();
    assert.deepEqual(declaredNames(readFileSync(join(TOOLS, name), 'utf8')), exported, `${name} and its script export the same names`);
  }
});
