import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot, tool, refusal, INTEGRATION_DECLARATIONS, UNION_JOINS } from './support/merge-branch.ts';
import { PROCESS_FAILURE, CATALOGUE_FAILURE, TIMED_SPEC, PLAIN_SPEC, measuredLog } from './support/merge-branch-kit.ts';

// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/union-joins.json — the driver's twin sets the same joins apart, row for row
// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json — the driver's twin reads the table row for row


// A repository's `integration` is read whole, in one place per twin; the driver's IntegrationDeclaration holds the
// same table, row for row, and row c's row tables are read through it.
test("the integration declaration reads whole as the table the driver's reader holds too (INTEGRATE1d)", () => {
  type Row = { name: string; declaration: string | null; problem: string | null; read: Record<string, unknown> | null };
  const table = JSON.parse(readFileSync(join(repoRoot, INTEGRATION_DECLARATIONS), 'utf8')) as { rows: Row[] };
  assert.ok(table.rows.length >= 40, 'the table holds its rows');
  for (const row of table.rows) {
    const said = tool.integrationOf(row.declaration);
    assert.deepEqual({ problem: said.problem, read: said.integration }, { problem: row.problem, read: row.read }, row.name);
  }
  // The row tables are the rules by rows, once each, in the order first declared; a line rule and a declaration with a problem none.
  assert.deepEqual(tool.rowTablesOf(JSON.stringify({ integration: { merge: [
    { paths: 'b.md', by: 'rows' }, { paths: 'docs/STATUS.md', line: '^Status: ', run: 'node s.mjs' }, { paths: 'a.md', by: 'rows' }, { paths: 'b.md', by: 'rows' },
  ] } })), ['b.md', 'a.md']);
  assert.deepEqual(tool.rowTablesOf(JSON.stringify({ integration: { by: 'agent', merge: [{ paths: 'b.md', by: 'rows' }] } })), []);
});


