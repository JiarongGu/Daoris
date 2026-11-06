import assert from 'node:assert/strict';
import { existsSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import { declareStubTwo, stubTwoDeclared, stubTwoListed } from './stub-two.mjs';

const scratch = join(import.meta.dirname, '..', '_fixtures', 'stub-two-test');

test('the second maker is declared with its two postures beside the kit\'s manifest', () => {
  const manifest = stubTwoDeclared('D:/scratch/turn-end-agent.mjs');

  assert.equal(manifest.id, 'rehearsal.stub-two');
  assert.deepEqual(manifest.harnesses, [{
    name: 'stub-b', command: ['node', 'D:/scratch/turn-end-agent.mjs'], maker: 'Stub Two', product: 'Stub B',
    askingPosture: 'ask', fullPosture: 'allow-all',
  }]);
});

test('it is written as the plugin\'s manifest in its own folder, and listed once the driver says it declared', () => {
  try {
    declareStubTwo(join(scratch, 'plugins', 'rehearsal.stub-two'), 'turn-end-agent.mjs');
    const file = join(scratch, 'plugins', 'rehearsal.stub-two', 'plugin.json');

    assert.ok(existsSync(file));
    assert.equal(JSON.parse(readFileSync(file, 'utf8')).harnesses[0].fullPosture, 'allow-all');
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
  assert.equal(stubTwoListed('stub-b  Stub B\n (declared by plugin `rehearsal.stub-two`)'), true);
  assert.equal(stubTwoListed('gate-agent (declared by plugin `rehearsal.agent`)'), false);
});
