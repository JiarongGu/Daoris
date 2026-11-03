import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { COMMANDS, HANDED_VARIABLE, runCli } from '../../src/cli.ts';

/**
 * ONEWAY1ai-i (D228; the oneway design §11): one name, `daoris`, one tree. The CLI owns the doctrine commands, offline, at the root;
 * every other word is the desktop's program's, handed to it whole, and a word handed once is never handed back. The tree is declared
 * once, in the driver tests' `fixtures/command-tree.json`, which the driver's table is held to as well (`CommandWordsTests`).
 */
const repository = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..');
const TREE = JSON.parse(readFileSync(
  join(repository, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'command-tree.json'), 'utf8')) as {
  doctrine: string[]; groups: { name: string }[];
};

test('ONEWAY1ai-i the CLI\'s commands are the tree\'s doctrine commands, and every one is doctrine', () => {
  const names = COMMANDS.map((command) => command.name);
  assert.deepEqual([...names].sort(), [...TREE.doctrine].sort());
  assert.deepEqual(COMMANDS.filter((command) => command.kind !== 'doctrine').map((command) => command.name), []);
  for (const group of TREE.groups) assert.ok(!names.includes(group.name), `\`${group.name}\` is a group of the desktop's program, not the CLI's`);
});

test('ONEWAY1ai-i every other word goes to the desktop\'s program whole, with nothing written first', async () => {
  for (const argv of [
    ['session', 'say', 's-1', 'a "quoted" & 100% 中文 word'],
    ['place', 'drive', 'engine'],
    ['remote', 'add', 'work', '--url', 'https://team.example'],
    ['repository', 'connect', '--workspace', 'work'],
    ['help'],
    ['session', '--help'],
    ['driver', 'drive', 'engine'],
    ['frobnicate'],
    ['toString'],
  ]) {
    for (const exit of [0, 1, 2] as const) {
      const handed: string[][] = [];
      const out: string[] = [];
      const code = await runCli(argv, process.cwd(), (line) => out.push(line), (words) => {
        handed.push([...words, `written:${out.length}`]);
        return exit;
      }, null);
      assert.equal(code, exit, `daoris ${argv.join(' ')}`);
      assert.deepEqual(handed, [[...argv, 'written:0']], `daoris ${argv.join(' ')}`);
    }
  }
});

test('ONEWAY1ai-i a word the desktop\'s program handed here is never handed back', async () => {
  const handed: string[][] = [];
  const out: string[] = [];
  const code = await runCli(['session', 'list'], process.cwd(), (line) => out.push(line), (words) => {
    handed.push([...words]);
    return 0;
  }, 'driver');
  assert.equal(code, 2);
  assert.deepEqual(handed, []);
  assert.match(out.join('\n'), /^daoris: `session` is no doctrine command/);
  assert.equal(HANDED_VARIABLE, 'DAORIS_HANDED');
});

test('ONEWAY1ai-i the CLI\'s own usage names the doctrine commands and says the rest is the desktop\'s program\'s', () => {
  const out: string[] = [];
  assert.equal(runCli(['--help'], process.cwd(), (line) => out.push(line), () => 2, null), 0);
  const help = out.join('\n');
  for (const command of COMMANDS) assert.match(help, new RegExp(`^ {2}${command.name}\\b`, 'm'));
  assert.match(help, /the desktop's program/);
  // The program's folder keeps its name (`app/daoris-driver/`, the design §11); no command is spelled with it.
  assert.doesNotMatch(help, /daoris-driver(?!\/)|^ {2}(?:driver|agent|plugin|tool|remote|browser|connect) /m);
});