// The runner's inputs, each missing, malformed or contradicting one a refusal in a sentence.
test('the declared runner validates its inputs: each missing, malformed or contradicting one is refused in a sentence (INTEGRATE1j)', () => {
  const tip = 'a'.repeat(40);
  const base = 'b'.repeat(40);
  const absolute = (name: string) => join(process.cwd(), name);
  const ok = { DAORIS_VERIFY_COMMIT: tip, DAORIS_VERIFY_RESULT: absolute('answer.json'), DAORIS_VERIFY_DECLARATION: absolute('declaration.json') };
  const without = (name: string) => Object.fromEntries(Object.entries(ok).filter(([each]) => each !== name));
  assert.deepEqual(tool.verifyInputs(ok), { commit: tip, result: ok.DAORIS_VERIFY_RESULT, mode: null, base: null, declaration: ok.DAORIS_VERIFY_DECLARATION, tree: null, gates: null, failure: null });
  assert.equal(tool.verifyInputs({ ...ok, DAORIS_VERIFY_MODE: 'commits', DAORIS_VERIFY_BASE: base }).mode, 'commits');
  // Every mode is built; each names its base, `run` and `plan` take the gates named, `probe` the failure's file.
  for (const mode of ['run', 'plan', 'probe']) {
    const more = mode === 'probe' ? { DAORIS_VERIFY_FAILURE: absolute('failure.json') } : {};
    assert.equal(tool.verifyInputs({ ...ok, DAORIS_VERIFY_MODE: mode, DAORIS_VERIFY_BASE: base, ...more }).mode, mode);
  }
  assert.deepEqual(tool.verifyInputs({ ...ok, DAORIS_VERIFY_MODE: 'run', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_GATES: ' check , The web gate' }).gates, ['check', 'The web gate']);
  assert.deepEqual(tool.verifyInputs({ ...ok, DAORIS_VERIFY_MODE: 'plan', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_GATES: 'check' }).gates, ['check']);
  assert.equal(tool.verifyInputs({ ...ok, DAORIS_VERIFY_MODE: 'probe', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_FAILURE: absolute('failure.json') }).failure, absolute('failure.json'));
  assert.equal(tool.verifyInputs({ ...ok, DAORIS_VERIFY_TREE: absolute('tree') }).tree, absolute('tree'));
  // A longer object id (sha-256) is a full id too, and the names are found whatever their case on a platform that folds it.
  assert.equal(tool.verifyInputs({ ...ok, DAORIS_VERIFY_COMMIT: 'c'.repeat(64) }).commit, 'c'.repeat(64));
  assert.equal(tool.verifyInputs({ ...without('DAORIS_VERIFY_COMMIT'), Daoris_Verify_Commit: tip }).commit, tip);
  const cases: [string, Record<string, string | undefined>, RegExp, { besideCommit?: string | null }?][] = [
    ['no commit', without('DAORIS_VERIFY_COMMIT'), /DAORIS_VERIFY_COMMIT is not set: --daoris-verify gates the commit it names/],
    ['an empty commit', { ...ok, DAORIS_VERIFY_COMMIT: '' }, /DAORIS_VERIFY_COMMIT is not set/],
    ['a short commit', { ...ok, DAORIS_VERIFY_COMMIT: 'aaaaaaa' }, /DAORIS_VERIFY_COMMIT is not a full object id/],
    ['a branch for a commit', { ...ok, DAORIS_VERIFY_COMMIT: 'main' }, /DAORIS_VERIFY_COMMIT is not a full object id/],
    ['an upper-case commit', { ...ok, DAORIS_VERIFY_COMMIT: 'A'.repeat(40) }, /DAORIS_VERIFY_COMMIT is not a full object id/],
    ['a commit named beside the flag', ok, /takes its commit from DAORIS_VERIFY_COMMIT/, { besideCommit: 'abc1234' }],
    ['no result file', without('DAORIS_VERIFY_RESULT'), /DAORIS_VERIFY_RESULT is not set: the file to answer in/],
    ['a relative result file', { ...ok, DAORIS_VERIFY_RESULT: 'answer.json' }, /DAORIS_VERIFY_RESULT is not an absolute path/],
    ['no declaration', without('DAORIS_VERIFY_DECLARATION'), /DAORIS_VERIFY_DECLARATION is not set: the gates are read from the declaration handed, never from the tree/],
    ['a relative declaration', { ...ok, DAORIS_VERIFY_DECLARATION: 'declaration.json' }, /DAORIS_VERIFY_DECLARATION is not an absolute path/],
    ['an unknown mode', { ...ok, DAORIS_VERIFY_MODE: 'bisect' }, /DAORIS_VERIFY_MODE 'bisect' is none of run, plan, probe or commits/],
    ['an empty mode', { ...ok, DAORIS_VERIFY_MODE: '' }, /DAORIS_VERIFY_MODE is set and empty/],
    ['run without a base', { ...ok, DAORIS_VERIFY_MODE: 'run' }, /DAORIS_VERIFY_BASE is not set: the run mode reaches its gates by base\.\.commit/],
    ['plan without a base', { ...ok, DAORIS_VERIFY_MODE: 'plan' }, /DAORIS_VERIFY_BASE is not set: the plan mode reaches its gates by base\.\.commit/],
    ['probe without a base', { ...ok, DAORIS_VERIFY_MODE: 'probe', DAORIS_VERIFY_FAILURE: absolute('failure.json') }, /DAORIS_VERIFY_BASE is not set: the probe mode's answer names the candidate's base/],
    ['probe without a failure', { ...ok, DAORIS_VERIFY_MODE: 'probe', DAORIS_VERIFY_BASE: base }, /DAORIS_VERIFY_FAILURE is not set: the probe mode repeats the failure that file names/],
    ['probe on a relative failure', { ...ok, DAORIS_VERIFY_MODE: 'probe', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_FAILURE: 'failure.json' }, /DAORIS_VERIFY_FAILURE is not an absolute path/],
    ['commits without a base', { ...ok, DAORIS_VERIFY_MODE: 'commits' }, /DAORIS_VERIFY_BASE is not set: the commits mode checks base\.\.commit/],
    ['commits on a short base', { ...ok, DAORIS_VERIFY_MODE: 'commits', DAORIS_VERIFY_BASE: 'bbbb' }, /DAORIS_VERIFY_BASE is not a full object id/],
    ['a base with no mode', { ...ok, DAORIS_VERIFY_BASE: base }, /DAORIS_VERIFY_BASE belongs to a mode: none is asked/],
    ['gates with no mode', { ...ok, DAORIS_VERIFY_GATES: 'check' }, /DAORIS_VERIFY_GATES belongs to the run and plan modes/],
    ['gates in commits', { ...ok, DAORIS_VERIFY_MODE: 'commits', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_GATES: 'check' }, /DAORIS_VERIFY_GATES belongs to the run and plan modes/],
    ['gates in a probe', { ...ok, DAORIS_VERIFY_MODE: 'probe', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_FAILURE: absolute('failure.json'), DAORIS_VERIFY_GATES: 'check' }, /DAORIS_VERIFY_GATES belongs to the run and plan modes/],
    ['gates set and empty', { ...ok, DAORIS_VERIFY_MODE: 'run', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_GATES: '' }, /DAORIS_VERIFY_GATES is set and empty/],
    ['gates with an empty name', { ...ok, DAORIS_VERIFY_MODE: 'run', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_GATES: 'check,,web' }, /DAORIS_VERIFY_GATES holds an empty name/],
    ['gates naming one twice', { ...ok, DAORIS_VERIFY_MODE: 'plan', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_GATES: 'check, check' }, /DAORIS_VERIFY_GATES names 'check' twice/],
    ['a failure with no mode', { ...ok, DAORIS_VERIFY_FAILURE: absolute('failure.json') }, /DAORIS_VERIFY_FAILURE belongs to the probe mode/],
    ['a failure in a run', { ...ok, DAORIS_VERIFY_MODE: 'run', DAORIS_VERIFY_BASE: base, DAORIS_VERIFY_FAILURE: absolute('failure.json') }, /DAORIS_VERIFY_FAILURE belongs to the probe mode/],
    ['a relative tree', { ...ok, DAORIS_VERIFY_TREE: 'tree' }, /DAORIS_VERIFY_TREE is not an absolute path/],
    ['a name the runner does not know', { ...ok, DAORIS_VERIFY_SPEED: 'fast' }, /DAORIS_VERIFY_SPEED is no input of the runner/],
  ];
  for (const [what, env, message, options] of cases) {
    const error = refusal(() => tool.verifyInputs(env, options));
    assert.equal(error.exitCode, 2, what);
    assert.match(error.message, message, what);
  }
});


