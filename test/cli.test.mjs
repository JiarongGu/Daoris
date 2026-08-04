import { test } from 'node:test';
import assert from 'node:assert/strict';
import { runCli } from '../bin/daoris.mjs';

test('--help prints usage and exits 0', () => {
  const out = [];
  const code = runCli(['--help'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 0);
  assert.match(out.join('\n'), /daoris <command>/);
  for (const cmd of ['init', 'sync', 'check', 'upstream', 'index', 'status']) {
    assert.match(out.join('\n'), new RegExp(`\\b${cmd}\\b`));
  }
});

test('--version prints the package version', () => {
  const out = [];
  const code = runCli(['--version'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 0);
  assert.match(out.join('\n'), /^\d+\.\d+\.\d+$/m);
});

test('an unknown command is a tool error (exit 2)', () => {
  const out = [];
  const code = runCli(['frobnicate'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 2);
  assert.match(out.join('\n'), /unknown command/i);
});

test('no arguments prints usage and exits 2', () => {
  const out = [];
  assert.equal(runCli([], process.cwd(), (s) => out.push(s)), 2);
});
