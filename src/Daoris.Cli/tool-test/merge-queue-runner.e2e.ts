/**
 * The merge tool as the declared runner end to end: its answer's schema, its inputs, and the run and plan modes.
 */
import { describe, test } from 'node:test';
import { writeFileSync, existsSync, readFileSync, mkdirSync, rmSync } from 'node:fs';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import {
  queueScratch, commitOnMain, verifier, VERIFY, recorderGate, realPath, modes, type Answered,
} from './support/merge-queue-scratch.ts';
import { tool } from './support/merge-queue-kit.ts';

describe('The declared runner, its schema and its inputs, end to end in a scratch repository', { concurrency: true }, () => {
  test('the declared runner answers D192\'s schema 1 when no mode is asked: each declared gate under its own name, the full commit, its verdict and exit, its log kept where it stays', async () => {
    const repo = queueScratch('verify-answer');
    try {
      const commit = commitOnMain(repo, { 'break-check': '' }, 'a gate that fails');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'The universal gate', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check' }] });
      writeFileSync(run.answer, '{"schema":1,"commit":"stale","gates":[]}');
      const done = await repo.run([...VERIFY, '--keep-going'], run.env(commit));
      assert.equal(done.status, 1, done.out);
      const answer = run.read()!;
      assert.deepEqual(Object.keys(answer).sort(), ['commit', 'gates', 'schema']);
      assert.equal(answer.schema, 1);
      assert.equal(answer.commit, commit, 'the full commit, not seven characters');
      type Answered = { name: string; verdict: string; exit?: number; wallSeconds?: number; log: string };
      const byName: Record<string, Answered> = Object.fromEntries((answer.gates as Answered[]).map((gate) => [gate.name, gate]));
      assert.deepEqual(Object.keys(byName).sort(), ['The universal gate', 'check']);
      assert.equal(byName['The universal gate']!.verdict, 'pass');
      assert.equal(byName['check']!.verdict, 'fail');
      assert.equal(byName['check']!.exit, 1);
      for (const gate of Object.values(byName)) {
        assert.equal(typeof gate.wallSeconds, 'number', gate.name);
        // The log stays in the main checkout once the tree moves, not in the gate tree the next run clears.
        assert.ok(gate.log.startsWith(join(repo.root, 'local', 'scratch', `beside-${commit.slice(0, 7)}`)), gate.log);
        assert.ok(existsSync(gate.log), gate.log);
      }
      assert.match(readFileSync(byName['The universal gate']!.log, 'utf8'), /universal ran/);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.json')), false, 'a stage of this tree is not allowed by a runner\'s answer');
      // The gate tree's lock is let go: a second run is not busy.
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false);
    } finally {
      await repo.cleanup();
    }
  });

  test('a member\'s edit of a gate\'s command in the tree does not run; the gates are the handed declaration\'s, and no DAORIS_VERIFY_* reaches one', async () => {
    const repo = queueScratch('verify-edit');
    try {
      const record = join(repo.root, 'local', 'recorded.json');
      mkdirSync(join(repo.root, 'local'), { recursive: true });
      writeFileSync(join(repo.root, 'local', 'sensitive-patterns.txt'), 'x\n');
      const commit = commitOnMain(repo, {
        'vgate.mjs': recorderGate(record),
        'edit.mjs': "import { writeFileSync } from 'node:fs';\nwriteFileSync('edited.marker', 'ran');\n",
        'daoris.gates.json': JSON.stringify({ gates: [{ name: 'recorder', run: 'node edit.mjs' }] }),
      }, 'a member edits the gate\'s command');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'recorder', run: 'node vgate.mjs' }] });
      const done = await repo.run(VERIFY, run.env(commit, { DAORIS_HOME: join(repo.root, 'local', 'install-home') }));
      assert.equal(done.status, 0, done.out);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree', 'edited.marker')), false, 'the edited command in the tree did not run');
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { daoris: string[]; cwd: string; copied: boolean };
      assert.deepEqual(seen.daoris, [], 'no Daoris variable reached the gate, DAORIS_VERIFY_* among them');
      assert.equal(realPath(seen.cwd), realPath(join(repo.root, 'local', 'gate-tree')), 'the runner made its own tree');
      assert.equal(seen.copied, true, 'the private files a gate reads are copied into a tree the runner made');
      assert.deepEqual(run.read()!.gates.map((gate: { name: string; verdict: string }) => [gate.name, gate.verdict]), [['recorder', 'pass']]);
    } finally {
      await repo.cleanup();
    }
  });

  // The declaration the runner is handed is the accepted blob (design §5.9), so its `env.pass` is the owner's.
  test('the declared runner passes the names the declaration handed lists, from its start environment, and no Daoris name or other removed one', async () => {
    const repo = queueScratch('verify-pass');
    try {
      const record = join(repo.root, 'local', 'passed.json');
      mkdirSync(join(repo.root, 'local'), { recursive: true });
      writeFileSync(join(repo.root, 'local', 'sensitive-patterns.txt'), 'x\n');
      const commit = commitOnMain(repo, {
        'pgate.mjs': [
          "import { writeFileSync } from 'node:fs';",
          `writeFileSync(${JSON.stringify(record)}, JSON.stringify(Object.fromEntries(['GIT_CONFIG_GLOBAL', 'GIT_CONFIG_NOSYSTEM', 'DAORIS_HOME'].map((name) => [name, process.env[name] ?? null]))));`,
          '',
        ].join('\n'),
      }, 'a gate that reads what it was passed');
      const run = verifier(repo);
      // `DAORIS_HOME` is refused by the declaration's own read, so the case declares only what a declaration may; the install's home rides the shell.
      run.declare({ gates: [{ name: 'passed', run: 'node pgate.mjs' }], integration: { env: { pass: ['GIT_CONFIG_GLOBAL'] } } });
      const done = await repo.run(VERIFY, run.env(commit, { DAORIS_HOME: join(repo.root, 'local', 'install-home'), GIT_CONFIG_GLOBAL: 'X:/cfg', GIT_CONFIG_NOSYSTEM: '1' }));
      assert.equal(done.status, 0, done.out);
      assert.deepEqual(JSON.parse(readFileSync(record, 'utf8')), { GIT_CONFIG_GLOBAL: 'X:/cfg', GIT_CONFIG_NOSYSTEM: null, DAORIS_HOME: null }, 'only the name the declaration passes came through');
    } finally {
      await repo.cleanup();
    }
  });

  test('a held gate-tree lock writes busy to the answer first and exits 75; without the flag the refusal is exit 2 as it was', async () => {
    const repo = queueScratch('verify-busy');
    try {
      const commit = repo.git('rev-parse', 'HEAD');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }] });
      writeFileSync(join(repo.root, 'local', 'gate-tree.lock'), `${JSON.stringify({ pid: process.pid, commit, at: '2026-10-29T10:00:00Z', holder: 'full' })}\n`);
      const busy = await repo.run(VERIFY, run.env(commit));
      assert.equal(busy.status, 75, busy.out);
      assert.match(busy.out, /a full set beside the merges is running already/);
      assert.deepEqual(run.read(), { schema: 1, commit, busy: true });
      // The variable a person's shell left behind retargets nothing: without the flag the run is the person's own and refuses as before.
      rmSync(run.answer);
      const own = await repo.run(['--full', '--beside'], run.env(commit));
      assert.equal(own.status, 2, own.out);
      assert.equal(run.read(), null, 'nothing is written without the flag');
    } finally {
      await repo.cleanup();
    }
  });

  test('the commits mode is the commit check on base..tip, a schema-2 answer naming the declaration blob it read', async () => {
    const repo = queueScratch('verify-commits');
    try {
      const base = repo.git('rev-parse', 'HEAD');
      repo.git('checkout', '--quiet', '-b', 'work');
      commitOnMain(repo, { 'a.txt': 'a\n' }, 'work with its maker');
      repo.git('commit', '--quiet', '--allow-empty', '-m', 'work with none');
      const tip = repo.git('rev-parse', 'HEAD');
      const run = verifier(repo);
      const declared = { gates: [{ name: 'universal', run: 'node qgate.mjs universal' }] };
      run.declare(declared);
      const more = { DAORIS_VERIFY_MODE: 'commits', DAORIS_VERIFY_BASE: base };
      const red = await repo.run(VERIFY, run.env(tip, more));
      assert.equal(red.status, 1, red.out);
      const answer = run.read()!;
      assert.equal(answer.schema, 2);
      assert.deepEqual([answer.mode, answer.commit, answer.base], ['commits', tip, base]);
      assert.equal(answer.declaration, repo.git('hash-object', join(repo.root, 'local', 'verify', 'declaration.json')));
      assert.equal(answer.problems.length, 1, JSON.stringify(answer));
      assert.match(answer.problems[0], /work with none: no Co-Authored-By line/);
      // A range whose commits all carry their maker has no problem, and exit 0; no gate ran.
      const clean = repo.git('rev-parse', 'HEAD~1');
      const green = await repo.run(VERIFY, run.env(clean, more));
      assert.equal(green.status, 0, green.out);
      assert.deepEqual(run.read()!.problems, []);
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree')), false, 'the commits mode makes no tree');
      // A declaration that checks commits by a gate leaves the check to that gate: the runner's mode refuses.
      run.declare({ ...declared, integration: { commits: 'universal' } });
      const gate = await repo.run(VERIFY, run.env(tip, more));
      assert.equal(gate.status, 2, gate.out);
      assert.match(gate.out, /checks commits by its gate 'universal', not by the runner/);
      assert.equal(run.read(), null);
    } finally {
      await repo.cleanup();
    }
  });

  test('a tree the caller hands is the run\'s tree: the runner makes none, takes no lock and copies no private file; one at another commit is refused', async () => {
    const repo = queueScratch('verify-tree');
    try {
      const record = join(repo.root, 'local', 'recorded.json');
      writeFileSync(join(repo.root, 'local', 'sensitive-patterns.txt'), 'x\n');
      const first = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'vgate.mjs': recorderGate(record) }, 'a recorder');
      const handed = join(dirname(repo.root), 'handed');
      repo.git('worktree', 'add', '--quiet', '--detach', handed, commit);
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'recorder', run: 'node vgate.mjs' }] });
      const done = await repo.run(VERIFY, run.env(commit, { DAORIS_VERIFY_TREE: handed }));
      assert.equal(done.status, 0, done.out);
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { cwd: string; copied: boolean };
      assert.equal(realPath(seen.cwd), realPath(handed), 'the gate ran in the caller\'s tree');
      assert.equal(seen.copied, false, 'a private file is not copied into the caller\'s tree');
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree')), false, 'the runner made no tree of its own');
      assert.equal(existsSync(join(repo.root, 'local', 'gate-tree.lock')), false, 'and took no lock');
      assert.equal(run.read()!.gates[0].log.startsWith(join(repo.root, 'local', 'scratch', `beside-${commit.slice(0, 7)}`)), true);
      // A tree standing at another commit than the one named is the caller's mistake: refused, nothing moved, nothing run.
      rmSync(run.answer);
      repo.git('-C', handed, 'checkout', '--quiet', '--detach', first);
      const wrong = await repo.run(VERIFY, run.env(commit, { DAORIS_VERIFY_TREE: handed }));
      assert.equal(wrong.status, 2, wrong.out);
      assert.match(wrong.out, /DAORIS_VERIFY_TREE stands at .* not at DAORIS_VERIFY_COMMIT/);
      assert.equal(run.read(), null);
    } finally {
      await repo.cleanup();
    }
  });

  test('the runner refuses a missing, malformed or contradicting input in a sentence with exit 2 and no answer, and the variable left in a shell retargets no run without the flag', async () => {
    const repo = queueScratch('verify-refusals');
    try {
      const first = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'later.txt': 'later\n' }, 'a later commit');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }] });
      const refused = async (what: string, env: Record<string, string>, message: RegExp) => {
        const done = await repo.run(VERIFY, env);
        assert.equal(done.status, 2, `${what}: ${done.out}`);
        assert.match(done.out, message, what);
        assert.equal(run.read(), null, `${what}: no answer is written for an input refused`);
      };
      await refused('nothing set', {}, /DAORIS_VERIFY_RESULT is not set/);
      await refused('no commit', { DAORIS_VERIFY_RESULT: run.answer }, /DAORIS_VERIFY_COMMIT is not set/);
      await refused('a run with no base', run.env(commit, { DAORIS_VERIFY_MODE: 'run' }), /DAORIS_VERIFY_BASE is not set: the run mode reaches its gates by base\.\.commit/);
      await refused('a commit the repository lacks', run.env('f'.repeat(40)), /is no commit of this repository/);
      await refused('a declaration that is no file', run.env(commit, { DAORIS_VERIFY_DECLARATION: join(repo.root, 'local', 'verify', 'none.json') }), /DAORIS_VERIFY_DECLARATION names no file/);
      const named = await repo.run(['--full', '--beside', commit, '--daoris-verify'], run.env(commit));
      assert.equal(named.status, 2, named.out);
      assert.match(named.out, /takes its commit from DAORIS_VERIFY_COMMIT/);
      // No flag: the variable names nothing, and `--full --beside` gates main's tip as it always did.
      const own = await repo.run(['--full', '--beside'], run.env(first));
      assert.equal(own.status, 0, own.out);
      assert.match(own.out, new RegExp(`the full set on ${commit.slice(0, 7)}`));
      assert.equal(run.read(), null, 'nothing is written without the flag');
    } finally {
      await repo.cleanup();
    }
  });
});

