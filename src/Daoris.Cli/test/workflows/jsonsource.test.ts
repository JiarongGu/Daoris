import { test } from 'node:test';
import assert from 'node:assert/strict';
import { numberText, parseWithSource, sourceOf, stringifyWithSource } from '../../src/workflows/jsonsource.ts';

/**
 * FABRIC-GRAPH1c, GRAPH1f: JSON read with what its text said. The values are `JSON.parse`'s, each object and array holding, out of sight,
 * its keys as written and each number's own text, and `stringifyWithSource` writes them back so; a value made otherwise is written as
 * `JSON.stringify(value, null, 2)` writes it.
 */

const SAMPLES = [
  '{}', '[]', 'null', 'true', '"text"', '-0', '12.5e-3', '[1, 2.0, -3, 4e2, "x", null, false, {"a": []}]',
  '{"b": 1, "a": {"c": [true, {"d": null}]}, "e": "quote \\" back \\\\ slash \\/ \\u0041\\n\\t"}',
  '{"__proto__": "kept", "constructor": 1}', ' \n\t{ "spaced" : [ 1 , 2 ] } \r\n',
];

test('json-source: every value is the one JSON.parse gives, and a plain value is written as JSON.stringify writes it', () => {
  for (const text of SAMPLES) {
    const read = parseWithSource(text);
    assert.deepEqual(read, JSON.parse(text), text);
    assert.equal(stringifyWithSource(JSON.parse(text)), JSON.stringify(JSON.parse(text), null, 2), text);
  }
  assert.deepEqual(Object.keys(parseWithSource('{"__proto__": "kept"}') as object), ['__proto__']);
});

test('json-source: a number keeps its own text and an object its keys as written, while the value read stands', () => {
  const text = '{"z": 1.0, "2": "two", "1": "one", "big": 12345678901234567890, "list": [1e2, 0.10]}';
  const read = parseWithSource(text) as Record<string, unknown>;
  assert.deepEqual(sourceOf(read)?.keys, ['z', '2', '1', 'big', 'list']);
  assert.equal(numberText(read, 'z'), '1.0');
  assert.equal(numberText(read['list'], 1), '0.10');
  assert.equal(stringifyWithSource(read), '{\n  "z": 1.0,\n  "2": "two",\n  "1": "one",\n  "big": 12345678901234567890,\n  "list": [\n    1e2,\n    0.10\n  ]\n}');

  // A number changed since is written as JavaScript writes it, and a key added since after those written.
  read['z'] = 2;
  read['added'] = true;
  assert.equal(numberText(read, 'z'), undefined);
  assert.match(stringifyWithSource(read), /^\{\n {2}"z": 2,\n {2}"2": "two",[\s\S]*"added": true\n\}$/);
});

test('json-source: a key written twice is the last value, in its first place, and said', () => {
  const read = parseWithSource('{"a": 1, "b": 2, "a": 3}') as Record<string, unknown>;
  assert.deepEqual(read, { a: 3, b: 2 });
  assert.deepEqual(sourceOf(read)?.keys, ['a', 'b']);
  assert.equal(sourceOf(read)?.twice, true);
});

test('json-source: what JSON.parse refuses is refused', () => {
  for (const text of ['', '{', '{"a" 1}', '[1,]', '{"a": 1,}', "{'a': 1}", '01', '1.', '"\u0001"', '"\\x"', 'tru', '{} x', `${String.fromCharCode(0xfeff)}{}`]) {
    assert.throws(() => JSON.parse(text), SyntaxError, `JSON.parse took ${JSON.stringify(text)}`);
    assert.throws(() => parseWithSource(text), SyntaxError, JSON.stringify(text));
  }
});
