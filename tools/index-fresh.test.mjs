/**
 * `freshIndex` writes a checkout's ignored index again only where `HEAD` moved or the index is gone, and `writeIndex` runs
 * the tree's own generator.
 *
 *   node --test tools/index-fresh.test.mjs
 *
 * Run by `npm run verify`. Its scratch is a gitignored folder of the repository.
 */
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { cleanEnv, freshIndex, main, writeIndex } from './index-fresh.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', '_fixtures', 'index-fresh-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

test('it writes where the index is missing or HEAD moved, and stamps only a write that worked', () => {
  const root = join(scratch, 'stamped');
  mkdirSync(join(root, 'docs', 'index'), { recursive: true });
  const stamp = join(root, 'index-head');
  const readme = join(root, 'docs', 'index', 'README.md');
  let head = 'a';
  let ok = true;
  let writes = 0;
  const ask = () => freshIndex(root, {
    stamp,
    head: () => head,
    write: () => {
      writes += 1;
      if (ok) writeFileSync(readme, '# Index\n');
      return { ok, said: ok ? 'written' : 'boom' };
    },
  });

  assert.deepEqual(ask(), { written: true, ok: true, said: 'written' });
  assert.equal(readFileSync(stamp, 'utf8'), 'a\n');
  assert.deepEqual(ask(), { written: false, ok: true, said: '' });

  head = 'b';
  ok = false;
  assert.deepEqual(ask(), { written: true, ok: false, said: 'boom' });
  assert.equal(readFileSync(stamp, 'utf8'), 'a\n', 'a failed write is not stamped');
  ok = true;
  assert.equal(ask().written, true);
  assert.equal(readFileSync(stamp, 'utf8'), 'b\n');

  rmSync(readme);
  assert.equal(ask().written, true, 'an index gone is written again at the same HEAD');
  assert.equal(writes, 4);
});

test("writeIndex runs the tree's generator over it, and says so where the tree has none", () => {
  const root = join(scratch, 'tree');
  mkdirSync(join(root, 'tools'), { recursive: true });
  writeFileSync(join(root, 'tools', 'orient-index.mjs'), [
    "import { mkdirSync, writeFileSync } from 'node:fs';",
    "import { join } from 'node:path';",
    "const root = process.argv[process.argv.indexOf('--root') + 1];",
    "mkdirSync(join(root, 'docs', 'index'), { recursive: true });",
    "writeFileSync(join(root, 'docs', 'index', 'README.md'), String(process.env.GIT_DIR ?? 'none') + '\\n');",
    "console.log('orient-index: docs/index/ written');",
    '',
  ].join('\n'));
  const written = writeIndex(root, { env: { ...process.env, GIT_DIR: 'elsewhere' } });
  assert.deepEqual(written, { ok: true, said: 'orient-index: docs/index/ written' });
  assert.equal(readFileSync(join(root, 'docs', 'index', 'README.md'), 'utf8'), 'none\n', "git's own variables never reach the generator");

  const bare = join(scratch, 'bare');
  mkdirSync(bare, { recursive: true });
  assert.equal(writeIndex(bare).ok, false);
  assert.equal(existsSync(join(bare, 'docs')), false);
  assert.equal(cleanEnv({ GIT_WORK_TREE: 'x', PATH: 'p' }).GIT_WORK_TREE, undefined);
});

test('a word it does not know is a usage error', () => {
  const said = [];
  assert.equal(main(['--chek'], { out: () => {}, err: (line) => said.push(line) }), 2);
  assert.match(said[0], /usage/);
});
