/**
 * The merge queue end to end: preview, queue, candidates, contained members, drops and holds in a scratch repository.
 */
import { describe, test } from 'node:test';
import { join, dirname } from 'node:path';
import assert from 'node:assert/strict';
import { existsSync, writeFileSync, readFileSync, mkdirSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { queueScratch, TRAILER, treeGate, QUEUE_GATE, alive, HANG_MS, envGate } from './support/merge-queue-scratch.ts';
import { q, type ScopeRun } from './support/merge-queue-kit.ts';

describe('the queue, end to end in a scratch repository', { concurrency: true }, () => {
  test('a dropped candidate cannot return when its held gate completes', async () => {
    const repo = queueScratch('drop-late');
    const hold = join(repo.root, 'local', 'release');
    const started = `${hold}.started`;
    try {
      const gate = [
        "import { existsSync, writeFileSync } from 'node:fs';",
        // The hold's path is written into the gate, since the runner passes no test's variable on to a gate.
        `const hold = ${JSON.stringify(hold)};`,
        "if (process.argv[2] === 'check') {",
        "  writeFileSync(`${hold}.started`, 'started');",
        '  while (!existsSync(hold)) await new Promise((done) => setTimeout(done, 25));',
        '}',
        'console.log(`${process.argv[2]} ran`);',
      ].join('\n');
      repo.branch('b1', { 'a.txt': 'a\n', 'qgate.mjs': gate });
      const before = repo.git('rev-parse', 'main');
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => existsSync(started));
      assert.ok(existsSync(started), repo.log());
      assert.equal((await repo.run(['--drop-batch', 'b1'])).status, 0);
      writeFileSync(hold, 'release');
      assert.equal((await waiting).status, 1);
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(repo.queue()?.candidate, null, repo.log());
      assert.equal(repo.queue()?.stop, null, repo.log());
      assert.equal(repo.git('rev-parse', 'main'), before);
      assert.deepEqual(repo.queue()!.handedBack.map((entry) => entry.handedBack?.how), ['dropped']);
    } finally {
      writeFileSync(hold, 'release');
      await repo.cleanup();
    }
  });

  test('requeueing an unchanged rejected tip waits for its new promotion after a base repair', async () => {
    const repo = queueScratch('requeue-same-tip');
    try {
      repo.branch('b1', { 'break-check': 'x\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const rejected = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(rejected.status, 1, rejected.out);
      await repo.until(() => !repo.queue()?.runner);
      const previous = repo.queue()!.handedBack[0]!;
      writeFileSync(join(repo.root, 'qgate.mjs'), 'console.log("the repaired check passed");\n');
      repo.git('add', 'qgate.mjs');
      repo.git('commit', '--quiet', '-m', `repair the base${TRAILER}`);
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const retried = await repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => !repo.queue()?.runner);
      assert.equal(retried.status, 0, `${retried.out}\n${repo.log()}`);
      assert.match(retried.out, /b1 is in main/);
      assert.deepEqual(repo.queue()!.handedBack, [previous]);
      const landed = repo.queue()!.landed!.at(-1)!;
      assert.equal(landed.tip, previous.tip);
      assert.ok(landed.request);
      assert.notEqual(landed.request, previous.request);
    } finally {
      await repo.cleanup();
    }
  });

  test('preview, then queue with a message: the detached runner forms the candidate in the scope tree, runs it, and fast-forwards main', async () => {
    const repo = queueScratch('green');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      let result = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(result.status, 2, result.out);
      assert.match(result.out, /no preview/);
      result = await repo.run(['b1', '--preview']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /nothing is committed/);
      assert.match(result.out, /\+a/);
      assert.equal(existsSync(join(repo.root, 'a.txt')), false, 'a preview touches no file of the checkout');
      result = await repo.run(['b1', '--message', 'local/b1.msg']);
      assert.equal(result.status, 0, `${result.out}\n${repo.log()}`);
      assert.match(result.out, /queued b1/);
      assert.match(result.out, /runner started detached/);
      assert.match(result.out, /b1 is in main/);
      assert.equal(readFileSync(join(repo.root, 'a.txt'), 'utf8'), 'a\n', 'the fast-forward moved the checkout');
      assert.equal(repo.git('log', '-1', '--format=%s'), 'Merge b1: its words', "the message is the parent's file, verbatim");
      assert.match(repo.git('log', '-1', '--format=%P'), / /, 'one merge commit, --no-ff');
      const runs = q.readScopeRuns(repo.root) as { state: string; ran: { gate: string }[] }[];
      assert.equal(runs.at(-1)?.state, 'green');
      assert.deepEqual(runs.at(-1)?.ran.map((each) => each.gate).sort(), ['check', 'universal']);
      const verdicts = JSON.parse(readFileSync(join(repo.root, 'local', 'gate-verdicts.json'), 'utf8')) as { verdicts: { kind?: string }[] };
      assert.ok(verdicts.verdicts.every((each) => each.kind === 'scoped'), 'every verdict says the scoped run gave it');
      result = await repo.run(['--queue']);
      assert.equal(result.status, 0, result.out);
      assert.match(result.out, /the queue is empty/);
    } finally {
      await repo.cleanup();
    }
  });

  test('a red candidate of two: its check run on the base and every prefix, the member that broke it handed back, the other promoted', async () => {
    const repo = queueScratch('red');
    const hold = join(repo.root, 'local', 'go');
    try {
      repo.branch('good', { 'good.txt': 'fine\n' });
      repo.branch('bad', { 'break-check': 'x\n' });
      for (const name of ['good', 'bad']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const good = repo.run(['good', '--message', 'local/good.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const bad = repo.run(['bad', '--message', 'local/bad.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [goodOut, badOut] = await Promise.all([good, bad]);
      assert.equal(badOut.status, 1, `${badOut.out}\n${repo.log()}`);
      assert.match(badOut.out, /bad is handed back \(pinned: check\)/);
      assert.equal(goodOut.status, 0, `${goodOut.out}\n${repo.log()}`);
      assert.equal(existsSync(join(repo.root, 'good.txt')), true);
      assert.equal(existsSync(join(repo.root, 'break-check')), false, 'a red member never reaches main');
      const runs = q.readScopeRuns(repo.root) as ScopeRun[];
      const red = runs.find((each) => each.state === 'red');
      assert.deepEqual(red?.members.map((each) => each.branch), ['good', 'bad'], `one candidate takes both, never good alone first:\n${repo.log()}`);
      assert.deepEqual(red?.judged?.map((each) => [each.outcome, each.member]), [['pinned', 2]]);
      // It fails again when run once more holding the whole admission, so it is bisected.
      assert.deepEqual(red?.confirms?.map((each) => [each.gate, each.result, each.admission?.conclusive]), [['check', 'confirmed', true]], repo.log());
      assert.equal(runs.at(-1)?.state, 'green', 'the retained candidate is judged again, and promoted on its own green');
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('a direct commit that breaks a check stops the queue; a direct commit that fixes it lifts the stop, and the candidate re-forms', async () => {
    const repo = queueScratch('stop');
    try {
      writeFileSync(join(repo.root, 'break-universal'), 'x\n');
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a records commit that breaks a check${TRAILER}`);
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => Boolean(repo.queue()?.stop));
      assert.ok(repo.queue()?.stop, `the queue never stopped:\n${repo.log()}`);
      const said = await repo.run(['--queue']);
      assert.match(said.out, /stopped since/);
      assert.match(said.out, /universal fails on the base/);
      repo.git('rm', '--quiet', 'break-universal');
      repo.git('commit', '--quiet', '-m', `the fix${TRAILER}`);
      const done = await waiting;
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /the queue is stopped/);
      assert.match(repo.log(), /the stop on universal is lifted/);
      assert.equal(existsSync(join(repo.root, 'a.txt')), true);
    } finally {
      await repo.cleanup();
    }
  });

  // A branch whose merge commit cannot be made because its work is already in an earlier
  // member of the batch. `inner` is a branch at the first of `outer`'s two commits, so queued after it, a candidate holds it already.
  const nested = (repo: ReturnType<typeof queueScratch>, files: Record<string, string> = { 'a.txt': 'a\n', 'b.txt': 'b\n' }, outer = 'outer') => {
    const [head, ...rest] = Object.entries(files);
    const [first, text] = head!;
    repo.branch(outer, { [first]: text });
    repo.git('branch', 'inner', outer);
    repo.git('checkout', '--quiet', outer);
    for (const [path, body] of rest) {
      writeFileSync(join(repo.root, path), body);
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `more work on ${outer}${TRAILER}`);
    }
    repo.git('checkout', '--quiet', 'main');
    writeFileSync(join(repo.root, 'local', 'inner.msg'), `Merge inner: its words${TRAILER}\n`);
  };

  test('a member already in an earlier member of its batch is contained in it, not set aside; settled when that member promotes', async () => {
    const repo = queueScratch('contained');
    const hold = join(repo.root, 'local', 'go');
    try {
      nested(repo);
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const inner = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [outerOut, innerOut] = await Promise.all([outer, inner]);
      assert.equal(outerOut.status, 0, `${outerOut.out}\n${repo.log()}`);
      assert.equal(innerOut.status, 0, `${innerOut.out}\n${repo.log()}`);
      assert.match(innerOut.out, /inner is contained in outer, which promoted/);
      assert.doesNotMatch(repo.log(), /set aside|could not be made/);
      assert.match(repo.log(), /contained: inner is in outer already; settled when outer promotes/);
      assert.match(repo.log(), /contained in them, settled with them: inner/);
      assert.equal(repo.git('rev-list', '--count', '--merges', 'main'), '1', "one merge commit, the container's; nothing was merged for the contained one");
      const queue = repo.queue()!;
      assert.deepEqual(queue.contained!.map((each) => [each.branch, each.containedIn?.branch]), [['inner', 'outer']]);
      assert.equal(queue.handedBack.length, 0);
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('a member the line holds by the time its candidate forms is contained at once, and said so', async () => {
    const repo = queueScratch('contained-line');
    const gate = join(repo.root, 'local', 'gate-go');
    try {
      nested(repo);
      // Queued behind a candidate in flight: the enqueue refuses a branch the line holds, but main lacks inner until outer promotes.
      // Outer's `check` gate waits on `gate` until inner is queued, so outer cannot promote first on a loaded machine.
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg'], { QGATE_HOLD: gate });
      await repo.until(() => repo.queue()?.candidate !== null && repo.queue()?.candidate !== undefined);
      const inner = repo.run(['inner', '--message', 'local/inner.msg']);
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      writeFileSync(gate, '');
      const [outerOut, done] = await Promise.all([outer, inner]);
      assert.equal(outerOut.status, 0, `${outerOut.out}\n${repo.log()}`);
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /inner is contained: main holds it already/);
      assert.doesNotMatch(repo.log(), /set aside|could not be made/);
      assert.deepEqual(repo.queue()!.contained!.map((each) => [each.branch, each.containedIn]), [['inner', { line: true }]]);
      assert.match((await repo.run(['--queue'])).out, /contained: inner \(the line held it already\)/);
    } finally {
      await repo.cleanup();
    }
  });

  test('a contained member whose container is then handed back is judged again, on its own, and promoted', async () => {
    const repo = queueScratch('contained-back');
    const hold = join(repo.root, 'local', 'go');
    try {
      // inner is the work before outer's commit that breaks the check: outer is pinned, and what it held of inner stands alone.
      nested(repo, { 'a.txt': 'a\n', 'break-check': 'x\n' });
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const inner = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [outerOut, innerOut] = await Promise.all([outer, inner]);
      assert.equal(outerOut.status, 1, `${outerOut.out}\n${repo.log()}`);
      assert.match(outerOut.out, /outer is handed back \(pinned: check\)/);
      assert.equal(innerOut.status, 0, `${innerOut.out}\n${repo.log()}`);
      assert.match(innerOut.out, /inner is in main/, 'judged again and landed on its own, not settled as contained in a member that failed');
      assert.equal(existsSync(join(repo.root, 'a.txt')), true);
      assert.equal(existsSync(join(repo.root, 'break-check')), false, 'a red member never reaches main');
      const queue = repo.queue() as unknown as { contained?: unknown[]; landed: { branch: string }[] };
      assert.equal(queue.contained, undefined, 'nothing was settled as contained: the container never promoted');
      assert.deepEqual(queue.landed.map((each) => each.branch), ['inner']);
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  // The run's own copy of the candidate must not keep a contained member dropped meanwhile.
  test('a contained member dropped by name while its container runs, and queued again, is not brought back by the container\'s hand-back: only the new request lands', async () => {
    const repo = queueScratch('contained-dropped');
    const dir = join(repo.root, 'local', 'drain');
    const hold = join(repo.root, 'local', 'go');
    mkdirSync(dir, { recursive: true });
    try {
      // The check holds where `break-check` stands, until `release`: the container's run is under way while the drop comes.
      writeFileSync(join(repo.root, 'qgate.mjs'), treeGate(dir, "existsSync('break-check')"));
      repo.git('commit', '--quiet', '-am', `a gate that holds a breaking tree${TRAILER}`);
      nested(repo, { 'a.txt': 'a\n', 'break-check': 'x\n' });
      for (const name of ['outer', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const outer = repo.run(['outer', '--message', 'local/outer.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const first = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      await repo.until(() => existsSync(join(dir, 'check.pid')));
      assert.deepEqual(repo.queue()!.candidate!.contained!.map((each) => each.branch), ['inner'], repo.log());
      assert.equal((await repo.run(['--drop-batch', 'inner'])).status, 0);
      const dropped = await first;
      assert.equal(dropped.status, 1, dropped.out);
      const old = repo.queue()!.handedBack.find((each) => each.branch === 'inner')!.request;
      assert.equal((await repo.run(['inner', '--preview'])).status, 0);
      const again = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => repo.queue()?.queued.some((each) => each.branch === 'inner') === true);
      const fresh = repo.queue()!.queued.find((each) => each.branch === 'inner')!.request;
      writeFileSync(join(dir, 'release'), '');
      const [outerOut, againOut] = await Promise.all([outer, again]);
      assert.equal(outerOut.status, 1, `${outerOut.out}\n${repo.log()}`);
      assert.equal(againOut.status, 0, `${againOut.out}\n${repo.log()}`);
      assert.match(againOut.out, /inner is in main/, 'the new request merges on its own');
      const landed = repo.queue()!.landed!.filter((each) => each.branch === 'inner');
      assert.deepEqual(landed.map((each) => each.request), [fresh], `the dropped request ${old} never lands:\n${repo.log()}`);
      assert.equal((repo.queue()!.contained ?? []).length, 0);
    } finally {
      writeFileSync(join(dir, 'release'), '');
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  // The row tables are not read from the candidate's tip, which an earlier member may have changed.
  test("a row table a member's own declaration adds settles nothing for a later member of its candidate: the declaration read is the line's the candidate started on", async () => {
    const repo = queueScratch('rows-line');
    const hold = join(repo.root, 'local', 'go');
    try {
      const table = (...rows: string[]) => ['# Table', '', '| When | Run |', '|---|---|', '| one | `a` |', ...rows, ''].join('\n');
      const gates = JSON.parse(readFileSync(join(repo.root, 'daoris.gates.json'), 'utf8')) as { gates: unknown[] };
      mkdirSync(join(repo.root, 'docs'), { recursive: true });
      writeFileSync(join(repo.root, 'docs', 'table.md'), table());
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a table${TRAILER}`);
      const declares = `${JSON.stringify({ ...gates, integration: { merge: [{ paths: 'docs/table.md', by: 'rows' }] } }, null, 2)}\n`;
      repo.branch('declares', { 'daoris.gates.json': declares, 'docs/table.md': table('| two | `b` |') });
      repo.branch('row', { 'docs/table.md': table('| three | `c` |') });
      for (const name of ['declares', 'row']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const first = repo.run(['declares', '--message', 'local/declares.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const second = repo.run(['row', '--message', 'local/row.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [declared, rowed] = await Promise.all([first, second]);
      assert.equal(declared.status, 0, `${declared.out}\n${repo.log()}`);
      assert.equal(rowed.status, 1, `${rowed.out}\n${repo.log()}`);
      assert.match(rowed.out, /row is handed back \(conflict: a conflict in docs\/table\.md\)/);
      assert.doesNotMatch(repo.log(), /resolved by the merge base \(a declared row table\)/);
      assert.equal(readFileSync(join(repo.root, 'docs', 'table.md'), 'utf8'), table('| two | `b` |'));
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test("a preview names a conflict in a row table the line declares as one the merge settles, never as one the resolvers cannot merge", async () => {
    const repo = queueScratch('rows-preview');
    try {
      const table = (...rows: string[]) => ['# Table', '', '| When | Run |', '|---|---|', '| one | `a` |', ...rows, ''].join('\n');
      const gates = JSON.parse(readFileSync(join(repo.root, 'daoris.gates.json'), 'utf8')) as { gates: unknown[] };
      mkdirSync(join(repo.root, 'docs'), { recursive: true });
      writeFileSync(join(repo.root, 'docs', 'table.md'), table());
      writeFileSync(join(repo.root, 'daoris.gates.json'), `${JSON.stringify({ ...gates, integration: { merge: [{ paths: 'docs/table.md', by: 'rows' }] } }, null, 2)}\n`);
      repo.git('add', '-A');
      repo.git('commit', '--quiet', '-m', `a declared table${TRAILER}`);
      repo.branch('row', { 'docs/table.md': table('| three | `c` |') });
      writeFileSync(join(repo.root, 'docs', 'table.md'), table('| two | `b` |'));
      repo.git('commit', '--quiet', '-am', `main adds a row${TRAILER}`);
      const previewed = await repo.run(['row', '--preview']);
      assert.equal(previewed.status, 0, previewed.out);
      assert.match(previewed.out, /conflicts: docs\/table\.md/);
      assert.doesNotMatch(previewed.out, /the resolvers cannot merge/);
      assert.match(previewed.out, /a union record, the backlog, a declared row table or a generated folder/);
    } finally {
      await repo.cleanup();
    }
  });

  // The line's marker is not the word `line`, which a branch may be named.
  test('a container whose branch is named `line` holds its member provisionally, as any member does: handed back, the member is judged again on its own', async () => {
    const repo = queueScratch('contained-named-line');
    const hold = join(repo.root, 'local', 'go');
    try {
      nested(repo, { 'a.txt': 'a\n', 'break-check': 'x\n' }, 'line');
      for (const name of ['line', 'inner']) assert.equal((await repo.run([name, '--preview'])).status, 0);
      const container = repo.run(['line', '--message', 'local/line.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 1);
      const inner = repo.run(['inner', '--message', 'local/inner.msg'], { MERGE_BRANCH_QUEUE_HOLD: hold });
      await repo.until(() => (repo.queue()?.queued.length ?? 0) === 2);
      writeFileSync(hold, '');
      const [containerOut, innerOut] = await Promise.all([container, inner]);
      assert.equal(containerOut.status, 1, `${containerOut.out}\n${repo.log()}`);
      assert.match(repo.log(), /contained: inner is in line already; settled when line promotes/);
      assert.equal(innerOut.status, 0, `${innerOut.out}\n${repo.log()}`);
      assert.doesNotMatch(innerOut.out, /main holds it already/, 'never settled as the line\'s');
      assert.match(innerOut.out, /inner is in main/);
      assert.equal(existsSync(join(repo.root, 'a.txt')), true, 'its work is in main, merged on its own');
    } finally {
      writeFileSync(hold, '');
      await repo.cleanup();
    }
  });

  test('a tracked change in the main checkout holds promotion until it is clean', async () => {
    const repo = queueScratch('dirty');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      writeFileSync(join(repo.root, 'qgate.mjs'), `${QUEUE_GATE}// an edit the parent has not committed\n`);
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => repo.queue()?.candidate?.state === 'held');
      assert.equal(repo.queue()?.candidate?.state, 'held', repo.log());
      repo.git('checkout', '--quiet', '--', 'qgate.mjs');
      const done = await waiting;
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      assert.match(done.out, /held on checkout/);
    } finally {
      await repo.cleanup();
    }
  });

  test('a test that fails on a held index leaves no enqueue and no runner behind, and an enqueue without a verdict is ended at its bound', async () => {
    const repo = queueScratch('forced-lock');
    let enqueue: number[] = [];
    let runner = 0;
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      writeFileSync(join(repo.root, 'qgate.mjs'), `${QUEUE_GATE}// an edit the parent has not committed\n`);
      // The enqueue the sighting left behind: a candidate green and held on the dirty checkout never gives a verdict.
      const waiting = repo.run(['b1', '--message', 'local/b1.msg']);
      await repo.until(() => repo.queue()?.candidate?.state === 'held');
      assert.equal(repo.queue()?.candidate?.state, 'held', repo.log());
      enqueue =repo.running();
      runner = (repo.queue()!.runner as { pid: number }).pid;
      assert.equal(enqueue.length, 1);
      // The lock the sighting met, held by another writer: the test's own git write is refused.
      writeFileSync(join(repo.root, '.git', 'index.lock'), '');
      const refused = spawnSync('git', ['checkout', '--quiet', '--', 'qgate.mjs'], { cwd: repo.root, encoding: 'utf8', env: repo.env, windowsHide: true });
      assert.notEqual(refused.status, 0);
      assert.match(refused.stderr, /index\.lock/);
      // Ending the test here, as a failed assertion does, must leave neither child holding the run.
      rmSync(join(repo.root, '.git', 'index.lock'), { force: true });
      await repo.cleanup();
      const ended = await waiting;
      assert.notEqual(ended.status, 0, 'the waiting enqueue ended with its test');
      assert.deepEqual(enqueue.filter(alive), [], 'no enqueue is left running');
      assert.equal(alive(runner), false, 'the held runner is ended');
    } finally {
      rmSync(join(repo.root, '.git', 'index.lock'), { force: true });
      await repo.cleanup();
    }
  });

  test('an invocation with no verdict is ended at its bound, saying so', async () => {
    const repo = queueScratch('bound');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      writeFileSync(join(repo.root, 'qgate.mjs'), `${QUEUE_GATE}// an edit the parent has not committed\n`);
      // patience: claim — the invocation is ended at this bound, which is what the row tests
      const ended = await repo.run(['b1', '--message', 'local/b1.msg'], {}, 5_000);
      assert.notEqual(ended.status, 0, ended.out);
      assert.match(ended.out, /no verdict within 5000 ms/);
      assert.deepEqual(repo.running(), [], ended.out);
    } finally {
      await repo.cleanup();
    }
  });

  // A runner started in a folder git reads as the session's tree would use the main checkout's queue home.
  test('a runner, a preview, an enqueue, a drop or a recheck started in a linked worktree of the repository is refused, saying whose the queue is, and its queue is left as it was', async () => {
    const repo = queueScratch('linked');
    try {
      repo.branch('b1', { 'a.txt': 'a\n' });
      const linked = join(dirname(repo.root), 'linked');
      repo.git('worktree', 'add', '--quiet', '--detach', linked, 'main');
      mkdirSync(join(linked, 'local'), { recursive: true });
      writeFileSync(join(linked, 'local', 'b1.msg'), `Merge b1: its words${TRAILER}\n`);
      for (const args of [['--queue-run'], ['b1', '--preview'], ['b1', '--message', 'local/b1.msg'], ['--drop-batch', 'b1'], ['--queue', '--recheck']]) {
        const refused = await repo.run(args, {}, HANG_MS, linked);
        assert.equal(refused.status, 2, `${args.join(' ')}: ${refused.out}`);
        assert.match(refused.out, /is not the queue's checkout: the queue is kept in .*repo.*, the repository's main checkout, and only that checkout queues to it or runs it/, args.join(' '));
      }
      assert.equal(repo.queue(), null, 'nothing was written to the queue');
      assert.equal(repo.log(), '', 'no runner was started');
    } finally {
      await repo.cleanup();
    }
  });

  test("a stand-in gate's variable and the tool's switches a runner inherited from the enqueue that started it reach none of its gates", async () => {
    const repo = queueScratch('gate-env');
    try {
      const record = join(repo.root, 'local', 'gate-env.json');
      repo.branch('b1', { 'qgate.mjs': envGate(record) });
      assert.equal((await repo.run(['b1', '--preview'])).status, 0);
      // An enqueue whose environment holds the class probe's folder: the runner it starts would inherit it.
      const done = await repo.run(['b1', '--message', 'local/b1.msg'], { QUEUE_DRAIN_DIR: join(repo.root, 'local', 'drain') });
      assert.equal(done.status, 0, `${done.out}\n${repo.log()}`);
      const seen = JSON.parse(readFileSync(record, 'utf8')) as { drain: string | null; tick: string | null };
      assert.deepEqual(seen.drain, null, 'the stand-in gate\'s variable is not passed on');
      assert.deepEqual(seen.tick, null, "the tool's own switches are not passed on");
    } finally {
      await repo.cleanup();
    }
  });
});
