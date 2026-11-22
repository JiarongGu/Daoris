import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { PATH_CHECKS } from './merge/reach.ts';
import { TYPECHECK_GATES, outsideDeclared, outsideMatched, typecheckReads } from './gate-reads.mjs';

// test-map: strings name paths as data — the gates' declared paths are what the compiler's file list is held to
// reaches: src/Daoris.Web/** — the page's compiler reads the whole folder's sources, scripts and configuration

const root = fileURLToPath(new URL('..', import.meta.url));
const gates = JSON.parse(readFileSync(new URL('../daoris.gates.json', import.meta.url), 'utf8')).gates;
const declaredPaths = (name) => gates.find((gate) => gate.name === name).paths;
const checkPaths = (name) => PATH_CHECKS.find((check) => check.gate === name).paths;

test('a file the compiler reads outside the gate\'s paths is named', () => {
  const reads = ['src/Daoris.Web/src/main.tsx', 'src/Daoris.Web/scripts/e2e-port.d.mts', 'src/Daoris.Web/tsconfig.json'];
  const paths = ['src/Daoris.Web/src/', 'src/Daoris.Web/tsconfig.json'];
  assert.deepEqual(outsideDeclared(reads, paths), ['src/Daoris.Web/scripts/e2e-port.d.mts']);
  assert.deepEqual(outsideDeclared(reads, [...paths, 'src/Daoris.Web/scripts/']), []);
  assert.deepEqual(outsideMatched(reads, ['src/Daoris.Web/src/**', 'src/Daoris.Web/tsconfig.json']), ['src/Daoris.Web/scripts/e2e-port.d.mts']);
  assert.deepEqual(outsideMatched(reads, ['src/Daoris.Web/src/**', 'src/Daoris.Web/scripts/**', 'src/Daoris.Web/tsconfig.json']), []);
});

test('each typecheck gate\'s paths cover every file its compiler reads', () => {
  assert.ok(TYPECHECK_GATES.length > 0);
  for (const { gate, project } of TYPECHECK_GATES) {
    const reads = typecheckReads(root, project);
    assert.ok(reads.length > 20 && reads.includes('src/Daoris.Web/scripts/e2e-port.d.mts'), `${gate}: the compiler's list is read: ${reads.length} files`);
    assert.deepEqual(outsideDeclared(reads, declaredPaths(gate)), [], `${gate}: files its compiler reads, outside the paths daoris.gates.json declares`);
    assert.deepEqual(outsideMatched(reads, checkPaths(gate)), [], `${gate}: files its compiler reads, outside the paths its path check (tools/merge/reach.ts) reaches by`);
  }
});

test('the rules gate reads the scripts its tests import beside the typecheck', () => {
  assert.ok(declaredPaths('web-rules').includes('src/Daoris.Web/scripts/'));
  assert.ok(checkPaths('web-rules').includes('src/Daoris.Web/scripts/**'));
});