test('the declared runner reads its gates from the declaration it is handed, under the names it writes, with no workflow step added (INTEGRATE1j)', () => {
  const declaration = JSON.stringify({
    gates: [
      { name: 'Web tests', run: 'node suite.mjs web' },
      { name: 'check', run: 'node tools/x.mjs --check' },
      { name: 'web/tests', run: 'node suite.mjs other' },
    ],
  });
  const plan = tool.verifyPlan(declaration);
  // Fast first, as the plan orders; the log names are the plan's own slugs, the answer's names the declaration's.
  assert.deepEqual(plan.map((gate) => gate.declared), ['check', 'Web tests', 'web/tests']);
  assert.deepEqual(plan.map((gate) => gate.name), ['check', 'Web-tests', 'web-tests']);
  assert.deepEqual(refusal(() => tool.verifyPlan(JSON.stringify({ gates: [{ name: 'a', run: 'x' }, { name: 'a', run: 'y' }] }))).message.includes("names 'a' twice"), true);
  assert.match(refusal(() => tool.verifyPlan('{ not json')).message, /declaration handed is not valid JSON/);
  assert.match(refusal(() => tool.verifyPlan(JSON.stringify({ gates: [] }))).message, /declares no gate/);
  assert.match(refusal(() => tool.verifyPlan(JSON.stringify({ gates: [{ name: 'a' }] }))).message, /needs both 'name' and 'run'/);
  // The declaration's own `integration` is read by the one reader; a broken one is refused with its sentence.
  assert.match(refusal(() => tool.verifyPlan(JSON.stringify({ gates: [{ name: 'a', run: 'x' }], integration: { by: 'nobody' } }))).message, /integration/);
  assert.match(refusal(() => tool.verifyPlan(JSON.stringify({ gates: [{ name: 'a', run: 'x' }], integration: { commits: 'nope' } }))).message, /integration\.commits/);
});


test('the declared runner answers each gate under the declared name with the verdict the schema names, its log where it is kept (INTEGRATE1j)', () => {
  const plan = ['check', 'suite', 'late', 'hung', 'flaky'].map((name) => ({ name: name.toLowerCase(), declared: `The ${name}` }));
  const result = (name: string, verdict: string, code?: number) => ({ gate: { name }, verdict, ...(code === undefined ? {} : { code }), log: `/tree/${name}.log` });
  const gates = tool.verifyGates(
    plan,
    [result('check', 'PASS', 0), result('suite', 'FAIL', 3), result('hung', 'ENDED'), result('flaky', 'FLAKE', 0)],
    { keep: (log) => log.replace('/tree/', '/kept/'), seconds: (name) => (name === 'check' ? 12 : undefined) },
  );
  assert.deepEqual(gates, [
    { name: 'The check', verdict: 'pass', wallSeconds: 12, log: '/kept/check.log' },
    { name: 'The suite', verdict: 'fail', exit: 3, log: '/kept/suite.log' },
    { name: 'The late', verdict: 'not-run' },
    { name: 'The hung', verdict: 'ended', log: '/kept/hung.log' },
    { name: 'The flaky', verdict: 'flake', log: '/kept/flaky.log' },
  ]);
});


