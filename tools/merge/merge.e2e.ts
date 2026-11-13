import { test } from 'vitest';
import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fixtureFolder } from './fixture-home.ts';
import { MERGE_RECORD, readRecords } from './merge-record.ts';

const TOOL = fileURLToPath(new URL('../merge-branch.mjs', import.meta.url));
const TRAILER = '\n\nCo-Authored-By: Claude Fixture (Anthropic) <fixture@example.test>';

/** The tool's environment: a fixed git identity, none of git's locating variables and none of the test runner's own. */
function toolEnvironment(): Record<string, string> {
  const own = /^(GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES)|NODE_TEST_CONTEXT|NODE_ENV|VITEST\w*)$/i;
  const kept = Object.entries(process.env).filter((entry): entry is [string, string] => entry[1] !== undefined && !own.test(entry[0]));
  return {
    ...Object.fromEntries(kept),
    GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
    GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
    GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.autocrlf', GIT_CONFIG_VALUE_0: 'false',
    MERGE_BRANCH_MEMORY: 'off', MERGE_BRANCH_CI_NIGHT_HOUR: 'off',
  };
}

function seeded(dir: string, env: Record<string, string>) {
  const root = join(dir, 'repo');
  const git = (...args: string[]) => execFileSync('git', args, { cwd: root, env, encoding: 'utf8', windowsHide: true }).trim();
  const write = (files: Record<string, string>) => {
    for (const [path, text] of Object.entries(files)) {
      mkdirSync(dirname(join(root, path)), { recursive: true });
      writeFileSync(join(root, path), text);
    }
  };
  mkdirSync(root, { recursive: true });
  git('init', '--quiet', '--template=', '-b', 'main');
  assert.equal(git('rev-parse', '--show-toplevel').toLowerCase(), root.replaceAll('\\', '/').toLowerCase(), 'the scratch is its own repository');
  write({
    '.gitignore': 'local/\n',
    'gate.mjs': "import { appendFileSync, mkdirSync } from 'node:fs';\nmkdirSync('local', { recursive: true });\nappendFileSync('local/ran.txt', `check ${process.env.VITEST ?? '-'}\\n`);\n",
    'daoris.gates.json': `${JSON.stringify({ gates: [{ name: 'check', run: 'node gate.mjs' }] }, null, 2)}\n`,
  });
  git('add', '-A');
  git('commit', '--quiet', '-m', `first${TRAILER}`);
  return { root, git, write };
}

/** The tool run in the scratch on `args`, its status and everything it printed. */
async function runTool(root: string, env: Record<string, string>, args: string[]): Promise<{ status: unknown; out: string }> {
  const child = spawn(process.execPath, [TOOL, ...args], { cwd: root, env: { ...env, VITEST: 'true', NODE_ENV: 'test' }, windowsHide: true });
  let out = '';
  child.stdout.on('data', (chunk) => { out += String(chunk); });
  child.stderr.on('data', (chunk) => { out += String(chunk); });
  const status = await new Promise((done) => child.on('close', done));
  return { status, out };
}

test('a merge whose package-lock.json moved runs npm ci there once, before the two gates that start together', async () => {
  const { dir, remove } = fixtureFolder('merge-e2e-lock');
  try {
    const env = toolEnvironment();
    const repo = seeded(dir, env);
    // The install counts itself by the package's own postinstall; each gate notes how many installs it found when it ran.
    const manifest = (version: string) => `${JSON.stringify({ name: 'scratch', version, private: true, scripts: { postinstall: 'node count-install.mjs' } }, null, 2)}\n`;
    const lockfile = (version: string) => `${JSON.stringify({ name: 'scratch', version, lockfileVersion: 3, requires: true, packages: { '': { name: 'scratch', version } } }, null, 2)}\n`;
    repo.write({
      '.gitignore': 'local/\nnode_modules/\n',
      'count-install.mjs': "import { appendFileSync, mkdirSync } from 'node:fs';\nmkdirSync('local', { recursive: true });\nappendFileSync('local/installs.txt', 'installed\\n');\n",
      'seen.mjs': [
        "import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';",
        "mkdirSync('local', { recursive: true });",
        "const installs = existsSync('local/installs.txt') ? readFileSync('local/installs.txt', 'utf8').split('\\n').filter(Boolean).length : 0;",
        "appendFileSync('local/ran.txt', `${process.argv[2]} ${installs}\\n`);",
        '',
      ].join('\n'),
      'daoris.gates.json': `${JSON.stringify({ gates: [{ name: 'one', run: 'node seen.mjs one' }, { name: 'two', run: 'node seen.mjs two' }] }, null, 2)}\n`,
      'package.json': manifest('1.0.0'),
      'package-lock.json': lockfile('1.0.0'),
    });
    repo.git('add', '-A');
    repo.git('commit', '--quiet', '-m', `the scratch's package${TRAILER}`);
    repo.git('checkout', '--quiet', '-b', 'feature/lock');
    repo.write({ 'package.json': manifest('1.0.1'), 'package-lock.json': lockfile('1.0.1') });
    repo.git('add', '-A');
    repo.git('commit', '--quiet', '-m', `move the lockfile${TRAILER}`);
    repo.git('checkout', '--quiet', 'main');

    const { status, out } = await runTool(repo.root, env, ['feature/lock', '--gated']);

    assert.equal(status, 0, out);
    assert.match(out, /npm ci in \. before the first gate: package-lock\.json moved there/);
    assert.deepEqual(readFileSync(join(repo.root, 'local', 'installs.txt'), 'utf8').split('\n').filter(Boolean), ['installed'], 'npm ci ran once');
    assert.deepEqual(readFileSync(join(repo.root, 'local', 'ran.txt'), 'utf8').split('\n').filter(Boolean).sort(), ['one 1', 'two 1'], 'each gate ran after it');
    assert.ok(existsSync(join(repo.root, 'node_modules', '.gate-tree-lockfile')), 'the install is stamped with the merged lockfile');
  } finally {
    remove();
  }
});

test('a branch merges in a scratch repository: its gate runs without the runner\'s variables, and the record holds the wall time', async () => {
  const { dir, remove } = fixtureFolder('merge-e2e');
  try {
    const env = toolEnvironment();
    const repo = seeded(dir, env);
    repo.git('checkout', '--quiet', '-b', 'feature/one');
    repo.write({ 'work/a.txt': 'x\n' });
    repo.git('add', '-A');
    repo.git('commit', '--quiet', '-m', `work on feature/one${TRAILER}`);
    repo.git('checkout', '--quiet', 'main');
    const head = repo.git('rev-parse', 'HEAD');

    const { status, out } = await runTool(repo.root, env, ['feature/one', '--gated']);

    assert.equal(status, 0, out);
    assert.match(out, /PASS\s+check\s/);
    assert.doesNotMatch(out, /npm ci/, 'no lockfile moved, so nothing installs');
    assert.match(out, /NOT committed/);
    assert.equal(repo.git('rev-parse', 'HEAD'), head, 'the tool committed');
    assert.deepEqual(readFileSync(join(repo.root, 'local', 'ran.txt'), 'utf8').split('\n').filter(Boolean), ['check -'], 'the gate ran once, given no runner variable');
    const records = readRecords(join(repo.root, MERGE_RECORD));
    assert.equal(records.length, 1);
    assert.equal(records[0]!.branch, 'feature/one');
    assert.ok((records[0]!.wallMs ?? 0) > 0, 'the wall time is measured');
    assert.equal(records[0]!.flakes, 0);
  } finally {
    remove();
  }
});
