import { test } from 'vitest';
import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fixtureFolder } from './fixture-home.ts';
import { readPlan } from './plan.ts';

const TOOL = fileURLToPath(new URL('../merge-branch.mjs', import.meta.url));
const TRAILER = '\n\nCo-Authored-By: Claude Fixture (Anthropic) <fixture@example.test>';

type Choice = { set: { run: { name: string }[]; kept: { name: string }[] } };
const tool = await import(
  // @ts-expect-error — untyped workspace tooling, read through the seam its own tests use
  '../merge-branch.mjs') as { scopedChoice: (home: string, tree: string, options: Record<string, unknown>) => Choice };

/** A fixed git identity, none of git's locating variables and none of the test runner's own. */
function toolEnvironment(): Record<string, string> {
  const own = /^(GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES)|NODE_TEST_CONTEXT|NODE_ENV|DAORIS_HOME|VITEST\w*)$/i;
  const kept = Object.entries(process.env).filter((entry): entry is [string, string] => entry[1] !== undefined && !own.test(entry[0]));
  return {
    ...Object.fromEntries(kept),
    GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
    GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
    GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.autocrlf', GIT_CONFIG_VALUE_0: 'false',
    MERGE_BRANCH_MEMORY: 'off', MERGE_BRANCH_CI_NIGHT_HOUR: 'off',
  };
}

// Each gate fails where its own `work/<name>.broken` exists, saying where, as a test runner would.
const GATE = [
  "import { existsSync, readFileSync } from 'node:fs';",
  'const name = process.argv[2];',
  "if (existsSync(`work/${name}.broken`)) { console.error(`AssertionError: ${name} is broken\\n    at work/${name}.check:3:1`); process.exit(1); }",
  "console.log(`${name} passed; ratchet since ${existsSync('_fixtures/test-timing/run-start.json') ? JSON.parse(readFileSync('_fixtures/test-timing/run-start.json', 'utf8')).startedAt : 'none'}; ${Object.keys(process.env).filter((key) => /^DAORIS_/i.test(key)).length} DAORIS_ names`);",
  '',
].join('\n');