test('the declared runner answers its commits mode with the commit check\'s problems for base..tip, and none for a clean range (INTEGRATE1j)', () => {
  const commit = 'a'.repeat(40);
  const base = 'b'.repeat(40);
  const own = { merge: false, subject: 'work', trailer: 'Claude <noreply@anthropic.com>' };
  const answer = tool.verifyCommitsAnswer({
    commit, base, declaration: 'c'.repeat(40),
    commits: [{ sha: '1111111', ...own }, { sha: '2222222', merge: false, subject: 'bare', trailer: '' }, { sha: '3333333', merge: true, subject: 'Merge main', trailer: '' }],
  });
  assert.equal(answer.schema, 2);
  assert.deepEqual([answer.mode, answer.commit, answer.base, answer.declaration], ['commits', commit, base, 'c'.repeat(40)]);
  assert.equal((answer.problems as string[]).length, 1);
  assert.match((answer.problems as string[])[0]!, /^2222222 bare: no Co-Authored-By line/);
  assert.deepEqual(tool.verifyCommitsAnswer({ commit, base, declaration: 'c'.repeat(40), commits: [{ sha: '1111111', ...own }] }).problems, []);
});


// What a gate ran with, kept beside its log, so a probe repeats one of its failures as composed.
test('a gate\'s composition records its units and settings with its environment\'s rule, and digests both the same whatever their order (INTEGRATE1j)', () => {
  const gate = { name: 'driver-process', declared: 'driver-process', run: 'dotnet test src/X.Tests --settings src/X/process.runsettings' };
  const runsettings = { path: 'src/X/process.runsettings', text: '<RunSettings><TestCaseFilter>Category=Process</TestCaseFilter></RunSettings>\n' };
  const made = tool.verifyComposition({ gate, runsettings, units: ['X.B', 'X.A'], narrowed: ['X.D', 'X.C'] });
  assert.deepEqual(made.units, ['X.A', 'X.B']);
  assert.deepEqual(made.settings, { gate: 'driver-process', run: gate.run, cwd: null, runsettings, narrowed: ['X.C', 'X.D'], environment: tool.compositionEnvironment() });
  assert.match(made.digest, /^sha256:[0-9a-f]{64}$/);
  assert.equal(made.digest, tool.compositionDigest(made.units, made.settings));
  // The digest is of the content, not of the order its keys were written in.
  assert.equal(tool.compositionDigest(['X.A', 'X.B'], Object.fromEntries(Object.entries(made.settings).reverse())), made.digest);
  // Each part moves it.
  const of = (change: object) => tool.verifyComposition({ gate, runsettings, units: ['X.A', 'X.B'], narrowed: ['X.C', 'X.D'], ...change }).digest;
  const digests = [
    made.digest, of({ units: ['X.A'] }), of({ units: null }), of({ narrowed: [] }), of({ runsettings: { ...runsettings, text: '<RunSettings/>' } }),
    of({ gate: { ...gate, run: `${gate.run} --no-restore` } }), of({ gate: { ...gate, cwd: 'src' } }), of({ gate: { ...gate, declared: 'Another' } }),
  ];
  assert.equal(new Set(digests).size, digests.length, 'every part of the composition is in its digest');
  // The environment is its rule (§5.5): the names a gate never inherits and the build servers' three, never a value the machine holds.
  const environment = tool.compositionEnvironment();
  assert.deepEqual(Object.keys(environment).sort(), ['added', 'removed']);
  assert.deepEqual(environment.added, tool.BESIDE_ENVIRONMENT);
  assert.ok(environment.removed.some((pattern) => pattern.includes('DAORIS_')) && environment.removed.some((pattern) => pattern.includes('GIT_')), environment.removed.join(' '));
  // A command naming no run settings has none, and a gate run whole has no units.
  const whole = tool.verifyComposition({ gate: { name: 'tools', declared: 'The tools', run: 'node --test' } });
  assert.deepEqual([whole.units, whole.settings.runsettings, whole.settings.gate, whole.settings.narrowed], [null, null, 'The tools', []]);
  // What differs between the composition a probe would build and the one kept, in words.
  assert.deepEqual(tool.compositionDifference(made.settings, made.settings), []);
  assert.deepEqual(tool.compositionDifference({ ...made.settings, runsettings: { ...runsettings, text: '<RunSettings/>' } }, made.settings), ['the run settings src/X/process.runsettings']);
  assert.deepEqual(tool.compositionDifference({ ...made.settings, run: 'node x', environment: { removed: [], added: {} } }, made.settings), ["the declaration's command", "the gate environment's rule"]);
});


