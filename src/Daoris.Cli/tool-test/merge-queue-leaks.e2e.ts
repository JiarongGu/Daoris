/**
 * Nothing the merge tool and its tests start outlives what started it, end to end, and what a dropped run's tests had left running under the scope tree.
 */
import { spawn, spawnSync } from 'node:child_process';
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { writeFileSync, mkdirSync, existsSync, readFileSync } from 'node:fs';
import { type Member, q, AT, type Queue, tool, type Candidate, procs, type ProcessRow, type Drained } from './support/merge-queue-kit.ts';
import { queueScratch, alive, treeGate, TRAILER, TREE, pidsIn } from './support/merge-queue-scratch.ts';
import { CASES_AT_ONCE } from './support/e2e-repository.ts';
import { PATIENCE } from '../test/_fixture.ts';

// What the merge tool and its tests leave running slows the machine: waiters whose requests
// are gone, runners no record names, a stand-in polling for a file its killed test never wrote.
/** A process of the test's own that does nothing, ended by `kill`: a live pid a record can name, and an owner that can end. */
const idle = () => spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'ignore', windowsHide: true });

/** A process whose parent is gone and whose command line names `marker`: started by a node that ended at once. Answers its pid. */
function orphanNaming(marker: string): number {
  const maker = spawnSync(process.execPath, ['-e',
    `const c = require('node:child_process').spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)', ${JSON.stringify(marker)}], { detached: true, stdio: 'ignore', windowsHide: true }); console.log(c.pid); c.unref();`],
  { encoding: 'utf8', windowsHide: true });
  return Number(maker.stdout.trim());
}

