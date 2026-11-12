import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { repoRoot } from './support/merge-branch.ts';
import { git, TRAILER, unordered, scratch, branch, merging } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row

describe('the tool, end to end in a scratch repository (e2e-merge)', { concurrency: true }, () => {

  test('MAKERTRAIL1 a Codex stand-in commit passes the real git trailer check', async () => {
    const repo = scratch('codex-attribution', [{ name: 'first', run: 'node gate.mjs first' }]);
    try {
      branch(repo.root, 'codex-work', { 'codex.txt': 'built by the stand-in\n' });
      git(repo.root, 'checkout', '--quiet', 'codex-work');
      git(repo.root, 'commit', '--amend', '--quiet', '-m', 'fix: Codex stand-in\n\nCo-Authored-By: Codex (OpenAI, gpt-fixture) <noreply@openai.com>');
      git(repo.root, 'checkout', '--quiet', 'main');
      const result = await repo.run(['--plan', 'codex-work']);
      assert.equal(result.status, 0, result.out);
      assert.doesNotMatch(result.out, /no Co-Authored-By|no allowed maker/);
      assert.deepEqual(repo.ran(), []);
      assert.equal(merging(repo.root), false);
    } finally {
      repo.cleanup();
    }
  });

  test('it refuses before touching anything, and the commit check refuses what a hand-back left out', async () => {
    const repo = scratch('refusals', [{ name: 'first', run: 'node gate.mjs first' }]);
    // Merged into main, so the merge's prune removes it; a refusal comes first and touches nothing.
    git(repo.root, 'branch', 'old');
    branch(repo.root, 'bare', { 'bare.txt': 'x\n' }, { trailer: false });
    const tree = join(dirname(repo.root), 'bare-tree');
    git(repo.root, 'worktree', 'add', '--quiet', tree, 'bare');
    writeFileSync(join(tree, 'forgotten.txt'), 'not committed\n');

    let result = await repo.run([]);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /usage/i);
    result = await repo.run(['nope']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /no branch 'nope'/);
    result = await repo.run(['bare']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /no Co-Authored-By line/);
    assert.match(result.out, /uncommitted[\s\S]*forgotten\.txt/);
    assert.match(result.out, /--no-commit-check/);
    assert.doesNotMatch(result.out, /prune/, 'the commit check refuses before the prune runs');
    writeFileSync(join(repo.root, 'stray.txt'), 'left here\n');
    result = await repo.run(['bare']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not clean:\s+\?\? stray\.txt/);
    rmSync(join(repo.root, 'stray.txt'));
    assert.equal(merging(repo.root), false, 'a refusal merged something');
    assert.deepEqual(repo.ran(), [], 'a refusal ran a gate');
    assert.doesNotMatch(result.out, /prune/, 'a refusal pruned');
    // `old` is still there to prune below: no refusal removes it.

    result = await repo.run(['bare', '--no-commit-check']);
    assert.equal(result.status, 0, result.out);
    assert.equal(merging(repo.root), true, '--no-commit-check lets the bare commit through');
    assert.match(result.out, /prune: 1 branch merged into main\n\s+removed\s+old\s+the branch \(no worktree has it checked out\)/);
    assert.ok(result.out.indexOf('removed') < result.out.indexOf('merged bare'), 'the prune runs before the merge');
    assert.equal(git(repo.root, 'branch', '--list', 'old'), '', 'the merged branch is gone');
    git(repo.root, 'merge', '--abort');
    result = await repo.run(['--drop-batch']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /forgot the batch \(bare\)/);
    git(repo.root, 'worktree', 'remove', '--force', tree);
    repo.cleanup();
  });

  test('by default a merge runs the blocking checks alone and lists every other gate it reaches as "in the CI beside" (GATESLIM1)', async () => {
    const repo = scratch('by-default', [
      { name: 'first', run: 'node gate.mjs first' },
      { name: 'universal', run: 'node gate.mjs universal' },
    ], {});
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n' });
    const plan = await repo.run(['--plan', 'feature/one'], { BY_DEFAULT: '1' });
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /first[^\n]*in the CI beside/);
    const result = await repo.run(['feature/one'], { BY_DEFAULT: '1' });
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /in the CI beside/);
    assert.deepEqual(repo.ran(), ['universal'], 'only the blocking check ran');
    repo.cleanup();
    // A scratch repository left under `_fixtures/` keeps a landed session's tree from the tidy.
    assert.equal(existsSync(dirname(repo.root)), false, 'the case left its scratch repository');
  });

  test("a merge whose two sides typecheck but not together stops at the web's typecheck, before any commit (MERGETSC1)", async () => {
    // The real compiler on a page of two files: main renames a helper, the branch names it by its old name.
    const tsc = join(repoRoot, 'node_modules', 'typescript', 'bin', 'tsc');
    const typecheck = [
      "import { spawnSync } from 'node:child_process';",
      "import { appendFileSync } from 'node:fs';",
      "appendFileSync('local/ran.txt', 'web-types\\n');",
      `const run = spawnSync(process.execPath, [${JSON.stringify(tsc)}, '--noEmit', '-p', 'src/Daoris.Web'], { stdio: 'inherit', windowsHide: true });`,
      'process.exit(run.status ?? 2);',
      '',
    ].join('\n');
    const repo = scratch('typecheck', [
      { name: 'universal', run: 'node gate.mjs universal' },
      { name: 'web-types', run: 'node typecheck.mjs' },
    ], {
      'typecheck.mjs': typecheck,
      'src/Daoris.Web/tsconfig.json': `${JSON.stringify({ compilerOptions: { strict: true, noEmit: true, module: 'ESNext', moduleResolution: 'bundler', target: 'ES2022', types: [] }, include: ['src'] })}\n`,
      'src/Daoris.Web/src/rail.ts': 'export const railLine = (text: string): string => text.trim();\n',
    });
    branch(repo.root, 'feature/notes', { 'notes.txt': 'x\n' });
    branch(repo.root, 'feature/story', { 'src/Daoris.Web/src/story.ts': "import { railLine } from './rail';\nexport const story = railLine(' a ');\n" });
    writeFileSync(join(repo.root, 'src/Daoris.Web/src/rail.ts'), 'export const callLine = (text: string): string => text.trim();\n');
    git(repo.root, 'commit', '--quiet', '-am', `rename the helper${TRAILER}`);

    // A merge that changes nothing the typecheck reads lists it in the CI beside and does not run it.
    const plain = await repo.run(['--plan', 'feature/notes'], { BY_DEFAULT: '1' });
    assert.equal(plain.status, 0, plain.out);
    assert.match(plain.out, /web-types[^\n]*in the CI beside/);

    // The plan names it among the checks a merge blocks on, and why.
    const plan = await repo.run(['--plan', 'feature/story'], { BY_DEFAULT: '1' });
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /web-types[^\n]*blocks the merge: src\/Daoris\.Web\/src\/story\.ts/);

    const head = git(repo.root, 'rev-parse', 'HEAD');
    const result = await repo.run(['feature/story'], { BY_DEFAULT: '1' });
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /FAIL\s+web-types/);
    assert.match(readFileSync(join(repo.root, 'local', 'scratch', 'merge-feature-story', 'web-types.log'), 'utf8'), /railLine/, 'the compiler said what broke');
    assert.deepEqual(repo.ran().sort(), ['universal', 'web-types'], 'the light gates run side by side');
    assert.equal(git(repo.root, 'rev-parse', 'HEAD'), head, 'nothing was committed');
    git(repo.root, 'merge', '--abort');
    repo.cleanup();
  });

  test('a clean merge prints its lanes, runs every gate, keeps each whole output, and never commits', async () => {
    const repo = scratch('clean', [
      { name: 'first', run: 'node gate.mjs first' },
      { name: 'second', run: 'node gate.mjs second' },
    ], {
      'daoris.lanes.json': JSON.stringify({ lanes: [
        { id: 'work', title: 'Work', summary: 'The work.', paths: ['work/**'] },
        { id: 'records', title: 'Records', summary: 'The backlog and this map.', steward: true, paths: ['TASKS.md', 'daoris.lanes.json'], gates: ['first'] },
      ] }),
      'TASKS.md': '# Tasks\n',
    });
    branch(repo.root, 'feature/one', { 'work/a.txt': 'x\n', 'notes.txt': 'y\n', 'TASKS.md': '# Tasks\n\n- a row\n' });

    const head = git(repo.root, 'rev-parse', 'HEAD');
    const plan = await repo.run(['--plan', 'feature/one']);
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /work\s+Work\s+1 file/);
    assert.match(plan.out, /1\.\s+first\s+suite\s+node gate\.mjs first/);
    assert.match(plan.out, /2\.\s+second/);
    assert.match(plan.out, /prune \(at the merge's start; a plan, so nothing is removed\): no branch but main is merged into main/);
    assert.equal(merging(repo.root), false, '--plan merges nothing');
    assert.match((await repo.run(['--plan', 'feature/one', '--no-prune'])).out, /prune: skipped \(--no-prune\)/);

    const result = await repo.run(['feature/one']);
    assert.equal(result.status, 0, result.out);
    // Lanes by id and title; the steward's lane is named, and so is what a subagent never edits.
    assert.match(result.out, /work\s+Work\s+1 file/);
    assert.match(result.out, /records\s+Records\s+1 file/);
    assert.match(result.out, /note: it edits the steward's records \(TASKS\.md\); a subagent never does/);
    assert.doesNotMatch(result.out, /crosses/, "the steward's lane is not a crossing: it is named on its own");
    assert.match(result.out, /outside every lane\s+1 file: notes\.txt/);
    // The steward's `gates` is for a landing of its lane alone (the queue's): this tool chooses by its
    // own lane table, which names neither gate here, so both run: side by side, as they build nothing.
    assert.deepEqual(unordered(repo.ran()), ['first', 'second']);
    assert.match(result.out, /PASS\s+first\s.*local\/scratch\/merge-feature-one\/first\.log/);
    assert.match(result.out, /PASS\s+second\s.*local\/scratch\/merge-feature-one\/second\.log/);

    const log = readFileSync(join(repo.root, 'local', 'scratch', 'merge-feature-one', 'first.log'), 'utf8');
    assert.match(log, /first line 1\r?\n/);
    assert.match(log, /first line 1000/);

    assert.equal(git(repo.root, 'rev-parse', 'HEAD'), head, 'the tool committed');
    assert.equal(git(repo.root, 'rev-parse', 'MERGE_HEAD'), git(repo.root, 'rev-parse', 'feature/one'));
    assert.match(git(repo.root, 'diff', '--cached', '--name-only'), /work\/a\.txt/);
    assert.match(result.out, /NOT committed/);
    repo.cleanup();
  });

  test('a conflict stops the merge, names the file and runs no gate; --continue gates it once resolved', async () => {
    const repo = scratch('conflict', [{ name: 'first', run: 'node gate.mjs first' }], { 'shared.txt': 'base\n' });
    branch(repo.root, 'left', { 'shared.txt': 'left\n' });
    writeFileSync(join(repo.root, 'shared.txt'), 'main\n');
    git(repo.root, 'commit', '--quiet', '-am', `main moved${TRAILER}`);

    let result = await repo.run(['left']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /conflict[\s\S]*shared\.txt/i);
    assert.deepEqual(repo.ran(), []);
    assert.equal(merging(repo.root), true, 'the merge is left for the parent to resolve');

    result = await repo.run(['--continue']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /shared\.txt/);
    assert.deepEqual(repo.ran(), []);

    writeFileSync(join(repo.root, 'shared.txt'), 'resolved\n');
    git(repo.root, 'add', 'shared.txt');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['first']);
    repo.cleanup();
  });

  test("the backlog's line conflict is resolved by the merge base and gated, and one row both sides edited is left (MERGETASKS1)", async () => {
    const rows = ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A2** two.', '- [ ] **A3** three.', ''].join('\n');
    const repo = scratch('backlog', [{ name: 'first', run: 'node gate.mjs first' }], { 'TASKS.md': rows });
    const backlog = () => readFileSync(join(repo.root, 'TASKS.md'), 'utf8');
    const unmerged = () => git(repo.root, 'diff', '--name-only', '--diff-filter=U');

    // The branch removes a row next to the one main adds after: git stops on it, and the tool resolves it by the base.
    branch(repo.root, 'left', { 'TASKS.md': rows.replace('- [ ] **A2** two.\n', '') });
    writeFileSync(join(repo.root, 'TASKS.md'), rows.replace('- [ ] **A1** one.\n', '- [ ] **A1** one.\n- [ ] **B1** new.\n'));
    git(repo.root, 'commit', '--quiet', '-am', `main moved${TRAILER}`);
    let result = await repo.run(['left']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /TASKS\.md: resolved by the merge base, 1 line a side added kept and 1 line a side removed dropped/);
    assert.equal(backlog(), ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **B1** new.', '- [ ] **A3** three.', ''].join('\n'));
    assert.equal(unmerged(), '');
    assert.deepEqual(repo.ran(), ['first']);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // Both sides edit one row: left as git wrote it, said, no gate run.
    const now = backlog();
    branch(repo.root, 'right', { 'TASKS.md': now.replace('**A3** three.', '**A3** three, right.') });
    writeFileSync(join(repo.root, 'TASKS.md'), now.replace('**A3** three.', '**A3** three, main.'));
    git(repo.root, 'commit', '--quiet', '-am', `main moved again${TRAILER}`);
    result = await repo.run(['right']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /TASKS\.md: row A3 changed on both sides; left for you: main has ".+, main\.", and the branch has/);
    assert.match(result.out, /conflict in 1 file:\n\s+TASKS\.md/);
    assert.match(backlog(), /^<<<<<<< /m);
    assert.deepEqual(repo.ran(), ['first']);
    git(repo.root, 'merge', '--abort');
    repo.cleanup();
  });

  // Two branches each add a row at one line of docs/development.md's census table.
  test("a row table the line declares by rows merges by the merge base, two rows added at one line both kept; one the line does not declare is left, and a branch's own declaration changes nothing", async () => {
    const census = (...rows: string[]) => ['# Guide', '', '| When | Run |', '|---|---|', '| a test names a login | `node tools/live-logins.mjs` |', ...rows, '', 'Next.', ''].join('\n');
    const gates = [{ name: 'first', run: 'node gate.mjs first' }];
    const declared = (merge: unknown[]) => `${JSON.stringify({ gates, integration: { merge } }, null, 2)}\n`;
    const repo = scratch('rows', gates, {
      'docs/development.md': census(),
      'daoris.gates.json': declared([{ paths: 'docs/development.md', by: 'rows' }, { paths: 'docs/STATUS.md', line: '^Status: ', run: 'node tools/status.mjs' }]),
    });
    const guide = () => readFileSync(join(repo.root, 'docs', 'development.md'), 'utf8');

    branch(repo.root, 'left', { 'docs/development.md': census('| a tool reads git | `node tools/optional-locks.mjs` |') });
    writeFileSync(join(repo.root, 'docs', 'development.md'), census('| a test waits | `node tools/short-waits.mjs` |'));
    git(repo.root, 'commit', '--quiet', '-am', `main moved${TRAILER}`);
    let result = await repo.run(['left']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /docs\/development\.md: resolved by the merge base \(a declared row table\), 2 lines a side added kept and 0 lines a side removed dropped/);
    assert.equal(guide(), census('| a test waits | `node tools/short-waits.mjs` |', '| a tool reads git | `node tools/optional-locks.mjs` |'), "the line's row first, then the branch's");
    assert.equal(git(repo.root, 'diff', '--name-only', '--diff-filter=U'), '');
    assert.deepEqual(repo.ran(), ['first']);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // A row both sides changed is still a conflict of meaning.
    const now = guide();
    branch(repo.root, 'right', { 'docs/development.md': now.replace('`node tools/optional-locks.mjs`', '`node tools/right.mjs`') });
    writeFileSync(join(repo.root, 'docs', 'development.md'), now.replace('`node tools/optional-locks.mjs`', '`node tools/main.mjs`'));
    git(repo.root, 'commit', '--quiet', '-am', `main moved again${TRAILER}`);
    result = await repo.run(['right']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /docs\/development\.md: both sides changed the same line/);
    assert.deepEqual(repo.ran(), ['first']);
    git(repo.root, 'merge', '--abort');

    // A file the line does not declare is git's own conflict, and a branch declaring it itself is not the line declaring it.
    const other = scratch('rows-own', gates, { 'docs/development.md': census(), 'daoris.gates.json': declared([]) });
    branch(other.root, 'mine', {
      'docs/development.md': census('| a tool reads git | `node tools/optional-locks.mjs` |'),
      'daoris.gates.json': declared([{ paths: 'docs/development.md', by: 'rows' }]),
    });
    writeFileSync(join(other.root, 'docs', 'development.md'), census('| a test waits | `node tools/short-waits.mjs` |'));
    git(other.root, 'commit', '--quiet', '-am', `main moved${TRAILER}`);
    result = await other.run(['mine']);
    assert.equal(result.status, 1, result.out);
    assert.doesNotMatch(result.out, /resolved by the merge base/);
    assert.match(result.out, /conflict in 1 file:\n\s+docs\/development\.md/);
    git(other.root, 'merge', '--abort');
    repo.cleanup();
    other.cleanup();
  });

  test('the orientation index is written into each merge from the merged tree, and a conflict in it is resolved by that writing', async () => {
    // A stand-in for the index's tool: it lists the tree's .txt files, so two branches that each add one disagree on it.
    const generator = [
      "import { mkdirSync, readdirSync, writeFileSync } from 'node:fs';",
      "const files = readdirSync('.').filter((name) => name.endsWith('.txt')).sort();",
      "mkdirSync('docs/index', { recursive: true });",
      "writeFileSync('docs/index/README.md', files.join('\\n') + '\\n');",
      "console.log('orient-index: docs/index/ written, 1 of 1 files changed');",
      '',
    ].join('\n');
    // The folder and its command are the repository's declaration, as this repository's own are.
    const gates = [{ name: 'first', run: 'node gate.mjs first' }];
    const repo = scratch('generated', gates, {
      'daoris.gates.json': `${JSON.stringify({ gates, generated: [{ paths: 'docs/index', run: 'node tools/orient-index.mjs' }] }, null, 2)}\n`,
      '.gitattributes': 'CHANGELOG.md merge=union\ndocs/index/** -merge\n',
      'tools/orient-index.mjs': generator,
      'docs/index/README.md': 'base.txt\n',
      'base.txt': 'base\n',
      'shared.md': 'base\n',
    });
    const index = () => readFileSync(join(repo.root, 'docs', 'index', 'README.md'), 'utf8');
    const unmerged = () => git(repo.root, 'diff', '--name-only', '--diff-filter=U');

    // Both sides wrote the index for their own tree: the merge stops on it alone, and writes it for the merged one.
    branch(repo.root, 'left', { 'left.txt': 'l\n', 'docs/index/README.md': 'base.txt\nleft.txt\n' });
    writeFileSync(join(repo.root, 'main.txt'), 'm\n');
    writeFileSync(join(repo.root, 'docs', 'index', 'README.md'), 'base.txt\nmain.txt\n');
    git(repo.root, 'add', '-A');
    git(repo.root, 'commit', '--quiet', '-m', `main moved${TRAILER}`);
    let result = await repo.run(['left']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /docs\/index\/: written from the merged tree \(docs\/index\/ written, 1 of 1 files changed\)/);
    assert.equal(index(), 'base.txt\nleft.txt\nmain.txt\n');
    assert.equal(unmerged(), '');
    assert.match(git(repo.root, 'diff', '--cached', '--name-only'), /docs\/index\/README\.md/);
    assert.deepEqual(repo.ran(), ['first']);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // Beside a conflict a person resolves, the index waits: it is written once that one is resolved.
    branch(repo.root, 'right', { 'right.txt': 'r\n', 'shared.md': 'right\n', 'docs/index/README.md': 'right only\n' });
    writeFileSync(join(repo.root, 'shared.md'), 'main\n');
    writeFileSync(join(repo.root, 'docs', 'index', 'README.md'), 'main only\n');
    git(repo.root, 'commit', '--quiet', '-am', `main moved again${TRAILER}`);
    result = await repo.run(['right']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /conflict in 1 file:\n\s+shared\.md\n\s+\(and 1 generated file, written again from the merged tree once these are resolved\)/);
    assert.deepEqual(repo.ran(), ['first']);
    writeFileSync(join(repo.root, 'shared.md'), 'resolved\n');
    git(repo.root, 'add', 'shared.md');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.equal(index(), 'base.txt\nleft.txt\nmain.txt\nright.txt\n');
    assert.equal(unmerged(), '');
    assert.deepEqual(repo.ran(), ['first', 'first']);
    repo.cleanup();
  });

  test('two notes a union merge leaves touching under one decision are set apart in the merge, and at --continue after a conflict', async () => {
    const decision = '## D1 — a thing (2026-10-04)\n\nBody of it.\n';
    const note = (task: string, words: string) => `\n**${task}, built 2026-10-04: ${words}.** What it settled.\n`;
    // The merge's one gate is the real check `verify` runs, copied with the module it imports and what that starts git through.
    const repo = scratch('notes', [{ name: 'duplicates', run: 'node tools/doc-duplicates.mjs' }], {
      '.gitattributes': 'CHANGELOG.md merge=union\ndocs/decisions/*.md merge=union\n',
      'daoris.json': JSON.stringify({ documents: { decisions: 'docs/decisions' } }),
      'tools/doc-duplicates.mjs': readFileSync(join(repoRoot, 'tools', 'doc-duplicates.mjs'), 'utf8'),
      'tools/fsx.mjs': readFileSync(join(repoRoot, 'tools', 'fsx.mjs'), 'utf8'),
      ...Object.fromEntries(['tools/gitforwindows.ts', 'platform/fsx.ts', 'platform/errors.ts'].map((file) =>
        [`src/Daoris.Cli/src/${file}`, readFileSync(join(repoRoot, 'src', 'Daoris.Cli', 'src', file), 'utf8')])),
      'docs/decisions/D1.md': decision,
      'shared.md': 'base\n',
    });
    const file = () => readFileSync(join(repo.root, 'docs', 'decisions', 'D1.md'), 'utf8');
    const duplicates = () => spawnSync(process.execPath, ['tools/doc-duplicates.mjs'], { cwd: repo.root, encoding: 'utf8', windowsHide: true });

    // Main took one branch's note; a branch from before it appended its own to the same decision.
    branch(repo.root, 'right', { 'docs/decisions/D1.md': decision + note('UX6f', 'right') });
    writeFileSync(join(repo.root, 'docs', 'decisions', 'D1.md'), decision + note('UX6e', 'left'));
    git(repo.root, 'commit', '--quiet', '-am', `main took a note${TRAILER}`);

    // Without the tool: union keeps once the blank line both sides opened with, so the second label lands under the first note.
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-commit', 'right');
    assert.equal(file(), decision + note('UX6e', 'left') + note('UX6f', 'right').slice(1));
    const without = duplicates();
    assert.equal(without.status, 1, without.stdout);
    assert.match(without.stderr, /docs\/decisions\/D1\.md: a note label after a non-blank line: \*\*UX6f/);
    git(repo.root, 'merge', '--abort');

    // With it: one blank line before the gates choose, said with its file and line, staged, and the check passes as the gate.
    let result = await repo.run(['right']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /set apart: D1's UX6f note \(docs\/decisions\/D1\.md:7\)/);
    assert.ok(result.out.indexOf('set apart') < result.out.indexOf('gates for right'), 'set apart before the gates choose');
    assert.equal(file(), decision + note('UX6e', 'left') + note('UX6f', 'right'));
    assert.equal(git(repo.root, 'diff', '--name-only'), '', 'what was set apart is staged with the merge');
    assert.match(result.out, /PASS\s+duplicates/);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // Beside a conflict a person resolves, nothing is set apart until --continue.
    const merged = file();
    branch(repo.root, 'third', { 'docs/decisions/D1.md': merged + note('UX6c', 'third'), 'shared.md': 'third\n' });
    writeFileSync(join(repo.root, 'docs', 'decisions', 'D1.md'), merged + note('UX6b', 'main'));
    writeFileSync(join(repo.root, 'shared.md'), 'main\n');
    git(repo.root, 'commit', '--quiet', '-am', `main moved${TRAILER}`);
    result = await repo.run(['third']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /conflict in 1 file:\n\s+shared\.md/);
    assert.doesNotMatch(result.out, /set apart/);
    writeFileSync(join(repo.root, 'shared.md'), 'resolved\n');
    git(repo.root, 'add', 'shared.md');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /set apart: D1's UX6c note \(docs\/decisions\/D1\.md:11\)/);
    assert.equal(file(), merged + note('UX6b', 'main') + note('UX6c', 'third'));
    assert.match(result.out, /PASS\s+duplicates/);
    repo.cleanup();
  });

  test('two archive entries a union merge leaves touching are spaced in the merge, and a join main already held is left', async () => {
    // Main's archive holds one join from before, which the merge never made and so never touches.
    const archive = '# Archive\n\n## A1 — old (2026-10-01)\n\nDone.\n## A2 — glued long ago (2026-10-02)\n\nDone.\n';
    const entry = (task: string) => `\n## ${task} — built (2026-10-20)\n\n- [x] **${task}**\n**Outcome** 2026-10-20: ${task} landed.\n`;
    const repo = scratch('archive', [{ name: 'first', run: 'node gate.mjs first' }], {
      '.gitattributes': 'docs/task-archive.md merge=union\n',
      'docs/task-archive.md': archive,
    });
    const file = () => readFileSync(join(repo.root, 'docs', 'task-archive.md'), 'utf8');

    // Main took one branch's entry; a branch from before it appended its own.
    branch(repo.root, 'right', { 'docs/task-archive.md': archive + entry('RIGHT1') });
    writeFileSync(join(repo.root, 'docs', 'task-archive.md'), archive + entry('LEFT1'));
    git(repo.root, 'commit', '--quiet', '-am', `main took an entry${TRAILER}`);

    // Without the tool: union keeps once the blank line both sides opened with, so the second heading lands under the first entry.
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-commit', 'right');
    assert.equal(file(), archive + entry('LEFT1') + entry('RIGHT1').slice(1));
    git(repo.root, 'merge', '--abort');

    // With it: one blank line before the gates choose, said with its file and line, staged; the old join stays.
    const result = await repo.run(['right']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /set apart: docs\/task-archive\.md's heading "## RIGHT1 — built \(2026-10-20\)" \(docs\/task-archive\.md:15\)/);
    assert.ok(result.out.indexOf('set apart') < result.out.indexOf('gates for right'), 'set apart before the gates choose');
    assert.equal((result.out.match(/set apart/g) ?? []).length, 1, result.out);
    assert.equal(file(), archive + entry('LEFT1') + entry('RIGHT1'));
    assert.equal(git(repo.root, 'diff', '--name-only'), '', 'what was set apart is staged with the merge');
    repo.cleanup();
  });

  test('the knowledge server is rebuilt once a merge\'s gates pass, and a build that fails is said and fails nothing', async () => {
    // A stand-in for the server's tool: `build` notes itself and says what the real one says, failing on request.
    const server = [
      "import { appendFileSync, existsSync } from 'node:fs';",
      "appendFileSync('local/ran.txt', `knowledge-server ${process.argv.slice(2).join(' ')}\\n`);",
      "if (existsSync('local/fail-build')) { console.error('knowledge-server: the build failed (no build yet); sessions keep the build they had'); process.exit(1); }",
      "console.log('knowledge-server: built 20261004T100000Z-aaaaaaaa (no build yet) in 9 s; 0 older removed');",
      '',
    ].join('\n');
    const repo = scratch('knowledge-server', [{ name: 'first', run: 'node gate.mjs first' }], { 'tools/knowledge-server.mjs': server });

    branch(repo.root, 'one', { 'one.txt': '1\n' });
    let result = await repo.run(['one']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['first', 'knowledge-server build'], 'built after the gates, never before them');
    assert.match(result.out, /knowledge server: built 20261004T100000Z-aaaaaaaa \(no build yet\)/);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    // A failed gate builds nothing: the merge is not one to serve.
    branch(repo.root, 'two', { 'two.txt': '2\n' });
    writeFileSync(join(repo.root, 'daoris.gates.json'), `${JSON.stringify({ gates: [{ name: 'first', run: 'node gate.mjs first 1' }] }, null, 2)}\n`);
    git(repo.root, 'commit', '--quiet', '-am', `a failing gate${TRAILER}`);
    result = await repo.run(['two']);
    assert.equal(result.status, 1, result.out);
    assert.deepEqual(repo.ran(), ['first', 'knowledge-server build', 'first']);
    git(repo.root, 'merge', '--abort');

    writeFileSync(join(repo.root, 'daoris.gates.json'), `${JSON.stringify({ gates: [{ name: 'first', run: 'node gate.mjs first' }] }, null, 2)}\n`);
    git(repo.root, 'commit', '--quiet', '-am', `the gate passes again${TRAILER}`);
    writeFileSync(join(repo.root, 'local', 'fail-build'), '');
    result = await repo.run(['two']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /knowledge server: NOT rebuilt, and the merge stands \(knowledge-server: the build failed/);
    repo.cleanup();
  });

  test('a batch gates each merge, waits for the parent to commit it, runs the rehearsals once after the last, and names a flake', async () => {
    const repo = scratch('batch', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'check', run: 'dotnet run --project devkit -- check' },
      { name: 'driver', run: 'dotnet test fake/Driver.Tests --settings fake/process.runsettings' },
      { name: 'deployment', run: 'npm run rehearse:deploy' },
    ], {
      // The release rehearsal dies the first time with the shell's 127, through npm, and passes run again.
      'package.json': `${JSON.stringify({ name: 'scratch', private: true, scripts: {
        rehearse: 'node gate.mjs rehearse 127 once', 'rehearse:deploy': 'node gate.mjs deployment',
      } }, null, 2)}\n`,
      // `rehearse` is the workflow's alone, as the release and family rehearsals are this repository's.
      '.github/workflows/release.yml': 'jobs:\n  release:\n    steps:\n      - run: npm run rehearse:deploy\n      - run: npm run rehearse\n',
    });
    // Both append to the changelog at one place: the union attribute keeps both, with no conflict.
    const changelog = readFileSync(join(repo.root, 'CHANGELOG.md'), 'utf8');
    branch(repo.root, 'one', { 'one.txt': '1\n', 'CHANGELOG.md': `${changelog}- **One** landed, and its line is long enough.\n` });
    branch(repo.root, 'two', { 'two.txt': '2\n', 'CHANGELOG.md': `${changelog}- **Two** landed, and its line is long enough.\n` });

    // --full: the real-process half and the deployment rehearsal run in the full set only.
    let result = await repo.run(['one', '--batch', 'two', '--full']);
    assert.equal(result.status, 0, result.out);
    // The Process half runs beside the light lane, so only the Process lane's own order is fixed; and the
    // check and the CLI's gate build nothing in common, so they run side by side too.
    const ran = repo.ran();
    assert.deepEqual(unordered(ran), ['cli', 'dotnet-run', 'dotnet-test'], 'the rehearsals wait for the last branch');
    assert.match(result.out, /light\s+check, cli\n.*Process\s+driver/);
    assert.match(result.out, /in the plan's order:\n\s+PASS\s+check\s[^\n]*\n\s+PASS\s+cli\s[^\n]*\n\s+PASS\s+driver\s/);
    assert.match(result.out, /--continue/);

    // The next merge waits until this one is in main: an open merge, or an abandoned one, is refused.
    git(repo.root, 'merge', '--abort');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 2, result.out);
    assert.match(result.out, /not committed/);
    // However the parent lands it, what --continue asks is that the branch's tip is in main's history.
    git(repo.root, 'merge', '--quiet', '--no-ff', '--no-edit', 'one');

    // The second merge meets a real-process test that fails in the suite and passes alone, and a
    // rehearsal that dies once; told to, it runs every gate in turn, as before the lanes.
    result = await repo.run(['--continue', '--one-at-a-time'], { FAKE_DOTNET: 'flake' });
    assert.equal(result.status, 0, result.out);
    // The next merge's start prunes the branch the last one landed.
    assert.match(result.out, /removed\s+one\s+the branch/);
    assert.deepEqual(repo.ran().slice(0, 3), ran, 'the first merge\'s, as read above');
    assert.deepEqual(repo.ran().slice(3), [
      'dotnet-run', 'cli', 'dotnet-test', 'dotnet-test-alone FullyQualifiedName=N.ProcessJobTests.A_child',
      'rehearse', 'rehearse', 'deployment',
    ]);
    assert.match(result.out, /FLAKE\s+driver\s.*ProcessJobTests\.A_child.*driver\.rerun\.log/);
    assert.match(result.out, /FLAKE\s+rehearse\s.*exit 127.*passed when run again.*rehearse\.rerun\.log/);
    assert.match(result.out, /with 2 flakes \(driver, rehearse\)/);
    assert.doesNotMatch(result.out, /in the plan's order/, 'one gate at a time needs no summary: its lines are in order');
    const merged = git(repo.root, 'show', ':CHANGELOG.md');
    assert.match(merged, /\*\*One\*\*/);
    assert.match(merged, /\*\*Two\*\*/);
    assert.ok(existsSync(join(repo.root, 'local', 'scratch', 'merge-two', 'deployment.log')));

    git(repo.root, 'commit', '--quiet', '--no-edit');
    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /batch is done/);
    // And the batch's end prunes the last.
    assert.match(result.out, /removed\s+two\s+the branch/);
    assert.equal(git(repo.root, 'branch', '--list', 'one', 'two'), '');
    assert.equal(existsSync(join(repo.root, 'local', 'scratch', 'merge-batch.json')), false);
    repo.cleanup();
  });

  test('a merge runs the gates its paths reach, never the long ones, and a batch\'s last merge runs each rehearsal any of its merges reached', async () => {
    const repo = scratch('reach', [
      { name: 'cli', run: 'node gate.mjs cli' },
      { name: 'driver-process', run: 'dotnet test fake/Driver.Tests --settings fake/process.runsettings' },
      { name: 'rehearse-family', run: 'npm run rehearse:family' },
    ], {
      'package.json': `${JSON.stringify({ name: 'scratch', private: true, scripts: { 'rehearse:family': 'node gate.mjs rehearse-family' } }, null, 2)}\n`,
    });
    // The canon reaches the family rehearsal; a document reaches neither it nor the driver's Process half.
    branch(repo.root, 'canon-edit', { 'canon/core/rules/x.md': 'x\n' });
    branch(repo.root, 'docs-edit', { 'docs/y.md': 'y\n' });
    // A path no rule places reaches every gate, and a merge still leaves the Process half to the full set.
    branch(repo.root, 'unplaced-edit', { 'elsewhere/z.cs': 'z\n' });

    let plan = await repo.run(['--plan', 'docs-edit']);
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /gates it reaches: 1 of 3/);
    assert.match(plan.out, /driver-process\s+suite\s+in the full set before staging/);
    assert.match(plan.out, /2 gates of the plan skipped; the full set runs before the install is staged \(node tools\/merge-branch\.mjs --full/);
    plan = await repo.run(['--plan', 'unplaced-edit']);
    assert.match(plan.out, /gates it reaches: 2 of 3/);
    assert.match(plan.out, /driver-process\s+suite\s+in the full set before staging.*elsewhere\/z\.cs/);
    plan = await repo.run(['--plan', 'unplaced-edit', '--full']);
    assert.match(plan.out, /gates it reaches: 3 of 3 \(--full\)/);

    let result = await repo.run(['canon-edit', '--batch', 'docs-edit']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['cli'], 'the rehearsal it reaches waits for the batch\'s last merge');
    assert.match(result.out, /rehearse-family\s+rehearsal\s+waits for the batch's last branch \(docs-edit\)/);
    git(repo.root, 'commit', '--quiet', '--no-edit');

    result = await repo.run(['--continue']);
    assert.equal(result.status, 0, result.out);
    assert.deepEqual(repo.ran(), ['cli', 'cli', 'rehearse-family'], 'never the Process half, which only the full set runs');
    assert.match(result.out, /rehearse-family\s+rehearsal\s+reached by canon-edit, earlier in this batch/);
    assert.match(result.out, /1 gate of the plan skipped;/);
    repo.cleanup();
  });
});
