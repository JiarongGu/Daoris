import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildKeys, BuildLedger, withoutBuild } from './build-once.ts';

test('a test project\'s build is keyed by project and options, never by its filter, settings or logger', () => {
  assert.deepEqual(buildKeys('dotnet test src/A --filter Category!=Process'), ['dotnet test src/A']);
  assert.deepEqual(buildKeys('dotnet test src/A --filter X --logger "trx;LogFileName=f"'), ['dotnet test src/A']);
  assert.deepEqual(buildKeys('dotnet test src/A -c Release --settings s.xml'), ['dotnet test src/A -c Release']);
  assert.deepEqual(buildKeys('dotnet test A --filter C && dotnet test B --filter C'), ['dotnet test A', 'dotnet test B']);
  assert.deepEqual(buildKeys('npm run verify'), []);
});

test('a gate that only runs tests of projects an earlier passed gate built on this tree runs with --no-build', () => {
  const ledger = new BuildLedger();
  ledger.passed('driver', 'T1', 'dotnet test src/Driver.Tests --filter Category!=Process');
  ledger.passed('modules', 'T1', 'dotnet test src/Modules.Tests --filter Category!=Process');
  const run = 'dotnet test src/Driver.Tests --filter Category=Census && dotnet test src/Modules.Tests --filter Category=Census';
  const answer = ledger.reuse('T1', run);
  assert.equal(answer.run, `${run.replace(' && ', ' --no-build && ')} --no-build`);
  assert.deepEqual(answer.by, ['driver', 'modules']);
});

test('another tree, a project nobody built, or a build that did not pass is built again', () => {
  const ledger = new BuildLedger();
  ledger.passed('driver', 'T1', 'dotnet test src/Driver.Tests');
  assert.equal(ledger.reuse('T2', 'dotnet test src/Driver.Tests --filter X').run, 'dotnet test src/Driver.Tests --filter X');
  assert.equal(ledger.reuse('T1', 'dotnet test src/Other.Tests').run, 'dotnet test src/Other.Tests');
  assert.equal(ledger.reuse('T1', 'dotnet test src/Driver.Tests && dotnet test src/Other.Tests').run, 'dotnet test src/Driver.Tests --no-build && dotnet test src/Other.Tests', 'a segment at a time');
});

test('a command already told not to build is left alone', () => {
  assert.equal(withoutBuild('dotnet test A --no-build'), 'dotnet test A --no-build');
});
