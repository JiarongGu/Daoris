import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { COMMANDS, runCli } from '../../src/cli.ts';
import type { CliCommand } from '../../src/platform/types.ts';
import { makeFixture } from '../_fixture.ts';

/** A stand-in for the desktop's program: the dispatcher's tests never start the real one. */
const nowhere = () => 2 as const;

test('--help prints usage and exits 0', () => {
  const out: string[] = [];
  const code = runCli(['--help'], process.cwd(), (s) => out.push(s), nowhere, null);
  assert.equal(code, 0);
  assert.match(out.join('\n'), /daoris <command>/);
  for (const cmd of ['init', 'sync', 'check', 'upstream', 'index', 'status']) {
    assert.match(out.join('\n'), new RegExp(`\\b${cmd}\\b`));
  }
});

test('--version prints the package version', () => {
  const out: string[] = [];
  const code = runCli(['--version'], process.cwd(), (s) => out.push(s), nowhere, null);
  assert.equal(code, 0);
  assert.match(out.join('\n'), /^\d+\.\d+\.\d+$/m);
});

/**
 * A word that is no doctrine command is the desktop's program's, which says the command is unknown; with no program
 * found, the CLI says where it is, a tool error (exit 2), and does nothing.
 */
test('a word outside the doctrine with no desktop program to hand it to is a tool error (exit 2) saying where the program is', () => {
  const previous = process.env.DAORIS_DRIVER_PROGRAM;
  delete process.env.DAORIS_DRIVER_PROGRAM;
  try {
    const out: string[] = [];
    const code = runCli(['frobnicate'], process.cwd(), (s) => out.push(s), undefined, null);
    assert.equal(code, 2);
    assert.match(out.join('\n'), /^daoris: every command but the doctrine's is run by Daoris's desktop program/);
  } finally {
    if (previous !== undefined) process.env.DAORIS_DRIVER_PROGRAM = previous;
  }
});

/**
 * The table answers for its own verbs only. Looked up as a plain object, `toString` and `constructor`
 * resolves to Object's own members, which run as handlers and whose return reaches `process.exit`,
 * which throws: a stack trace and exit 1, the policy code. Such a word is no doctrine command,
 * handed to the desktop's program whole and never run here.
 */
test('a verb named like a member of Object is no handler: it is handed on whole, never a crash', async () => {
  for (const verb of ['toString', 'constructor', '__proto__', 'hasOwnProperty', 'valueOf']) {
    const handed: (readonly string[])[] = [];
    const out: string[] = [];
    const code = await runCli([verb], process.cwd(), (s) => out.push(s), (words) => {
      handed.push(words);
      return 2;
    }, null);
    assert.equal(code, 2, `daoris ${verb}`);
    assert.deepEqual(handed, [[verb]], `daoris ${verb}`);
    assert.deepEqual(out, [], `daoris ${verb}`);
  }
});

/**
 * Exit codes are the contract, and 1 is POLICY — the code a build gate reads as "the doctrine is
 * wrong". A failure nobody anticipated (a file system that refused, a shape nobody expected) is a tool
 * error, 2, said in one line, never a stack trace and Node's own exit 1.
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
  assert.equal(runCli([], process.cwd(), (s) => out.push(s), nowhere, null), 2);
});

/**
 * The commands are a table: each row is a module under `cli/` carrying its verb, its usage lines
 * and its handler, and `--help` is assembled from the rows in the table's order. A row that registered
 * no usage, or usage the help never prints, would be a verb nobody can discover.
 */
test('every registered command has its usage lines, and --help prints them in the table order', () => {
  const out: string[] = [];
  runCli(['--help'], process.cwd(), (s) => out.push(s), nowhere, null);
  const help = out.join('\n');

  const names = COMMANDS.map((command) => command.name);
  assert.ok(names.length >= 6, `the table holds ${names.length} commands, so this proves little`);
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
  const folder = join(dirname(dirname(dirname(fileURLToPath(import.meta.url)))), 'src', 'cli');
  const files = readdirSync(folder).filter((file) => file.endsWith('.ts')).sort();

  for (const file of files) {
    const { command } = await import(pathToFileURL(join(folder, file)).href) as { command?: CliCommand };
    assert.equal(command?.name, file.replace(/\.ts$/, ''), `cli/${file} does not export the command it is named for`);
    assert.ok(COMMANDS.includes(command!), `cli/${file} is not registered in the table in cli.ts`);
    // Why is on the table in cli.ts; dogfood.test.ts walks what each doctrine row reaches.
    assert.doesNotMatch(readFileSync(join(folder, file), 'utf8'), /['"]\.\.\/cli\.ts['"]/,
      `cli/${file} imports the dispatcher`);
  }
  assert.equal(files.length, COMMANDS.length, 'a registered command has no module of its own under cli/');
});
