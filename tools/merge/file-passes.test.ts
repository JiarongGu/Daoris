import { test } from 'node:test';
import assert from 'node:assert/strict';
import { junitPasses, vitestPasses } from './file-passes.ts';

test('a node file passed when it has test cases and none failed, errored or skipped, even where another file failed', () => {
  const xml = `<testsuites>
	<testcase name="a" file="C:\\r\\a.test.ts"/>
	<testcase name="b" file="C:\\r\\a.test.ts"/>
	<testcase name="c" file="C:\\r\\b.test.ts"><failure message="x"/></testcase>
	<testcase name="d" file="C:\\r\\c.test.ts"><skipped/></testcase>
	<testcase name="e" file="C:\\r\\d &amp; e.test.ts"/>
</testsuites>`;
  assert.deepEqual([...junitPasses(xml)].sort(), ['C:\\r\\a.test.ts', 'C:\\r\\d & e.test.ts']);
});

test('a page file passed when vitest says so, and a report that does not read names none', () => {
  const json = JSON.stringify({ testResults: [{ name: '/r/a.test.ts', status: 'passed' }, { name: '/r/b.test.ts', status: 'failed' }] });
  assert.deepEqual([...vitestPasses(json)], ['/r/a.test.ts']);
  assert.equal(vitestPasses('not json').size, 0);
});
