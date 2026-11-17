/**
 * The merge queue's end-to-end kit: a scratch repository per case, its tool run with an environment the case builds, the stand-in gates it runs and the declared runner's inputs, for `merge-queue-*.e2e.ts`.
 */
import { join, dirname } from 'node:path';
import { spawnSync, type ChildProcess } from 'node:child_process';
import assert from 'node:assert/strict';
import { realpathSync, mkdirSync, writeFileSync, existsSync, readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { repoRoot, type Candidate, type Failure, type Queue, tool, type Outcome } from './merge-queue-kit.ts';
import { PATIENCE, makeFixture } from '../../test/_fixture.ts';
import { boundedNode } from './bounded-node.ts';

const TOOL = join(repoRoot, 'tools', 'merge-branch.mjs');
const GIT_ENV = {
  GIT_TERMINAL_PROMPT: '0',
  GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
  GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
  GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.autocrlf', GIT_CONFIG_VALUE_0: 'false',
};
export const TRAILER = '\n\nCo-Authored-By: Claude Fixture (Anthropic) <fixture@example.test>';

/** The test process's environment as this file loaded, before any case could change it. */
export const START_ENV: Readonly<Record<string, string | undefined>> = Object.freeze({ ...process.env });

/**
 * What a case's children never inherit. Git's repository-locating variables point a scratch's git at another
 * repository (`git init` re-initialises it, and the queue home becomes its first worktree); the tool's switches, the stand-in gates'
 * variables and Daoris's home are each case's own or the machine's.
 */
const NOT_INHERITED = [
  /^GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES|DISCOVERY_ACROSS_FILESYSTEM|PREFIX|CONFIG.*)$/i,
  /^MERGE_BRANCH_/i, /^QUEUE_/i, /^DAORIS_/i, /^NODE_TEST_CONTEXT$/i,
];

/**
 * The environment a case builds for its children, from the test process's start (never its live `process.env`, which
 * a case may not change while others run): nothing `NOT_INHERITED`, the fixture's git identity, `ceiling` as git's ceiling so a
 * scratch whose `.git` is gone is no repository rather than the checkout around it, and the case's own variables.
 */
export function caseEnvironment(ceiling: string, own: Record<string, string> = {}): Record<string, string> {
  const env: Record<string, string> = {};
  for (const [name, value] of Object.entries(START_ENV)) {
    if (value !== undefined && !NOT_INHERITED.some((pattern) => pattern.test(name))) env[name] = value;
  }
  return { ...env, ...GIT_ENV, GIT_CEILING_DIRECTORIES: ceiling, ...own };
}

/** `env` with `extra` over it, a name given in either spelling replacing the other (Windows names `Path` and `PATH` as one). */
function withEnv(env: Record<string, string>, extra: Record<string, string>): Record<string, string> {
  const given = new Set(Object.keys(extra).map((name) => name.toUpperCase()));
  return { ...Object.fromEntries(Object.entries(env).filter(([name]) => !given.has(name.toUpperCase()))), ...extra };
}

/** The search path in `env`, under whichever spelling it has. */
export const pathOf = (env: Record<string, string | undefined>) => Object.entries(env).find(([name]) => name.toUpperCase() === 'PATH')?.[1] ?? '';

/** What `PROBE_DRIVER` probes: the candidate, the plan, the probe's folder, the failure and the prefixes, in order. */
interface ProbeSpec { candidate: Candidate; plan: { name: string; run: string }[]; dir: string; failure: Failure; ks: number[]; live?: string; after?: Record<string, string> }

/**
 * The tool's failure probe (`prefixRunner`) in a process of its own. It reads its spec from the file it is given:
 * the tool's URL, the root, `ProbeSpec`. The candidate stands while `live` exists (where one is named); `after` is set in its own
 * environment once the tool has loaded. Each probe's answer, or the error it threw, is a line of JSON.
 */
