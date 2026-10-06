import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compareNames, findName, foldName, sameName } from '../src/casefold.ts';

/**
 * A name compared without case (CASEFOLD1; AGENTREAD1c, D125's notes): as the driver's `OrdinalIgnoreCase` compares it,
 * each code point to its one capital and never a wider one. Every CLI twin of a driver file that compares a name without
 * case goes through this module, so the two doors find the same entry. The table, `fixtures/name-case.json`, is .NET's
 * answers, measured: each pair, whether it is one name, and the sign `OrdinalIgnoreCase` orders it by.
 */

type Row = [why: string, a: string, b: string, same: boolean, order: -1 | 0 | 1];

const here = dirname(fileURLToPath(import.meta.url));
const ROWS = JSON.parse(readFileSync(join(here, 'fixtures', 'name-case.json'), 'utf8')) as Row[];

test('two names are one as the driver\'s OrdinalIgnoreCase finds them (the shared table)', () => {
  for (const [why, a, b, same] of ROWS) {
    assert.equal(sameName(a, b), same, why);
    assert.equal(sameName(b, a), same, `${why}, the other way`);
    assert.equal(foldName(a) === foldName(b), same, `${why}, folded`);
  }
});

test('two names order as the driver\'s OrdinalIgnoreCase orders them (the shared table)', () => {
  for (const [why, a, b, , order] of ROWS) {
    assert.equal(Math.sign(compareNames(a, b)), order, why);
    assert.equal(Math.sign(compareNames(b, a)), -order || 0, `${why}, the other way`);
  }
});

test('a name is found among others in any case, the first as written, and none is null', () => {
  assert.equal(findName(['Work', 'work', 'home'], 'WORK'), 'Work');
  assert.equal(findName(['straße'], 'STRASSE'), null);
  assert.equal(findName(['ışık'], 'IŞIK'), null);
  assert.equal(findName(new Set(['Νίκος']), 'ΝΊΚΟΣ'), 'Νίκος');
  assert.equal(findName([], 'work'), null);
});

test('folding keeps a name\'s length, so names of different lengths are never one', () => {
  for (const [, a, b] of ROWS) {
    assert.equal(Array.from(foldName(a)).length, Array.from(a).length, a);
    assert.equal(Array.from(foldName(b)).length, Array.from(b).length, b);
  }
});
