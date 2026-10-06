/**
 * GATE1: a check run while a merge is open judges the commit the merge would make, not the commit before it.
 *
 *   node --test tools/as-merged.test.mjs
 *
 * Outside `npm run verify`, whose tests are the CLI package's: this branch's lane is the tools, and the CLI suite is
 * another lane's (`knowledge-bench.test.mjs` is outside it for the same reason). The cases build scratch repositories
 * in a gitignored folder of this one; one runs the merge tool in its own, and the last builds and runs the devkit's
 * docs gate, so it needs `dotnet`.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
const tool = join(here, 'as-merged.mjs');
const scratch = join(root, 'local', 'scratch', 'as-merged-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

// A git location inherited from whoever runs this (a run under the tool itself sets two) would point every scratch
// repository's git at the repository around it.
const inherited = Object.fromEntries(Object.entries(process.env)
  .filter(([key]) => !/^GIT_(DIR|WORK_TREE|INDEX_FILE|COMMON_DIR|OBJECT_DIRECTORY)$/i.test(key)));
const run = (cwd, command, args, env = {}) =>
  spawnSync(command, args, { cwd, encoding: 'utf8', env: { ...inherited, ...env }, windowsHide: true });

function git(cwd, args, env) {
  const result = run(cwd, 'git', args, env);
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}

/** Commit dates a fixture controls, so a check's verdict does not depend on when the test runs. */
const day = (n) => {
  const at = `2026-01-0${n}T10:00:00Z`;
  return { GIT_AUTHOR_DATE: at, GIT_COMMITTER_DATE: at };
};

/**
 * The devkit's docs gate's rule (`DocsGate.cs`), with nothing else: the date of the last commit touching each side,
 * read with the gate's own `git log -1 --format=%aI -- <path>` from HEAD, compared by day.
 */
const CHECK = join(scratch, 'docs-check.mjs');
writeFileSync(CHECK, [
  "import { spawnSync } from 'node:child_process';",
  "const at = (path) => spawnSync('git', ['log', '-1', '--format=%aI', '--', path], { encoding: 'utf8' }).stdout.trim().slice(0, 10);",
  "const [document, described] = [at('README.md'), at('src')];",
  'const behind = (Date.parse(described) - Date.parse(document)) / 86400000;',
  'if (behind > 0) {',
  '  console.log(`README.md last changed ${document}, but src changed ${described} (${behind} days later)`);',
  '  process.exit(1);',
  '}',
  'console.log(`README.md keeping up: ${document}, src ${described}`);',
  '',
].join('\n'));

/**
 * A repository with a merge open, TOOL4e's shape: main holds the README and the source from day 1 and another
 * file from day 2, and a branch changed the source on day 5 (and the README with it, when `readme`). With
 * `conflict`, main changed the same source line, so the merge stops on it.
 */
function mergeOpen(name, { readme = false, conflict = false, files = {} } = {}) {
  const repo = join(scratch, name);
  const write = (path, text) => {
    mkdirSync(dirname(join(repo, path)), { recursive: true });
    writeFileSync(join(repo, path), text);
  };
  mkdirSync(repo, { recursive: true });
  git(repo, ['init', '-q', '-b', 'main']);
  for (const [key, value] of [['user.name', 'Fixture'], ['user.email', 'fixture@example.invalid'], ['core.autocrlf', 'false']]) {
    git(repo, ['config', key, value]);
  }
  write('README.md', '# Fixture\n\nWhat src does.\n');
  write('src/a.txt', 'one\n');
  for (const [path, text] of Object.entries(files)) write(path, text);
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'one'], day(1));
  git(repo, ['checkout', '-q', '-b', 'work']);
  write('src/a.txt', 'one\ntwo\n');
  if (readme) write('README.md', '# Fixture\n\nWhat src does, both lines.\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'two'], day(5));
  git(repo, ['checkout', '-q', 'main']);
  write(conflict ? 'src/a.txt' : 'other.txt', 'main\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', 'three'], day(2));
  const merge = run(repo, 'git', ['merge', '--no-ff', '--no-commit', 'work']);
  assert.equal(merge.status, conflict ? 1 : 0, `${merge.stdout}${merge.stderr}`);
  return repo;
}