const PROBE_DRIVER = [
  "import { existsSync, readFileSync } from 'node:fs';",
  "const spec = JSON.parse(readFileSync(process.argv[2], 'utf8'));",
  'const tool = await import(spec.tool);',
  'Object.assign(process.env, spec.after ?? {});',
  'const live = () => (spec.live && !existsSync(spec.live) ? null : spec.candidate);',
  'const watch = tool.watchCandidate(spec.candidate, live, tool.stepTrees(), { ms: 50 }).start();',
  'const probe = tool.prefixRunner(spec.root, spec.candidate, spec.plan, spec.dir, watch);',
  'for (const k of spec.ks) {',
  '  try {',
  '    console.log(JSON.stringify({ k, answer: await probe(spec.failure, k) }));',
  '  } catch (error) {',
  '    console.log(JSON.stringify({ k, error: error.message }));',
  '  }',
  '}',
  'watch.stop();',
  '',
].join('\n');

// A gate that fails, saying so on one line, where a file named `break-<gate>` stands in the tree it runs in.
export const QUEUE_GATE = [
  "import { existsSync } from 'node:fs';",
  'const name = process.argv[2];',
  // A case that must queue a branch while a candidate is still running holds its `check` gate on a file it writes after.
  'const hold = process.env.QGATE_HOLD;',
  "if (hold && name === 'check') { const end = Date.now() + 300000; while (!existsSync(hold) && Date.now() < end) await new Promise((done) => setTimeout(done, 100)); }",
  'console.log(`${name} ran`);',
  "if (existsSync(`break-${name}`)) { console.log(`error: ${name} is broken`); process.exit(1); }",
  '',
].join('\n');

/** Git in `cwd` with the case's environment `env`, which must succeed; its output trimmed. */
function sh(env: Record<string, string>, cwd: string, ...args: string[]): string {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', env, windowsHide: true });
  assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  return result.stdout.trim();
}

/** A path as the file system names it, for comparing two spellings of one folder. */
export const realPath = (path: string) => realpathSync(path).toLowerCase();

export const pause = (ms: number) => new Promise((done) => { setTimeout(done, ms); });

/**
 * How long one scratch run of the merge tool, or one wait on its queue, may take before the test calls it hung:
 * a guard against the hang, never a pace, so it elapses only on failure. A scratch queue gates real git
 * under whatever else the machine runs, and a healthy run can take longer than the two-minute shared patience under load,
 * so the guard is ten minutes; the hang it catches lasts ninety. It is
 * five times the shared patience, so the one knob moves it, and stays longer because a healthy run is.
 */
export const HANG_MS = 5 * PATIENCE;

/** Waits for the condition, at most `ms`: a bound that elapses only on failure. */
async function until(holds: () => boolean, ms = HANG_MS) {
  for (const end = Date.now() + ms; !holds() && Date.now() < end;) await pause(100);
}

export const alive = (pid: number) => {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
};

