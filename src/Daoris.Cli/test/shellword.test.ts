import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
// @ts-expect-error — untyped workspace tooling; see desktop-tool.test.ts for why
import { CLI_BIN, CLI_ENTRY, CLI_PACKAGE, cliLaunchers } from '../../../tools/desktop-publish.mjs';
import { coolingLine } from '../src/cooling.ts';
import { joinRefusal } from '../src/rotation.ts';
import { shellWord, shellWords } from '../src/shellword.ts';
import { TOOLCHAINS, accountLines, commandHarness, profileHome, readHarnessSettings } from '../src/toolchain.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * An argument as a terminal hint prints it (ACCTQUOTE1, D125's ACCTQUOTE1 note): bare where every shell reads it as itself,
 * in double quotes where they keep it whole in PowerShell, Command Prompt and a POSIX shell alike, and a placeholder where
 * no spelling does. The CLI's half of a TWIN with the page's `shellWord.ts`: both read `fixtures/shell-words.json`, row for
 * row, so the two doors spell an argument the same way.
 *
 * The table is a claim about shells, so it is held against them: every spelled row is handed through the install's own
 * launchers (D124 §1.2) by the shells this platform has, and must arrive as one argument, itself.
 */

const here = dirname(fileURLToPath(import.meta.url));

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

function at(fx: { root: string }): string {
  return join(fx.root, 'harnesses.json');
}

function run(argv: string[], path: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HARNESS_CONFIG;
  process.env.DAORIS_HARNESS_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = commandHarness({ root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd() }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HARNESS_CONFIG;
    else process.env.DAORIS_HARNESS_CONFIG = saved;
  }
}

function accounts(fx: { root: string }, ...names: string[]): void {
  for (const name of names) mkdirSync(profileHome(fx.root, 'claude-code', name), { recursive: true });
}

test('a refusal\'s command names a workspace with a space in double quotes, and an account it cannot spell by a placeholder', () => {
  const fx = makeFixture('shell-words-refusals');
  accounts(fx, 'account-1', 'account-2', 'R&D');
  run(['profile', 'order', 'claude-code', 'account-1', '--workspace', 'my team'], at(fx));

  assert.match(captureError(() => run(['profile', 'default', 'claude-code', 'account-2', '--workspace', 'my team'], at(fx))).message,
    /`daoris agent profile order claude-code account-1 account-2 --workspace "my team"` adds it/);
  assert.match(captureError(() => run(['profile', 'order', 'claude-code', 'R&D', 'account-1', '--clear'], at(fx))).message,
    /`daoris agent profile order claude-code <account> account-1` sets it/);
  assert.match(captureError(() => run(['profile', 'ready', 'claude-code', 'R&D', '--own'], at(fx))).message,
    /`daoris agent profile ready claude-code <account>` is that account/);
  assert.match(captureError(() => run(['profile', 'rename', 'claude-code', 'R&D'], at(fx))).message,
    /`daoris agent profile rename claude-code <account> work`; naming it `R&D` gives it none/);
  assert.match(captureError(() => run(['profile', 'join', 'claude-code', 'R&D'], at(fx))).message,
    /`daoris agent profile join claude-code <account> work`/);
  assert.match(joinRefusal('claude-code', 'my team'),
    /`daoris agent profile order claude-code <account>… --workspace "my team"`/);
  fx.cleanup();
});

test('a cool-off\'s Try now names an account it cannot spell by a placeholder', () => {
  const until = new Date('2026-10-03T16:02:00Z');
  const line = coolingLine({ agent: 'claude-code', account: 'R&D', until, stated: true, window: null, seen: until, session: null,
    assumedZone: false, notBelieved: false }, new Date('2026-10-03T12:00:00Z'), 'UTC');

  assert.match(line, /`daoris agent profile ready claude-code <account>` tries it now/);
});

test('`agent list` names an account in no list by its name where a shell can take it, and by its id where none can', () => {
  const fx = makeFixture('shell-words-list');
  accounts(fx, 'account-1', 'account-2');
  run(['profile', 'rename', 'claude-code', 'account-1', 'R&D'], at(fx));
  run(['profile', 'rename', 'claude-code', 'account-2', "O'Brien"], at(fx));

  const lines = accountLines('claude-code', TOOLCHAINS['claude-code']!, {
    harness: 'claude-code', present: true, version: '1', problem: null, machineDefault: null,
    profiles: [
      { name: 'account-1', home: '', login: 'in', account: null, key: null },
      { name: 'account-2', home: '', login: 'in', account: null, key: null },
    ],
  }, readHarnessSettings(at(fx)), fx.root, new Date(), 'UTC').join('\n');

  assert.match(lines, /`daoris agent profile join claude-code account-1 <workspace>…\|--machine` puts it in one/);
  assert.match(lines, /`daoris agent profile join claude-code "O'Brien" <workspace>…\|--machine` puts it in one/);
  fx.cleanup();
});