test('a run answers each gate it ran with its composition, kept beside its log, and the failures it read from it (INTEGRATE1j)', () => {
  const plan = [{ name: 'check', declared: 'The check' }, { name: 'suite', declared: 'suite' }, { name: 'late', declared: 'late' }];
  const results = [
    { gate: { name: 'check' }, verdict: 'PASS', code: 0, log: '/tree/check.log' },
    { gate: { name: 'suite' }, verdict: 'FAIL', code: 1, log: '/tree/suite.log' },
  ];
  const digest = (n: string) => `sha256:${n.repeat(64)}`;
  const failure = { unit: 'S.SessionTests', test: 'S.SessionTests.Resumes', signature: { test: 'S.SessionTests.Resumes', exception: 'X', message: 'y' } };
  const gates = tool.verifyRunGates(plan, results, {
    keep: (path) => (path.startsWith('/tree/') ? path.replace('/tree/', '/kept/') : undefined),
    seconds: (name) => (name === 'check' ? 3 : undefined),
    compositions: new Map([
      ['check', { units: null, digest: digest('a'), file: '/tree/check.composition.json' }],
      ['suite', { units: ['S.SessionTests'], digest: digest('b'), file: '/tree/suite.composition.json' }],
      ['late', { units: null, digest: digest('c'), file: '/gone/late.composition.json' }],
    ]),
    failures: new Map([['suite', [failure]]]),
  });
  assert.deepEqual(gates, [
    { name: 'The check', verdict: 'pass', wallSeconds: 3, log: '/kept/check.log', composition: { units: null, settings: '/kept/check.composition.json', digest: digest('a') }, failures: [] },
    { name: 'suite', verdict: 'fail', exit: 1, log: '/kept/suite.log', composition: { units: ['S.SessionTests'], settings: '/kept/suite.composition.json', digest: digest('b') }, failures: [failure] },
    // A settings file that is not kept is not named: the digest still says what the gate is to run with.
    { name: 'late', verdict: 'not-run', composition: { units: null, digest: digest('c') }, failures: [] },
  ]);
});


test('the declared runner\'s plan names each declared gate once, under its declared name: run, kept on the pass that stands, or deferred (INTEGRATE1j)', () => {
  const gate = (name: string, run: string, declared = name) => ({ name, declared, run, kind: 'suite' });
  const plan = [
    gate('universal', 'npm run universal'), gate('web-types', 'npm run typecheck:web'), gate('driver', 'dotnet test src/D.Tests --filter "Category!=Process"', 'The driver'),
    gate('driver-process', 'dotnet test src/D.Tests --settings src/D/process.runsettings'), gate('driver-git', 'dotnet test src/D.Tests --settings src/D/git.process.runsettings'),
    gate('family', 'npm run rehearse:family'), gate('service', 'dotnet test src/S.Tests'), gate('web', 'npm run test:web'),
  ];
  const set = {
    run: [
      { name: 'universal', why: 'the cheap tier runs in every candidate' }, { name: 'driver', why: 'a reached .NET fast half runs whole' },
      { name: 'driver-process', why: 'a reached Process half, by class' }, { name: 'driver-git', why: 'the git half, within its budget' },
    ],
    kept: [{ name: 'web-types', why: 'its pass stands on this tree' }],
    deferred: [{ name: 'family', why: 'a browser spec or a rehearsal: deferred to the background run, which the install waits for' }],
  };
  const pass = (name: string, commit: string, tree: string, standing = 'exact') => ({
    name, verdict: { gate: name, verdict: 'PASS', tree, at: '2026-10-29T10:00:00Z', commit }, standing: { standing, paths: [], records: [] },
  });
  const kept = [pass('web-types', 'c1', 't1'), pass('service', 'c2', 't2', 'unreached')];
  const shaped = [
    { name: 'universal', units: null, narrowed: [] }, { name: 'driver', units: null, narrowed: [] },
    { name: 'driver-process', units: ['D.A', 'D.B'], narrowed: ['D.C'], reach: { whole: null } },
    { name: 'driver-git', units: ['G.A'], narrowed: ['G.B', 'G.C'], deferredClasses: ['G.C'], reach: { whole: null } },
  ];
  const answer = tool.verifyPlanAnswer({ plan, set, kept, gates: shaped });
  assert.deepEqual(answer.run, [
    { name: 'universal', why: 'the cheap tier runs in every candidate' },
    { name: 'The driver', why: 'a reached .NET fast half runs whole' },
    { name: 'driver-process', why: 'a reached Process half, by class: 2 classes by reach, 1 class kept on passes that stand' },
    { name: 'driver-git', why: 'the git half, within its budget: 1 class within its budget, 1 class deferred to the background run' },
  ]);
  assert.deepEqual(answer.kept, [
    { name: 'web-types', why: 'its pass stands on this tree', commit: 'c1', tree: 't1', standing: 'exact' },
    { name: 'service', why: 'no path the candidate changes reaches it, and its pass stands on this tree', commit: 'c2', tree: 't2', standing: 'unreached' },
  ]);
  assert.deepEqual(answer.deferred, [
    { name: 'family', why: 'a browser spec or a rehearsal: deferred to the background run, which the install waits for' },
    { name: 'web', why: 'no path the candidate changes reaches it: the background run answers for it' },
  ]);
  // A Process half every class of which is kept passes without running: kept, on its last pass.
  const composed = tool.verifyPlanAnswer({
    plan, set, kept, gates: shaped.filter((each) => each.name !== 'driver-process'),
    composed: [{ name: 'driver-process', why: 'its last verdict went stale by reach alone', last: { verdict: { commit: 'c3', tree: 't3' } } }],
  });
  assert.equal(composed.run.some((each) => each.name === 'driver-process'), false);
  assert.deepEqual(composed.kept.find((each) => each.name === 'driver-process'), {
    name: 'driver-process', why: "every class's pass stands, so it passes without running: its last verdict went stale by reach alone", commit: 'c3', tree: 't3', standing: 'composed',
  });
  // DAORIS_VERIFY_GATES: the plan is the gates named, each run whole, in the plan's order; a name not declared is refused.
  const whole = 'named in DAORIS_VERIFY_GATES: it runs whole';
  assert.deepEqual(tool.verifyPlanAnswer({ plan, named: ['service', 'The driver'] }), { run: [{ name: 'The driver', why: whole }, { name: 'service', why: whole }], kept: [], deferred: [] });
  assert.deepEqual(tool.verifyNamed(plan, ['service', 'The driver']).map((each) => each.name), ['driver', 'service']);
  assert.match(refusal(() => tool.verifyNamed(plan, ['driver'])).message, /DAORIS_VERIFY_GATES names 'driver', which the declaration handed does not declare: it declares 'universal', .*'The driver'/);
});