/** What git says of a checkout's state: a run through the tool must leave all of it as it was. */
function state(repo) {
  const gitDir = git(repo, ['rev-parse', '--absolute-git-dir']);
  return {
    head: git(repo, ['rev-parse', 'HEAD']),
    mergeHead: existsSync(join(gitDir, 'MERGE_HEAD')) ? readFileSync(join(gitDir, 'MERGE_HEAD'), 'utf8') : null,
    staged: git(repo, ['diff', '--cached', '--name-status']),
    status: git(repo, ['status', '--porcelain=v2', '--branch']),
    folders: readdirSync(gitDir).filter((entry) => entry.startsWith('daoris-as-merged')),
  };
}

const through = (repo, command, args) => run(repo, 'node', [tool, command, ...args]);

test('during a merge, a check of commit dates judges the commit the merge would make, and leaves the merge as it was', () => {
  const repo = mergeOpen('stale');
  const before = state(repo);
  // A run killed mid-way leaves its folder; the next run removes it once that process has ended.
  const killed = join(git(repo, ['rev-parse', '--absolute-git-dir']), 'daoris-as-merged-999999');
  mkdirSync(killed);
  writeFileSync(join(killed, 'HEAD'), `${before.head}\n`);

  // As the gate ran before GATE1: HEAD is main, where the README and the source last changed on the same day.
  const direct = run(repo, 'node', [CHECK]);
  assert.equal(direct.status, 0, direct.stdout);
  assert.match(direct.stdout, /keeping up: 2026-01-01, src 2026-01-01/);

  // Through the tool, HEAD is the merge commit: the branch's source is day 5 and the README is still day 1.
  const judged = through(repo, 'node', [CHECK]);
  assert.equal(judged.status, 1, `${judged.stdout}${judged.stderr}`);
  assert.match(judged.stdout, /README\.md last changed 2026-01-01, but src changed 2026-01-05 \(4 days later\)/);
  // What it judged is said beside the command's output, never in it.
  assert.match(judged.stderr, /a merge is open/);
  assert.doesNotMatch(judged.stdout, /a merge is open/);

  assert.deepEqual(state(repo), before, 'the merge, the index and the git folder are untouched');
  assert.deepEqual(before.folders, []);
});

test('it passes the merge that brings the document with its source, or one whose tree changes the document before it is committed', () => {
  const kept = mergeOpen('kept', { readme: true });
  const judged = through(kept, 'node', [CHECK]);
  assert.equal(judged.status, 0, `${judged.stdout}${judged.stderr}`);
  assert.match(judged.stdout, /keeping up: 2026-01-05, src 2026-01-05/);

  // The parent fixes the README in the merge and has not staged it yet: the commit is the tree as it will be added.
  const fixed = mergeOpen('fixed');
  writeFileSync(join(fixed, 'README.md'), '# Fixture\n\nWhat src does, both lines.\n');
  const before = state(fixed);
  const after = through(fixed, 'node', [CHECK]);
  assert.equal(after.status, 0, `${after.stdout}${after.stderr}`);
  const shown = through(fixed, 'git', ['show', 'HEAD:README.md']);
  assert.match(shown.stdout, /both lines/);
  assert.deepEqual(state(fixed), before, 'the fix is still unstaged');
});

test("git inside it reads HEAD as that commit: the merge's tree, both parents, and the person's own index", () => {
  const repo = mergeOpen('inside');
  const head = git(repo, ['rev-parse', 'HEAD']);
  const merging = git(repo, ['rev-parse', 'MERGE_HEAD']);

  const parents = through(repo, 'git', ['rev-parse', 'HEAD^1', 'HEAD^2']);
  assert.equal(parents.status, 0, parents.stderr);
  assert.deepEqual(parents.stdout.trim().split(/\r?\n/), [head, merging]);

  const tree = through(repo, 'git', ['rev-parse', 'HEAD^{tree}']);
  assert.equal(tree.stdout.trim(), git(repo, ['write-tree']), 'its tree is what the merge stages');

  const listed = through(repo, 'git', ['ls-files']);
  assert.equal(listed.stdout.trim(), git(repo, ['ls-files']));
  // Nothing open inside: the commit is made, as far as the command can tell.
  assert.notEqual(through(repo, 'git', ['rev-parse', '-q', '--verify', 'MERGE_HEAD']).status, 0);
});

