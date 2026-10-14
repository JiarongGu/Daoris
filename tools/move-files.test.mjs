/**
 * MOVETOOL1: the move tool respells imports, asserts its counts and checks every import after, on a fixture repository.
 *
 *   node --test tools/move-files.test.mjs
 *
 * Run by `npm run verify`.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { main, specifiersOf } from './move-files.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'move-files-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const env = Object.fromEntries(Object.entries(process.env)
  .filter(([key]) => !/^GIT_(DIR|WORK_TREE|INDEX_FILE|COMMON_DIR|OBJECT_DIRECTORY)$/i.test(key)));
const git = (repo, ...args) => {
  const run = spawnSync('git', ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', '-c', 'core.autocrlf=false', ...args],
    { cwd: repo, encoding: 'utf8', env, windowsHide: true });
  assert.equal(run.status, 0, run.stderr);
  return run.stdout;
};

let count = 0;
/** A repository with the files, committed. */
function fixture(files) {
  const repo = join(scratch, `r${++count}`);
  mkdirSync(repo, { recursive: true });
  git(repo, 'init', '-q');
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(repo, path)), { recursive: true });
    writeFileSync(join(repo, path), text);
  }
  git(repo, 'add', '-A');
  git(repo, 'commit', '-q', '-m', 'fixture');
  return repo;
}
const read = (repo, path) => readFileSync(join(repo, path), 'utf8');
const run = (repo, args) => {
  const out = [];
  const err = [];
  const code = main(['--root', repo, ...args], { out: (l) => out.push(l), err: (l) => err.push(l), env });
  return { code, out, err };
};

const tree = () => ({
  'src/a.ts': "import { b } from './b.ts';\nimport x from '../lib/x.js';\nexport const a = b;\n",
  'src/b.ts': "export const b = 1;\nexport { c } from './sub/index.ts';\n",
  'src/sub/index.ts': "export const c = 2;\n",
  'src/a.test.tsx': "import { a } from './a';\nvi.mock('./b.ts', () => ({}));\nconst m = await import('./sub');\n",
  'lib/x.ts': "export default 3;\n",
  'src/view.tsx': "import './style.css';\nimport { a } from './a.ts';\n",
  'src/style.css': 'body {}\n',
});

test('a file moved is respelled in its importers, and its own imports reach what they did', () => {
  const repo = fixture(tree());
  const result = run(repo, ['src/b.ts=src/core/b.ts', '--expect-moved', '1', '--expect-rewritten', '3', '--expect-files', '3']);
  assert.equal(result.code, 0, result.err.join('\n'));
  assert.ok(existsSync(join(repo, 'src/core/b.ts')) && !existsSync(join(repo, 'src/b.ts')));
  assert.match(git(repo, 'status', '--porcelain'), /^R/m, 'git sees a rename');
  assert.match(read(repo, 'src/a.ts'), /from '\.\/core\/b\.ts'/);
  assert.match(read(repo, 'src/a.test.tsx'), /vi\.mock\('\.\/core\/b\.ts'/);
  assert.match(read(repo, 'src/core/b.ts'), /from '\.\.\/sub\/index\.ts'/);
  assert.match(read(repo, 'src/a.ts'), /from '\.\.\/lib\/x\.js'/, 'a specifier that needs no change is untouched');
});

test('a folder moves whole; a .js spelling, a bare one, a folder import and a css file keep their form', () => {
  const repo = fixture(tree());
  const result = run(repo, ['src/sub=src/deep/sub', 'src/style.css=assets/style.css']);
  assert.equal(result.code, 0, result.err.join('\n'));
  assert.match(read(repo, 'src/b.ts'), /from '\.\/deep\/sub\/index\.ts'/);
  assert.match(read(repo, 'src/a.test.tsx'), /import\('\.\/deep\/sub'\)/, 'a folder import stays one');
  assert.match(read(repo, 'src/view.tsx'), /import '\.\.\/assets\/style\.css'/);
  const js = fixture({ 'a.ts': "import './m.js';\n", 'm.ts': 'export {};\n' });
  assert.equal(run(js, ['m.ts=n/m.ts']).code, 0);
  assert.equal(read(js, 'a.ts'), "import './n/m.js';\n");
  const bare = fixture({ 'a.ts': "import './m';\n", 'm.ts': 'export {};\n' });
  assert.equal(run(bare, ['a.ts=z/a.ts']).code, 0);
  assert.equal(read(bare, 'z/a.ts'), "import '../m';\n");
});

test('--plan prints and changes nothing', () => {
  const repo = fixture(tree());
  const before = git(repo, 'status', '--porcelain');
  const result = run(repo, ['--plan', 'src/b.ts=src/core/b.ts']);
  assert.equal(result.code, 0);
  assert.ok(result.out.includes('move src/b.ts -> src/core/b.ts'));
  assert.ok(result.out.some((line) => /3 specifiers rewritten in 3 files/.test(line)));
  assert.equal(git(repo, 'status', '--porcelain'), before);
  assert.ok(existsSync(join(repo, 'src/b.ts')));
});

test('a count that differs stops before any change, with exit 1', () => {
  const repo = fixture(tree());
  const result = run(repo, ['src/b.ts=src/core/b.ts', '--expect-rewritten', '9']);
  assert.equal(result.code, 1);
  assert.match(result.err.join('\n'), /rewritten: expected 9, found 3/);
  assert.ok(existsSync(join(repo, 'src/b.ts')));
  assert.equal(git(repo, 'status', '--porcelain'), '');
});

test('a bad move is a policy failure, a bad call a tool error', () => {
  const repo = fixture(tree());
  assert.equal(run(repo, ['src/nope.ts=src/x.ts']).code, 1);
  assert.equal(run(repo, ['src/a.ts=src/b.ts']).code, 1, 'onto a file that stays');
  assert.equal(run(repo, ['src/a.ts=q/t.ts', 'src/b.ts=q/t.ts']).code, 1, 'two onto one');
  assert.equal(run(repo, []).code, 2);
  assert.equal(run(repo, ['--bogus']).code, 2);
  assert.equal(run(repo, ['--moves', 'absent.json']).code, 2);
  assert.equal(git(repo, 'status', '--porcelain'), '');
});

test('moves read from a JSON file, with its counts', () => {
  const repo = fixture(tree());
  const file = join(repo, '..', `moves${count}.json`);
  writeFileSync(file, JSON.stringify({ moves: [{ from: 'src/b.ts', to: 'src/core/b.ts' }, ['lib/x.ts', 'src/x.ts']], expect: { moved: 2 } }));
  const result = run(repo, ['--moves', file]);
  assert.equal(result.code, 0, result.err.join('\n'));
  assert.match(read(repo, 'src/a.ts'), /from '\.\/x\.js'/);
});

test('a specifier that named nothing before is reported and left alone', () => {
  const repo = fixture({ 'a.ts': "import './ghost';\nimport './m';\n", 'm.ts': 'export {};\n' });
  const result = run(repo, ['m.ts=n/m.ts']);
  assert.equal(result.code, 0);
  assert.ok(result.out.some((line) => line.includes('unresolved before') && line.includes('./ghost')));
  assert.equal(read(repo, 'a.ts'), "import './ghost';\nimport './n/m';\n");
});

test('specifiersOf finds the relative ones only', () => {
  const text = "import a from 'pkg';\nimport b from './b';\nexport * from \"../c.ts\";\nrequire('./d');\nconst s = './not-an-import';\n";
  assert.deepEqual(specifiersOf(text).map((s) => s.specifier), ['./b', '../c.ts', './d']);
});