test('a probe reads the failure it repeats from its file: one failure of a run answer with its gate and that gate\'s composition, each missing part refused (INTEGRATE1j)', () => {
  const plan = [{ name: 'driver', declared: 'The driver', run: 'dotnet test src/D.Tests' }];
  const digest = `sha256:${'a'.repeat(64)}`;
  const settings = join(process.cwd(), 'driver.composition.json');
  const good = {
    gate: 'The driver', unit: 'D.SessionTests', test: 'D.SessionTests.Resumes', signature: { test: 'D.SessionTests.Resumes', exception: 'X', message: 'y' },
    composition: { units: null, settings, digest },
  };
  const read = tool.verifyFailure(JSON.stringify(good), plan);
  assert.equal(read.gate.name, 'driver');
  assert.deepEqual(read.failure, { names: 'test', gate: 'The driver', unit: 'D.SessionTests', test: 'D.SessionTests.Resumes', signature: good.signature });
  assert.deepEqual(read.composition, good.composition);
  // A gate that named no failing test is signed by its step, and probed by its own command.
  assert.equal(tool.verifyFailure(JSON.stringify({ ...good, signature: { gate: 'The driver', step: 'run', message: 'x' } }), plan).failure.names, 'gate');
  const with_ = (change: object) => JSON.stringify({ ...good, ...change });
  const cases: [string, string, RegExp][] = [
    ['not JSON', '{ nope', /DAORIS_VERIFY_FAILURE is not valid JSON/],
    ['not an object', '[]', /DAORIS_VERIFY_FAILURE is not an object/],
    ['no gate', with_({ gate: undefined }), /DAORIS_VERIFY_FAILURE has no gate/],
    ['no unit', with_({ unit: '' }), /DAORIS_VERIFY_FAILURE has no unit/],
    ['no test', with_({ test: 3 }), /DAORIS_VERIFY_FAILURE has no test/],
    ['no signature', with_({ signature: 'x' }), /DAORIS_VERIFY_FAILURE has no signature/],
    ['no composition', with_({ composition: undefined }), /DAORIS_VERIFY_FAILURE has no composition/],
    ['a short digest', with_({ composition: { ...good.composition, digest: 'sha256:abc' } }), /has no composition digest/],
    ['units that are not names', with_({ composition: { ...good.composition, units: [1] } }), /has no composition units/],
    ['a relative settings file', with_({ composition: { ...good.composition, settings: 'x.json' } }), /has no composition settings/],
    ['an undeclared gate', with_({ gate: 'driver' }), /names the gate 'driver', which the declaration handed does not declare: it declares 'The driver'/],
  ];
  for (const [what, text, message] of cases) {
    const error = refusal(() => tool.verifyFailure(text, plan));
    assert.equal(error.exitCode, 2, what);
    assert.match(error.message, message, what);
  }
});