test('outside a merge the command runs as it would without it: nothing said, nothing set, its exit kept', () => {
  const repo = join(scratch, 'plain');
  mkdirSync(join(repo, 'src'), { recursive: true });
  git(repo, ['init', '-q', '-b', 'main']);
  writeFileSync(join(repo, 'README.md'), '# Plain\n');
  writeFileSync(join(repo, 'src', 'a.txt'), 'one\n');
  git(repo, ['add', '-A']);
  git(repo, ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '-q', '-m', 'one'], day(1));

  const env = through(repo, 'node', ['-e', "console.log(process.env.GIT_DIR ?? 'unset')"]);
  assert.equal(env.status, 0, env.stderr);
  assert.equal(env.stdout, 'unset\n');

  const direct = run(repo, 'node', [CHECK]);
  const judged = through(repo, 'node', [CHECK]);
  assert.deepEqual([judged.status, judged.stdout], [direct.status, direct.stdout]);

  assert.equal(through(repo, 'node', ['-e', 'process.exit(3)']).status, 3);
  assert.equal(through(mergeOpen('exit'), 'node', ['-e', 'process.exit(3)']).status, 3, 'and during a merge');
});

test('a merge with unmerged paths has no commit to judge, and a command that cannot start is a tool error', () => {
  const conflicted = mergeOpen('conflict', { conflict: true });
  const refused = through(conflicted, 'node', [CHECK]);
  assert.equal(refused.status, 2, `${refused.stdout}${refused.stderr}`);
  assert.match(refused.stderr, /unmerged/);
  assert.match(refused.stderr, /src\/a\.txt/);
  assert.doesNotMatch(refused.stdout, /keeping up|last changed/, 'the command never ran');

  const missing = through(join(scratch, 'plain'), 'no-such-command-gate1', []);
  assert.equal(missing.status, 2);
  assert.match(missing.stderr, /could not start no-such-command-gate1/);

  const bare = run(scratch, 'node', [tool]);
  assert.equal(bare.status, 2);
  assert.match(bare.stderr, /usage: node tools\/as-merged\.mjs <command>/);
});

test('the merge tool, gating a merge it has not committed, fails the check run through this and passes the same check run bare', { timeout: 300_000 }, () => {
  // A repository the merge tool can merge in: main checked out and clean, its scratch ignored, this tool beside it.
  const repo = join(scratch, 'merge-tool');
  const write = (path, text) => {
    mkdirSync(dirname(join(repo, path)), { recursive: true });
    writeFileSync(join(repo, path), text);
  };
  mkdirSync(repo, { recursive: true });
  git(repo, ['init', '-q', '-b', 'main']);
  for (const [key, value] of [['user.name', 'Fixture'], ['user.email', 'fixture@example.invalid'], ['core.autocrlf', 'false']]) {
    git(repo, ['config', key, value]);
  }
  const trailer = '\n\nCo-Authored-By: Fixture <fixture@example.invalid>';
  write('.gitignore', 'local/\n');
  write('README.md', '# Fixture\n\nWhat src does.\n');
  write('src/a.txt', 'one\n');
  write('docs-check.mjs', readFileSync(CHECK, 'utf8'));
  for (const file of ['as-merged.mjs', 'fsx.mjs']) write(`tools/${file}`, readFileSync(join(here, file), 'utf8'));
  // The same check twice: as the declared gates ran it before GATE1, and through this tool.
  write('daoris.gates.json', `${JSON.stringify({
    gates: [{ name: 'bare', run: 'node docs-check.mjs' }, { name: 'universal', run: 'node tools/as-merged.mjs node docs-check.mjs' }],
  }, null, 2)}\n`);
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', `one${trailer}`], day(1));
  git(repo, ['checkout', '-q', '-b', 'work']);
  write('src/a.txt', 'one\ntwo\n');
  git(repo, ['add', '-A']);
  git(repo, ['commit', '-q', '-m', `two${trailer}`], day(5));
  git(repo, ['checkout', '-q', 'main']);

  const merged = run(repo, 'node', [join(here, 'merge-branch.mjs'), 'work', '--keep-going', '--no-prune']);
  const said = `${merged.stdout}${merged.stderr}`;
  assert.equal(merged.status, 1, said);
  assert.match(said, /PASS\s+bare\b/);
  assert.match(said, /FAIL\s+universal\b/);
  const log = readFileSync(join(repo, 'local', 'scratch', 'merge-work', 'universal.log'), 'utf8');
  assert.match(log, /README\.md last changed 2026-01-01, but src changed 2026-01-05/);
  assert.match(log, /a merge is open/);
  assert.equal(run(repo, 'git', ['rev-parse', '-q', '--verify', 'MERGE_HEAD']).status, 0, 'the merge is still open, uncommitted');
});

