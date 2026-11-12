import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { type ChildProcess, spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { tool } from './support/merge-branch.ts';
import { git, TRAILER, age, scratch, branch } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row

describe('the tool, end to end in a scratch repository (e2e-prune)', { concurrency: true }, () => {

  test('--prune removes each merged branch with its worktree, and keeps one in use, changed or holding private output, saying why', async () => {
    const repo = scratch('prune', [{ name: 'first', run: 'node gate.mjs first' }], { '.gitignore': 'local/\nbuild/\n', 'tracked.txt': 'base\n' });
    const trees = join(dirname(repo.root), 'trees');
    const add = (name: string): string => {
      const path = join(trees, name);
      git(repo.root, 'worktree', 'add', '--quiet', '-b', name, path, 'main');
      return path;
    };
    // `ahead` has a commit main lacks, made before main moved on: never merged, so never considered.
    branch(repo.root, 'ahead', { 'ahead.txt': 'x\n' });
    git(repo.root, 'commit', '--quiet', '--allow-empty', '-m', `main moves on${TRAILER}`);
    git(repo.root, 'worktree', 'add', '--quiet', join(trees, 'ahead'), 'ahead');

    // Clean but for ignored build output, which a plain `git worktree remove` takes with it.
    const free = add('free');
    mkdirSync(join(free, 'build'), { recursive: true });
    writeFileSync(join(free, 'build', 'out.dll'), 'built\n');
    // At main's tip, so merged: the case a naive rule deletes while an agent works in it. Locked, it is in
    // use: `fresh` is an agent just dispatched, clean, and `in-use` one mid-edit, which is never looked into.
    const fresh = add('fresh');
    git(repo.root, 'worktree', 'lock', '--reason', 'claude agent agent-y (pid 2)', fresh);
    const inUse = add('in-use');
    git(repo.root, 'worktree', 'lock', '--reason', 'claude agent agent-x (pid 1)', inUse);
    writeFileSync(join(inUse, 'tracked.txt'), 'the agent is mid-edit\n');
    // Idle for hours, so only what the plan says of them decides (a tree written in the last two hours is kept).
    age(fresh, 300);
    age(inUse, 300);
    writeFileSync(join(add('changed'), 'tracked.txt'), 'changed\n');
    const kept = add('private');
    mkdirSync(join(kept, 'local'), { recursive: true });
    writeFileSync(join(kept, 'local', 'out.md'), 'an agent left this\n');
    const stray = add('stray');
    writeFileSync(join(stray, 'new.txt'), 'never added\n');
    // Its folder deleted by hand: git still lists the worktree, and holds the branch for it.
    rmSync(add('gone'), { recursive: true, force: true });
    git(repo.root, 'branch', 'plain');
    // Merged into main, but its upstream lacks main's last commit, so `git branch -d` refuses; nothing forces it.
    git(repo.root, 'branch', 'tracking');
    git(repo.root, 'config', 'branch.tracking.remote', '.');
    git(repo.root, 'config', 'branch.tracking.merge', 'refs/heads/ahead');
    // A folder git does not know is not the prune's to remove.
    mkdirSync(join(trees, 'orphan'), { recursive: true });
    writeFileSync(join(trees, 'orphan', 'keep.txt'), 'mine\n');
    const branches = () => git(repo.root, 'branch', '--format=%(refname:short)').split('\n');
    const every = branches();

    const plan = await repo.run(['--prune', '--plan']);
    assert.equal(plan.status, 0, plan.out);
    assert.match(plan.out, /prune \(a plan, so nothing is removed\): 9 branches merged into main/);
    assert.match(plan.out, /\n\s+keep\s+fresh\s+its worktree is locked by the agent harness \(claude agent agent-y \(pid 2\)\), and stays locked: its branch is at main's tip, as a just-dispatched agent's is\n/);
    assert.match(plan.out, /\n\s+remove\s+free\s+its worktree \.\.\/trees\/free and the branch\n/);
    assert.match(plan.out, /\n\s+remove\s+gone\s+its worktree's record \(the folder \.\.\/trees\/gone is gone already\) and the branch\n/);
    assert.match(plan.out, /\n\s+remove\s+plain\s+the branch \(no worktree has it checked out\)\n/);
    assert.match(plan.out, /\n\s+remove\s+tracking\s/, 'only the deletion itself can hear git refuse');
    const inUseLine = plan.out.split('\n').find((line) => /^\s+keep\s+in-use\s/.test(line)) ?? '';
    assert.match(inUseLine, /its worktree is locked by the agent harness \(claude agent agent-x \(pid 1\)\), and stays locked: /);
    assert.match(inUseLine, /main's tip, as a just-dispatched agent's is; it has modified or staged tracked files \(tracked\.txt\)$/, 'the harness\'s lock is looked behind (CLEANUP2)');
    assert.match(plan.out, /\n\s+keep\s+changed\s+its worktree has modified or staged tracked files \(tracked\.txt\)\n/);
    assert.match(plan.out, /\n\s+keep\s+private\s+its worktree has private output under local\/ \(local\/out\.md\), which nothing else keeps\n/);
    assert.match(plan.out, /\n\s+keep\s+stray\s+its worktree has untracked files git removes only with --force \(new\.txt\)\n/);
    assert.ok(!plan.out.split('\n').some((line) => /^\s+(remove|keep)\s+(ahead|main)\s/.test(line)), 'an unmerged branch, and main, are never considered');
    assert.deepEqual(branches(), every, '--plan removes nothing');
    assert.ok(existsSync(free));

    const result = await repo.run(['--prune']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /prune: 9 branches merged into main\n/);
    for (const name of ['free', 'gone', 'plain']) assert.match(result.out, new RegExp(`\\n\\s+removed\\s+${name}\\s`));
    for (const name of ['changed', 'fresh', 'in-use', 'private', 'stray']) assert.match(result.out, new RegExp(`\\n\\s+kept\\s+${name}\\s`));
    assert.match(result.out, /\n\s+kept\s+tracking\s+git refused to delete the branch, which is never forced: error: the branch 'tracking' is not fully merged/);

    assert.deepEqual(branches(), ['ahead', 'changed', 'fresh', 'in-use', 'main', 'private', 'stray', 'tracking']);
    const listed = tool.parseWorktrees(git(repo.root, 'worktree', 'list', '--porcelain'));
    assert.deepEqual(listed.map((t) => t.branch).sort(), ['ahead', 'changed', 'fresh', 'in-use', 'main', 'private', 'stray']);
    assert.ok(listed.filter((t) => t.locked).map((t) => t.branch).sort().join() === 'fresh,in-use', 'a locked worktree is never unlocked');
    assert.equal(readFileSync(join(inUse, 'tracked.txt'), 'utf8'), 'the agent is mid-edit\n');
    assert.equal(existsSync(free), false, "the removed worktree's folder is gone, its ignored build output with it");
    assert.ok(existsSync(join(kept, 'local', 'out.md')) && existsSync(join(stray, 'new.txt')));
    assert.equal(readFileSync(join(trees, 'orphan', 'keep.txt'), 'utf8'), 'mine\n');
    repo.cleanup();
  });

  // A shared `core.worktree` (under `extensions.worktreeConfig`) makes a linked worktree report the main checkout's root.
  test('--prune never removes through a worktree git binds to the main checkout, and goes on where the value is in the main checkout\'s own worktree configuration', async () => {
    const repo = scratch('prune-bound', [{ name: 'first', run: 'node gate.mjs first' }], { '.gitignore': 'local/\n', 'tracked.txt': 'base\n' });
    const trees = join(dirname(repo.root), 'trees');
    const bound = join(trees, 'bound');
    git(repo.root, 'worktree', 'add', '--quiet', '-b', 'bound', bound, 'main');
    git(repo.root, 'config', 'extensions.worktreeConfig', 'true');
    git(repo.root, 'config', 'core.worktree', repo.root);
    const toplevel = (): string => git(bound, 'rev-parse', '--show-toplevel').replace(/\\/g, '/').toLowerCase();
    const answer = toplevel();
    console.log(`git answers for the linked worktree: ${answer} (the main checkout is ${repo.root})`);
    assert.equal(answer, repo.root.replace(/\\/g, '/').toLowerCase(), 'the fixture binds the linked worktree to the main checkout');
    const before = () => [git(repo.root, 'rev-parse', 'HEAD'), git(repo.root, 'ls-files', '--stage'), readFileSync(join(repo.root, 'tracked.txt'), 'utf8')].join('\n--\n');
    const was = before();

    const refused = await repo.run(['--prune']);
    assert.match(refused.out, /\n\s+kept\s+bound\s.*reports .* as its working root: a core\.worktree in the repository's shared git configuration/);
    assert.match(refused.out, /nothing was reset, checked out or removed/);
    assert.equal(before(), was, "the main checkout's HEAD, index and files are unchanged");
    assert.ok(existsSync(join(bound, '.git')), 'the worktree stays');
    assert.ok(git(repo.root, 'branch', '--format=%(refname:short)').split('\n').includes('bound'), 'its branch stays');
    assert.equal(git(repo.root, 'config', '--get', 'core.worktree'), repo.root, 'the tool writes no git configuration');

    // The value where it belongs: the main checkout's own worktree configuration.
    git(repo.root, 'config', '--unset', 'core.worktree');
    git(repo.root, 'config', '--worktree', 'core.worktree', repo.root);
    assert.equal(toplevel().includes('/trees/bound'), true, 'now the linked worktree reports its own root');
    const result = await repo.run(['--prune']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /\n\s+removed\s+bound\s/);
    assert.equal(existsSync(bound), false);
    assert.equal(before(), was);
    repo.cleanup();
  });

  // The leftovers a prune meets: worktrees kept for worker logs under local/, worktrees under the harness's lock, which it
  // holds for its session's life, and a half-removed folder that leaked processes hold.
  test('--prune lifts the harness\'s lock from a merged worktree left with nothing, takes worker logs and half-removed folders, and names what holds the rest', async () => {
    const repo = scratch('prune-leftovers', [{ name: 'first', run: 'node gate.mjs first' }], { '.gitignore': 'local/\n.claude/worktrees/\n', 'tracked.txt': 'base\n' });
    const trees = join(dirname(repo.root), 'trees');
    const holders: ChildProcess[] = [];
    // A process that names `named` in its command line, as the two `node land.mjs` a test left running named theirs.
    const hold = (named: string, cwd = dirname(repo.root), args = [join(named, 'held')]): ChildProcess => {
      const child = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)', ...args], { cwd, stdio: 'ignore', windowsHide: true });
      holders.push(child);
      return child;
    };
    try {
      const add = (name: string, reason?: string): string => {
        const path = join(trees, name);
        git(repo.root, 'worktree', 'add', '--quiet', '-b', name, path, 'main');
        if (reason !== undefined) git(repo.root, 'worktree', 'lock', '--reason', reason, path);
        return path;
      };
      const harness = (id: string) => `claude agent agent-${id} (pid 4242)`;
      const done = add('done', harness('d'));
      const busy = add('busy', harness('b'));
      const busyHolder = hold(busy);
      const logs = add('logs', harness('l'));
      for (const path of ['local/scratch/run/out.md', 'local/gate.log']) {
        mkdirSync(dirname(join(logs, path)), { recursive: true });
        writeFileSync(join(logs, path), 'a worker wrote this\n');
      }
      const notes = add('notes');
      for (const path of ['local/scratch/a.log', 'local/notes.md']) {
        mkdirSync(dirname(join(notes, path)), { recursive: true });
        writeFileSync(join(notes, path), 'a worker wrote this\n');
      }
      add('owned', 'the owner keeps this');
      // A worker started minutes ago, clean, merged, behind main's tip and with no process naming its folder
      // (a shell's working folder is not on a command line) is kept; the others were last written hours ago.
      const young = add('young', harness('y'));
      for (const path of [done, busy, logs]) age(path, 300);
      // Main moves on, so these merged branches stand behind its tip, as a finished agent's always does.
      git(repo.root, 'commit', '--quiet', '--allow-empty', '-m', `main moves on${TRAILER}`);

      const worktrees = join(repo.root, '.claude', 'worktrees');
      const outside = join(dirname(repo.root), 'outside');
      mkdirSync(outside, { recursive: true });
      writeFileSync(join(outside, 'keep.txt'), 'never followed\n');
      const half = join(worktrees, 'agent-h');
      mkdirSync(join(half, 'examples', 'plugins', 'p'), { recursive: true });
      writeFileSync(join(half, 'examples', 'plugins', 'p', 'land.mjs'), 'left\n');
      symlinkSync(outside, join(half, 'linked'), 'junction');
      symlinkSync(outside, join(worktrees, 'agent-link'), 'junction');
      const held = join(worktrees, 'agent-held');
      mkdirSync(held, { recursive: true });
      writeFileSync(join(held, 'a.txt'), 'held\n');
      const heldHolder = hold(held, held);
      mkdirSync(join(worktrees, 'agent-git'), { recursive: true });
      writeFileSync(join(worktrees, 'agent-git', '.git'), 'gitdir: elsewhere\n');
      // Windows keeps a folder that is a process's working folder, and that process's command line need not name it.
      const stuck = join(worktrees, 'agent-stuck');
      const windows = process.platform === 'win32';
      if (windows) {
        mkdirSync(stuck, { recursive: true });
        writeFileSync(join(stuck, 'b.txt'), 'stuck\n');
        hold(stuck, stuck, []);
      }
      const folders = windows ? 5 : 4;

      const branches = () => git(repo.root, 'branch', '--format=%(refname:short)').split('\n');
      const every = branches();
      const listing = () => tool.parseWorktrees(git(repo.root, 'worktree', 'list', '--porcelain'));
      const before = listing();

      const plan = await repo.run(['--prune', '--plan']);
      assert.equal(plan.status, 0, plan.out);
      assert.match(plan.out, new RegExp(`prune \\(a plan, so nothing is removed\\): 6 branches merged into main; ${folders} folders under \\.claude/worktrees/ git does not list\\n`));
      assert.match(plan.out, /\n\s+keep\s+young\s+its worktree is locked by the agent harness \(claude agent agent-y \(pid 4242\)\), and stays locked: written \d+ minutes? ago, a worker may be in it\n/);
      assert.match(plan.out, /\n\s+remove\s+done\s+its worktree \.\.\/trees\/done, unlocked from the agent harness \(claude agent agent-d \(pid 4242\)\), and the branch\n/);
      assert.match(plan.out, new RegExp(`\\n\\s+keep\\s+busy\\s+its worktree is locked by the agent harness \\(claude agent agent-b \\(pid 4242\\)\\), and stays locked: a running process is inside it \\(pid ${busyHolder.pid}: `));
      assert.match(plan.out, /\n\s+remove\s+logs\s+its worktree \.\.\/trees\/logs, unlocked from the agent harness/, 'worker logs keep nothing');
      assert.match(plan.out, /\n\s+keep\s+notes\s+its worktree has private output under local\/ \(local\/notes\.md\), which nothing else keeps\n/);
      assert.match(plan.out, /\n\s+keep\s+owned\s+its worktree is locked, so in use \(the owner keeps this\); it is never looked into or unlocked here\n/);
      assert.match(plan.out, /\n\s+remove\s+\.claude\/worktrees\/agent-h\s+a folder git does not list, with no \.git: what a worktree removal that did not finish left\n/);
      assert.match(plan.out, /\n\s+keep\s+\.claude\/worktrees\/agent-link\s+a link \(a reparse point\), never followed or removed here\n/);
      assert.match(plan.out, new RegExp(`\\n\\s+keep\\s+\\.claude/worktrees/agent-held\\s+.*a running process is inside it \\(pid ${heldHolder.pid}: `));
      assert.match(plan.out, /\n\s+keep\s+\.claude\/worktrees\/agent-git\s+.*holds a \.git/);
      if (windows) assert.match(plan.out, /\n\s+remove\s+\.claude\/worktrees\/agent-stuck\s/, 'only the removal itself finds the folder held');
      assert.deepEqual(branches(), every, '--plan removes no branch');
      assert.deepEqual(listing(), before, '--plan removes, unlocks and locks no worktree');
      for (const path of [done, logs, half, held, join(worktrees, 'agent-link', 'keep.txt')]) assert.ok(existsSync(path), path);

      const result = await repo.run(['--prune']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, new RegExp(`prune: 6 branches merged into main; ${folders} folders under \\.claude/worktrees/ git does not list\\n`));
      assert.match(result.out, /\n\s+removed\s+done\s+its worktree \.\.\/trees\/done, unlocked from the agent harness/);
      assert.match(result.out, /\n\s+removed\s+logs\s/);
      for (const name of ['busy', 'notes', 'owned', 'young']) assert.match(result.out, new RegExp(`\\n\\s+kept\\s+${name}\\s`));
      assert.match(result.out, /\n\s+removed\s+\.claude\/worktrees\/agent-h\s/);
      for (const name of ['agent-link', 'agent-held', 'agent-git']) assert.match(result.out, new RegExp(`\\n\\s+kept\\s+\\.claude/worktrees/${name}\\s`));
      if (windows) {
        assert.match(result.out, /\n\s+kept\s+\.claude\/worktrees\/agent-stuck\s+it could not be removed \(E[A-Z]+\b[^)]*\); no running process names it in its command line, so one holds it another way \(its working folder, an open file\); the next prune tries again\n/);
      }

      assert.deepEqual(branches(), ['busy', 'main', 'notes', 'owned', 'young']);
      const after = listing();
      assert.deepEqual(after.filter((t) => t.locked).map((t) => [t.branch, t.lockReason]).sort(), [['busy', harness('b')], ['owned', 'the owner keeps this'], ['young', harness('y')]]);
      assert.ok(existsSync(young), 'a worker written minutes ago keeps its tree, locked as it was (CLEANUP2b)');
      assert.equal(existsSync(done), false);
      assert.equal(existsSync(logs), false, 'the worker logs went with their worktree');
      assert.equal(existsSync(half), false, 'the half-removed folder is gone');
      assert.equal(readFileSync(join(outside, 'keep.txt'), 'utf8'), 'never followed\n', 'a link inside is removed, never followed');
      assert.ok(existsSync(join(worktrees, 'agent-link')) && existsSync(join(held, 'a.txt')) && existsSync(join(worktrees, 'agent-git', '.git')));
      assert.ok(existsSync(busy) && existsSync(join(notes, 'local', 'notes.md')));
      assert.equal(busyHolder.exitCode, null, 'a process is never stopped by the prune');
    } finally {
      await Promise.all(holders.map((child) => new Promise<void>((done) => {
        if (child.exitCode !== null || child.signalCode !== null) return done();
        child.once('exit', () => done());
        child.kill();
      })));
      repo.cleanup();
    }
  });
});
