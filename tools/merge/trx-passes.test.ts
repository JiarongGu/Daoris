import { test } from 'node:test';
import assert from 'node:assert/strict';
import { passedClasses } from './trx-passes.ts';

const trx = (tests: Array<[string, string, string]>): string => `<TestRun><TestDefinitions>${tests.map(([id, cls]) => `<UnitTest name="m" id="${id}"><TestMethod className="${cls}" name="m" /></UnitTest>`).join('')}</TestDefinitions>
<Results>${tests.map(([id, , outcome]) => `<UnitTestResult testId="${id}" outcome="${outcome}" />`).join('')}</Results></TestRun>`;

test('a class passed when every test of it ran and passed; a failure or a skip in it withdraws it', () => {
  const passed = passedClasses(trx([['1', 'N.A', 'Passed'], ['2', 'N.A', 'Passed'], ['3', 'N.B', 'Passed'], ['4', 'N.B', 'Failed'], ['5', 'N.C', 'NotExecuted'], ['6', 'N.D', 'Passed'], ['7', 'N.D', 'NotExecuted']]));
  assert.deepEqual([...passed].sort(), ['N.A']);
});

test('a nested class keeps its plus and an escaped name its characters', () => {
  assert.deepEqual([...passedClasses(trx([['1', 'N.Outer+Inner', 'Passed'], ['2', 'N.G&lt;int&gt;', 'Passed']]))].sort(), ['N.G<int>', 'N.Outer+Inner']);
});
