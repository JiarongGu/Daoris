import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';

const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/private-gate-files.mjs') as {
  privateGateFiles: (home: string) => string[];
  copyPrivateFiles: (home: string, tree: string, files: string[]) => string[];
};

test('the private files a gate reads come from the declaration, only under local/, with the sibling builds\' props; a copy is refreshed and a missing source says so', () => {
  const fx = makeFixture('private-gate-files');
  const home = join(fx.root, 'home');
  const tree = join(fx.root, 'tree');
  mkdirSync(join(home, 'local'), { recursive: true });
  mkdirSync(tree, { recursive: true });
  const declare = (sensitive?: unknown) => writeFileSync(join(home, 'daoris.gates.json'), JSON.stringify({ gates: [{ name: 'a', run: 'x' }], ...(sensitive ? { sensitive } : {}) }));
  const siblings = ['local/lyntai-local.props', 'local/shenora-local.props'];

  declare();
  assert.deepEqual(tool.privateGateFiles(home), ['local/sensitive-patterns.txt', ...siblings], 'the default, and the sibling builds\' props');
  declare({ patternsFile: 'local/own.txt' });
  assert.deepEqual(tool.privateGateFiles(home), ['local/own.txt', ...siblings]);
  declare({ patternsFile: 'private/patterns.txt' });
  assert.deepEqual(tool.privateGateFiles(home), siblings, 'not under local/: nothing copied');
  declare({ patternsFile: 'local/../secret.txt' });
  assert.deepEqual(tool.privateGateFiles(home), siblings, 'no way out of local/');

  const files = ['local/sensitive-patterns.txt'];
  assert.match(tool.copyPrivateFiles(home, tree, files).join('\n'), /local\/sensitive-patterns\.txt.*missing/);
  assert.equal(existsSync(join(tree, files[0]!)), false);
  writeFileSync(join(home, files[0]!), 'one\n');
  assert.match(tool.copyPrivateFiles(home, tree, files).join('\n'), /copied/);
  assert.equal(readFileSync(join(tree, files[0]!), 'utf8'), 'one\n');
  writeFileSync(join(home, files[0]!), 'two\n');
  tool.copyPrivateFiles(home, tree, files);
  assert.equal(readFileSync(join(tree, files[0]!), 'utf8'), 'two\n', 'refreshed on every run');
  fx.cleanup();
});
