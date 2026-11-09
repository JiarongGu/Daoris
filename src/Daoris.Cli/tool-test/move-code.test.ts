import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { makeFixture, type Fixture } from '../test/_fixture.ts';

/**
 * The mover splits the large tools: it moves statements as text and asserts what it moved, so a builder never retypes
 * code. These cases run it on small modules in a scratch folder and import what it wrote.
 */

interface Move { to: string; names?: string[]; span?: [string, string]; expect: number; header?: string }
interface Planned { from: string; sourceText: string; outputs: { to: string; text: string }[] }

const mover = await import(
  // @ts-expect-error -- a plain .mjs tool, typed below
  '../../../tools/move-code.mjs') as {
  MoveRefused: new (message: string) => Error;
  planMoves(root: string, plan: { from: string; moves: Move[] }): Planned;
  planLines(result: Planned): string[];
  applyMoves(root: string, result: Planned): void;
};

const TOOL = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'tools', 'move-code.mjs');

const SOURCE = [
  "import { join } from 'node:path';",
  "import { helper } from './lib/helper.mjs';",
  '',
  '/** The width. */',
  'export const WIDTH = 3;',
  '',
  '// Kept here.',
  'const local = () => 1;',
  '',
  '/**',
  ' * Doubles.',
  ' */',
  'export function double(x) {',
  '  return helper(x) * WIDTH;',
  '}',
  '',
  "function inner() { return join('a', 'b'); }",
  '',
  'export const user = () => double(local()) + inner().length;',
  '',
].join('\n');

function scratch(name: string): Fixture {
  const fx = makeFixture(`move-code-${name}`);
  fx.write('a.mjs', SOURCE);
  fx.write('lib/helper.mjs', 'export const helper = (x) => x * 2;\n');
  return fx;
}

const refusal = (fn: () => unknown): Error => {
  try {
    fn();
  } catch (error) {
    return error as Error;
  }
  throw new Error('expected the plan to be refused');
};

const run = (fx: Fixture, plan: unknown, ...flags: string[]) => {
  fx.write('moves.json', JSON.stringify(plan));
  return spawnSync(process.execPath, [TOOL, '--moves', 'moves.json', '--root', fx.root, ...flags], { encoding: 'utf8' });
};

test('a move takes its statements with their comments, byte for byte, and each side imports what it then reads', async () => {
  const fx = scratch('basic');
  try {
    const before = await import(pathToFileURL(join(fx.root, 'a.mjs')).href) as { user: () => number };
    const result = mover.planMoves(fx.root, { from: 'a.mjs', moves: [{ to: 'a/b.mjs', names: ['WIDTH', 'double'], expect: 2, header: 'The doubling.' }] });
    mover.applyMoves(fx.root, result);
    assert.equal(fx.read('a/b.mjs'), [
      '/**',
      ' * The doubling.',
      ' */',
      "import { helper } from '../lib/helper.mjs';",
      '',
      '/** The width. */',
      'export const WIDTH = 3;',
      '',
      '/**',
      ' * Doubles.',
      ' */',
      'export function double(x) {',
      '  return helper(x) * WIDTH;',
      '}',
      '',
    ].join('\n'));
    assert.equal(fx.read('a.mjs'), [
      "import { join } from 'node:path';",
      "import { double } from './a/b.mjs';",
      "export { WIDTH, double } from './a/b.mjs';",
      '',
      '// Kept here.',
      'const local = () => 1;',
      '',
      "function inner() { return join('a', 'b'); }",
      '',
      'export const user = () => double(local()) + inner().length;',
      '',
    ].join('\n'));
    const after = await import(`${pathToFileURL(join(fx.root, 'a.mjs')).href}?after`) as { user: () => number; WIDTH: number };
    assert.equal(after.user(), before.user());
    assert.equal(after.WIDTH, 3);
  } finally {
    fx.cleanup();
  }
});

test("a plan whose count differs is refused naming what it found, and nothing is written; --plan writes nothing either", () => {
  const fx = scratch('counts');
  try {
    const wrong = refusal(() => mover.planMoves(fx.root, { from: 'a.mjs', moves: [{ to: 'a/b.mjs', span: ['WIDTH', 'double'], expect: 2 }] }));
    assert.ok(wrong instanceof mover.MoveRefused);
    assert.match(wrong.message, /takes 3 statement\(s\), not the 2 expected: WIDTH \(4-5\), local \(7-8\), double \(10-15\)/);
    const refused = run(fx, { from: 'a.mjs', moves: [{ to: 'a/b.mjs', names: ['WIDTH', 'double'], expect: 3 }] });
    assert.equal(refused.status, 1, refused.stderr);
    assert.match(refused.stderr, /not the 3 expected/);
    const planned = run(fx, { from: 'a.mjs', moves: [{ to: 'a/b.mjs', names: ['WIDTH', 'double'], expect: 2 }] }, '--plan');
    assert.equal(planned.status, 0, planned.stderr);
    assert.match(planned.stdout, /a\.mjs → a\/b\.mjs: 2 statement\(s\), lines 4-15/);
    assert.match(planned.stdout, /a\.mjs re-exports from \.\/a\/b\.mjs: WIDTH, double/);
    assert.match(planned.stdout, /drops the imports it no longer reads: helper \(\.\/lib\/helper\.mjs\)/);
    assert.match(planned.stdout, /planned, nothing written/);
    assert.equal(fx.read('a.mjs'), SOURCE);
    assert.equal(fx.exists('a/b.mjs'), false);
    const usage = run(fx, { from: 'a.mjs', moves: [{ to: 'a/b.mjs', names: ['WIDTH'] }] });
    assert.equal(usage.status, 2, usage.stderr);
  } finally {
    fx.cleanup();
  }
});

