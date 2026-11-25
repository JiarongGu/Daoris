import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, lstatSync, mkdirSync, readFileSync, symlinkSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';

const setup = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/worktree-setup.mjs') as {
  localFiles: (home: string) => string[];
  copyLocal: (home: string, tree: string) => string[];
  dependencyState: (tree: string, folder: string) => 'current' | 'missing' | 'stale' | 'linked';
  setupCommands: (
    states: Record<string, string>, options?: { build?: boolean; prep?: { regenerate: string[]; baseline: string | null } },
  ) => { label: string; command: string; cwd: string }[];
  refuseHome: (home: string, tree: string) => void;
};

import { declaredPrep } from '../../../tools/tree-prep.mjs';
import { repoRoot } from '../../../tools/merge/repo-gates.ts';
import { repoBudgetSource } from '../../../tools/merge/repo-home.ts';

const ours = { prep: declaredPrep(repoRoot, repoBudgetSource()) };

const lock = (versions: Record<string, string>) => JSON.stringify({
  lockfileVersion: 3, packages: { '': {}, ...Object.fromEntries(Object.entries(versions).map(([name, version]) => [`node_modules/${name}`, { version }])) },
});

test('the local files a builder needs are what the declaration names (localInputs, then the sensitive patterns), never every local/*.props, copied as files and never linked', () => {
  const fx = makeFixture('worktree-setup-copy');
  const home = join(fx.root, 'home');
  const tree = join(fx.root, 'tree');
  mkdirSync(join(home, 'local'), { recursive: true });
  mkdirSync(tree, { recursive: true });
  writeFileSync(join(home, 'daoris.gates.json'), JSON.stringify({ gates: [{ name: 'a', run: 'x' }], localInputs: ['local/lyntai-local.props', 'local/y.props'] }));
  writeFileSync(join(home, 'local', 'lyntai-local.props'), '<a/>\n');
  writeFileSync(join(home, 'local', 'y.props'), '<b/>\n');
  writeFileSync(join(home, 'local', 'x.props'), '<undeclared/>\n');
  writeFileSync(join(home, 'local', 'sensitive-patterns.txt'), 'p\n');
  writeFileSync(join(home, 'local', 'notes.md'), 'private\n');
  assert.deepEqual(setup.localFiles(home), ['local/lyntai-local.props', 'local/sensitive-patterns.txt', 'local/y.props']);

  const lines = setup.copyLocal(home, tree);
  assert.match(lines.join('\n'), /local\/lyntai-local\.props: copied/);
  assert.match(lines.join('\n'), /local\/sensitive-patterns\.txt: copied/);
  assert.equal(readFileSync(join(tree, 'local', 'y.props'), 'utf8'), '<b/>\n');
  assert.equal(existsSync(join(tree, 'local', 'x.props')), false, 'an undeclared props file is not handed over');
  assert.equal(existsSync(join(tree, 'local', 'shenora-local.props')), false, 'a props file the declaration does not name is not made');
  assert.equal(lstatSync(join(tree, 'local', 'lyntai-local.props')).isSymbolicLink(), false, 'a file, never a link');
  assert.equal(existsSync(join(tree, 'local', 'notes.md')), false, 'only the props and the patterns are handed over');

  // A tree whose local/ is a link into the main checkout would write through it: refused, naming the link.
  const linked = join(fx.root, 'linked');
  mkdirSync(linked, { recursive: true });
  symlinkSync(join(home, 'local'), join(linked, 'local'), 'junction');
  assert.throws(() => setup.copyLocal(home, linked), /local is a link/);
  fx.cleanup();
});

