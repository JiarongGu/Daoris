/**
 * The merge queue end to end: a case reaches only its own scratch repository, and a gate sees none of the shell's Daoris or git variables.
 */
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { join, delimiter } from 'node:path';
import { mkdirSync, writeFileSync, readFileSync, existsSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tool, q, machine } from './support/merge-queue-kit.ts';
import { makeFixture } from '../test/_fixture.ts';
import { caseEnvironment, TRAILER, START_ENV, queueScratch, envGate, type SeenEnvironment } from './support/merge-queue-scratch.ts';
import { CASES_AT_ONCE } from './support/e2e-repository.ts';

// A case's enqueue must not wait on the main checkout's queue or start a runner there from the tree's tool,
// with the class-probe case's PATH. Its cases run one at a time here, so the variables it sets in the test process meet no other case.
describe('A case reaches only its own scratch repository and queue', () => {
  test("only the queue's own checkout queues to it or runs it, and a tree's tool never runs the main checkout's queue", () => {
    const home = 'X:/daoris';
    assert.equal(tool.queueHomeRefusal({ root: home, home }), null);
    assert.equal(tool.queueHomeRefusal({ root: 'X:/daoris/', home, tool: home, sameRepository: true, run: true }), null, 'its own tool, the path spelled with a slash');
    assert.match(tool.queueHomeRefusal({ root: 'X:/daoris/local/tree', home })!, /X:\/daoris\/local\/tree is not the queue's checkout: the queue is kept in X:\/daoris, the repository's main checkout/);
    assert.match(tool.queueHomeRefusal({ root: home, home, tool: 'X:/daoris/local/scope-tree', sameRepository: true, run: true })!,
      /the tool of X:\/daoris\/local\/scope-tree, another checkout of the queue's repository: a tree's tool never runs X:\/daoris's queue/);
    assert.equal(tool.queueHomeRefusal({ root: home, home, tool: 'X:/daoris/local/scope-tree', sameRepository: true }), null, 'a verb that starts no runner here runs any checkout\'s tool');
    assert.equal(tool.queueHomeRefusal({ root: home, home, tool: 'X:/elsewhere', sameRepository: false, run: true }), null, "another repository's tool (the tool tests' scratch queues) runs it");
  });

  // A shared `core.worktree` can make a linked worktree report another checkout's root as its own.
  test("a tree that reports another folder as its working root is refused before any write, the same folder spelled otherwise is not", () => {
    const path = 'X:/daoris/local/scope-tree';
    assert.equal(tool.treeBindingRefusal({ path, reported: path }), null);
    assert.equal(tool.treeBindingRefusal({ path, reported: 'x:/daoris/local/scope-tree/' }), null, 'the same folder, spelled with another case and a slash');
    assert.equal(tool.treeBindingRefusal({ path, reported: null }), null, 'git reported none: nothing to say here, git refuses on its own');
    const why = tool.treeBindingRefusal({ path, reported: 'X:/daoris' })!;
    assert.match(why, /X:\/daoris\/local\/scope-tree reports X:\/daoris as its working root/);
    assert.match(why, /core\.worktree/);
    assert.match(why, /nothing was reset, checked out or removed/);
    assert.match(why, /worktree configuration/);
  });

  test("a gate the tool starts gets the environment the tool had at its start, without a Daoris name, git's locating variables, the tool tests' variables or the tool's switches, with the build servers of its own", () => {
    const start = { Path: 'X:/bin', HOME: 'X:/home', QUEUE_DRAIN_DIR: 'X:/drain', queue_drop_hold: 'X:/hold', MERGE_BRANCH_QUEUE_TICK_MS: '300', MERGE_BRANCH_MEMORY: 'off', GIT_OPTIONAL_LOCKS: '0' };
    assert.deepEqual(tool.gateEnvironment(start), { Path: 'X:/bin', HOME: 'X:/home', GIT_OPTIONAL_LOCKS: '0', ...tool.BESIDE_ENVIRONMENT });
  });

  // A steward's shell can hold the install's home (design §5.5), and every gate the queue's runners start would inherit it.
  test("a gate gets no Daoris home, role or key and none of git's locating variables, in either spelling, and what a person set for git otherwise stays", () => {
    const start = {
      Path: 'X:/bin', DAORIS_HOME: 'X:/install', daoris_role: 'steward', DAORIS_SUPERVISOR_KEY: 'k', DAORIS_ADMISSION_HOLD: 'h-1',
      GIT_DIR: 'X:/g', git_work_tree: 'X:/w', GIT_INDEX_FILE: 'i', GIT_OBJECT_DIRECTORY: 'o', GIT_ALTERNATE_OBJECT_DIRECTORIES: 'a', GIT_COMMON_DIR: 'c', GIT_NAMESPACE: 'n',
      GIT_CEILING_DIRECTORIES: 'X:/', GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'k', GIT_CONFIG_GLOBAL: 'X:/cfg', GIT_AUTHOR_NAME: 'A', GIT_OPTIONAL_LOCKS: '0',
    };
    assert.deepEqual(tool.gateEnvironment(start), { Path: 'X:/bin', GIT_AUTHOR_NAME: 'A', GIT_OPTIONAL_LOCKS: '0', ...tool.BESIDE_ENVIRONMENT });
    assert.ok(tool.GATE_REMOVED.length > 0, 'the list is exported, so the driver\'s twin and a census read the same names');
  });

  // The names (design §5.5, §4.1's `env.pass`) an accepted declaration passes come back from the tool's own start environment,
  // the removed list's included, and never a Daoris name; a name not passed stays out.
  test("the names an accepted declaration passes reach a gate from the start environment, a removed one's among them; a Daoris name and a name not passed never do", () => {
    const start = { Path: 'X:/bin', GIT_CONFIG_GLOBAL: 'X:/cfg', git_config_key_0: 'k', MERGE_BRANCH_MEMORY: 'off', DAORIS_HOME: 'X:/install', daoris_role: 'steward', TOKEN_FOR_GATE: 't' };
    const base = { Path: 'X:/bin', TOKEN_FOR_GATE: 't', ...tool.BESIDE_ENVIRONMENT };
    assert.deepEqual(tool.gateEnvironment(start), base, 'nothing is passed unless an accepted declaration says so');
    assert.deepEqual(tool.gateEnvironment(start, { pass: ['GIT_CONFIG_GLOBAL'] }), { ...base, GIT_CONFIG_GLOBAL: 'X:/cfg' }, 'a removed name comes back only by name');
    assert.deepEqual(tool.gateEnvironment(start, { pass: ['GIT_CONFIG_KEY_0', 'NOT_SET_HERE'] }), { ...base, git_config_key_0: 'k' }, 'the name is matched in any case, and one the start lacks adds nothing');
    assert.deepEqual(tool.gateEnvironment(start, { pass: ['DAORIS_HOME', 'daoris_role'] }), base, 'a Daoris name is never passed, however it is spelled or declared');
  });

  test('git\'s repository-locating variables, a stand-in gate\'s variable and a PATH set in the test process reach no case: its tool reads and writes only its scratch queue', async () => {
    // The repository around the tests, stood in for by a decoy with a queue of its own that must be left as it is.
    const decoy = makeFixture(`merge-queue-decoy-${process.pid}`);
    const outer = join(decoy.root, 'repo');
    mkdirSync(join(outer, 'local'), { recursive: true });
    const env = caseEnvironment(decoy.root);
    const outerGit = (...args: string[]) => spawnSync('git', args, { cwd: outer, encoding: 'utf8', env, windowsHide: true });
    outerGit('init', '--quiet', '--template=', '-b', 'main');
    writeFileSync(join(outer, '.gitignore'), 'local/\n');
    outerGit('add', '-A');
    outerGit('commit', '--quiet', '-m', `the decoy${TRAILER}`);
    q.writeQueue(outer, { ...q.emptyQueue(), previews: { sentinel: { tip: '0'.repeat(40) } } });
    const before = { head: outerGit('rev-parse', 'HEAD').stdout.trim(), queue: readFileSync(join(outer, 'local', 'merge-queue.json'), 'utf8') };
    const shims = join(decoy.root, 'shims');
    mkdirSync(shims, { recursive: true });
    const leaked: Record<string, string> = {
      GIT_DIR: join(outer, '.git'), GIT_COMMON_DIR: join(outer, '.git'), GIT_INDEX_FILE: join(outer, '.git', 'index'),
      QUEUE_DRAIN_DIR: join(decoy.root, 'drain'), PATH: `${shims}${delimiter}${START_ENV.PATH ?? START_ENV.Path ?? ''}`,
    };
    const saved = Object.fromEntries(Object.keys(leaked).map((name) => [name, process.env[name]]));
    Object.assign(process.env, leaked);
    let repo: ReturnType<typeof queueScratch> | null = null;
    try {
      repo = queueScratch('leak');
      const record = join(repo.root, 'local', 'gate-env.json');
      repo.branch('b1', { 'qgate.mjs': envGate(record) });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const done = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /b1 is in main/);
      assert.deepEqual(repo.queue()!.landed!.map((each) => each.branch), ['b1'], 'the scratch queue is the one that landed it');
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { drain: string | null; gitDir: string | null; path: string };
      assert.equal(seen.drain, null, 'a stand-in gate\'s variable set in the test process reaches no gate');
      assert.equal(seen.gitDir, null);
      assert.ok(!seen.path.split(delimiter).includes(shims), 'a PATH set in the test process reaches no gate');
    } finally {
      for (const [name, value] of Object.entries(saved)) {
        if (value === undefined) delete process.env[name];
        else process.env[name] = value;
      }
      await repo?.cleanup();
    }
    assert.equal(outerGit('rev-parse', 'HEAD').stdout.trim(), before.head, 'nothing was committed in the repository around the case');
    assert.equal(outerGit('rev-parse', '--verify', '--quiet', 'refs/heads/b1').status, 1, 'no branch of the case is made there');
    assert.equal(readFileSync(join(outer, 'local', 'merge-queue.json'), 'utf8'), before.queue, "the decoy's queue is as it was");
    assert.equal(existsSync(join(outer, 'local', 'merge-queue.log')), false, 'no runner ran on it');
    decoy.cleanup();
  });
});

