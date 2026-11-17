import { test } from 'vitest';
import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fixtureFolder } from './fixture-home.ts';

// The queue's candidate and a preflight's branch are committed trees, where a gate reading the changed paths from the tree's own
// diff reads none; each run hands such a gate its base in `{base}`, as the `reached` gate's declaration names it.

const TOOL = fileURLToPath(new URL('../merge-branch.mjs', import.meta.url));
const TRAILER = '\n\nCo-Authored-By: Claude Fixture (Anthropic) <fixture@example.test>';

/** A fixed git identity, none of git's locating variables and none of the test runner's own. */
function toolEnvironment(extra: Record<string, string> = {}): Record<string, string> {
  const own = /^(GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES)|NODE_TEST_CONTEXT|NODE_ENV|VITEST\w*|DAORIS_\w*|MERGE_BRANCH_\w*)$/i;
  const kept = Object.entries(process.env).filter((entry): entry is [string, string] => entry[1] !== undefined && !own.test(entry[0]));
  return {
    ...Object.fromEntries(kept),
    GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
    GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
    GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.autocrlf', GIT_CONFIG_VALUE_0: 'false',
    MERGE_BRANCH_MEMORY: 'off', MERGE_BRANCH_CI_NIGHT_HOUR: 'off', ...extra,
  };
}

/** The reached gate's stand-in: it writes the paths changed from the base it is handed, and fails where it reads none. */
const pathsGate = (record: string) => [
  "import { execFileSync } from 'node:child_process';",
  "import { writeFileSync } from 'node:fs';",
  "const paths = execFileSync('git', ['diff', '--name-only', process.argv[2]], { encoding: 'utf8', windowsHide: true }).split('\\n').filter(Boolean);",
  `writeFileSync(${JSON.stringify(record)}, paths.join('\\n'));`,
  'process.exit(paths.length ? 0 : 1);',
  '',
].join('\n');

/** A scratch repository whose one gate is the stand-in, declared as the `reached` gate is: `{base}` in its run, its key with the diff. */
function seeded(dir: string, env: Record<string, string>) {
  const root = join(dir, 'repo');
  const record = join(dir, 'reached-paths.txt');
  const git = (cwd: string, ...args: string[]) => execFileSync('git', args, { cwd, env, encoding: 'utf8', windowsHide: true }).trim();
  const write = (at: string, files: Record<string, string>) => {
    for (const [path, text] of Object.entries(files)) {
      mkdirSync(dirname(join(at, path)), { recursive: true });
      writeFileSync(join(at, path), text);
    }
  };
  mkdirSync(root, { recursive: true });
  git(root, 'init', '--quiet', '--template=', '-b', 'main');
  write(root, {
    '.gitignore': 'local/\n_fixtures/\n',
    'paths.mjs': pathsGate(record),
    'daoris.gates.json': `${JSON.stringify({ gates: [{ name: 'reached', run: 'node paths.mjs {base}', reuse: 'inputs+diff' }] }, null, 2)}\n`,
  });
  git(root, 'add', '-A');
  git(root, 'commit', '--quiet', '-m', `first${TRAILER}`);
  return { root, record, git, write };
}

async function runTool(cwd: string, env: Record<string, string>, args: string[]): Promise<{ status: unknown; out: string }> {
  const child = spawn(process.execPath, [TOOL, ...args], { cwd, env: { ...env, VITEST: 'true', NODE_ENV: 'test' }, windowsHide: true });
  let out = '';
  child.stdout.on('data', (chunk) => { out += String(chunk); });
  child.stderr.on('data', (chunk) => { out += String(chunk); });
  const status = await new Promise((done) => child.on('close', done));
  return { status, out };
}

test("a preflight hands the reached gate the branch's base, so on the committed branch it reads what the branch changed", async () => {
  const { dir, remove } = fixtureFolder('reached-base-preflight');
  try {
    const env = toolEnvironment();
    const { root, record, git, write } = seeded(dir, env);
    const tree = join(dir, 'tree');
    git(root, 'worktree', 'add', '--quiet', '-b', 'feature/reach', tree, 'main');
    write(tree, { 'src/unit.txt': 'x\n' });
    git(tree, 'add', '-A');
    git(tree, 'commit', '--quiet', '-m', `a unit${TRAILER}`);

    const { status, out } = await runTool(tree, env, ['--preflight']);
    assert.equal(status, 0, out);
    assert.equal(readFileSync(record, 'utf8'), 'src/unit.txt', "the branch's paths from its merge base with main");
  } finally {
    remove();
  }
});

test("the queue hands the reached gate its candidate's base, so in the committed scope tree it reads what the candidate changed", async () => {
  const { dir, remove } = fixtureFolder('reached-base-queue');
  try {
    // The queue's own polls short, its runner ended with this process, and no machine admission to wait on.
    const env = toolEnvironment({ MERGE_BRANCH_QUEUE_POLL_MS: '200', MERGE_BRANCH_QUEUE_TICK_MS: '300', MERGE_BRANCH_QUEUE_OWNER: String(process.pid) });
    const { root, record, git, write } = seeded(dir, env);
    git(root, 'checkout', '--quiet', '-b', 'b1');
    write(root, { 'src/unit.txt': 'x\n' });
    git(root, 'add', '-A');
    git(root, 'commit', '--quiet', '-m', `a unit${TRAILER}`);
    git(root, 'checkout', '--quiet', 'main');
    write(root, { 'local/b1.msg': `Merge b1: a unit${TRAILER}\n` });

    assert.equal((await runTool(root, env, ['b1', '--preview'])).status, 0);
    const { status, out } = await runTool(root, env, ['b1', '--message', 'local/b1.msg']);
    const log = existsSync(join(root, 'local', 'merge-queue.log')) ? readFileSync(join(root, 'local', 'merge-queue.log'), 'utf8') : '';
    assert.equal(status, 0, `${out}\n${log}`);
    assert.equal(readFileSync(record, 'utf8'), 'src/unit.txt', "the candidate's paths from its base");
  } finally {
    await runnerEnded(join(dir, 'repo'));
    remove();
  }
});

/** The queue's runner, idle once its candidate landed, ended by itself within ten seconds, or ended here. */
async function runnerEnded(root: string): Promise<void> {
  const runner = (): number | null => {
    try {
      return (JSON.parse(readFileSync(join(root, 'local', 'merge-queue.json'), 'utf8')) as { runner?: { pid: number } | null }).runner?.pid ?? null;
    } catch {
      return null;
    }
  };
  for (let i = 0; i < 100 && runner() !== null; i += 1) await new Promise((done) => { setTimeout(done, 100); });
  const pid = runner();
  if (pid === null) return;
  try {
    process.kill(pid);
  } catch {
    return;
  }
  for (let i = 0; i < 50; i += 1) {
    try {
      process.kill(pid, 0);
    } catch {
      return;
    }
    await new Promise((done) => { setTimeout(done, 100); });
  }
}
