import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { sweepAndSay, sweepMergeTemps } from './merge-temps.ts';

const scratch = join(dirname(dirname(dirname(fileURLToPath(import.meta.url)))), '_fixtures', 'merge-temps');

function withRoot(files: string[], body: (root: string) => void): void {
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(scratch, { recursive: true });
  try {
    for (const file of files) writeFileSync(join(scratch, file), 'x');
    body(scratch);
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
}

test('the temp files git left at the root are removed and named, and no other file is touched', () => {
  const stray = ['.merge_file_a1B2c3', '.merge_file_ZZZZZZ'];
  const keep = ['TASKS.md', '.merge_file_notes.md', '.merge_file_a1B2c3.bak', 'merge_file_a1B2c3'];
  withRoot([...stray, ...keep], (root) => {
    assert.deepEqual(sweepMergeTemps(root).sort(), [...stray].sort());
    assert.deepEqual(readdirSync(root).sort(), [...keep].sort());
  });
});

test('a root with none left says nothing, and one with some says how many', () => {
  withRoot(['TASKS.md'], (root) => {
    const said: string[] = [];
    sweepAndSay(root, (line) => said.push(line));
    assert.deepEqual(said, []);
  });
  withRoot(['.merge_file_abcdef'], (root) => {
    const said: string[] = [];
    sweepAndSay(root, (line) => said.push(line));
    assert.deepEqual(said, ['removed 1 stray git merge temp file at the root: .merge_file_abcdef']);
    assert.equal(existsSync(join(root, '.merge_file_abcdef')), false);
  });
});