export function queueScratch(name: string) {
  // Named for this process too, so a second run of the file beside this one, or one after it killed, never
  // clears a scratch whose processes still stand in it: a scratch that loses its `.git` is git's way out to the checkout around it.
  const fx = makeFixture(`merge-queue-${name}-${process.pid}`);
  const root = join(fx.root, 'repo');
  mkdirSync(root, { recursive: true });
  // The runner's own `git status` (its clean-checkout look, every tick while a candidate is held) refreshes the
  // index and takes `index.lock` to write it back, so a test's git write on this repository can meet the lock. The
  // runner is told to take no optional lock: it reads the checkout and never holds the index, so the test's writes
  // need no retry and no sleep. A write the queue itself makes (the fast-forward, the scope tree's merges) is not optional.
  // Every child of the case, the tool's invocations and every git, gets this environment and no other, with the
  // fixture's folder as git's ceiling.
  // The machine's admission is the case's own, in a Daoris home of its own two places wide, so a red candidate's
  // confirm run takes the whole of it as the runner does on a machine, and no case waits on what the machine around it runs. Without
  // it every confirm run would read inconclusive, since no admission could be shown.
  const home = join(fx.root, 'home');
  mkdirSync(home, { recursive: true });
  const env = caseEnvironment(fx.root, {
    GIT_OPTIONAL_LOCKS: '0', MERGE_BRANCH_MEMORY: 'off', MERGE_BRANCH_CI_NIGHT_HOUR: 'off', MERGE_BRANCH_QUEUE_POLL_MS: '200', MERGE_BRANCH_QUEUE_TICK_MS: '300', MERGE_BRANCH_DROP_POLL_MS: '200',
    MERGE_BRANCH_ADMISSION: 'on', DAORIS_HOME: home, DAORIS_ADMISSION_SLOTS: '2',
    // The runner and the waiter this case starts end with this process, so a test killed leaves neither polling on.
    MERGE_BRANCH_QUEUE_OWNER: String(process.pid),
  });
  const git = (...args: string[]) => sh(env, root, ...args);
  git('init', '--quiet', '--template=', '-b', 'main');
  // The guard: its own repository, its own git storage and its own queue home (the first worktree), never the one around it.
  assert.equal(realPath(git('rev-parse', '--show-toplevel')), realPath(root));
  assert.equal(realPath(git('rev-parse', '--path-format=absolute', '--git-common-dir')), realPath(join(root, '.git')));
  assert.equal(realPath(git('worktree', 'list', '--porcelain').split('\n')[0]!.replace(/^worktree /, '')), realPath(root));
  writeFileSync(join(root, '.gitignore'), 'local/\n');
  writeFileSync(join(root, 'qgate.mjs'), QUEUE_GATE);
  const gates = [{ name: 'universal', run: 'node qgate.mjs universal' }, { name: 'check', run: 'node qgate.mjs check' }];
  writeFileSync(join(root, 'daoris.gates.json'), `${JSON.stringify({ gates }, null, 2)}\n`);
  git('add', '-A');
  git('commit', '--quiet', '-m', `first${TRAILER}`);
  mkdirSync(join(root, 'local'), { recursive: true });
  // Every invocation the test started and has not seen end: a failed assertion leaves them for `cleanup` to end.
  const started = new Set<ChildProcess>();
  const queueFile = join(root, 'local', 'merge-queue.json');
  const queue = () => (existsSync(queueFile)
    ? JSON.parse(readFileSync(queueFile, 'utf8')) as Queue & { runner: unknown }
    : null);
  const admissionFile = join(root, 'local', 'admission.json');
  let probes = 0;
  const node = (args: string[], extra: Record<string, string>, ms: number, cwd: string) =>
    boundedNode(args, { cwd, env: withEnv(env, extra), ms, end: tool.endTree, started });
  return {
    root,
    /** The case's Daoris home, whose machine admission the runner takes its places in. */
    home,
    /** The environment the case's children get. */
    env,
    /** Git in the scratch with the case's environment. */
    git,
    queue,
    /** The admission record as it stands, `{}` where none is written, undefined for a read that met a write. */
    admission(): { scopedTier?: { state: string } | null } | undefined {
      if (!existsSync(admissionFile)) return {};
      try {
        return JSON.parse(readFileSync(admissionFile, 'utf8'));
      } catch {
        return undefined;
      }
    },
    branch(branchName: string, files: Record<string, string>) {
      git('checkout', '--quiet', '-b', branchName, 'main');
      for (const [path, text] of Object.entries(files)) {
        mkdirSync(dirname(join(root, path)), { recursive: true });
        writeFileSync(join(root, path), text);
      }
      git('add', '-A');
      git('commit', '--quiet', '-m', `work on ${branchName}${TRAILER}`);
      git('checkout', '--quiet', 'main');
      writeFileSync(join(root, 'local', `${branchName}.msg`), `Merge ${branchName}: its words${TRAILER}\n`);
    },
    /**
     * One invocation of the tool, bounded: one that has given no verdict by `ms` is ended with its tree, and answers `status: null`.
     * It runs in `cwd` (the scratch unless named) with the case's environment and `extra`.
     */
    run(args: string[], extra: Record<string, string> = {}, ms = HANG_MS, cwd = root): Promise<{ status: number | null; out: string }> {
      return node([TOOL, ...args], extra, ms, cwd);
    },
    /**
     * A failure probe of the tool's runner (`prefixRunner`) in a process of its own, with the case's environment and `extra`
     * (never in the test process, whose environment no case may change): `PROBE_DRIVER` over `spec`, each probe's
     * answer or error a line of JSON.
     */
    async probe(spec: ProbeSpec, extra: Record<string, string> = {}): Promise<{ status: number | null; out: string; lines: { k: number; answer?: Outcome; error?: string }[] }> {
      probes += 1;
      const driver = join(fx.root, 'probe-driver.mjs');
      const file = join(fx.root, `probe-${probes}.json`);
      writeFileSync(driver, PROBE_DRIVER);
      writeFileSync(file, JSON.stringify({ tool: pathToFileURL(TOOL).href, root, ...spec }));
      const ran = await node([driver, file], extra, HANG_MS, root);
      return { ...ran, lines: ran.out.split('\n').filter((line) => line.startsWith('{')).map((line) => JSON.parse(line)) };
    },
    /** The process ids of the invocations still running. */
    running: () => [...started].map((child) => child.pid!),
    log: () => (existsSync(join(root, 'local', 'merge-queue.log')) ? readFileSync(join(root, 'local', 'merge-queue.log'), 'utf8') : ''),
    until,
    async cleanup() {
      // An invocation a failed assertion leaves waiting is ended first, so nothing started here can hold the run.
      for (const child of started) tool.endTree(child.pid!);
      // The runner ends once idle; its scope tree goes with the fixture.
      // A grace for an idle runner to end by itself, kept at five seconds: a runner holding a candidate never does, and is ended below.
      for (let i = 0; i < 50 && queue()?.runner; i += 1) await pause(100);
      // One that is not idle (a candidate held on the checkout, a stop) never ends by itself: it is ended, with its gates.
      const runner = queue()?.runner as { pid: number } | null | undefined;
      if (runner && alive(runner.pid)) {
        tool.endTree(runner.pid);
        await until(() => !alive(runner.pid), PATIENCE);
      }
      fx.cleanup();
    },
  };
}

