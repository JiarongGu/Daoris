// `init` pins the line endings `sync` writes (INIT1, D25's note): a repository with no `.gitattributes` gets one
// holding `* text=auto eol=lf`, and one that has its own keeps it byte for byte, named when it pins nothing.
// The file is never recorded in the lock, so from then on it is the repository's own (D5, D19).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { layoutFixture } from './_layout.ts';
import { applyLineEndings, LINE_ENDINGS_RULE, pinsLineEndings, planLineEndings } from '../src/lineendings.ts';

/** A repository about to adopt: the layout fixture, before its manifest is written. */
function adopting(tag: string) {
  const fx = layoutFixture(tag);
  rmSync(join(fx.repoFx.root, 'daoris.json'));
  return fx;
}

const bytes = (root: string, rel: string) => readFileSync(join(root, rel));

// ---------------------------------------------------------------- the plan

test('the plan names the .gitattributes it would create, and writes nothing', () => {
  const fx = adopting('init-eol-plan');

  const plan = planLineEndings(fx.repoFx.root);

  assert.equal(plan.path, '.gitattributes');
  assert.equal(plan.state, 'create');
  assert.ok(plan.state === 'create' && plan.content.split('\n').includes(LINE_ENDINGS_RULE), plan.state === 'create' ? plan.content : '');
  assert.equal(LINE_ENDINGS_RULE, '* text=auto eol=lf');
  assert.equal(fx.repoFx.exists('.gitattributes'), false, 'a plan touches no disk');
  fx.cleanup();
});

test('the plan keeps a .gitattributes the repository has, and says whether it pins line endings', () => {
  const fx = adopting('init-eol-plan-kept');

  fx.repoFx.write('.gitattributes', '*.png binary\n');
  assert.deepEqual(planLineEndings(fx.repoFx.root), { path: '.gitattributes', state: 'kept', pinned: false });

  fx.repoFx.write('.gitattributes', '# ours\r\n* text=auto eol=lf\r\n');
  assert.deepEqual(planLineEndings(fx.repoFx.root), { path: '.gitattributes', state: 'kept', pinned: true });
  fx.cleanup();
});

test('applying a kept plan writes nothing', () => {
  const fx = adopting('init-eol-apply-kept');
  fx.repoFx.write('.gitattributes', '*.png binary');
  const before = bytes(fx.repoFx.root, '.gitattributes');

  applyLineEndings(fx.repoFx.root, planLineEndings(fx.repoFx.root));

  assert.deepEqual(bytes(fx.repoFx.root, '.gitattributes'), before);
  fx.cleanup();
});

// What counts as pinning: a line for every file whose last word on `eol` is `lf`. Anything narrower leaves some
// file `sync` writes to the machine's `core.autocrlf`.
const pinning: [string, string, boolean][] = [
  ['the rule init writes', '* text=auto eol=lf\n', true],
  ['text set, eol lf', '* text eol=lf\n', true],
  ['after other lines and comments', '# ours\n*.png binary\n\n* text=auto eol=lf\n', true],
  ['CRLF and a BOM', '﻿* text=auto eol=lf\r\n', true],
  ['tabs between the words', '*\ttext=auto\teol=lf\n', true],
  ['nothing at all', '', false],
  ['only for markdown', '*.md text eol=lf\n', false],
  ['text=auto with no eol', '* text=auto\n', false],
  ['eol crlf', '* text=auto eol=crlf\n', false],
  ['commented out', '# * text=auto eol=lf\n', false],
  ['unset by a later line', '* text=auto eol=lf\n* -eol\n', false],
  ['set again after an unset', '* -eol\n* eol=lf\n', true],
];

for (const [name, text, expected] of pinning) {
  test(`pinsLineEndings: ${name}`, () => {
    assert.equal(pinsLineEndings(text), expected);
  });
}

// ---------------------------------------------------------------- init

test('init creates .gitattributes holding the rule, LF and BOM-less, and says it wrote it', () => {
  const fx = adopting('init-eol-create');

  const { code, out } = fx.cli('init');

  assert.equal(code, 0, out);
  const written = bytes(fx.repoFx.root, '.gitattributes');
  assert.notEqual(written[0], 0xef, 'no BOM');
  assert.equal(written.includes(0x0d), false, 'no CR');
  const text = written.toString('utf8');
  assert.ok(text.endsWith('\n'));
  assert.ok(text.split('\n').includes('* text=auto eol=lf'), text);
  assert.equal(pinsLineEndings(text), true);
  assert.match(out, /wrote \.gitattributes/);
  fx.cleanup();
});

test('init leaves a .gitattributes that pins nothing byte-identical, and names the rule it lacks', () => {
  const fx = adopting('init-eol-unpinned');
  // CRLF and no final newline: every byte of it is the repository's, and none is "fixed".
  fx.repoFx.write('.gitattributes', '# ours\r\n*.png binary');
  const before = bytes(fx.repoFx.root, '.gitattributes');

  const { code, out } = fx.cli('init');

  assert.equal(code, 0, out);
  assert.deepEqual(bytes(fx.repoFx.root, '.gitattributes'), before);
  assert.doesNotMatch(out, /wrote \.gitattributes/);
  assert.match(out, /\.gitattributes/);
  assert.match(out, /\* text=auto eol=lf/);
  fx.cleanup();
});

test('init leaves a .gitattributes that pins line endings byte-identical, and has nothing to say about it', () => {
  const fx = adopting('init-eol-pinned');
  fx.repoFx.write('.gitattributes', '* text=auto eol=lf\r\n*.png binary\r\n');
  const before = bytes(fx.repoFx.root, '.gitattributes');

  const { code, out } = fx.cli('init');

  assert.equal(code, 0, out);
  assert.deepEqual(bytes(fx.repoFx.root, '.gitattributes'), before);
  assert.doesNotMatch(out, /\.gitattributes/);
  fx.cleanup();
});

test('init refusing a harness it does not know leaves no .gitattributes behind', () => {
  const fx = adopting('init-eol-refused');

  const { code } = fx.cli('init', '--harness', 'nonesuch');

  assert.equal(code, 2);
  assert.equal(fx.repoFx.exists('.gitattributes'), false);
  fx.cleanup();
});

// ---------------------------------------------------------------- the lock

test('the lock never records .gitattributes, so the repository edits it freely and check stays clean', () => {
  const fx = adopting('init-eol-lock');
  assert.equal(fx.cli('init').code, 0);
  const sync = fx.cli('sync');
  assert.equal(sync.code, 0, sync.out);

  const lock = readFileSync(join(fx.repoFx.root, 'daoris.lock'), 'utf8');
  assert.doesNotMatch(lock, /gitattributes/);

  fx.repoFx.write('.gitattributes', `${fx.repoFx.read('.gitattributes')}*.png binary\n`);
  const edited = bytes(fx.repoFx.root, '.gitattributes');
  const check = fx.cli('check');
  assert.equal(check.code, 0, check.out);
  assert.doesNotMatch(check.out, /gitattributes/);

  // Nor does a later sync touch it: a lock entry the canon does not hold would be a retirement (D19).
  const again = fx.cli('sync');
  assert.equal(again.code, 0, again.out);
  assert.deepEqual(bytes(fx.repoFx.root, '.gitattributes'), edited);
  fx.cleanup();
});