describe('Nothing the merge tool and its tests start outlives what started it', { concurrency: CASES_AT_ONCE }, () => {
  const request = (branch: string, id: string): Member => ({ branch, tip: `${branch}-tip`, request: id });
  const asked = (member: Member) => ({ branch: member.branch, tip: member.tip, request: member.request! });

  test('a runner stands while the queue names it, and is told so where the queue names another or none', () => {
    const queue = { ...q.emptyQueue(), runner: { pid: 41, started: AT } } as Queue;
    assert.deepEqual(q.runnerStanding(queue, 41), { stands: true });
    const other = q.runnerStanding(queue, 42);
    assert.equal(other.stands, false);
    assert.equal((other as { why: string }).why, 'the queue names pid 41 as its runner');
    const none = q.runnerStanding({ ...queue, runner: null }, 42);
    assert.equal(none.stands, false);
    assert.equal((none as { why: string }).why, 'the queue names no runner');
    assert.equal(q.runnerStanding({ ...q.emptyQueue() }, 42).stands, false, 'a record that was never written names none');
  });

  test('an owner a runner was started with ends it when the owner is gone; one with no owner has none to lose', () => {
    assert.equal(tool.ownerEnded({}, () => false), null, 'no owner named: a runner started by a person has none');
    assert.equal(tool.ownerEnded({ MERGE_BRANCH_QUEUE_OWNER: '' }, () => false), null);
    assert.equal(tool.ownerEnded({ MERGE_BRANCH_QUEUE_OWNER: 'soon' }, () => false), null, 'a value that is no pid names no owner');
    assert.equal(tool.ownerEnded({ MERGE_BRANCH_QUEUE_OWNER: '77' }, (pid) => pid === 77), null, 'the owner runs');
    assert.equal(tool.ownerEnded({ MERGE_BRANCH_QUEUE_OWNER: '77' }, () => false), 'its owner (pid 77), the process that started it, has ended');
  });

  test('a candidate run asks its runner too: a watch whose runner is no longer the queue\'s ends every step it owns, naming why', () => {
    const ended: number[] = [];
    const trees = tool.stepTrees({ end: (pid) => { ended.push(pid); } });
    trees.hold(4242);
    const candidate: Candidate = { base: 'b0', tip: 'c1', members: [{ branch: 'b1', tip: 't1', request: 'r1', commit: 'k1' }], state: 'running', reforms: 0, recordsOnly: false };
    let lost: string | null = null;
    const watch = tool.watchCandidate(candidate, () => candidate, trees, { superseded: () => lost });
    assert.equal(watch.check(), false, 'the candidate stands and so does its runner');
    lost = "this runner is no longer the queue's";
    assert.equal(watch.check(), true);
    assert.equal(trees.ended, "this runner is no longer the queue's");
    assert.deepEqual(ended, [4242]);
  });

  test("a request's wait ends only with an outcome, and a request kept through a re-form, a contained member or a hand-back is never lost between them", () => {
    const [one, two] = [request('b1', 'r1'), request('b2', 'r2')];
    const standing = (queue: Queue, member: Member) => q.requestStanding(queue, asked(member));
    const empty = q.emptyQueue();
    const first: Candidate = { base: 'm0', tip: 'c1', members: [{ ...one, commit: 'k1' }], state: 'running', reforms: 0, recordsOnly: false };
    // Re-formed: the candidate ends and its requests stand as `forming`, then as the next candidate's members; main holds them then.
    assert.equal(standing({ ...empty, candidate: first }, one).pending, 'candidate');
    assert.equal(standing({ ...empty, candidate: null, forming: { base: 'm1', members: [one] } }, one).pending, 'forming');
    const second = { ...first, base: 'm1', tip: 'c2', reforms: 1 };
    assert.equal(standing({ ...empty, candidate: second }, one).pending, 'candidate');
    assert.equal(tool.waitSaid(standing({ ...empty, candidate: second }, one), { branch: 'b1' }), null);
    const landed = standing({ ...empty, landed: [{ ...one, commit: 'k2' }] }, one);
    assert.equal(tool.waitSaid(landed, { branch: 'b1' })!.code, 0);
    // Contained in another member: pending while its container runs, and with the container's outcome it has its own.
    const holding: Candidate = { ...first, contained: [{ ...two, containedIn: { branch: 'b1', request: 'r1' } }] };
    assert.equal(standing({ ...empty, candidate: holding }, two).pending, 'contained');
    assert.equal(tool.waitSaid(standing({ ...empty, candidate: holding }, two), { branch: 'b2' }), null);
    const settled = q.settleContained(holding, AT);
    const promoted = { ...empty, landed: [{ ...one, commit: 'k1' }], contained: settled };
    assert.match(tool.waitSaid(standing(promoted, two), { branch: 'b2' })!.lines[0]!, /b2 is contained in b1, which promoted/);
    assert.equal(tool.waitSaid(standing(promoted, two), { branch: 'b2' })!.code, 0);
    // The container handed back: the contained one is its own request again, forming, and still pending.
    const again = q.requestsOf(holding, []);
    assert.deepEqual(again.map((each) => each.request), ['r2']);
    assert.equal(standing({ ...empty, forming: { base: 'm0', members: again } }, two).pending, 'forming');
    // Dropped by name alone while contained: handed back as dropped, so its wait ends; the container's candidate stands.
    const byName = q.dropQueued({ ...empty, candidate: holding }, ['b2']);
    assert.equal(byName.ended, null);
    assert.equal(standing(byName.queue, one).pending, 'candidate');
    const said = tool.waitSaid(standing(byName.queue, two), { branch: 'b2' })!;
    assert.equal(said.code, 1);
    assert.match(said.lines[0]!, /b2's request was taken off the queue/);
    // The whole candidate dropped: its member and the contained one are both handed back as dropped.
    const whole = q.dropQueued({ ...empty, candidate: holding }, ['b1']);
    assert.deepEqual(whole.queue.handedBack.map((each) => [each.branch, each.handedBack?.how]), [['b1', 'dropped'], ['b2', 'dropped']]);
    for (const member of [one, two]) assert.equal(tool.waitSaid(standing(whole.queue, member), { branch: member.branch })!.code, 1);
    // A request in none of the records ends its wait, whatever became of the candidate that held it.
    assert.equal(tool.waitSaid(standing({ ...empty, candidate: null }, two), { branch: 'b2', request: 'r2' })!.code, 1);
  });

  test("a process list reads as rows from the machine reader on Windows and from ps elsewhere, and one that cannot be read is null, never an empty machine", () => {
    const reader = [
      { pid: 0, ppid: 0, created: 1, name: 'System Idle Process', path: null, command: null },
      { pid: 12, ppid: 4, created: 1700000000000, name: 'node.exe', path: null, command: 'node C:\\repo\\local\\scope-tree\\a.js --flag' },
      { pid: 13, ppid: 12, created: 1700000000500, name: 'git.exe', path: null, command: '"C:\\Program Files\\git\\git.exe" status' },
    ];
    const asked: string[] = [];
    assert.deepEqual(procs.processList({ platform: 'win32', helper: 'H:\\machine-read.exe', run: (command: string) => { asked.push(command); return { status: 0, stdout: JSON.stringify(reader) }; } }), [
      { pid: 12, ppid: 4, command: 'node C:\\repo\\local\\scope-tree\\a.js --flag' },
      { pid: 13, ppid: 12, command: '"C:\\Program Files\\git\\git.exe" status' },
    ]);
    assert.deepEqual(asked, ['H:\\machine-read.exe'], 'no PowerShell');
    const ps = ['  501     1 Thu Nov 26 05:47:01 2026 node /repo/local/scope-tree/a.js --flag', '  502   501 Thu Nov 26 05:47:02 2026 git status', '   bad'].join('\n');
    assert.deepEqual(procs.processList({ platform: 'linux', run: () => ({ status: 0, stdout: ps }) }), [
      { pid: 501, ppid: 1, command: 'node /repo/local/scope-tree/a.js --flag' },
      { pid: 502, ppid: 501, command: 'git status' },
    ]);
    assert.equal(procs.processList({ platform: 'win32', helper: 'H', run: () => ({ status: 1, stdout: '', stderr: 'denied' }) }), null);
  });

  test("the census names a process whose command line is under the repository's trees and whose parent is gone, and no other", () => {
    const trees = q.censusTrees('C:/repo');
    assert.deepEqual(trees, ['C:/repo/local/scope-tree', 'C:/repo/local/gate-tree', 'C:/repo/.claude/worktrees/', 'C:/repo/_fixtures/']);
    const rows: ProcessRow[] = [
      { pid: 10, ppid: 1, command: 'node C:\\Repo\\local\\scope-tree\\tool-test\\x.js' },
      { pid: 11, ppid: 10, command: 'node C:/repo/local/scope-tree/child.js' },
      { pid: 12, ppid: 999, command: 'node C:/other/thing.js' },
      { pid: 13, ppid: 998, command: 'git -C C:/repo/_fixtures/review-leave/x status' },
      { pid: 14, ppid: 997, command: 'node C:/repo/local/gate-tree/y.js' },
      { pid: 15, ppid: 996, command: 'node C:/repo/_fixtures-other/z.js' },
      { pid: 16, ppid: 995, command: 'node C:/repo/local/scope-tree2/w.js' },
      { pid: 17, ppid: 994, command: 'node C:/repo/.claude/worktrees/s-1/a.js' },
    ];
    assert.deepEqual(q.orphansUnder(rows, { trees }).map((each) => each.pid), [10, 13, 14, 17], 'a live parent, no tree and a folder that only starts like one are not named');
    assert.deepEqual(q.orphansUnder(rows, { trees, ignore: [10, 14] }).map((each) => each.pid), [13, 17], 'the runner and what it names are left out');
  });

  test('the census says its count and the first few command lines, or that none has lost its parent', () => {
    assert.equal(q.censusLine([]), "census at the candidate's end: no process under this repository's trees has lost its parent");
    const one = q.censusLine([{ pid: 10, ppid: 1, command: 'node x.js' }]);
    assert.equal(one, "census at the candidate's end: 1 process under this repository's trees has lost its parent and runs on (this runner stops none): pid 10 (node x.js)");
    const many = q.censusLine([1, 2, 3, 4, 5].map((pid) => ({ pid, ppid: 0, command: `node ${'long '.repeat(40)}${pid}` })), { limit: 2, width: 30 });
    assert.match(many, /^census at the candidate's end: 5 processes under this repository's trees have lost their parent and run on \(this runner stops none\): pid 1 \(node long long long long long…\), pid 2 \(node long long long long long…\) and 3 more$/);
  });

  test('a runner the queue no longer names ends while it holds, saying so, and leaves the record that names another as it is', async () => {
    const repo = queueScratch('runner-named');
    const hold = join(repo.root, 'local', 'go');
    const other = idle();
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold }, PATIENCE);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1 && Boolean(repo.queue()?.runner));
      const runner = repo.queue()!.runner as unknown as { pid: number };
      assert.ok(alive(runner.pid), repo.log());
      // Another runner took the record (as one does where a start found this pid gone): this one is no longer the queue's.
      q.writeQueue(repo.root, { ...repo.queue()!, runner: { pid: other.pid!, started: AT } } as unknown as Queue);
      await repo.until(() => !alive(runner.pid), PATIENCE);
      assert.equal(alive(runner.pid), false, `the runner went on holding, no longer named:\n${repo.log()}`);
      assert.match(repo.log(), new RegExp(`this runner \\(pid ${runner.pid}\\) is no longer the queue's: the queue names pid ${other.pid} as its runner; it ends`));
      assert.deepEqual((repo.queue()!.runner as unknown as { pid: number }).pid, other.pid, 'it never clears a record that names another');
      assert.equal(repo.queue()!.queued.length, 1, 'its request is still queued: ending leaves it to the runner the queue names');
      void waiting;
    } finally {
      q.writeQueue(repo.root, { ...repo.queue()!, runner: null } as unknown as Queue);
      other.kill();
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test("a runner the queue no longer names ends the gate it runs with its whole tree, and starts no more", async () => {
    const repo = queueScratch('runner-named-gate');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    const other = idle();
    try {
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, "existsSync('hold-check')"));
      repo.git('commit', '--quiet', '-am', `a gate that holds${TRAILER}`);
      repo.branch('b1', { 'hold-check': 'x\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg'], {}, PATIENCE);
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const pids = pidsIn(dir);
      assert.equal(pids.length, 3, repo.log());
      const runner = repo.queue()!.runner as unknown as { pid: number };
      q.writeQueue(repo.root, { ...repo.queue()!, runner: { pid: other.pid!, started: AT } } as unknown as Queue);
      await repo.until(() => pids.every((pid) => !alive(pid)) && !alive(runner.pid), PATIENCE);
      assert.deepEqual(pids.filter(alive), [], `the gate, its child and its grandchild ran on with their runner no longer named:\n${repo.log()}`);
      assert.equal(alive(runner.pid), false, repo.log());
      assert.match(repo.log(), /is no longer the queue's: the queue names pid \d+ as its runner; it ends/);
      assert.equal(readFileSync(join(dir, 'runs'), 'utf8').length, 1, 'no gate started after');
      void waiting;
    } finally {
      writeFileSync(join(dir, 'release'), '');
      q.writeQueue(repo.root, { ...repo.queue()!, runner: null } as unknown as Queue);
      other.kill();
      await repo.cleanup();
    }
  });

  test('a runner and its waiter end with the process that owned them, saying so, and leave the request queued', async () => {
    const repo = queueScratch('owner-gone');
    const hold = join(repo.root, 'local', 'go');
    const owner = idle();
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold, MERGE_BRANCH_QUEUE_OWNER: String(owner.pid) }, PATIENCE);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1 && Boolean(repo.queue()?.runner));
      const runner = repo.queue()!.runner as unknown as { pid: number };
      assert.ok(alive(runner.pid), repo.log());
      owner.kill();
      const ended = await waiting;
      assert.equal(ended.status, 1, ended.out);
      assert.match(ended.out, new RegExp(`its owner \\(pid ${owner.pid}\\), the process that started it, has ended: this wait ends; b1 stays queued`));
      await repo.until(() => !alive(runner.pid), PATIENCE);
      assert.equal(alive(runner.pid), false, `the runner outlived its owner:\n${repo.log()}`);
      assert.match(repo.log(), new RegExp(`its owner \\(pid ${owner.pid}\\), the process that started it, has ended: this runner \\(pid ${runner.pid}\\) ends`));
      assert.equal(repo.queue()!.queued.length, 1);
      assert.equal(repo.log().split("the queue's runner started").length - 1, 1, 'no waiter went on starting runners');
    } finally {
      owner.kill();
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test("a request re-formed into another candidate, because main moved, keeps its waiter, which ends with the landing", async () => {
    const repo = queueScratch('reformed');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    try {
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, "existsSync('hold-check')"));
      repo.git('commit', '--quiet', '-am', `a gate that holds${TRAILER}`);
      repo.branch('b1', { 'hold-check': 'x\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => TREE.every((role) => existsSync(join(dir, `${role}.pid`))));
      const first = repo.queue()!.candidate!;
      const id = first.members[0]!.request!;
      // A direct commit moves main while the candidate's gate holds: the candidate is formed again on it from the same request.
      writeFileSync(join(repo.root, 'moved.txt'), 'm\n');
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a direct commit${TRAILER}`);
      writeFileSync(join(dir, 'release'), '');
      const done = await waiting;
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /b1 is in main/);
      assert.match(repo.log(), /by a direct commit.*the candidate is formed again/);
      const queue = repo.queue() as unknown as { landed: { request: string; commit: string }[] };
      assert.deepEqual(queue.landed.map((each) => each.request), [id], 'the request the waiter holds is the one that landed');
      assert.notEqual(queue.landed[0]!.commit, first.tip, 'on the candidate formed again, not the first');
    } finally {
      writeFileSync(join(dir, 'release'), '');
      await repo.cleanup();
    }
  });

  test("the census at a candidate's end names a process under the trees whose parent is gone, stops it not, and says none where none is", async () => {
    const repo = queueScratch('census');
    let orphan = 0;
    try {
      orphan = orphanNaming(join(repo.root, 'local', 'scope-tree', 'planted-by-a-killed-test'));
      assert.ok(orphan > 0 && alive(orphan), 'the orphan was planted');
      repo.branch('b1', { 'a.txt': 'a\n' });
      repo.branch('b2', { 'b.txt': 'b\n' });
      for (const name of ['b1', 'b2']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const first = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(first.status, 0, `${first.out}\n${repo.log()}`);
      // The waiter ends with the promotion; the runner's census, a process list read, follows it.
      await repo.until(() => /census at the candidate's end/.test(repo.log()), PATIENCE);
      assert.match(repo.log(), new RegExp(`census at the candidate's end: 1 process under this repository's trees has lost its parent and runs on \\(this runner stops none\\): pid ${orphan} \\(.*setInterval`));
      assert.equal(alive(orphan), true, 'the census stops nothing');
      process.kill(orphan);
      await repo.until(() => !alive(orphan), PATIENCE);
      const second = await repo.run(['b2', '--message', 'local/b2.msg']);
      assert.equal(second.status, 0, `${second.out}\n${repo.log()}`);
      await repo.until(() => /census at the candidate's end: no process/.test(repo.log()), PATIENCE);
      assert.match(repo.log(), /census at the candidate's end: no process under this repository's trees has lost its parent/);
    } finally {
      if (orphan > 0) try { process.kill(orphan); } catch { /* ended */ }
      await repo.cleanup();
    }
  });
});

// A drop that says it ended the run "with everything it started" must leave no tool-tests gate process running with its parent
// gone: a fixture's `tree.mjs`, or a `node qgate.mjs check` with two node children. The drop ends each step by its tree of
// parent ids; a test killed with its step has already orphaned those, or never gets to end them.
describe('A dropped run ends what its tests had left running under the scope tree', { concurrency: CASES_AT_ONCE }, () => {
  const row = (pid: number, ppid: number, created: number, command: string) => ({ pid, ppid, created, command });
  const FOLDER = 'D:/work/repo/local/scope-tree';
  const SINCE = 1_000_000;

  test('the roots are the processes since the run began whose parent is gone and which, or what they hold, name the folder', () => {
    const rows = [
      row(1, 0, 0, 'init'),
      row(10, 99, SINCE + 5, 'node D:\\work\\repo\\local\\scope-tree\\src\\_fixtures\\tree.mjs'),
      row(20, 98, SINCE + 5, 'node qgate.mjs check'),
      row(21, 20, SINCE + 6, 'node "D:\\Work\\Repo\\local\\scope-tree\\src\\_fixtures\\qgate.mjs" child'),
      row(22, 21, SINCE + 7, 'node qgate.mjs grandchild'),
      row(30, 97, SINCE + 5, 'node D:/work/repo/local/scope-tree-other/x.mjs'),
      row(31, 96, SINCE + 5, 'node outsider.mjs'),
      row(32, 31, SINCE + 6, 'node D:/work/repo/local/other/x.mjs'),
      row(40, 95, SINCE - 5000, 'node D:/work/repo/local/scope-tree/older.mjs'),
      row(50, 10, SINCE + 6, 'node D:/work/repo/local/scope-tree/held.mjs'),
      row(60, 94, SINCE + 5, 'cmd /c "D:/work/repo/local/scope-tree"'),
    ];
    const found = (options: { except?: number[] } = {}) => procs.strandedRoots(rows, { folder: FOLDER, since: SINCE, ...options }).map((each) => each.pid);
    assert.deepEqual(found(), [10, 20, 60],
      'a root naming the folder, a root that names nothing but holds a process that does, and the folder itself as a whole word; not a sibling folder, not an older process, not a process whose parent runs, not a root of another folder');
    assert.deepEqual(found({ except: [22] }), [10, 60], "a root holding the runner is not the run's to end");
    assert.deepEqual(procs.strandedRoots(rows, { folder: '', since: SINCE }), [], 'no folder names nothing');
    assert.deepEqual(procs.strandedRoots([row(5, 6, SINCE, 'node D:/work/repo/local/scope-tree/a.mjs'), row(6, 5, SINCE, 'node b.mjs')], { folder: FOLDER, since: SINCE }), [],
      "a reused id that makes two processes each other's parent holds no root, and ends the walk");
  });

  test('only Windows ends any, each root with its tree and nothing the run did not make; a list that cannot be read ends none', () => {
    const rows = [
      row(10, 99, 1000500, 'node D:/work/repo/local/scope-tree/a.mjs'), row(11, 99, 1000500, 'node unrelated.mjs'), row(12, 10, 1000600, 'node b.mjs'),
    ];
    const ended: number[] = [];
    const asked: string[] = [];
    const list = () => { asked.push('list'); return rows; };
    assert.deepEqual(procs.endStrandedUnder(FOLDER, 1_001_000, { platform: 'linux', list, end: (pid) => ended.push(pid) }), []);
    assert.deepEqual(asked, [], 'nothing is read elsewhere');
    assert.deepEqual(procs.endStrandedUnder(FOLDER, 1_001_000, { platform: 'win32', list, end: (pid) => ended.push(pid) }), [10]);
    assert.deepEqual(ended, [10]);
    assert.deepEqual(procs.endStrandedUnder(FOLDER, 1_001_000, { platform: 'win32', list: () => { throw new Error('the machine reader failed'); }, end: (pid) => ended.push(pid) }), []);
    assert.deepEqual(procs.endStrandedUnder('', 1_001_000, { platform: 'win32', list, end: (pid) => ended.push(pid) }), []);
  });

  test('a candidate dropped while its gate runs ends the orphans its tests left under the scope tree, one named by a descendant alone; a process outside it is untouched', { skip: process.platform !== 'win32' }, async () => {
    const repo = queueScratch('drain-strays');
    const dir = join(repo.root, 'local', 'drain');
    mkdirSync(dir, { recursive: true });
    const roles = ['one', 'two', 'three'];
    const pidOf = (role: string) => Number(readFileSync(join(dir, `${role}.pid`), 'utf8'));
    const strays = () => roles.filter((role) => existsSync(join(dir, `${role}.pid`))).map(pidOf);
    let outsider = 0;
    try {
      repo.branch('b1', { 'hold-check': 'x\n', 'qgate.mjs': strayGate(dir) });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const first = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => roles.every((role) => existsSync(join(dir, `${role}.pid`))), PATIENCE);
      const pids = strays();
      assert.ok(pids.every(alive), `the orphans run: ${repo.log()}`);
      // A process the run did not make: orphaned too, created after the run began, naming a folder beside the scope tree and not it.
      outsider = orphanNaming(join(dirname(repo.root), 'outsider'));
      assert.ok(outsider > 0 && alive(outsider));
      const dropped = await repo.run(['--drop-batch', 'b1']);
      assert.equal(dropped.status, 0, dropped.out);
      assert.match(dropped.out, /the runner \(pid \d+\) ended gates .*check.*each with everything it started, and 2 processes their tests had left running under the scope tree \(pid \d+, \d+\)/);
      await repo.until(() => pids.every((pid) => !alive(pid)), PATIENCE);
      assert.deepEqual(pids.filter(alive), [], 'the orphan naming the folder, the one named by its child, and that child are gone');
      assert.equal(alive(outsider), true, "a process outside the run's trees is left alone");
      assert.equal((await first).status, 1);
      const ended = (q.readScopeRuns(repo.root) as { state: string; drained?: Drained }[]).find((each) => each.state === 'ended');
      assert.equal(ended?.drained?.strays.length, 2, 'the two roots are recorded, the child going with its root');
    } finally {
      writeFileSync(join(dir, 'release'), '');
      for (const pid of [...strays(), outsider]) if (pid > 0) try { process.kill(pid); } catch { /* ended */ }
      await repo.cleanup();
    }
  });
});

/**
 * What a test of a gate leaves running: it waits for `release` in the folder it is given, and gives up at the bound whatever happens, so
 * one a failed case misses ends by itself. `two` starts `three`, which its absolute path names.
 */
const STRAY = [
  "import { existsSync, writeFileSync } from 'node:fs';",
  "import { spawn } from 'node:child_process';",
  "import { fileURLToPath } from 'node:url';",
  'const [role, dir] = process.argv.slice(2);',
  'writeFileSync(`${dir}/${role}.pid`, String(process.pid));',
  "if (role === 'two') spawn(process.execPath, [fileURLToPath(import.meta.url), 'three', dir], { detached: true, stdio: 'ignore', windowsHide: true }).unref();",
  'const bound = Date.now() + 120000;',
  'while (Date.now() < bound && !existsSync(`${dir}/release`)) await new Promise((done) => setTimeout(done, 100));',
  '',
].join('\n');

/** What the gate runs to leave them: `one` is named by its absolute path, `two` by a relative one, so its command line names no folder. */
const MAKE_STRAYS = [
  "import { spawn } from 'node:child_process';",
  "import { resolve } from 'node:path';",
  'const dir = process.argv[2];',
  "spawn(process.execPath, [resolve('_fixtures/stray.mjs'), 'one', dir], { detached: true, stdio: 'ignore', windowsHide: true }).unref();",
  "spawn(process.execPath, ['_fixtures/stray.mjs', 'two', dir], { detached: true, stdio: 'ignore', windowsHide: true }).unref();",
  '',
].join('\n');

/**
 * A `check` that, where `hold-check` stands, writes both scripts into the scope tree it runs in and leaves three orphans there, as a test
 * killed with its step does (a node that starts them ends at once, so their parent is gone), then waits for `release` as `treeGate` does.
 */
function strayGate(dir: string): string {
  return [
    "import { existsSync, mkdirSync, writeFileSync } from 'node:fs';",
    "import { spawnSync } from 'node:child_process';",
    `const dir = ${JSON.stringify(dir)};`,
    "if (process.argv[2] === 'check' && existsSync('hold-check')) {",
    "  mkdirSync('_fixtures', { recursive: true });",
    `  writeFileSync('_fixtures/stray.mjs', ${JSON.stringify(STRAY)});`,
    `  writeFileSync('_fixtures/make.mjs', ${JSON.stringify(MAKE_STRAYS)});`,
    "  spawnSync(process.execPath, ['_fixtures/make.mjs', dir], { stdio: 'ignore', windowsHide: true });",
    '  while (!existsSync(`${dir}/release`)) await new Promise((done) => setTimeout(done, 25));',
    '}',
    "console.log(`${process.argv[2]} ran`);",
    '',
  ].join('\n');
}