/** The processes a held gate writes its pid for: the gate itself, what it started, and what that started. */
export const TREE = ['check', 'child', 'grandchild'];
export const pidsIn = (dir: string) => TREE.filter((role) => existsSync(join(dir, `${role}.pid`))).map((role) => Number(readFileSync(join(dir, `${role}.pid`), 'utf8')));

/**
 * A gate that, when `holdWhen` holds, writes its pid, starts a child that starts a grandchild (each writing its own), and waits
 * for `release` in `dir`: a process tree three deep, the shape a build and its test hosts make. `count` is how many times `check`
 * has run there. With `record`, the arguments it is given are kept in `args`. The folder is written into the gate:
 * the queue's runner passes no test's variable on to a gate.
 */
export function treeGate(dir: string, holdWhen: string, record = ''): string {
  return [
    "import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';",
    "import { spawn } from 'node:child_process';",
    `const dir = ${JSON.stringify(dir)};`,
    'const role = process.argv[2];',
    "const wait = async () => { while (!existsSync(`${dir}/release`)) await new Promise((done) => setTimeout(done, 25)); };",
    'let count = 0;',
    "if (dir && role === 'check') { appendFileSync(`${dir}/runs`, 'x'); count = readFileSync(`${dir}/runs`, 'utf8').length; }",
    record ? `if (dir && role === 'check') writeFileSync(\`\${dir}/${record}\`, process.argv.slice(3).join(' '));` : '',
    `const holding = role === 'child' || role === 'grandchild' || (role === 'check' && dir && (${holdWhen}));`,
    'if (holding) {',
    "  writeFileSync(`${dir}/${role}.pid`, String(process.pid));",
    "  if (role === 'check') spawn(process.execPath, [process.argv[1], 'child'], { stdio: 'ignore', windowsHide: true });",
    "  if (role === 'child') spawn(process.execPath, [process.argv[1], 'grandchild'], { stdio: 'ignore', windowsHide: true });",
    '  await wait();',
    '}',
    'console.log(`${role} ran`);',
    "if (role === 'check' && existsSync('break-check')) { console.log('error: check is broken'); process.exit(1); }",
    '',
  ].join('\n');
}

/**
 * A `check` that counts its runs in `dir` (`runs`) and fails where `failWhen` holds, an expression over `count` and
 * `load`, how many holds named `the load beside it` the machine's admission file `admission` keeps. Where `holdWhen` holds it first
 * writes `<count>.started` and waits for `release` in `dir`. Every other gate passes.
 */
