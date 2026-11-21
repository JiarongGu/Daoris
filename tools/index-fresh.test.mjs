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
import { spawnGit } from './fsx.mjs';
import { cleanEnv, freshIndex, keptPlace, main, writeIndex } from './index-fresh.mjs';

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

test('a tree at a commit whose index was written before gets that index, and a changed tree keeps none', () => {
  const root = join(scratch, 'kept');
  const runs = join(scratch, 'kept-runs.txt');
  mkdirSync(join(root, 'tools'), { recursive: true });
  writeFileSync(join(root, 'tools', 'orient-index.mjs'), [
    "import { appendFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';",
    "import { join } from 'node:path';",
    "const root = process.argv[process.argv.indexOf('--root') + 1];",
    `appendFileSync(${JSON.stringify(runs)}, 'ran\\n');`,
    "mkdirSync(join(root, 'docs', 'index'), { recursive: true });",
    "writeFileSync(join(root, 'docs', 'index', 'README.md'), readFileSync(join(root, 'content.txt'), 'utf8'));",
    "console.log('orient-index: docs/index/ written');",
    '',
  ].join('\n'));
  writeFileSync(join(root, 'content.txt'), 'one\n');
  writeFileSync(join(root, '.gitignore'), 'docs/index/\n');
  const git = (cwd, ...args) => {
    const ran = spawnGit(['-c', 'user.email=t@example.com', '-c', 'user.name=T', '-c', 'core.autocrlf=false', ...args], { cwd, encoding: 'utf8' });
    assert.equal(ran.status, 0, ran.stderr);
    return ran.stdout;
  };
  git(root, 'init', '-q', '-b', 'main');
  git(root, 'add', '-A');
  git(root, 'commit', '-q', '-m', 'one');
  const say = () => ({ out: [], err: [] });
  const run = (at) => {
    const said = say();
    const code = main(['--root', at], { tool: join(root, 'tools', 'orient-index.mjs'), out: (line) => said.out.push(line), err: (line) => said.err.push(line), cwd: at });
    return { code, ...said };
  };
  const count = () => readFileSync(runs, 'utf8').trim().split('\n').length;

  const first = run(root);
  assert.equal(first.code, 0, JSON.stringify(first));
  assert.equal(count(), 1);

  const second = join(scratch, 'kept-tree');
  git(root, 'worktree', 'add', '-q', '-b', 'two', second, 'main');
  assert.equal(run(second).code, 0);
  assert.equal(count(), 1, 'a tree at the same commit ran no generator');
  assert.equal(readFileSync(join(second, 'docs', 'index', 'README.md'), 'utf8'), 'one\n');

  writeFileSync(join(root, 'content.txt'), 'two\n');
  git(root, 'add', '-A');
  git(root, 'commit', '-q', '-m', 'two');
  writeFileSync(join(root, 'content.txt'), 'three\n');
  assert.equal(run(root).code, 0);
  assert.equal(count(), 2, 'a new tree id runs the generator');
  assert.equal(readFileSync(join(root, 'docs', 'index', 'README.md'), 'utf8'), 'three\n');
  const place = keptPlace(root);
  assert.equal(existsSync(place.dir), false, 'the index of a changed tree is kept for none');
  assert.equal(keptPlace(join(scratch, 'nothing')), null);
});

test('a kept index is put only in a clean tree: a changed file or a new one makes the working files say another', () => {
  const root = join(scratch, 'dirty');
  const runs = join(scratch, 'dirty-runs.txt');
  mkdirSync(join(root, 'tools'), { recursive: true });
  writeFileSync(join(root, 'tools', 'orient-index.mjs'), [
    "import { appendFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';",
    "import { join } from 'node:path';",
    "const root = process.argv[process.argv.indexOf('--root') + 1];",
    `appendFileSync(${JSON.stringify(runs)}, 'ran\\n');`,
    "mkdirSync(join(root, 'docs', 'index'), { recursive: true });",
    "writeFileSync(join(root, 'docs', 'index', 'README.md'), readFileSync(join(root, 'content.txt'), 'utf8'));",
    '',
  ].join('\n'));
  writeFileSync(join(root, 'content.txt'), 'one\n');
  writeFileSync(join(root, '.gitignore'), 'docs/index/\n');
  const git = (cwd, ...args) => {
    const ran = spawnGit(['-c', 'user.email=t@example.com', '-c', 'user.name=T', '-c', 'core.autocrlf=false', ...args], { cwd, encoding: 'utf8' });
    assert.equal(ran.status, 0, ran.stderr);
  };
  git(root, 'init', '-q', '-b', 'main');
  git(root, 'add', '-A');
  git(root, 'commit', '-q', '-m', 'one');
  const said = [];
  const run = () => main(['--root', root], { tool: join(root, 'tools', 'orient-index.mjs'), out: () => {}, err: (line) => said.push(line), cwd: root });
  const count = () => readFileSync(runs, 'utf8').trim().split('\n').length;

  assert.equal(run(), 0, said.join('|'));
  assert.equal(count(), 1);
  assert.equal(run(), 0);
  assert.equal(count(), 1, 'a clean tree at a kept tree id is handed the kept index');

  writeFileSync(join(root, 'content.txt'), 'edited\n');
  assert.equal(run(), 0);
  assert.equal(count(), 2, 'a changed tracked file writes the index again');
  assert.equal(readFileSync(join(root, 'docs', 'index', 'README.md'), 'utf8'), 'edited\n');

  git(root, 'checkout', '-q', '--', 'content.txt');
  writeFileSync(join(root, 'new.ts'), 'export {};\n');
  assert.equal(run(), 0);
  assert.equal(count(), 3, 'a new file writes the index again');
});