test('a statement that reads one left behind, reads import.meta, or is named twice or not at all is refused', () => {
  const fx = scratch('refusals');
  try {
    const plan = (moves: Move[]) => refusal(() => mover.planMoves(fx.root, { from: 'a.mjs', moves })).message;
    assert.match(plan([{ to: 'a/b.mjs', names: ['double'], expect: 1 }]), /'double' reads 'WIDTH' \(line 4\), which stays in a\.mjs/);
    assert.match(plan([{ to: 'a/b.mjs', names: ['nothing'], expect: 1 }]), /'nothing' names 0 statements/);
    assert.match(plan([{ to: 'a/b.mjs', names: ['WIDTH'], expect: 1 }, { to: 'a/c.mjs', names: ['WIDTH'], expect: 1 }]), /taken by two moves/);
    assert.match(plan([{ to: 'a.mjs', names: ['WIDTH'], expect: 1 }]), /is the module moved from/);
    fx.write('m.mjs', "export const here = import.meta.url;\nexport const there = () => here;\n");
    assert.match(refusal(() => mover.planMoves(fx.root, { from: 'm.mjs', moves: [{ to: 'n.mjs', names: ['here'], expect: 1 }] })).message, /reads import\.meta/);
  } finally {
    fx.cleanup();
  }
});

test('a name one target reads of another is imported from it and exported there; a move into a module that exists appends', async () => {
  const fx = scratch('targets');
  try {
    fx.write('a/b.mjs', "import { join } from 'node:path';\n\nexport const SEP = join('x');\n");
    mover.applyMoves(fx.root, mover.planMoves(fx.root, {
      from: 'a.mjs',
      moves: [{ to: 'a/w.mjs', names: ['WIDTH', 'local'], expect: 2 }, { to: 'a/b.mjs', names: ['double'], expect: 1 }],
    }));
    assert.equal(fx.read('a/w.mjs'), '/** The width. */\nexport const WIDTH = 3;\n\n// Kept here.\nexport const local = () => 1;\n');
    assert.equal(fx.read('a/b.mjs'), [
      "import { join } from 'node:path';",
      "import { helper } from '../lib/helper.mjs';",
      "import { WIDTH } from './w.mjs';",
      '',
      "export const SEP = join('x');",
      '',
      '/**',
      ' * Doubles.',
      ' */',
      'export function double(x) {',
      '  return helper(x) * WIDTH;',
      '}',
      '',
    ].join('\n'));
    assert.match(fx.read('a.mjs'), /import \{ double \} from '\.\/a\/b\.mjs';\nimport \{ local \} from '\.\/a\/w\.mjs';\nexport \{ WIDTH \} from '\.\/a\/w\.mjs';\nexport \{ double \} from '\.\/a\/b\.mjs';\n/);
    const after = await import(pathToFileURL(join(fx.root, 'a.mjs')).href) as { user: () => number };
    assert.equal(after.user(), 9);
    fx.write('a/x.mjs', 'export const local = 2;\n');
    fx.write('c.mjs', 'export const local = 1;\n');
    assert.match(refusal(() => mover.planMoves(fx.root, { from: 'c.mjs', moves: [{ to: 'a/x.mjs', names: ['local'], expect: 1 }] })).message, /already binds 'local'/);
  } finally {
    fx.cleanup();
  }
});

test("a test is named by its title, and a type it reads is imported as a type, so the moved file runs with types stripped", () => {
  const fx = makeFixture('move-code-typed');
  try {
    fx.write('t.test.ts', [
      "import { test } from 'node:test';",
      '',
      '// The file is held to a census.',
      '',
      'interface Shape { n: number }',
      'const make = (): Shape => ({ n: 1 });',
      '',
      "test('makes one', () => {",
      '  const shape: Shape = make();',
      "  if (shape.n !== 1) throw new Error('not one');",
      '});',
      '',
      "test('stays', () => {});",
      '',
    ].join('\n'));
    mover.applyMoves(fx.root, mover.planMoves(fx.root, {
      from: 't.test.ts',
      moves: [{ to: 'support.ts', span: ['Shape', 'make'], expect: 2 }, { to: 'moved/one.test.ts', names: ['makes one'], expect: 1 }],
    }));
    assert.equal(fx.read('support.ts'), 'export interface Shape { n: number }\nexport const make = (): Shape => ({ n: 1 });\n');
    assert.equal(fx.read('moved/one.test.ts'), [
      "import { test } from 'node:test';",
      "import { type Shape, make } from '../support.ts';",
      '',
      "test('makes one', () => {",
      '  const shape: Shape = make();',
      "  if (shape.n !== 1) throw new Error('not one');",
      '});',
      '',
    ].join('\n'));
    assert.equal(fx.read('t.test.ts'), "import { test } from 'node:test';\n\n// The file is held to a census.\n\ntest('stays', () => {});\n");
    const ran = spawnSync(process.execPath, ['--test', join(fx.root, 'moved', 'one.test.ts')], { encoding: 'utf8' });
    assert.equal(ran.status, 0, ran.stdout + ran.stderr);
  } finally {
    fx.cleanup();
  }
});
