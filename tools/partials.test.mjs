/**
 * PART1: the partial-types gate (D166's note "no partial type without generated code").
 *
 *   node --test tools/partials.test.mjs
 *
 * `checkPartials` reads a source tree and a list of today's offenders. Each case builds a small tree in a gitignored
 * scratch folder of the repository and holds the verdict: a type declared `partial` in more than one hand-written file
 * is refused unless it is listed with exactly those files; a listed entry that is no longer true is refused too, so the
 * list only shrinks.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkPartials, scanPartials } from './partials.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'partials-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

let n = 0;
/** A fresh tree from `{ 'src/A/X.cs': 'source' }`; returns its root. */
function tree(files) {
  const dir = join(scratch, `t${n++}`);
  for (const [rel, text] of Object.entries(files)) {
    const full = join(dir, rel);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, text);
  }
  return dir;
}

const part = (name, kind = 'class') => `namespace N;\npublic partial ${kind} ${name} { }\n`;

test('scratch under _fixtures is never read as source: a run beside the check makes and removes it', () => {
  const dir = tree({ 'src/A/X.cs': part('Foo'), 'src/Cli/_fixtures/run/Y.cs': part('Foo'), 'src/Cli/_fixtures/run/Z.cs': part('Foo') });
  assert.deepEqual(checkPartials(dir, {}).problems, []);
});

test('a type partial in two files is refused, naming the type and both files', () => {
  const dir = tree({ 'src/A/X.cs': part('Foo'), 'src/A/X.Part.cs': part('Foo') });
  const { problems } = checkPartials(dir, {});
  assert.equal(problems.length, 1);
  assert.match(problems[0], /Foo/);
  assert.match(problems[0], /src\/A\/X\.cs/);
  assert.match(problems[0], /src\/A\/X\.Part\.cs/);
});

test('a type partial in one file passes, with a generated pattern beside it', () => {
  const dir = tree({
    'src/A/X.cs': 'public static partial class Rx {\n [GeneratedRegex("a")] private static partial Regex A();\n}\n',
  });
  assert.deepEqual(checkPartials(dir, {}).problems, []);
});

test('a listed offender still split passes', () => {
  const dir = tree({ 'src/A/X.cs': part('Foo'), 'src/A/Y.cs': part('Foo') });
  assert.deepEqual(checkPartials(dir, { Foo: ['src/A/X.cs', 'src/A/Y.cs'] }).problems, []);
});

test('a listed type no longer split is a stale entry', () => {
  const dir = tree({ 'src/A/X.cs': part('Foo') });
  const { problems } = checkPartials(dir, { Foo: ['src/A/X.cs', 'src/A/Y.cs'] });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /Foo/);
  assert.match(problems[0], /cut it from the list/);
});

test('a listed file that no longer holds the type is a stale entry', () => {
  const dir = tree({ 'src/A/X.cs': part('Foo'), 'src/A/Y.cs': part('Foo'), 'src/A/Z.cs': 'class Z { }' });
  const { problems } = checkPartials(dir, { Foo: ['src/A/X.cs', 'src/A/Y.cs', 'src/A/Z.cs'] });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /src\/A\/Z\.cs/);
  assert.match(problems[0], /cut it from the list/);
});

test('a type split in a new file beyond its listed files is refused', () => {
  const dir = tree({ 'src/A/X.cs': part('Foo'), 'src/A/Y.cs': part('Foo'), 'src/A/New.cs': part('Foo') });
  const { problems } = checkPartials(dir, { Foo: ['src/A/X.cs', 'src/A/Y.cs'] });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /src\/A\/New\.cs/);
});

test('bin and obj are skipped', () => {
  const dir = tree({
    'src/A/X.cs': part('Foo'),
    'src/A/obj/Debug/Gen.cs': part('Foo'),
    'src/A/bin/Gen.cs': part('Foo'),
  });
  assert.deepEqual(checkPartials(dir, {}).problems, []);
});

test('a partial inside a comment or a string is not counted', () => {
  const dir = tree({
    'src/A/X.cs': part('Foo'),
    'src/A/Y.cs':
      '// public partial class Foo { }\n/* public partial class Foo { } */\n' +
      'class Y { string a = "public partial class Foo"; string b = @"partial class Foo"; string c = $"partial class {1}Foo"; }\n',
  });
  assert.deepEqual(checkPartials(dir, {}).problems, []);
});

test('record, struct, interface and nested partials count; a method partial does not', () => {
  const dir = tree({
    'src/A/X.cs': 'partial record R;\npartial record struct RS;\npartial struct S { }\npartial interface I { }\nclass O { partial class In { } partial void M(); }\n',
    'src/A/Y.cs': 'partial record R;\npartial record struct RS;\npartial struct S { }\npartial interface I { }\nclass O { partial class In { } partial void M(); }\n',
  });
  const found = scanPartials(dir);
  assert.deepEqual([...found.keys()].sort(), ['I', 'In', 'R', 'RS', 'S']);
});

test('a type partial twice in one file is one file', () => {
  const dir = tree({ 'src/A/X.cs': part('Foo') + part('Foo') });
  assert.deepEqual(checkPartials(dir, {}).problems, []);
});
