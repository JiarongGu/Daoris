import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { PATIENCE } from '../test/_fixture.ts';
import { tool, type Verdict } from './support/merge-branch.ts';
import { within, alive, TREE, treePids, stillRunning, cleanupAfterKill, git, TRAILER, scratch, branch } from './support/merge-branch-kit.ts';

// Split from the merge tool's one test file by concern (MERGEFLOW1); the fixtures and helpers are in support/merge-branch-kit.ts.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row

describe('the tool, end to end in a scratch repository (e2e-beside)', { concurrency: true }, () => {

  // A full set run in the main checkout leaves no merge able to run for its hour and a half.
  test('--full --beside gates main\'s tip in a worktree of its own while a merge lands, its verdicts in the main checkout\'s store', async () => {
    // The install's output is ignored, as this repository's is: the tree gated is the commit's.
    const repo = scratch('beside', [{ name: 'first', run: 'node gate.mjs first' }], {
      '.gitignore': 'local/\nnode_modules/\n', 'code.txt': 'base\n', 'package-lock.json': '{"lockfileVersion":3}\n',
    });
    branch(repo.root, 'feature', { 'feature.txt': 'x\n' });
    const gated = git(repo.root, 'rev-parse', 'HEAD');
    const sha = gated.slice(0, 7);
    const gateTree = join(repo.root, 'local', 'gate-tree');
    const hold = join(dirname(repo.root), 'hold');
    // `npm`, faked: `ci` lays node_modules where it runs and notes it, as the real one installs from the lockfile.
    const npmBin = join(dirname(repo.root), 'npm-bin');
    const npmLog = join(dirname(repo.root), 'npm.log');
    mkdirSync(npmBin, { recursive: true });
    writeFileSync(join(npmBin, 'fake-npm.mjs'), [
      "import { appendFileSync, mkdirSync } from 'node:fs';",
      "mkdirSync('node_modules', { recursive: true });",
      `appendFileSync(${JSON.stringify(npmLog)}, process.argv.slice(2).join(' ') + '\\n');`,
      '',
    ].join('\n'));
    if (process.platform === 'win32') writeFileSync(join(npmBin, 'npm.cmd'), '@echo off\r\nnode "%~dp0fake-npm.mjs" %*\r\n');
    else {
      writeFileSync(join(npmBin, 'npm'), '#!/bin/sh\nexec node "$(dirname "$0")/fake-npm.mjs" "$@"\n');
      chmodSync(join(npmBin, 'npm'), 0o755);
    }
    const PATH = `${npmBin}${delimiter}${join(dirname(repo.root), 'bin')}${delimiter}${process.env['PATH'] ?? process.env['Path'] ?? ''}`;
    const npmRan = () => (existsSync(npmLog) ? readFileSync(npmLog, 'utf8').split('\n').filter(Boolean) : []);
    const ranThere = join(gateTree, 'local', 'ran.txt');

    // Named, though it is main's tip: the merge that lands meanwhile would supersede a run of main's tip.
    const beside = repo.run(['--full', '--beside', gated], { GATE_HOLD: hold, PATH });
    try {
      // Bounded as waitFor is, five times the shared patience: the gate tree's gate has started, and holds until the test lets
      // it go. A fixed count of 100 ms tries is outlasted by a loaded machine's start; the
      // shared patience alone, two minutes, is shorter still. The start is a
      // worktree, an install and the full set's first gate, not one process.
      for (const end = Date.now() + 5 * PATIENCE; Date.now() < end && !(existsSync(ranThere) && readFileSync(ranThere, 'utf8').includes('first'));) {
        await new Promise((done) => setTimeout(done, 100));
      }
      assert.ok(existsSync(ranThere), 'the full set runs in the gate tree');
      assert.equal(git(gateTree, 'rev-parse', 'HEAD'), gated, 'detached at main\'s tip');
      assert.ok(existsSync(join(repo.root, 'local', 'gate-tree.lock')), 'the run holds its lock in the main checkout');

      // One at a time.
      let result = await repo.run(['--full', '--beside']);
      assert.equal(result.status, 2, result.out);
      assert.match(result.out, new RegExp(`a full set beside the merges is running already: on ${sha} since .*\\(pid \\d+, local/gate-tree\\); one runs at a time`));

      // A merge lands meanwhile, and its prune keeps the gate tree.
      result = await repo.run(['feature']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, new RegExp(`\\n\\s+kept\\s+local/gate-tree\\s+its full set is running \\(pid \\d+, since [^)]+, on ${sha}\\): never removed while it runs\\n`));
      git(repo.root, 'commit', '--quiet', '-m', `merge feature${TRAILER}`);
      result = await repo.run(['--prune']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /\n\s+kept\s+local\/gate-tree\s+its full set is running/);
      assert.ok(existsSync(gateTree));
    } finally {
      writeFileSync(hold, '');
    }
    const done = await beside;
    assert.equal(done.status, 0, done.out);
    assert.match(done.out, new RegExp(`the gate tree: made at ${sha} \\(local/gate-tree\\)`));
    assert.match(done.out, /installed \. from its lockfile \(npm ci\)/);
    assert.match(done.out, /under load: a full set beside the merges/i);
    assert.match(done.out, new RegExp(`merge-branch: the full set passed ${sha}`));
    assert.match(done.out, new RegExp(`npm run publish:desktop -- --to <install> --service --stage --from-gated ${sha}`));
    assert.deepEqual(npmRan(), ['ci --no-audit --no-fund'], 'installed once, in the gate tree\'s root folder');
    assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'the lock is let go');

    // Its verdict is in the main checkout's store, keyed by the tree it gated; none is in the gate tree's own.
    const record = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { verdicts: Verdict[] };
    // The merge's own verdict is there too, on the merged tree, with the branch it merged.
    const there = record.verdicts.filter((verdict) => verdict.tree === git(repo.root, 'rev-parse', `${gated}^{tree}`));
    assert.deepEqual(there.map((verdict) => `${verdict.gate} ${verdict.verdict} ${verdict.commit} ${verdict.branch}`), [`first PASS ${gated} null`]);
    assert.deepEqual(record.verdicts.filter((verdict) => verdict.branch === 'feature').map((verdict) => verdict.gate), ['first']);
    assert.equal(existsSync(join(gateTree, 'local', 'gate-verdicts.json')), false);
    // `--passed` asked in the gate tree, as its stage asks, reads the main checkout's store.
    let result = await repo.run(['--passed'], {}, gateTree);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /first\s+PASS\s+\d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC\s+on this tree/);

    // Run again at the same commit, it is reused and installs nothing: its lockfile is as installed.
    result = await repo.run(['--full', '--beside', gated], { PATH });
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, new RegExp(`the gate tree: reused at ${sha} \\(local/gate-tree\\)`));
    assert.equal(npmRan().length, 1);

    // --prune removes it once its run has ended, and says so.
    result = await repo.run(['--prune']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, new RegExp(`\\n\\s+removed\\s+local/gate-tree\\s+the gate tree at ${sha}, never staged`));
    assert.equal(existsSync(gateTree), false);
    assert.equal(tool.parseWorktrees(git(repo.root, 'worktree', 'list', '--porcelain')).length, 1);
    repo.cleanup();
  });

  // A gate on an old tip would run to its end, up to an hour of the machine's time, for a verdict nobody uses.
  test('--full --beside on main\'s tip ends the gate it is running, with its whole tree, once main moves on, and is recorded superseded', async () => {
    const repo = scratch('beside-superseded', [{ name: 'held', run: 'node tree.mjs' }, { name: 'after', run: 'node gate.mjs after' }], { 'tree.mjs': TREE });
    const at = join(dirname(repo.root), 'pids');
    mkdirSync(at, { recursive: true });
    const from = git(repo.root, 'rev-parse', 'HEAD');
    const sha = from.slice(0, 7);
    const pids: number[] = [];
    const beside = repo.run(['--full', '--beside', '--one-at-a-time'], { TREE_PIDS: at });
    try {
      // Bounded: the tool, its gate tree and the gate start first, under the rest of the suite's load.
      pids.push(...await treePids(at));
      writeFileSync(join(repo.root, 'next.txt'), 'next\n');
      git(repo.root, 'add', '-A');
      git(repo.root, 'commit', '--quiet', '-m', `next${TRAILER}`);
      const to = git(repo.root, 'rev-parse', 'HEAD');
      // The gate waits ten minutes otherwise; the poll sees the move within seconds, and the shared patience is load's margin.
      let done = { status: null as number | null, out: '' };
      await within(beside.then((answer) => { done = answer; }), 'the run ending once main moved on');
      assert.equal(done.status, 1, done.out);
      assert.deepEqual(await stillRunning(pids), [], `the gate's child and grandchild still run:\n${done.out}`);
      assert.match(done.out, new RegExp(`superseded: main moved from ${sha} to ${to.slice(0, 7)}, so this run ended the gates it was running and started no more; it is not a pass`));
      assert.match(done.out, /ENDED\s+held\s.*\(ended, not failed: main moved from/);
      assert.match(done.out, /ran: none\n\s+ended: held\n\s+left: after/);
      assert.doesNotMatch(done.out, /FAIL\s+held/);
      assert.match(done.out, /Run it again on the new tip/);

      const record = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-tree.json'), 'utf8')) as { commit: string; passed: boolean | null; superseded?: string };
      assert.deepEqual([record.commit, record.passed, record.superseded], [from, false, to]);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'the lock is let go');
      // Its log says it is ended, and an ended gate gives no verdict on the tree.
      assert.match(readFileSync(join(repo.root, 'local', 'scratch', `beside-${sha}`, 'held.log'), 'utf8'), /\(ended, not failed: main moved from/);
      const store = join(repo.root, 'local', 'gate-verdicts.json');
      const verdicts = existsSync(store) ? (JSON.parse(readFileSync(store, 'utf8')) as { verdicts: Verdict[] }).verdicts : [];
      assert.deepEqual(verdicts.filter((verdict) => verdict.gate === 'held'), []);
    } finally {
      for (const pid of pids) if (alive(pid)) process.kill(pid);
      await stillRunning(pids);
      await beside;
      await cleanupAfterKill(repo);
    }
  });

  // The gate tree's universal gate fails for want of the private patterns file, and a prune
  // after the stage would delete the only copy of a failed run's logs.
  test('--full --beside copies the declared private file into the tree on every run and keeps the run\'s logs past --prune', async () => {
    const needs = [
      "import { existsSync, readFileSync } from 'node:fs';",
      "const file = 'local/sensitive-patterns.txt';",
      "if (!existsSync(file)) { console.log('missing ' + file); process.exit(1); }",
      "const text = readFileSync(file, 'utf8');",
      'console.log(`read ${text.trim()}`);',
      "process.exit(text.startsWith('bad') ? 1 : 0);",
      '',
    ].join('\n');
    const repo = scratch('beside-private', [{ name: 'needs', run: 'node needs.mjs' }], {
      'needs.mjs': needs,
      'daoris.gates.json': `${JSON.stringify({ gates: [{ name: 'needs', run: 'node needs.mjs' }], sensitive: { patternsFile: 'local/sensitive-patterns.txt' } })}\n`,
    });
    const sha = git(repo.root, 'rev-parse', 'HEAD').slice(0, 7);
    const gateTree = join(repo.root, 'local', 'gate-tree');
    const mine = join(repo.root, 'local', 'sensitive-patterns.txt');
    const theirs = join(gateTree, 'local', 'sensitive-patterns.txt');
    const kept = join(repo.root, 'local', 'scratch', `beside-${sha}`);

    // Without the file the run goes on and says so; the gate's own answer decides.
    let result = await repo.run(['--full', '--beside']);
    assert.equal(result.status, 1, result.out);
    assert.match(result.out, /local\/sensitive-patterns\.txt.*missing/);

    writeFileSync(mine, 'good\n');
    result = await repo.run(['--full', '--beside']);
    assert.equal(result.status, 0, result.out);
    assert.match(result.out, /the gate tree: reused/);
    assert.equal(readFileSync(theirs, 'utf8'), 'good\n');
    assert.deepEqual(git(gateTree, 'status', '--porcelain'), '', 'ignored, so the tree is still clean');
    assert.match(readFileSync(join(kept, 'needs.log'), 'utf8'), /read good/);

    // Refreshed on a reused tree; a failed run's logs are kept, and its prune removes the tree but not them.
    writeFileSync(mine, 'bad\n');
    result = await repo.run(['--full', '--beside']);
    assert.equal(result.status, 1, result.out);
    assert.equal(readFileSync(theirs, 'utf8'), 'bad\n');
    result = await repo.run(['--prune']);
    assert.equal(result.status, 0, result.out);
    assert.equal(existsSync(gateTree), false);
    assert.match(readFileSync(join(kept, 'needs.log'), 'utf8'), /read bad/, 'the newest run\'s log outlives the tree');
    repo.cleanup();
  });
});
