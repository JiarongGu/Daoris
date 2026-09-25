import { test } from 'node:test';
import assert from 'node:assert/strict';
import { flagValue, operands } from '../src/args.ts';
import { DaorisError } from '../src/errors.ts';

/**
 * The CLI's one argument parser (REV3 CLI C2). Five commands each scanned `argv` their own way, and
 * two of them read a flag's value as an operand whenever the flag came first.
 */

test('operands skip a valued flag and its value, wherever the flag sits', () => {
  const valued = new Set(['--workspace']);
  assert.deepEqual(operands(['pin', '--workspace', 'aurora', 'claude-code', '2.1.87'], valued),
    ['pin', 'claude-code', '2.1.87']);
  assert.deepEqual(operands(['pin', 'claude-code', '2.1.87', '--workspace', 'aurora'], valued),
    ['pin', 'claude-code', '2.1.87']);
});

test('a flag the command does not declare as valued takes nothing with it', () => {
  assert.deepEqual(operands(['trust', 'claude-code', '--yes', 'here'], new Set()), ['trust', 'claude-code', 'here']);
});

test('flagValue refuses a flag with nothing after it, or another flag', () => {
  assert.equal(flagValue(['--workspace', 'aurora'], '--workspace'), 'aurora');
  assert.equal(flagValue(['list'], '--workspace'), undefined);
  assert.throws(() => flagValue(['--workspace'], '--workspace'), DaorisError);
  assert.throws(() => flagValue(['--workspace', '--yes'], '--workspace'), DaorisError);
});
