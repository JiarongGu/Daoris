import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { ChildProcess } from 'node:child_process';
import { PATIENCE } from '../test/_fixture.ts';
import { tool } from './support/merge-branch.ts';
import { boundedNode } from './support/bounded-node.ts';

/**
 * The tool tests' bounded invocation (`support/bounded-node.ts`): a case's child that gives no verdict is ended at its bound, and a
 * tree the end leaves holding the child's output is named and left, never waited on. Run by the `tool-tests` gate.
 */

// A child that starts a grandchild holding the child's own output, then waits: its `close` comes only once both are gone.
const HOLDS_ITS_OUTPUT = [
  '-e',
  "require('node:child_process').spawn(process.execPath, ['-e', 'setTimeout(() => {}, 600000)'], { stdio: 'inherit' }); setTimeout(() => {}, 600000);",
];

const pause = (ms: number) => new Promise((done) => setTimeout(done, ms));

async function gone(started: Set<ChildProcess>) {
  for (const end = Date.now() + PATIENCE; started.size > 0 && Date.now() < end;) await pause(100);
}

test('TOOLTESTHANG1: an invocation whose tree outlives a refused end is answered after the grace, the leak named and left running', async () => {
  const started = new Set<ChildProcess>();
  const ends: number[] = [];
  try {
    // patience: claim — the bound and the grace are what the row tests, each elapsing by design
    const answered = await boundedNode(HOLDS_ITS_OUTPUT, { cwd: process.cwd(), env: process.env, ms: 500, grace: 500, started, end: (pid) => { ends.push(pid); return false; } });
    assert.equal(answered.status, null);
    assert.match(answered.out, /no verdict within 500 ms/);
    assert.match(answered.out, /its tree outlived the end, which taskkill refused: pid \d+ or what it started still holds its output after 500 ms/);
    assert.equal(ends.length, 1);
    assert.equal(started.size, 1, 'the child the end left is still the case\'s to end');
  } finally {
    for (const child of started) tool.endTree(child.pid!);
    await gone(started);
  }
  assert.equal(started.size, 0, 'the tree ended once it was ended whole');
});

test('TOOLTESTHANG1: an invocation the end reaches whole is answered at its close, saying only that it was ended', async () => {
  const started = new Set<ChildProcess>();
  try {
    // patience: claim — the bound is what the row tests, elapsing by design
    const answered = await boundedNode(HOLDS_ITS_OUTPUT, { cwd: process.cwd(), env: process.env, ms: 500, started, end: (pid) => tool.endTree(pid) });
    assert.notEqual(answered.status, 0);
    assert.match(answered.out, /no verdict within 500 ms/);
    assert.doesNotMatch(answered.out, /outlived/);
    assert.equal(started.size, 0);
  } finally {
    for (const child of started) tool.endTree(child.pid!);
    await gone(started);
  }
});
