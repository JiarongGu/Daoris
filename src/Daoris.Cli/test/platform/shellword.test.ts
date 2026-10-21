import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
// @ts-expect-error — untyped workspace tooling; see desktop-tool.test.ts for why
import { CLI_BIN, CLI_ENTRY, CLI_PACKAGE, cliLaunchers } from '../../../../tools/desktop-publish.mjs';
import { joinRefusal } from '../../src/accounts/rotation.ts';
import { shellWord, shellWords } from '../../src/platform/shellword.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * An argument as a terminal hint prints it (ACCTQUOTE1, D125's ACCTQUOTE1 note): bare where every shell reads it as itself,
 * in double quotes where they keep it whole in PowerShell, Command Prompt and a POSIX shell alike, and a placeholder where
 * no spelling does. The CLI's half of a TWIN with the page's `shellWord.ts`: both read `fixtures/shell-words.json`, row for
 * row, so the two doors spell an argument the same way.
 *
 * The table is a claim about shells, so it is held against them: every spelled row is handed through the install's own
 * launchers (D124 §1.2) by the shells this platform has, and must arrive as one argument, itself.
 */

const here = dirname(dirname(fileURLToPath(import.meta.url)));

type Row = [why: string, value: string, word: string];
const ROWS = JSON.parse(readFileSync(join(here, 'fixtures', 'shell-words.json'), 'utf8')) as Row[];

/** The rows a hint prints the value of, and the rows it names a placeholder for. */
const SPELLED = ROWS.filter(([, , word]) => word !== '<name>');
const UNSPELLED = ROWS.filter(([, , word]) => word === '<name>');

test('each value is spelled as the table spells it: bare, in double quotes, or the placeholder', () => {
  for (const [why, value, word] of ROWS) assert.equal(shellWord(value, '<name>'), word, why);
  assert.ok(SPELLED.length > 0 && UNSPELLED.length > 0, 'the table holds both kinds');
});

test('several values are spelled one by one, a space between', () => {
  assert.equal(shellWords(['work', 'my team', 'R&D'], '<account>'), 'work "my team" <account>');
  assert.equal(shellWords([], '<account>'), '');
});

/**
 * The install's launchers (`daoris` and `daoris.cmd`, as the publish lays them) in front of a package whose entry prints
 * the arguments it was handed as JSON, each character past ASCII escaped, so a shell's code page cannot change it on the way
 * back. The folder is put first on PATH, as the install's own is found.
 */
function install(root: string): NodeJS.ProcessEnv {
  const bin = join(root, ...CLI_BIN);
  const entry = join(root, ...CLI_PACKAGE, ...CLI_ENTRY);
  mkdirSync(bin, { recursive: true });
  mkdirSync(dirname(entry), { recursive: true });
  for (const [name, text] of Object.entries(cliLaunchers() as Record<string, string>)) {
    writeFileSync(join(bin, name), text, { mode: 0o755 });
  }
  writeFileSync(entry, [
    'const said = JSON.stringify(process.argv.slice(2));',
    "process.stdout.write(said.replace(/[\\u007f-\\uffff]/g, (c) => '\\\\u' + c.charCodeAt(0).toString(16).padStart(4, '0')) + '\\n');",
  ].join('\n'), 'utf8');
  const env: NodeJS.ProcessEnv = { ...process.env };
  const key = Object.keys(env).find((name) => name.toUpperCase() === 'PATH') ?? 'PATH';
  env[key] = `${bin}${delimiter}${env[key] ?? ''}`;
  return env;
}

/** A command line run by each shell this platform has, as a person types it: what it printed, and how it ended. */
function shells(line: string, cwd: string, env: NodeJS.ProcessEnv): { shell: string; out: string; code: number | null }[] {
  const ran = (shell: string, result: ReturnType<typeof spawnSync>) =>
    ({ shell, out: `${result.stdout ?? ''}${result.stderr ?? ''}${result.error?.message ?? ''}`, code: result.status });
  if (process.platform !== 'win32') return [ran('sh', spawnSync('sh', ['-c', line], { cwd, env, encoding: 'utf8' }))];
  return [
    // Command Prompt finds `daoris.cmd` by PATHEXT; PowerShell 5.1 finds it too, and hands it its arguments itself.
    ran('cmd', spawnSync(env.ComSpec ?? 'cmd.exe', [`/d /s /c "${line}"`], {
      cwd, env, encoding: 'utf8', windowsVerbatimArguments: true,
    })),
    ran('powershell', spawnSync('powershell.exe', [
      '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', Buffer.from(line, 'utf16le').toString('base64'),
    ], { cwd, env, encoding: 'utf8' })),
  ];
}

test('every spelled value reaches the installed daoris as one argument, itself, through each shell here', () => {
  const fx = makeFixture('shell-words-spelled');
  const env = install(fx.root);

  const line = `daoris ${SPELLED.map(([, , word]) => word).join(' ')}`;
  for (const { shell, out, code } of shells(line, fx.root, env)) {
    assert.equal(code, 0, `${shell}: ${out}`);
    assert.deepEqual(JSON.parse(out.trim().split(/\r?\n/).at(-1)!), SPELLED.map(([, value]) => value), `${shell}: ${out}`);
  }
  fx.cleanup();
});

test('a line ending in a placeholder, pasted as it is, reaches nothing: each shell here refuses it', () => {
  const fx = makeFixture('shell-words-placeholder');
  const env = install(fx.root);

  for (const { shell, out, code } of shells('daoris agent profile rename claude-code acct-1a2b3c4d <name>', fx.root, env)) {
    assert.notEqual(code, 0, `${shell}: ${out}`);
    assert.doesNotMatch(out, /"agent"/, `${shell} handed the line on: ${out}`);
  }
  fx.cleanup();
});

// ——— The hints: every command the CLI prints from an account's name or id, or a workspace's name, spells it.

// The account verbs' own refusals are the driver's program's since PUSH1f2e, held by `AgentsCommandTests`' twin of this test;
// `agent list`'s *Try now* and join hints since PUSH1f2e4, held by `AgentsTextTests`' rows.
test('a refusal\'s command names a workspace with a space in double quotes, and an account it cannot spell by a placeholder', () => {
  assert.match(joinRefusal('claude-code', 'my team'),
    /`daoris agent profile order claude-code <account>… --workspace "my team"`/);
});
