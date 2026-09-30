import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { COMMANDS, runCli } from '../src/cli.ts';
import type { CliCommand } from '../src/types.ts';
import { makeFixture } from './_fixture.ts';

test('--help prints usage and exits 0', () => {
  const out: string[] = [];
  const code = runCli(['--help'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 0);
  assert.match(out.join('\n'), /daoris <command>/);
  for (const cmd of ['init', 'sync', 'check', 'upstream', 'index', 'status']) {
    assert.match(out.join('\n'), new RegExp(`\\b${cmd}\\b`));
  }
});

test('--version prints the package version', () => {
  const out: string[] = [];
  const code = runCli(['--version'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 0);
  assert.match(out.join('\n'), /^\d+\.\d+\.\d+$/m);
});

test('an unknown command is a tool error (exit 2)', () => {
  const out: string[] = [];
  const code = runCli(['frobnicate'], process.cwd(), (s) => out.push(s));
  assert.equal(code, 2);
  assert.match(out.join('\n'), /unknown command/i);
});

/**
 * AGT1: the tools a session runs are AGENTS to a person — the owner, reading `daoris harness`:
 * *"I have no idea what harness is"*. The old verb is not a second name for the new one; it says
 * where the command went, once, and fails like any unknown command.
 */
test('the agent tools are `daoris agent`, and the old verb says where it went', () => {
  const out: string[] = [];
  assert.equal(runCli(['harness', 'list'], process.cwd(), (s) => out.push(s)), 2);
  assert.match(out.join('\n'), /`daoris agent`/);

  const help: string[] = [];
  runCli(['--help'], process.cwd(), (s) => help.push(s));
  assert.match(help.join('\n'), /^ {2}agent \[verb\]/m);
  assert.doesNotMatch(help.join('\n'), /^ {2}harness /m);
});

/**
 * Exit codes are the contract, and 1 is POLICY — the code a build gate reads as "the doctrine is
 * wrong". A failure nobody anticipated (a file system that refused, a shape nobody expected) is a tool
 * error, 2, said in one line. It escaped as a stack trace and Node's own exit 1 (REV3).
 */
test('a failure nobody anticipated is a tool error (exit 2) in one line, never a stack trace', async () => {
  const fx = makeFixture('cli-unanticipated');
  mkdirSync(join(fx.root, 'daoris.json'));
  const out: string[] = [];
  const code = await runCli(['check'], fx.root, (s) => out.push(s));
  assert.equal(code, 2);
  assert.match(out.join('\n'), /^daoris: /);
  fx.cleanup();
});

test('no arguments prints usage and exits 2', () => {
  const out: string[] = [];
  assert.equal(runCli([], process.cwd(), (s) => out.push(s)), 2);
});

/**
 * The commands are a table (MOD7): each row is a module under `cli/` carrying its verb, its usage lines
 * and its handler, and `--help` is assembled from the rows in the table's order. A row that registered
 * no usage, or usage the help never prints, would be a verb nobody can discover.
 */
test('every registered command has its usage lines, and --help prints them in the table order', () => {
  const out: string[] = [];
  runCli(['--help'], process.cwd(), (s) => out.push(s));
  const help = out.join('\n');

  const names = COMMANDS.map((command) => command.name);
  assert.ok(names.length >= 10, `the table holds ${names.length} commands, so this proves little`);
  assert.equal(new Set(names).size, names.length, `a verb is registered twice: ${names.join(', ')}`);

  let after = 0;
  for (const command of COMMANDS) {
    assert.ok(command.usage.length > 0, `\`${command.name}\` registers no usage lines`);
    assert.match(command.usage[0]!, new RegExp(`^ {2}${command.name}(?: |$)`),
      `\`${command.name}\`'s first usage line does not name it`);
    for (const line of command.usage.slice(1)) {
      // A continuation sits under the description column, so it can never read as another verb.
      assert.match(line, /^ {23}/, `\`${command.name}\` has a usage line that could read as a verb: ${line}`);
    }

    const block = `\n${command.usage.join('\n')}\n`;
    const at = help.indexOf(block, after);
    assert.ok(at >= 0, `\`${command.name}\`'s usage lines are not in --help in the table's order`);
    after = at + block.length - 1;

    for (const line of command.options ?? []) {
      assert.ok(help.includes(`\n${line}\n`), `\`${command.name}\`'s option line is not in --help: ${line}`);
    }
    for (const former of command.formerly ?? []) {
      assert.equal(names.includes(former), false, `\`${former}\` is both a verb and the old name of \`${command.name}\``);
    }
  }
});

test('every module under cli/ is one registered row, named as its file, and none reaches the table', async () => {
  const folder = join(dirname(dirname(fileURLToPath(import.meta.url))), 'src', 'cli');
  const files = readdirSync(folder).filter((file) => file.endsWith('.ts')).sort();

  for (const file of files) {
    const { command } = await import(pathToFileURL(join(folder, file)).href) as { command?: CliCommand };
    assert.equal(command?.name, file.replace(/\.ts$/, ''), `cli/${file} does not export the command it is named for`);
    assert.ok(COMMANDS.includes(command!), `cli/${file} is not registered in the table in cli.ts`);
    // A row that imported the dispatcher would reach every other row through it — for a doctrine
    // command, the management class's network and spawning (dogfood.test.ts holds the walk).
    assert.doesNotMatch(readFileSync(join(folder, file), 'utf8'), /['"]\.\.\/cli\.ts['"]/,
      `cli/${file} imports the dispatcher`);
  }
  assert.equal(files.length, COMMANDS.length, 'a registered command has no module of its own under cli/');
});