test('a run that did not finish still answers, saying so in every mode, so its reader judges it unknown and never reads nothing (INTEGRATE1j)', () => {
  const commit = 'a'.repeat(40);
  const base = 'b'.repeat(40);
  const declaration = 'c'.repeat(40);
  const plan = [{ name: 'check', declared: 'The check' }, { name: 'suite', declared: 'suite' }];
  const results = [{ gate: { name: 'check' }, verdict: 'PASS', code: 0, log: '/tree/check.log' }];
  const why = "npm ci failed in the gate tree's . (exit 1), so the full set did not start";
  const unfinished = `the run did not finish: ${why}`;
  assert.deepEqual(tool.verifyUnfinished(null, { commit, base: null, declaration, why, plan, results }), {
    schema: 1, commit, gates: [{ name: 'The check', verdict: 'pass' }, { name: 'suite', verdict: 'not-run' }], unfinished,
  });
  const kept = [{ name: 'web', why: 'its pass stands on this tree', commit, tree: 't', standing: 'exact' }];
  assert.deepEqual(tool.verifyUnfinished('run', { commit, base, declaration, why, plan, results, kept, deferred: [] }), {
    schema: 2, mode: 'run', commit, base, declaration,
    gates: [{ name: 'The check', verdict: 'pass', failures: [] }, { name: 'suite', verdict: 'not-run', failures: [] }], kept, deferred: [], unfinished,
  });
  assert.deepEqual(tool.verifyUnfinished('plan', { commit, base, declaration, why }), { schema: 2, mode: 'plan', commit, base, declaration, unfinished });
  assert.deepEqual(tool.verifyUnfinished('probe', { commit, base, declaration, why }), { schema: 2, mode: 'probe', commit, base, declaration, result: 'cannot', signature: null, unfinished });
});


// The joins union leaves are set apart in a landing's replay too, by the driver's twin, which holds the same table.
test("the joins a union merge leaves are set apart as the table the driver's replay holds too (INTEGRATE1c)", () => {
  type Join = { name: string; kind: 'labels' | 'headings'; sides?: string[][]; text: string[]; result: string[]; set: { line: number; label: string }[] };
  const table = JSON.parse(readFileSync(join(repoRoot, UNION_JOINS), 'utf8')) as { joins: Join[] };
  assert.ok(table.joins.length >= 10 && table.joins.some((row) => row.kind === 'labels') && table.joins.some((row) => row.kind === 'headings'), 'the table holds its rows');
  for (const row of table.joins) {
    const made = row.kind === 'labels'
      ? tool.setApart(row.text.join('\n'))
      : tool.setApartHeadings(row.text.join('\n'), (row.sides ?? []).map((side) => side.join('\n')));
    assert.deepEqual({ result: made.text.split('\n'), set: made.set }, { result: row.result, set: row.set }, row.name);
  }
});


test('a note set apart is named by its task, or by its label when it names none', () => {
  assert.equal(tool.noteName('**UX6e, built 2026-10-04: Agents is a place.** The bar.'), 'UX6e note');
  assert.equal(tool.noteName('**Built 2026-10-03 (DOC8b): the check for a folder** (point 4).'), 'DOC8b note');
  assert.equal(tool.noteName('*Amended by D130 (ROW3, 2026-10-02): the change.*'), 'ROW3 note');
  assert.equal(tool.noteName('**Read 2026-10-02: the reading.** More.'), 'note "Read 2026-10-02: the reading."');
});