export function confirmGate(dir: string, { failWhen, holdWhen = 'false', admission = '' }: { failWhen: string; holdWhen?: string; admission?: string }): string {
  return [
    "import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';",
    `const dir = ${JSON.stringify(dir)};`,
    `const admission = ${JSON.stringify(admission)};`,
    'const role = process.argv[2];',
    "if (role !== 'check') { console.log(`${role} ran`); process.exit(0); }",
    "appendFileSync(`${dir}/runs`, 'x');",
    "const count = readFileSync(`${dir}/runs`, 'utf8').length;",
    "const load = admission && existsSync(admission) ? JSON.parse(readFileSync(admission, 'utf8')).holds.filter((hold) => hold.what === 'the load beside it').length : 0;",
    `if (${holdWhen}) {`,
    "  writeFileSync(`${dir}/${count}.started`, '');",
    "  while (!existsSync(`${dir}/release`)) await new Promise((done) => setTimeout(done, 25));",
    '}',
    'console.log(`check ran (${count})`);',
    `if (${failWhen}) { console.log('error: check is broken'); process.exit(1); }`,
    '',
  ].join('\n');
}

/** A gate that writes what of its environment a leak would carry into `file`, then passes. */
export const envGate = (file: string, admission = '', role = 'check') => [
  "import { existsSync, readFileSync, writeFileSync } from 'node:fs';",
  // Also what of the install's home, a role and git's locating variables reached it, and the admission file it can read.
  `const admission = ${JSON.stringify(admission)};`,
  "const held = admission && existsSync(admission) ? JSON.parse(readFileSync(admission, 'utf8')).holds.map((hold) => hold.what) : null;",
  `if (process.argv[2] === ${JSON.stringify(role)}) writeFileSync(${JSON.stringify(file)}, JSON.stringify({ drain: process.env.QUEUE_DRAIN_DIR ?? null, tick: process.env.MERGE_BRANCH_QUEUE_TICK_MS ?? null, gitDir: process.env.GIT_DIR ?? null, ceiling: process.env.GIT_CEILING_DIRECTORIES ?? null, home: process.env.DAORIS_HOME ?? null, role: process.env.DAORIS_ROLE ?? null, beside: process.env.MSBUILDDISABLENODEREUSE ?? null, held, path: process.env.PATH ?? '' }));`,
  'console.log(`${process.argv[2]} ran`);',
  '',
].join('\n');

/** What `envGate` records. */
export interface SeenEnvironment { drain: string | null; tick: string | null; gitDir: string | null; ceiling: string | null; home: string | null; role: string | null; beside: string | null; held: string[] | null; path: string }

// This tool (design §5.9) as the declared runner, `--full --beside --daoris-verify`, its inputs in
// DAORIS_VERIFY_* set on the job alone, the gates read from the declaration handed and never from the tree.
type Scratch = ReturnType<typeof queueScratch>;
export const VERIFY = ['--full', '--beside', '--daoris-verify'];
export const verifier = (repo: Scratch) => {
  const dir = join(repo.root, 'local', 'verify');
  mkdirSync(dir, { recursive: true });
  const declaration = join(dir, 'declaration.json');
  const answer = join(dir, 'answer.json');
  return {
    answer,
    declare: (declared: object) => writeFileSync(declaration, JSON.stringify(declared)),
    env: (commit: string, more: Record<string, string> = {}) => ({ DAORIS_VERIFY_COMMIT: commit, DAORIS_VERIFY_RESULT: answer, DAORIS_VERIFY_DECLARATION: declaration, ...more }),
    read: () => (existsSync(answer) ? JSON.parse(readFileSync(answer, 'utf8')) as Record<string, any> : null),
  };
};
/** A gate that records which Daoris variables reach it, where it runs, and whether the private patterns file is copied in. */
export const recorderGate = (file: string) => [
  "import { existsSync, writeFileSync } from 'node:fs';",
  `writeFileSync(${JSON.stringify(file)}, JSON.stringify({ daoris: Object.keys(process.env).filter((name) => /^DAORIS_/i.test(name)), cwd: process.cwd(), copied: existsSync('local/sensitive-patterns.txt') }));`,
  "console.log('recorder ran');",
  '',
].join('\n');
export const commitOnMain = (repo: Scratch, files: Record<string, string>, what: string) => {
  for (const [path, text] of Object.entries(files)) writeFileSync(join(repo.root, path), text);
  repo.git('add', '-A');
  repo.git('commit', '--quiet', '-m', `${what}${TRAILER}`);
  return repo.git('rev-parse', 'HEAD');
};

