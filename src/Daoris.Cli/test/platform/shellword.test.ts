import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execSync, spawnSync } from 'node:child_process';
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
// @ts-expect-error — untyped workspace tooling; see desktop-tool.test.ts for why
import { CLI_BIN, DRIVER_EXE, DRIVER_HOME, commandPublishCommand, layCommand } from '../../../../tools/desktop-publish.mjs';
import { makeFixture } from '../_fixture.ts';

/**
 * An argument as a terminal hint prints it: bare where every shell reads it as itself,
 * in double quotes where they keep it whole in PowerShell, Command Prompt and a POSIX shell alike, and a placeholder where
 * no spelling does. The driver's `ShellWord` and the page's `shellWord.ts` read one table, the driver's
 * `fixtures/shell-words.json`, row for row.
 *
 * The table is a claim about shells, so it is held against them here, where the install's command is: every spelled row is handed through the install's own
 * `daoris.exe`, published as the install lays it, by the shells here (Windows has the install), and must arrive as one argument, itself.
 */

const repository = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..');

type Row = [why: string, value: string, word: string];
const ROWS = JSON.parse(readFileSync(
  join(repository, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'shell-words.json'), 'utf8')) as Row[];

/** The rows a hint prints the value of, and the rows it names a placeholder for. */
const SPELLED = ROWS.filter(([, , word]) => word !== '<name>');
const UNSPELLED = ROWS.filter(([, , word]) => word === '<name>');

/** Words of several lines arrive whole: PowerShell is the one shell here whose line carries a break. */
test('words of several lines typed in PowerShell reach the installed daoris whole, line breaks and all', { skip: process.platform !== 'win32' }, () => {
  const fx = makeFixture('shell-words-lines');
  const env = install(fx.root);

  const line = 'daoris first session say s-1 "first line`nsecond line`r`nthird line" --level urgent';
  const ran = spawnSync('powershell.exe', [
    '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', Buffer.from(line, 'utf16le').toString('base64'),
  ], { cwd: fx.root, env, encoding: 'utf8', windowsHide: true });

  const out = `${ran.stdout}${ran.stderr}${ran.error?.message ?? ''}`;
  assert.equal(ran.status, 0, out);
  const said = JSON.parse(out.trim().split(/\r?\n/).at(-1)!);
  assert.deepEqual(said, ['session', 'say', 's-1', 'first line\nsecond line\r\nthird line', '--level', 'urgent'], out);
  fx.cleanup();
});

test('the table holds a spelled row and a placeholder row', () => {
  assert.ok(SPELLED.length > 0 && UNSPELLED.length > 0, 'the table holds both kinds');
});

/**
 * The install's command (`daoris.exe`, as the publish lays it) in front of a desktop program that prints the
 * arguments it receives as JSON, each character past ASCII escaped, so a shell's code page cannot change it on the way back. The
 * program is this test's node under the program's name, which a preload makes print and end before it reads its first word as a
 * script (the command runs the program, not node). The folder is put first on PATH, as the install's own is found.
 */
function install(root: string): NodeJS.ProcessEnv {
  const bin = join(root, ...CLI_BIN);
  const program = join(root, ...DRIVER_HOME, DRIVER_EXE);
  mkdirSync(bin, { recursive: true });
  mkdirSync(dirname(program), { recursive: true });
  // Built into the fixture's own folder, never the project's beside another run's.
  const published = join(root, 'command-publish');
  execSync(commandPublishCommand(published, join(root, 'artifacts')), { cwd: repository, stdio: 'pipe', maxBuffer: 64 * 1024 * 1024, windowsHide: true });
  layCommand(published, root);
  copyFileSync(process.execPath, program);
  const echo = join(root, 'echo.cjs');
  writeFileSync(echo, [
    // The first word is the one node took for its script, resolved: the line hands a placeholder there.
    'const said = JSON.stringify(process.argv.slice(2));',
    "process.stdout.write(said.replace(/[\\u007f-\\uffff]/g, (c) => '\\\\u' + c.charCodeAt(0).toString(16).padStart(4, '0')) + '\\n');",
    'process.exit(0);',
  ].join('\n'), 'utf8');
  const env: NodeJS.ProcessEnv = { ...process.env, NODE_OPTIONS: `--require ${JSON.stringify(echo)}` };
  const key = Object.keys(env).find((name) => name.toUpperCase() === 'PATH') ?? 'PATH';
  env[key] = `${bin}${delimiter}${env[key] ?? ''}`;
  return env;
}

/** A command line run by each shell this platform has, as a person types it: what it printed, and how it ended. */
function shells(line: string, cwd: string, env: NodeJS.ProcessEnv): { shell: string; out: string; code: number | null }[] {
  const ran = (shell: string, result: ReturnType<typeof spawnSync>) =>
    ({ shell, out: `${result.stdout ?? ''}${result.stderr ?? ''}${result.error?.message ?? ''}`, code: result.status });
  return [
    // Command Prompt finds `daoris.exe` by PATHEXT; PowerShell 5.1 finds it too, and hands it its arguments itself.
    ran('cmd', spawnSync(env.ComSpec ?? 'cmd.exe', [`/d /s /c "${line}"`], {
      cwd, env, encoding: 'utf8', windowsVerbatimArguments: true, windowsHide: true,
    })),
    ran('powershell', spawnSync('powershell.exe', [
      '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', Buffer.from(line, 'utf16le').toString('base64'),
    ], { cwd, env, encoding: 'utf8', windowsHide: true })),
  ];
}

test('every spelled value reaches the installed daoris as one argument, itself, through each shell here', { skip: process.platform !== 'win32' }, () => {
  const fx = makeFixture('shell-words-spelled');
  const env = install(fx.root);

  const line = `daoris first ${SPELLED.map(([, , word]) => word).join(' ')}`;
  for (const { shell, out, code } of shells(line, fx.root, env)) {
    assert.equal(code, 0, `${shell}: ${out}`);
    assert.deepEqual(JSON.parse(out.trim().split(/\r?\n/).at(-1)!), SPELLED.map(([, value]) => value), `${shell}: ${out}`);
  }
  fx.cleanup();
});

test('a line ending in a placeholder, pasted as it is, reaches nothing: each shell here refuses it', { skip: process.platform !== 'win32' }, () => {
  const fx = makeFixture('shell-words-placeholder');
  const env = install(fx.root);

  for (const { shell, out, code } of shells('daoris agent account rename claude-code acct-1a2b3c4d <name>', fx.root, env)) {
    assert.notEqual(code, 0, `${shell}: ${out}`);
    assert.doesNotMatch(out, /"agent"/, `${shell} handed the line on: ${out}`);
  }
  fx.cleanup();
});
