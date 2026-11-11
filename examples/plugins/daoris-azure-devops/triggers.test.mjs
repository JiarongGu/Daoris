// The trigger readers on their own: a pipeline's YAML `trigger:` in the shapes pipelines write it, and which stages of a release start themselves.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { startsByHand, yamlTrigger } from './triggers.mjs';

test('a YAML trigger is read in its scalar, list, flow and map shapes, and left unread in any other', () => {
  assert.equal(yamlTrigger('pool:\n  vmImage: x\n'), undefined);
  assert.equal(yamlTrigger('trigger: none  # off\n'), 'none');
  assert.deepEqual(yamlTrigger('trigger: [main, "release/*"]\n').branches.include, ['main', 'release/*']);
  assert.deepEqual(yamlTrigger('trigger:\n- main\n- dev\npr: none\n').branches.include, ['main', 'dev']);
  const map = yamlTrigger('trigger:\n  batch: true\n  branches:\n    include:\n    - main\n    exclude: [old/*]\n  paths:\n    include: [src]\n    exclude:\n    - src/docs\n');
  assert.deepEqual([map.branches, map.paths], [{ include: ['main'], exclude: ['old/*'] }, { include: ['src'], exclude: ['src/docs'] }]);
  assert.equal(yamlTrigger('trigger:\n  branches: &anchor\n    include: [a]\n  surprise: 1\n'), null);
  assert.equal(yamlTrigger('trigger: main\n  extra: 1\n'), null);
});

test('a release whose every stage waits on no event or earlier stage starts by hand; one a stage chains to an event does not', () => {
  const event = [{ conditionType: 'event', name: 'ReleaseStarted' }];
  const after = (name) => [{ conditionType: 'environmentState', name }];
  assert.equal(startsByHand({ environments: [{ name: 'A', conditions: [] }, { name: 'B', conditions: after('A') }] }), true);
  assert.equal(startsByHand({ environments: [{ name: 'A', conditions: event }, { name: 'B', conditions: after('A') }] }), false);
  assert.equal(startsByHand({ environments: [{ name: 'A' }] }), false);
  assert.equal(startsByHand({ environments: [{ name: 'A', conditions: after('B') }, { name: 'B', conditions: after('A') }] }), true);
});