test('a preflight in the branch\'s worktree runs the fast gates its merge would, each to its end within its bound, and briefs each red marked by main', async () => {
  const { dir, remove } = fixtureFolder('preflight-e2e');
  try {
    const env = toolEnvironment();
    const root = join(dir, 'repo');
    const git = (cwd: string, ...args: string[]) => execFileSync('git', args, { cwd, env, encoding: 'utf8', windowsHide: true }).trim();
    const write = (at: string, files: Record<string, string>) => {
      for (const [path, text] of Object.entries(files)) {
        mkdirSync(dirname(join(at, path)), { recursive: true });
        writeFileSync(join(at, path), text);
      }
    };
    mkdirSync(root, { recursive: true });
    git(root, 'init', '--quiet', '--template=', '-b', 'main');
    // Main stands red in `two` already; the branch brings `one`'s red.
    write(root, {
      '.gitignore': 'local/\n_fixtures/\n',
      'gate.mjs': GATE,
      'work/two.broken': 'x\n',
      'daoris.gates.json': `${JSON.stringify({ gates: ['one', 'two', 'three'].map((name) => ({ name, run: `node gate.mjs ${name}` })) }, null, 2)}\n`,
    });
    git(root, 'add', '-A');
    git(root, 'commit', '--quiet', '-m', `first${TRAILER}`);
    git(root, 'branch', 'feature/pre');
    const tree = join(dir, 'tree');
    git(root, 'worktree', 'add', '--quiet', tree, 'feature/pre');
    write(tree, { 'work/one.broken': 'x\n' });
    git(tree, 'add', '-A');
    git(tree, 'commit', '--quiet', '-m', `break one${TRAILER}`);

    // The merge's own choice on the candidate the queue would form: the branch merged onto main's tip.
    const candidate = join(dir, 'candidate');
    git(root, 'worktree', 'add', '--quiet', '--detach', candidate, 'main');
    git(candidate, 'merge', '--quiet', '--no-ff', '--no-edit', 'feature/pre');
    const choose = () => tool.scopedChoice(root, candidate, { plan: readPlan(candidate), base: git(root, 'rev-parse', 'main'), tip: git(candidate, 'rev-parse', 'HEAD') });
    const gates = choose().set.run.map((entry) => entry.name);
    assert.deepEqual(gates, ['one', 'two', 'three']);

    const started = Date.now();
    const child = spawn(process.execPath, [TOOL, '--preflight'], { cwd: tree, env: { ...env, VITEST: 'true', NODE_ENV: 'test' }, windowsHide: true });
    let out = '';
    child.stdout.on('data', (chunk) => { out += String(chunk); });
    child.stderr.on('data', (chunk) => { out += String(chunk); });
    const status = await new Promise((done) => child.on('close', done));

    assert.equal(status, 1, out);
    assert.match(out, new RegExp(`the merge's fast plan: ${gates.join(', ')}\\n`), 'the preflight picks what the merge picks');
    assert.match(out, /its bound: 25 min \(--bound <minutes>\)/);
    assert.match(out, /merge-branch: the preflight of feature\/pre failed 2 of 3 gates in \d+ min: one, two\. Fix the branch's reds/);
    assert.deepEqual(choose().set.kept.map((entry) => entry.name), ['three'], "the preflight's pass stands for the merge of the same tree");
    assert.match(out, /FAIL\s+one\s/);
    assert.match(out, /FAIL\s+two\s/);
    assert.match(out, /PASS\s+three\s/, 'a failure stops no gate after it');
    const said = readFileSync(join(tree, 'local', 'scratch', 'merge-feature-pre', 'three.log'), 'utf8');
    const since = Number(/ratchet since (\d+)/.exec(said)?.[1]);
    assert.ok(since >= started && since <= Date.now(), 'a time ratchet inside a gate judges only the records written since the run began');
    assert.match(said, /; 0 DAORIS_ names/, 'no DAORIS_ variable reaches a gate');

    const treeId = git(tree, 'rev-parse', 'HEAD^{tree}');
    const receipts = JSON.parse(readFileSync(join(root, 'local', 'receipts', `${treeId}.json`), 'utf8')) as { units: Record<string, { verdict: string }> };
    assert.deepEqual(Object.fromEntries(Object.entries(receipts.units).map(([unit, each]) => [unit, each.verdict])), { one: 'fail', two: 'fail', three: 'pass' });
    const runs = readFileSync(join(root, 'local', 'receipts', 'runs.jsonl'), 'utf8').trim().split('\n').map((line) => JSON.parse(line) as { trigger: string; gates: unknown[] });
    assert.deepEqual([runs.length, runs[0]?.trigger, runs[0]?.gates.length], [1, 'agent', 3]);
    const summary = JSON.parse(readFileSync(join(tree, 'local', 'scratch', 'merge-feature-pre', 'summary.json'), 'utf8')) as { changed: { files: number }; verified: { failed: number }; judgement: { kind: string }[] };
    assert.deepEqual([summary.changed.files, summary.verified.failed, summary.judgement.map((each) => each.kind)], [1, 2, ['failed', 'failed']]);
    assert.match(out, /^Changed: 1 files/m);
    assert.match(out, /^Verified: 1 passed, 2 failed, 0 without a receipt/m);
    assert.match(out, /^Left for judgement: 2/m);

    const brief = join(tree, 'local', 'scratch', 'merge-feature-pre', 'brief.md');
    assert.ok(existsSync(brief), out);
    assert.match(out, /local[\\/]scratch[\\/]merge-feature-pre[\\/]brief\.md/, 'its path is printed');
    const text = readFileSync(brief, 'utf8');
    assert.match(text, /## one \[BRANCH: passes on main/);
    assert.match(text, /## two \[MAIN TOO: fails on main too/);
    assert.match(text, /AssertionError: one is broken/);
    assert.match(text, /at `work\/one\.check:3`/);
    assert.doesNotMatch(text, /## three/);
    assert.equal(git(root, 'rev-parse', 'main'), git(root, 'rev-parse', 'main~0'), 'main is where it was');
    assert.equal(git(root, 'status', '--porcelain'), '', 'the main checkout is untouched');
  } finally {
    remove();
  }
});