describe('The run, plan and probe modes, end to end in a scratch repository', { concurrency: true }, () => {
  test('the run mode answers by gate, each with its composition kept beside its log and the failures read from it, beside what its plan kept and deferred', async () => {
    const repo = queueScratch('verify-run');
    try {
      const base = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'break-check': '' }, 'a gate that fails');
      const run = verifier(repo);
      run.declare({ gates: [
        { name: 'universal', run: 'node qgate.mjs universal' }, { name: 'The check', run: 'node qgate.mjs check' }, { name: 'rehearse', run: 'npm run rehearse:fixture' },
      ] });
      const done = await modes(repo, run, base)('run', commit, {});
      assert.equal(done.status, 1, done.out);
      const answer = run.read()!;
      assert.deepEqual(Object.keys(answer).sort(), ['base', 'commit', 'declaration', 'deferred', 'gates', 'kept', 'mode', 'schema']);
      assert.deepEqual([answer.schema, answer.mode, answer.commit, answer.base], [2, 'run', commit, base]);
      assert.equal(answer.declaration, repo.git('hash-object', join(repo.root, 'local', 'verify', 'declaration.json')));
      // The scoped set (§4.3): the cheap tier and what the change reaches run; a rehearsal is deferred to the background run.
      assert.deepEqual(answer.gates.map((gate: Answered) => [gate.name, gate.verdict]), [['universal', 'pass'], ['The check', 'fail']]);
      assert.deepEqual(answer.kept, []);
      assert.deepEqual(answer.deferred.map((each: Answered) => each.name), ['rehearse']);
      for (const gate of answer.gates as Answered[]) {
        // Run whole, so no units; its settings kept beside its log, which the digest is of with them.
        assert.equal(gate.composition.units, null, gate.name);
        assert.match(gate.composition.digest, /^sha256:[0-9a-f]{64}$/);
        assert.equal(dirname(gate.composition.settings), dirname(gate.log), 'beside its log, where it stays once the tree moves');
        const kept = JSON.parse(readFileSync(gate.composition.settings, 'utf8')) as Answered;
        assert.equal(kept.digest, gate.composition.digest);
        assert.deepEqual([kept.settings.gate, kept.settings.runsettings, kept.settings.narrowed], [gate.name, null, []]);
        assert.deepEqual(kept.settings.environment.added, tool.BESIDE_ENVIRONMENT);
      }
      // A gate that names no failing test fails by its step, signed under the name the declaration writes.
      const check = (answer.gates as Answered[]).find((gate) => gate.name === 'The check')!;
      assert.deepEqual(check.failures, [{ unit: 'The check', test: 'The check', signature: { gate: 'The check', step: 'run', message: 'error: check is broken' } }]);
      assert.deepEqual((answer.gates as Answered[]).find((gate) => gate.name === 'universal')!.failures, []);
    } finally {
      await repo.cleanup();
    }
  });

  test('the plan mode says what a run would run, keep and defer, running nothing and the same each time for a tree; a run then keeps what passed, on the commit and tree it passed on; DAORIS_VERIFY_GATES narrows both', async () => {
    const repo = queueScratch('verify-plan');
    try {
      const record = join(repo.root, 'local', 'recorded.json');
      const base = repo.git('rev-parse', 'HEAD');
      const commit = commitOnMain(repo, { 'vgate.mjs': recorderGate(record) }, 'a recorder');
      const run = verifier(repo);
      run.declare({ gates: [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'The recorder', run: 'node vgate.mjs' }, { name: 'rehearse', run: 'npm run rehearse:fixture' }] });
      const asked = modes(repo, run, base);
      const planned = await asked('plan', commit);
      assert.equal(planned.status, 0, planned.out);
      const first = run.read()!;
      assert.deepEqual(Object.keys(first).sort(), ['base', 'commit', 'declaration', 'deferred', 'kept', 'mode', 'run', 'schema']);
      assert.deepEqual([first.schema, first.mode, first.commit, first.base], [2, 'plan', commit, base]);
      assert.deepEqual(first.run.map((each: Answered) => each.name), ['universal', 'The recorder']);
      assert.equal(first.run[0].why, 'the cheap tier runs in every candidate');
      assert.deepEqual(first.kept, []);
      assert.deepEqual(first.deferred, [{ name: 'rehearse', why: 'a browser spec or a rehearsal: deferred to the background run, which the install waits for' }]);
      assert.equal(existsSync(record), false, 'a plan runs nothing');
      // The same answer for the same tree.
      assert.equal((await asked('plan', commit)).status, 0);
      assert.deepEqual(run.read(), first);
      // A run runs the plan's run; their passes stand on its tree, so the next plan keeps them, on that commit and tree.
      const ran = await asked('run', commit);
      assert.equal(ran.status, 0, ran.out);
      assert.equal(existsSync(record), true);
      assert.deepEqual(run.read()!.gates.map((gate: Answered) => [gate.name, gate.verdict]), [['universal', 'pass'], ['The recorder', 'pass']]);
      assert.equal((await asked('plan', commit)).status, 0);
      const tree = repo.git('rev-parse', `${commit}^{tree}`);
      const again = run.read()!;
      assert.deepEqual(again.run, []);
      assert.deepEqual(again.kept, [
        { name: 'universal', why: 'its pass stands on this tree', commit, tree, standing: 'exact' },
        { name: 'The recorder', why: 'its pass stands on this tree', commit, tree, standing: 'exact' },
      ]);
      // DAORIS_VERIFY_GATES: the gates named, each run whole, nothing kept or deferred; a name the declaration does not write is refused.
      rmSync(record);
      assert.equal((await asked('plan', commit, { DAORIS_VERIFY_GATES: 'The recorder' })).status, 0);
      assert.deepEqual([run.read()!.run, run.read()!.kept, run.read()!.deferred], [[{ name: 'The recorder', why: 'named in DAORIS_VERIFY_GATES: it runs whole' }], [], []]);
      const named = await asked('run', commit, { DAORIS_VERIFY_GATES: 'The recorder' });
      assert.equal(named.status, 0, named.out);
      assert.deepEqual(run.read()!.gates.map((gate: Answered) => gate.name), ['The recorder']);
      assert.equal(existsSync(record), true, 'a gate named runs, whatever pass of it stands');
      const unknown = await asked('run', commit, { DAORIS_VERIFY_GATES: 'recorder' });
      assert.equal(unknown.status, 2, unknown.out);
      assert.match(unknown.out, /DAORIS_VERIFY_GATES names 'recorder', which the declaration handed does not declare/);
      assert.equal(run.read(), null);
    } finally {
      await repo.cleanup();
    }
  });
});