test("this repository's gates that run the devkit's docs gate run it through this, and the merge tool still orders it as a check", () => {
  const devkitVerify = /^node tools\/as-merged\.mjs dotnet run --project src\/Daoris\.Devkit\/Daoris\.Devkit\.Cli -- verify\b/;
  const universal = JSON.parse(readFileSync(join(root, 'daoris.gates.json'), 'utf8')).gates.find((gate) => gate.name === 'universal');
  assert.match(universal.run, devkitVerify);
  const verify = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).scripts.verify;
  const steps = verify.split('&&').map((step) => step.trim());
  const devkitSteps = steps.filter((step) => /Daoris\.Devkit\.Cli -- verify\b/.test(step));
  assert.ok(devkitSteps.length > 0, 'verify no longer runs the devkit');
  for (const step of devkitSteps) assert.match(step, devkitVerify, 'every devkit verify in npm run verify');
  // GATE1 must not reorder the plan: a devkit check run through it is still a check, so it runs with the fast ones.
  return import('./merge-branch.mjs').then(({ gateKind }) => {
    assert.equal(gateKind(universal.run), 'check');
    assert.equal(gateKind('node tools/as-merged.mjs npm run verify'), 'suite');
  });
});

test("the devkit's own docs gate, run through it, fails the merge TOOL4e made and passes it once the README is fixed", { timeout: 600_000 }, () => {
  const devkit = JSON.parse(readFileSync(join(root, 'daoris.gates.json'), 'utf8')).devkit;
  const declaration = {
    devkit,
    gates: [],
    disabled: ['sensitive', 'links'],
    docs: { tracked: [{ document: 'README.md', describes: ['src'] }] },
  };
  const repo = mergeOpen('devkit', { files: { 'daoris.gates.json': `${JSON.stringify(declaration, null, 2)}\n` } });
  const verify = ['run', '--project', join(root, 'src', 'Daoris.Devkit', 'Daoris.Devkit.Cli'), '--', 'verify', '--universal-only', '--allow-builtins-only'];

  const direct = run(repo, 'dotnet', verify);
  assert.equal(direct.status, 0, `${direct.stdout}${direct.stderr}`);
  assert.match(direct.stdout, /docs\s+1 document\(s\) keeping up/);

  const judged = through(repo, 'dotnet', verify);
  assert.equal(judged.status, 1, `${judged.stdout}${judged.stderr}`);
  assert.match(judged.stdout, /README\.md last changed 2026-01-01, but src changed 2026-01-05/);

  writeFileSync(join(repo, 'README.md'), '# Fixture\n\nWhat src does, both lines.\n');
  const fixed = through(repo, 'dotnet', verify);
  assert.equal(fixed.status, 0, `${fixed.stdout}${fixed.stderr}`);
  assert.match(fixed.stdout, /docs\s+1 document\(s\) keeping up/);
});