// The schema-2 run (design §4.3, §4.5, §5.9), plan and probe modes.
export type Answered = Record<string, any>;
export const modes = (repo: Scratch, run: ReturnType<typeof verifier>, base: string) => (mode: string, commit: string, more: Record<string, string> = {}) => (
  repo.run(VERIFY, run.env(commit, { DAORIS_VERIFY_MODE: mode, DAORIS_VERIFY_BASE: base, ...more })));

/**
 * A stand-in `dotnet test` with a pollution within a class: `PollutedTests.B_Reads` fails only after `A_Pollutes` ran before it in the
 * same run, so it fails in its gate and in its class run alone, and passes run by itself. It runs the tests its `--filter` selects,
 * writes their results to the trx its logger names, and records each call's arguments in `calls`. With `marker`, the pollution is
 * only in a tree that holds that file (a member's change), so the base and a prefix without it pass.
 */
export const pollutedDotnet = (calls: string, marker = '') => [
  "import { appendFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';",
  "import { dirname } from 'node:path';",
  'const args = process.argv.slice(2);',
  `appendFileSync(${JSON.stringify(calls)}, JSON.stringify(args) + '\\n');`,
  'const value = (flag) => { const at = args.indexOf(flag); return at >= 0 ? args[at + 1] : null; };',
  "const filter = value('--filter') ?? '';",
  "const trx = /LogFileName=(.+)$/.exec(value('--logger') ?? '')?.[1] ?? null;",
  "const tests = ['Fixture.PollutedTests.A_Pollutes', 'Fixture.PollutedTests.B_Reads', 'Fixture.OtherTests.C_Passes'];",
  "const holds = (test) => filter.replace(/[()]/g, '').split('&').map((term) => term.trim()).filter(Boolean).every((term) => {",
  '  const not = /^FullyQualifiedName!~(.+)$/.exec(term);',
  '  if (not) return !test.includes(not[1]);',
  '  const has = /^FullyQualifiedName~(.+)$/.exec(term);',
  '  return has ? test.includes(has[1]) : true;',
  '});',
  'const ran = tests.filter(holds);',
  `const polluting = ${JSON.stringify(marker)} ? existsSync(${JSON.stringify(marker)}) : true;`,
  'const failed = polluting && ran.includes(tests[0]) && ran.includes(tests[1]) ? [tests[1]] : [];',
  'const id = (test) => `t${tests.indexOf(test)}`;',
  'const own = (test) => test.split(\'.\').slice(0, -1).join(\'.\');',
  'if (trx) {',
  '  mkdirSync(dirname(trx), { recursive: true });',
  '  const results = ran.map((test) => (failed.includes(test)',
  '    ? `<UnitTestResult testId="${id(test)}" testName="${test}" outcome="Failed"><Output><ErrorInfo><Message>Assert.Equal() Failure: the state A_Pollutes left</Message><StackTrace>   at ${test}()</StackTrace></ErrorInfo></Output></UnitTestResult>`',
  '    : `<UnitTestResult testId="${id(test)}" testName="${test}" outcome="Passed" />`));',
  '  const definitions = ran.map((test) => `<UnitTest id="${id(test)}" name="${test}"><TestMethod className="${own(test)}" name="${test.split(\'.\').at(-1)}" /></UnitTest>`);',
  '  writeFileSync(trx, `<?xml version="1.0" encoding="utf-8"?><TestRun id="r"><Results>${results.join(\'\')}</Results><TestDefinitions>${definitions.join(\'\')}</TestDefinitions></TestRun>`);',
  '}',
  'for (const test of ran) console.log(`  ${failed.includes(test) ? \'Failed\' : \'Passed\'} ${test}`);',
  'process.exit(failed.length ? 1 : 0);',
  '',
].join('\n');