describe("A gate sees none of the shell's home, role or git locating variables, end to end in a scratch repository", { concurrency: CASES_AT_ONCE }, () => {
  // The invoker's shell can hold the install's home, a role and git's locating variables; the runner it starts, and the gates
  // of every verb, must not inherit them, while the machine's admission is still found from the home the tool read at its start.
  const ownedGate = () => JSON.stringify({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check', memoryGb: 0.1 }] });
  // GIT_DIR is set only where the tool's own git may take it: in the queue's verbs it makes the tool's checkout and the scratch one repository,
  // and the tool refuses (`queueHomeRefusal`); git's ceiling, which the case's environment always sets, is the locating variable proved there.
  const shell = (repo: { root: string }, gitDir = false) => ({
    DAORIS_HOME: join(repo.root, 'local', 'install-home'), DAORIS_ROLE: 'steward', MERGE_BRANCH_ADMISSION: 'on', ...(gitDir ? { GIT_DIR: join(repo.root, '.git') } : {}),
  });
  const assertClean = (seen: SeenEnvironment, what: string) => {
    assert.equal(seen.home, null, `${what}: the home reached the gate`);
    assert.equal(seen.role, null, `${what}: a role variable reached the gate`);
    assert.equal(seen.gitDir, null, `${what}: GIT_DIR reached the gate`);
    assert.equal(seen.ceiling, null, `${what}: git's ceiling reached the gate`);
    assert.equal(seen.beside, '1', `${what}: the gate has the build servers of its own`);
  };

  test("a gate the queue's runner starts sees no Daoris home, role or git locating variable the shell that started the runner held, and the admission is still found from the home", async () => {
    const repo = queueScratch('gate-shell');
    try {
      const home = join(repo.root, 'local', 'install-home');
      mkdirSync(home, { recursive: true });
      const record = join(repo.root, 'local', 'gate-shell.json');
      repo.branch('b1', { 'qgate.mjs': envGate(record, machine.admissionFile(home)), 'daoris.gates.json': ownedGate() });
      assert.equal((await repo.run(['b1', '--preview'], shell(repo))).status, 0);
      const done = await repo.run(['b1', '--message', 'local/b1.msg'], shell(repo));
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      const seen = JSON.parse(readFileSync(record, 'utf8')) as SeenEnvironment;
      assertClean(seen, 'the queue');
      assert.deepEqual(seen.held, ['check gate'], 'the runner took its place in the admission of the home it read at its start');
    } finally {
      await repo.cleanup();
    }
  });

  test('a merge in place, a gate run again and a full run of the checkout start their gates the same way', async () => {
    const repo = queueScratch('gate-shell-direct');
    try {
      const record = join(repo.root, 'local', 'gate-shell-direct.json');
      // `universal` runs at every merge and every full run of a tree not passed; `check` is skipped by the lane table for a file like this.
      repo.branch('b1', { 'qgate.mjs': envGate(record, '', 'universal') });
      // A gate run again on the checkout answers 1 while the full set lacks a verdict (`check` has none here): the gate ran all the same.
      const seenAfter = (what: string, { status, out }: { status: number | null; out: string }) => {
        assert.ok(status === 0 || (status === 1 && /^merge-branch: .*has NOT passed this checkout/m.test(out)),
          `${what}: ${status} ${out}`);
        assert.ok(existsSync(record), `${what}: the check gate ran: ${out}`);
        assertClean(JSON.parse(readFileSync(record, 'utf8')) as SeenEnvironment, what);
        rmSync(record);
      };
      const direct = shell(repo, true);
      seenAfter('merge in place', await repo.run(['b1'], direct));
      seenAfter('--rerun with the merge open', await repo.run(['--rerun', 'universal'], direct));
      repo.git('commit', '--quiet', '-m', `Merge b1${TRAILER}`);
      seenAfter('--rerun with none open', await repo.run(['--rerun', 'universal'], direct));
      for (const args of [['--full'], ['--full', '--beside']]) {
        writeFileSync(join(repo.root, '.gitignore'), `local/\n${args}\n`);
        repo.git('commit', '--quiet', '-am', `${args}${TRAILER}`);
        const ran = await repo.run(args, direct);
        seenAfter(`${args}`, ran);
      }
    } finally {
      await repo.cleanup();
    }
  });
});
