import { test } from 'node:test';
import assert from 'node:assert/strict';
import { COMMANDS, HANDED_VARIABLE, runCli } from '../../src/cli.ts';

/**
 * One name, `daoris`, one tree (D228). The CLI's rows declare the doctrine commands, offline, at the root; every other word is the
 * desktop's program's, handed to it whole, and a word handed once is never handed back. The program's groups are declared by its own
 * commands, so neither half holds the other's list.
 */

test('every doctrine command the CLI declares is its own, run here and never handed', async () => {
  assert.ok(COMMANDS.length >= 9, 'the rows emptied — this test is worthless if they are');
  assert.deepEqual(COMMANDS.filter((command) => command.kind !== 'doctrine').map((command) => command.name), []);
  for (const command of COMMANDS) {
    const handed: string[][] = [];
    const out: string[] = [];
    const code = await runCli([command.name, '--help'], process.cwd(), (line) => out.push(line), (words) => {
      handed.push([...words]);
      return 2;
    }, null);
    assert.equal(code, 0, `daoris ${command.name} --help`);
    assert.deepEqual(handed, [], `daoris ${command.name} was handed to the desktop's program`);
    assert.match(out.join('\n'), new RegExp(`^ {2}${command.name}\\b`, 'm'));
  }
});

test('every other word goes to the desktop\'s program whole, with nothing written first', async () => {
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

test('a word the desktop\'s program handed here is never handed back', async () => {
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

test('the CLI\'s own usage names the doctrine commands and says the rest is the desktop\'s program\'s', () => {
  const out: string[] = [];
  assert.equal(runCli(['--help'], process.cwd(), (line) => out.push(line), () => 2, null), 0);
  const help = out.join('\n');
  for (const command of COMMANDS) assert.match(help, new RegExp(`^ {2}${command.name}\\b`, 'm'));
  assert.match(help, /the desktop's program/);
  // The program's folder keeps its name (`app/daoris-driver/`, the design §11); no command is spelled with it.
  assert.doesNotMatch(help, /daoris-driver(?!\/)|^ {2}(?:driver|agent|plugin|tool|remote|browser|connect) /m);
});
