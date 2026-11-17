import { test } from 'vitest';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { carriers, check, type LockRow } from './packages.ts';

const row = (fingerprint: string): LockRow => ({ fingerprint, tree: 't', ports: {}, outside: {}, suite: 's', toolchain: 'x', passed: 'now' });

test('a branch that still holds the adapters\' lock is refused, naming the merge of main, and the refs that hold it are listed until none does', () => {
  mkdirSync('_fixtures', { recursive: true });
  const root = mkdtempSync(join('_fixtures', 'packages-old-lock-'));
  const git = (...args: string[]) => execFileSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@t', ...args], { cwd: root, encoding: 'utf8', windowsHide: true });
  const said: string[] = [];
  const error = console.error;
  try {
    git('init', '-q', '-b', 'main');
    writeFileSync(join(root, 'daoris.packages.lock'), `${JSON.stringify({ schema: 2, packages: { 'Daoris.Adapters.Clock': row('kept-row') } }, null, 2)}\n`);
    git('add', '.');
    git('commit', '-q', '-m', 'packages');
    assert.deepEqual(carriers(root), [], 'no ref holds the old lock');

    git('checkout', '-q', '-b', 'before-the-move');
    writeFileSync(join(root, 'daoris.adapters.lock'), JSON.stringify({ schema: 1, adapters: { 'Daoris.Adapters.Clock': row('released-there') } }));
    git('add', '.');
    git('commit', '-q', '-m', 'old');
    assert.deepEqual(carriers(root), ['before-the-move'], 'the branch made before the move');

    console.error = (line: string) => said.push(line);
    assert.equal(check(root, { tree: () => null, text: () => null, dirty: () => [], packages: () => [], listing: () => null }), 1);
    assert.match(said.join('\n'), /holds daoris\.adapters\.lock.*merge main into it/s);
    assert.match(said.join('\n'), /Daoris\.Adapters\.Clock/, 'the row it released there');

    git('checkout', '-q', 'main');
    git('branch', '-q', '-D', 'before-the-move');
    assert.deepEqual(carriers(root), []);
  } finally {
    console.error = error;
    rmSync(root, { recursive: true, force: true });
  }
}, 60_000);