test('a folder\'s dependencies are current by the stamp its install left or by npm\'s own record of the versions, and a link into another checkout is never installed over', () => {
  const fx = makeFixture('worktree-setup-deps');
  const tree = join(fx.root, 'tree');
  mkdirSync(join(tree, 'node_modules'), { recursive: true });
  assert.equal(setup.dependencyState(join(fx.root, 'none'), '.'), 'missing');
  writeFileSync(join(tree, 'package-lock.json'), lock({ a: '1.0.0', b: '2.0.0' }));
  // Installed by something else: npm's hidden lockfile records what is in node_modules.
  assert.equal(setup.dependencyState(tree, '.'), 'stale', 'no record of what was installed');
  writeFileSync(join(tree, 'node_modules', '.package-lock.json'), lock({ a: '1.0.0' }));
  assert.equal(setup.dependencyState(tree, '.'), 'current', 'every installed version is the lockfile\'s');
  assert.equal(setup.dependencyState(tree, '.'), 'current', 'and it is stamped, so the next read is the stamp\'s');
  writeFileSync(join(tree, 'node_modules', '.package-lock.json'), lock({ a: '0.9.0' }));
  writeFileSync(join(tree, 'package-lock.json'), lock({ a: '1.0.0', b: '2.0.1' }));
  assert.equal(setup.dependencyState(tree, '.'), 'stale', 'a lockfile that moved past the stamp');

  const other = join(fx.root, 'other');
  mkdirSync(join(other, 'real'), { recursive: true });
  writeFileSync(join(other, 'package-lock.json'), lock({}));
  symlinkSync(join(other, 'real'), join(other, 'node_modules'), 'junction');
  assert.equal(setup.dependencyState(other, '.'), 'linked');
  fx.cleanup();
});

test('the setup installs what is missing or stale from the lockfile, refuses a link, and always sets the merge drivers and primes the index, the declared regenerate steps, the restore and the base', () => {
  const commands = setup.setupCommands({ '.': 'current', 'src/Daoris.Web': 'stale' }, ours);
  assert.deepEqual(commands.map((each) => each.label), [
    'install src/Daoris.Web', 'drivers', 'index', 'regenerate', 'restore', 'baseline',
  ]);
  assert.equal(commands.find((each) => each.label === 'regenerate')!.command, 'node tools/contracts.mjs --if-stale', 'the gates file declares it, as the driver reads it');
  assert.equal(commands.find((each) => each.label === 'baseline')!.command, 'node tools/quick.mjs --record-base');
  assert.equal(commands[0]!.command, 'npm ci --no-audit --no-fund');
  assert.equal(commands[0]!.cwd, 'src/Daoris.Web');
  assert.match(commands.find((each) => each.label === 'restore')!.command, /^dotnet restore src\/Daoris\.Desktop\/Daoris\.Desktop\.Driver\.Tests\b/);
  assert.throws(() => setup.setupCommands({ '.': 'linked' }), /node_modules is a link/);
  assert.deepEqual(setup.setupCommands({ '.': 'current', 'src/Daoris.Web': 'current' }, ours).map((each) => each.label), ['drivers', 'index', 'regenerate', 'restore', 'baseline']);
  const bare = setup.setupCommands({ '.': 'current', 'src/Daoris.Web': 'current' }, { prep: { regenerate: ['a', 'b'], baseline: null } });
  assert.deepEqual(bare.map((each) => each.label), ['drivers', 'index', 'regenerate', 'regenerate', 'restore'], 'nothing declared, no base run');
});

test('the declared preparation is each gate\'s regenerate once and the top-level baseline, nothing where the file does not read', () => {
  const fx = makeFixture('tree-prep-declared');
  writeFileSync(join(fx.root, 'daoris.gates.json'), JSON.stringify({
    gates: [{ name: 'a', run: 'x', regenerate: 'one' }, { name: 'b', run: 'y', regenerate: 'one' }, { name: 'c', run: 'z', regenerate: 'two' }, { name: 'd', run: 'w' }],
    treeBaseline: 'base',
  }));
  assert.deepEqual(declaredPrep(fx.root), { regenerate: ['one', 'two'], baseline: 'base' });
  assert.deepEqual(declaredPrep(join(fx.root, 'absent')), { regenerate: [], baseline: null });
  fx.cleanup();
});

test('the main checkout is never set up: the setup is a builder\'s worktree\'s', () => {
  assert.throws(() => setup.refuseHome('/x/home', '/x/home'), /the main checkout/);
  assert.doesNotThrow(() => setup.refuseHome('/x/home', '/x/tree'));
});