test('only a gate that runs the Process category is one whose failures may be flakes (MOD8)', () => {
  assert.equal(tool.isProcessGate({ name: 'driver-process', run: 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings' }), true);
  // The git-only classes' half is a Process half too, by its settings file's name: its trx and its alone-rerun hold.
  assert.equal(tool.isProcessGate({ name: 'driver-process-git', run: 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/git.process.runsettings' }), true);
  assert.equal(tool.isProcessGate({ name: 'driver', run: 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process' }), false);
  assert.equal(tool.isProcessGate({ name: 'service', run: 'dotnet test src/Daoris.Service' }), false);
});


test('a real-process test that failed is re-run alone; a theory is re-run by its method, once', () => {
  assert.deepEqual(tool.flakeDecision(PROCESS_FAILURE).rerun,
    ['Daoris.Desktop.Driver.Tests.ProcessJobTests.A_child_that_outlives_its_parent_ends_when_the_session_is_untracked']);

  const theory = [
    '  Failed Daoris.Desktop.Driver.Tests.DrivenSessionInputTests.A_stop_is_the_person_s(harness: "acp-stub") [15 s]',
    '  Failed Daoris.Desktop.Driver.Tests.DrivenSessionInputTests.A_stop_is_the_person_s(harness: "claude") [15 s]',
    'Failed!  - Failed:     2, Passed:   934, Skipped:     0, Total:   936, Duration: 3 m - Daoris.Desktop.Driver.Tests.dll (net10.0)',
  ].join('\n');
  assert.deepEqual(tool.flakeDecision(theory).rerun, ['Daoris.Desktop.Driver.Tests.DrivenSessionInputTests.A_stop_is_the_person_s']);
});


test('a failure that is not a real-process test is never re-run, and says why', () => {
  const catalogue = tool.flakeDecision(CATALOGUE_FAILURE, () => false);
  assert.deepEqual(catalogue.rerun, []);
  assert.match(catalogue.reason!, /not a real-process test: RefusalCatalogueTests\.Every_refusal/);

  // A flake beside a real failure is still a failure: nothing is re-run.
  const mixed = [PROCESS_FAILURE.replace(/Failed!.*$/, ''), CATALOGUE_FAILURE.replace('Failed:     2', 'Failed:     3')].join('\n');
  assert.deepEqual(tool.flakeDecision(mixed, (name) => name.includes("ProcessJobTests")).rerun, []);

  const build = 'src/X.cs(3,1): error CS1002: ; expected\nBuild FAILED.\n';
  assert.match(tool.flakeDecision(build).reason!, /printed no result/);

  // A count the named lines do not reach means a failure went unnamed; re-running the named ones proves nothing.
  const unnamed = PROCESS_FAILURE.replace('Failed:     1,', 'Failed:     2,');
  assert.match(tool.flakeDecision(unnamed).reason!, /2 failed but 1 are named/);

  const crashed = 'Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12, Duration: 1 s - A.dll (net10.0)\nThe active test run was aborted.';
  assert.match(tool.flakeDecision(crashed).reason!, /no failing test is named/);
});


test('a solution prints one summary per project, and the counts are summed', () => {
  const service = [
    'Passed!  - Failed:     0, Passed:   652, Skipped:     0, Total:   652, Duration: 7 s - Daoris.Service.Tests.dll (net10.0)',
    '  Failed Daoris.Service.Http.Tests.HostTests.A_route [1 s]',
    'Failed!  - Failed:     1, Passed:    45, Skipped:     0, Total:    46, Duration: 10 s - Daoris.Service.Http.Tests.dll (net10.0)',
  ].join('\n');
  assert.match(tool.flakeDecision(service, () => false).reason!, /not a real-process test: HostTests\.A_route/);
});


test('MEASUREDALONE1: only failures all in timed specs, all named, are re-run, by their file and line', () => {
  const timed = tool.timedDecision(measuredLog([TIMED_SPEC]));
  assert.deepEqual(timed.rerun, ['measured/large.spec.ts:73']);
  assert.equal(timed.playwright, true);
  assert.equal(tool.timedDecision(measuredLog([TIMED_SPEC.replace('[chromium] › ', '')])).rerun.length, 1, 'no project prefix');
  // Four sizes defined on one line are one location.
  assert.deepEqual(tool.timedDecision(measuredLog([TIMED_SPEC, TIMED_SPEC.replace('Hundred', 'Ten')])).rerun, ['measured/large.spec.ts:73']);

  const mixed = tool.timedDecision(measuredLog([TIMED_SPEC, PLAIN_SPEC]));
  assert.deepEqual(mixed.rerun, []);
  assert.match(mixed.reason!, /not a timed spec: canvas\.spec\.ts:108/);
  const counted = tool.timedDecision(measuredLog([TIMED_SPEC], 2));
  assert.deepEqual(counted.rerun, []);
  assert.match(counted.reason!, /2 failed but 1 are named/);

  assert.equal(tool.timedDecision('$ npm run test:web:unit\n Tests  1 failed | 5 passed\n').playwright, false, 'a vitest summary is no Playwright result');
  assert.equal(tool.timedDecision('(exited 1)\n').playwright, false);
  assert.equal(tool.timedRerunCommand('npm run test:web:measured', ['measured/large.spec.ts:73', 'measured/canvas.spec.ts:210']),
    'npm run test:web:measured -- -- measured/large.spec.ts:73 measured/canvas.spec.ts:210');
});


test('a re-run runs that one test with no build, and passes only on a test that ran and passed', () => {
  assert.equal(tool.rerunCommand('dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests', 'N.ProcessJobTests.A'),
    'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --no-build --filter "FullyQualifiedName=N.ProcessJobTests.A"');
  assert.equal(tool.rerunPassed('Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 5 s - D.dll (net10.0)'), true);
  // A filter that matches nothing exits 0: that is not a pass.
  assert.equal(tool.rerunPassed('No test matches the given testcase filter `FullyQualifiedName=N.A` in D.dll'), false);
  assert.equal(tool.rerunPassed(PROCESS_FAILURE), false);
});