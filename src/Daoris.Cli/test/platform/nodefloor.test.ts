import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { NODE_FLOOR, nodeFloorRefusal } from '../../bin/node-floor.mjs';
import { cliRoot } from '../_sources.ts';

/**
 * The CLI's floor: the entry refuses a Node older than the package's `engines` before it loads the dispatcher, whichever way the CLI
 * is started (the desktop's program, an npm install, a build gate). The check takes the version as a value, so a stub stands for
 * the Node that runs the test.
 */

test('a Node at or above the floor is let through', () => {
  assert.equal(nodeFloorRefusal(`${NODE_FLOOR}.0.0`), null);
  assert.equal(nodeFloorRefusal(`${NODE_FLOOR}.11.1`), null);
  assert.equal(nodeFloorRefusal(`${NODE_FLOOR + 2}.1.0`), null);
});

test('a Node below the floor is refused in one sentence naming the floor and the version found', () => {
  assert.equal(
    nodeFloorRefusal('20.11.1'),
    'daoris: the doctrine commands run on Node.js 22 or later, and this is Node.js 20.11.1; nothing was done.',
  );
  assert.equal(nodeFloorRefusal('18.0.0')?.includes('Node.js 18.0.0'), true);
});

test('a version that is no version is refused, said as found', () => {
  assert.equal(
    nodeFloorRefusal('nonsense'),
    'daoris: the doctrine commands run on Node.js 22 or later, and this is Node.js nonsense; nothing was done.',
  );
});

test('the floor is the package\'s engines', () => {
  const engines = JSON.parse(readFileSync(join(cliRoot, 'package.json'), 'utf8')).engines.node as string;
  assert.equal(engines, `>=${NODE_FLOOR}`);
});

test('the entry asks the floor first, before it loads the dispatcher', () => {
  const entry = readFileSync(join(cliRoot, 'bin', 'daoris.mjs'), 'utf8');
  const asked = entry.indexOf('nodeFloorRefusal(process.versions.node)');
  assert.ok(asked > 0, 'the entry never asks the floor of the running Node');
  assert.ok(asked < entry.indexOf('await import('), 'the entry loads the dispatcher before asking the floor');
  assert.match(entry, /process\.exitCode = 2/);
});
