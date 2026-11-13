import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, posix, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { gzipSync } from 'node:zlib';
import { driverFile } from '../test/_csharp.ts';
// Untyped workspace tooling, suppressed at the one site — see desktop-tool.test.ts for why.
import {
  BUILD_MANIFEST, CLI_BIN, CLI_ENTRY, CLI_HOME, CLI_LAUNCHERS, CLI_PACKAGE, CONNECTOR_EXE, CONNECTOR_HOME, GATE_COMMAND, HOME, HOST_EXE, HOST_HOME,
  KEPT_LOCALES, LAUNCHER, MANIFEST_SCHEMA, MARKER, MARKER_HEADER, OFFERED_PLUGINS, OWN, PLUGIN_OFFERS, RESOURCES, RESOURCES_SOURCE, RETIRED_IN_APP,
  RETIRED_LAUNCHERS, SHELL_EXE, SHELL_FILES, SHELL_HOME, STAGE, STAGED, STAGED_REQUIRED, SWAP_JOURNAL, buildId, cliLaunchers, insideFixtures,
  DRIVER_EXE, DRIVER_HOME, DRIVER_PROJECT, driverPublishCommand,
  APP_PROJECT, LAUNCHER_PROJECT, appPublishCommand, artifactsOf, launcherPublishCommand, publishStages,
  installedNote, isInstall, layCli, layOffers, layResources, promoteStage, recordedShellFiles, refusal, retiredPaths, servicePlan, stageRefusal,
  stagedManifest, ungatedRefusal, unstage, writeManifest, GATE_TREE_LOCK, GATE_TREE_RECORD, childArgs, gatedSource, recordStaged, recordStage, stageReport, stagedAdvice,
  // @ts-expect-error — untyped workspace tooling; see above
} from '../../../tools/desktop-publish.mjs';
// The service hosts' one build recipe, which the desktop's `--service` publishes by.
// @ts-expect-error — untyped workspace tooling; see above
import { HOSTS, publishCommand } from '../../../tools/service-publish.mjs';
// @ts-expect-error — untyped workspace tooling; see above
import { renameHeld } from '../../../tools/fsx.mjs';
import { resolveCanonRoot } from '../src/doctrine/canon.ts';
import { DRIVER_PROGRAM_FILE, DRIVER_PROGRAM_HOME } from '../src/platform/driverprogram.ts';
import { INSTALL_APP, INSTALL_DATA, INSTALL_MARKER, installOf } from '../src/platform/home.ts';
import { makeFixture } from '../test/_fixture.ts';

/** The address file the running desktop leaves in its home (the driver's `RunningAddress.FileName`); the CLI does not read it. */
const RUNNING_FILE = 'running.json';
import { TAR_END, tarEntry } from '../test/_tar.ts';

/**
 * The publish guard (`tools/desktop-publish.mjs`): what it refuses to write into, and the one door
 * through it. Tested here because the decision is a pure function of a folder's contents, and the
 * deployment rehearsal proves only the default refusal — every other branch costs a full build there.
 *
 * The guard's own reason stands: a publish into somebody's documents folder and a publish into an
 * install folder look identical to `cpSync`. What the second deployment added is a third folder — the
 * one the person actually wants, holding the repositories the application drives — and `--beside`
 * names that intent. It is not a weaker default: it still refuses to write over a name it did not
 * write, because those are the only names it touches.
 */

const here = dirname(fileURLToPath(import.meta.url));

const folder = (): string => mkdtempSync(join(tmpdir(), 'daoris-publish-'));

const markInstalled = (at: string) => writeFileSync(join(at, MARKER), `${MARKER_HEADER}\n\nours\n`);

/** A background run's receipt on `tree` in the main checkout's `local/ci-runs.jsonl`, which a stage needs beside its verdicts. */
function receipt(home: string, tree: string): void {
  const file = join(home, 'local', 'ci-runs.jsonl');
  mkdirSync(dirname(file), { recursive: true });
  const held = existsSync(file) ? readFileSync(file, 'utf8') : '';
  writeFileSync(file, `${held}${JSON.stringify({ commit: 'c', tree, kind: 'ci', started: '2026-10-24T10:00:00Z', ended: '2026-10-24T10:30:00Z', finished: true, state: 'green', gates: [] })}\n`);
}

test('five names make an install, the marker and the update’s folder among them', () => {
  assert.deepEqual([...OWN].sort(), ['Daoris.exe', 'INSTALLED.md', 'app', 'data', 'update']);
  assert.ok(OWN.includes(MARKER));
  assert.ok(OWN.includes(STAGE[0]));
});

/**
 * The publish's `--stage` writes the build where the application and the launcher look for it,
 * with the manifest their one check reads (`StagedBuild.cs`, which the launcher compiles). Two languages, one layout:
 * this reads the C# for its spellings and its required files.
 */
test('the staged build is laid out where StagedBuild looks, with the files it requires', () => {
  const source = readFileSync(driverFile('StagedBuild.cs'), 'utf8');
  assert.ok(source.includes(`Folder = "${STAGED[0]}"`), 'the update’s folder');
  assert.ok(source.includes(`Staged = "${STAGED[1]}"`), 'the staged build inside it');
  assert.ok(source.includes(`Manifest = "${BUILD_MANIFEST}"`), 'its manifest');
  assert.ok(source.includes(`Journal = "${SWAP_JOURNAL}"`), 'the swap’s journal');
  assert.ok(source.includes(`Schema = ${MANIFEST_SCHEMA};`), 'the manifest’s schema');
  const required = /Required =\s*\[([^\]]+)\]/.exec(source)?.[1] ?? '';
  const named = [...required.matchAll(/"([^"]+)"|\b(Launcher|Marker)\b/g)]
    .map((match) => match[1] ?? (match[2] === 'Launcher' ? LAUNCHER : MARKER));
  assert.deepEqual(named, [...STAGED_REQUIRED]);
  assert.ok(STAGED_REQUIRED.includes(`${SHELL_HOME[0]}/${SHELL_EXE}`));
  assert.ok(source.includes(`Host = "${HOST_HOME.join('/')}/daoris-knowledge-http.exe"`), 'the host it requires of an install that carries one');
});

test('a staged build’s manifest names every file but itself, with its size and SHA-256, in one order', () => {
  const root = folder();
  mkdirSync(join(root, 'app', 'locales'), { recursive: true });
  writeFileSync(join(root, 'Daoris.exe'), 'launcher');
  writeFileSync(join(root, 'app', 'Daoris.Desktop.exe'), 'application');
  writeFileSync(join(root, 'app', 'locales', 'zh-CN.pak'), '中文');
  writeFileSync(join(root, BUILD_MANIFEST), '{ "old": true }');

  const manifest = stagedManifest(root, { id: 'b1', version: '0.0.1', commit: 'abc1234', at: '2026-10-03T12:00:00Z' });

  assert.equal(manifest.schema, MANIFEST_SCHEMA);
  assert.equal(manifest.id, 'b1');
  assert.equal(manifest.version, '0.0.1');
  assert.equal(manifest.commit, 'abc1234');
  assert.deepEqual(manifest.files.map((file: { path: string }) => file.path), ['Daoris.exe', 'app/Daoris.Desktop.exe', 'app/locales/zh-CN.pak']);
  const pak = manifest.files[2];
  assert.equal(pak.size, Buffer.byteLength('中文'));
  assert.equal(pak.sha256, createHash('sha256').update('中文').digest('hex'));

  writeManifest(root, { id: 'b1', version: '0.0.1', commit: null, at: '2026-10-03T12:00:00Z' });
  const written = readFileSync(join(root, BUILD_MANIFEST), 'utf8');
  assert.ok(!written.includes('\r'), 'LF');
  assert.equal(JSON.parse(written).files.length, 3);
  assert.equal(JSON.parse(written).commit, null);
});

test('a build id names its moment and is never the same twice', () => {
  const at = new Date('2026-10-03T12:34:56Z');
  assert.match(buildId(at), /^20261003T123456Z-[0-9a-f]{8}$/);
  assert.notEqual(buildId(at), buildId(at));
});

test('--stage stages only beside an install, never over a swap in progress, and with the host when the install carries one', () => {
  const at = folder();
  assert.match(stageRefusal(join(at, 'nowhere'), { service: true }) ?? '', /not an install/);
  writeFileSync(join(at, 'notes.txt'), 'someone else’s');
  assert.match(stageRefusal(at, { service: true }) ?? '', /not an install/);

  markInstalled(at);
  assert.equal(stageRefusal(at, { service: false }), null, 'an install without a host stages without --service');

  mkdirSync(join(at, ...HOST_HOME), { recursive: true });
  writeFileSync(join(at, ...HOST_HOME, 'daoris-knowledge-http.exe'), '');
  assert.match(stageRefusal(at, { service: false }) ?? '', /--service/);
  assert.equal(stageRefusal(at, { service: true }), null);

  mkdirSync(join(at, ...STAGE), { recursive: true });
  for (const phase of ['swapping', 'started', 'confirmed']) {
    writeFileSync(join(at, ...STAGE, SWAP_JOURNAL), JSON.stringify({ phase }));
    assert.match(stageRefusal(at, { service: true }) ?? '', /swap/, phase);
  }
  for (const phase of ['installed', 'rolled-back', 'refused']) {
    writeFileSync(join(at, ...STAGE, SWAP_JOURNAL), JSON.stringify({ phase }));
    assert.equal(stageRefusal(at, { service: true }), null, phase);
  }
});

/**
 * A merge runs only the gates its lanes reach, so the full set runs before the install is built. A
 * "web only" batch can skip the .NET suites and break main unseen; the lane table could be wrong the
 * same way, and this is where that is caught, the same day. The deployment rehearsal's own publishes go
 * into a scratch folder under `_fixtures`, which is never asked: it is one of the gates being run.
 */
type Ask = (root: string) => { code: number; out: string };

test('a publish into a scratch folder under _fixtures is never asked about gates; any other folder is', () => {
  const fx = makeFixture('publish-inside-fixtures');
  assert.equal(insideFixtures(join(fx.root, '_fixtures', 'deployment-rehearsal', 'install'), fx.root), true);
  assert.equal(insideFixtures(join(fx.root, '_fixtures'), fx.root), true);
  assert.equal(insideFixtures(join(fx.root, '_fixtures-not', 'install'), fx.root), false, 'a sibling whose name starts the same is not inside');
  assert.equal(insideFixtures(join(fx.root, 'install'), fx.root), false);
  assert.equal(insideFixtures(join(dirname(fx.root), 'install'), fx.root), false);
  if (process.platform === 'win32') assert.equal(insideFixtures(join(fx.root.toUpperCase(), '_FIXTURES', 'x'), fx.root), true);
  fx.cleanup();
});

test('a publish refuses a commit the full set has not passed, naming what is missing, the command that runs it, and the override', async () => {
  const fx = makeFixture('publish-ungated');
  const to = join(fx.root, 'install');
  const asked: string[] = [];
  const ask = (code: number, out: string): Ask => (root) => {
    asked.push(root);
    return { code, out };
  };
  const missing = 'merge-branch: the full set has NOT passed this checkout (tree 1a2b3c4, HEAD abc1234): 11 of 13 gates.\n'
    + '  driver-process   none   no verdict on this tree\n  deployment       none   no verdict on this tree\n';

  const passed = ungatedRefusal(to, fx.root, { ask: ask(0, 'merge-branch: the full set has passed this checkout (tree 1a2b3c4, HEAD abc1234): 13 of 13 gates.\n') });
  assert.equal(passed.refusal, null);
  assert.match(passed.note!, /the full set has passed/);
  assert.deepEqual(asked, [fx.root]);

  const refused = ungatedRefusal(to, fx.root, { ask: ask(1, `${missing}  Run every gate on it: node tools/merge-branch.mjs --full\n`) });
  assert.match(refused.refusal!, /the full set of gates has not passed/);
  assert.match(refused.refusal!, /driver-process\s+none/);
  assert.equal(refused.refusal!.split(GATE_COMMAND).length, 2, 'it names the command that runs the full set, once');
  // One command, spelled in two files: the publish's refusal and the merge tool's own.
  const mergeTool = await import(
    // @ts-expect-error — untyped workspace tooling; see above
    '../../../tools/merge-branch.mjs') as { FULL_COMMAND: string };
  assert.equal(GATE_COMMAND, mergeTool.FULL_COMMAND);
  assert.equal(GATE_COMMAND, 'node tools/merge-branch.mjs --full');
  assert.match(refused.refusal!, /--force-ungated/);
  assert.match(refused.refusal!, /run every gate on this checkout: node tools\/merge-branch\.mjs --full\n/);

  // When a smaller command would pass it, the tool names that one, and so does the refusal, in place of the full set.
  const stale = ungatedRefusal(to, fx.root, { ask: ask(1, `${missing}  Run the 2 stale gates on it: node tools/merge-branch.mjs --stale\n`) });
  assert.match(stale.refusal!, /run the 2 stale gates on this checkout: node tools\/merge-branch\.mjs --stale\n/);
  assert.equal(stale.refusal!.split('merge-branch.mjs --stale').length, 2, 'it names the command once');
  assert.doesNotMatch(stale.refusal!, /--full/);
  assert.match(stale.refusal!, /driver-process\s+none/);
  const lacks = ungatedRefusal(to, fx.root, { ask: ask(1, `${missing}  Run the gate it lacks on it: node tools/merge-branch.mjs --rerun cli\n`) });
  assert.match(lacks.refusal!, /run the gate it lacks on this checkout: node tools\/merge-branch\.mjs --rerun cli\n/);

  // The person's explicit override publishes, and says it is ungated.
  const forced = ungatedRefusal(to, fx.root, { force: true, ask: ask(1, missing) });
  assert.equal(forced.refusal, null);
  assert.match(forced.note!, /ungated \(--force-ungated\)/);
  assert.match(forced.note!, /driver-process/);

  // A record that cannot be read is no proof either.
  const broken = ungatedRefusal(to, fx.root, { ask: ask(2, 'merge-branch: daoris.gates.json is not valid JSON') });
  assert.match(broken.refusal!, /could not tell whether the full set passed/);
  assert.match(broken.refusal!, /not valid JSON/);

  // The deployment rehearsal's scratch install is not asked at all.
  asked.length = 0;
  assert.deepEqual(ungatedRefusal(join(fx.root, '_fixtures', 'deployment-rehearsal', 'install'), fx.root, { ask: ask(1, missing) }), { refusal: null, note: null });
  assert.deepEqual(asked, []);
  fx.cleanup();
});

test('the stage refuses an ungated commit, as the merge tool\'s gates record reads, and publishes once every gate passed it', () => {
  const fx = makeFixture('publish-gates-record');
  const repo = join(fx.root, 'repo');
  mkdirSync(repo, { recursive: true });
  const env = {
    ...process.env, GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
    GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
  };
  const git = (...args: string[]): string => {
    const result = spawnSync('git', args, { cwd: repo, encoding: 'utf8', env, windowsHide: true });
    assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
    return result.stdout.trim();
  };
  git('init', '--quiet', '--template=', '-b', 'main');
  // git walks up: the tool must find this repository, never the one this suite sits in.
  assert.equal(git('rev-parse', '--show-toplevel').toLowerCase().replace(/\\/g, '/'), repo.toLowerCase().replace(/\\/g, '/'));
  writeFileSync(join(repo, '.gitignore'), 'local/\n');
  writeFileSync(join(repo, 'daoris.gates.json'), `${JSON.stringify({ gates: [{ name: 'first', run: 'node a.mjs' }, { name: 'second', run: 'node b.mjs' }] })}\n`);
  writeFileSync(join(repo, 'code.txt'), 'built\n');
  git('add', '-A');
  git('commit', '--quiet', '-m', 'first');
  const tree = git('rev-parse', 'HEAD^{tree}');
  const record = (gates: string[]) => {
    mkdirSync(join(repo, 'local'), { recursive: true });
    const at = '2026-10-04T12:00:00Z';
    writeFileSync(join(repo, 'local', 'gate-verdicts.json'),
      `${JSON.stringify({ schema: 1, verdicts: gates.map((gate) => ({ gate, verdict: 'PASS', tree, at, commit: null, branch: null })) }, null, 2)}\n`);
  };
  const to = join(fx.root, 'install');

  let gated = ungatedRefusal(to, repo);
  assert.match(gated.refusal!, /first\s+none/);
  assert.match(gated.refusal!, /second\s+none/);
  // No background run finished on it, so the stage's own run is what it names.
  assert.match(gated.refusal!, /not staged\s+no background run's receipt on its commit's tree/);
  assert.match(gated.refusal!, /run the stage's own CI run on this checkout: node tools\/merge-branch\.mjs --ci --stage [0-9a-f]{7}\n/);
  receipt(repo, tree);

  gated = ungatedRefusal(to, repo);
  assert.match(gated.refusal!, /run every gate on this checkout: node tools\/merge-branch\.mjs --full/);

  record(['first']);
  gated = ungatedRefusal(to, repo);
  assert.match(gated.refusal!, /second\s+none/);
  assert.match(gated.refusal!, /first\s+PASS/);
  // One gate lacking is one gate to run, which the refusal names in place of the full set.
  assert.match(gated.refusal!, /run the gate it lacks on this checkout: node tools\/merge-branch\.mjs --rerun second\n/);
  assert.doesNotMatch(gated.refusal!, /--full/);

  record(['first', 'second']);
  gated = ungatedRefusal(to, repo);
  assert.equal(gated.refusal, null, gated.refusal ?? '');
  assert.match(gated.note!, /the full set has passed/);
  fx.cleanup();
});

// The full set runs in a worktree of its own while main moves on; the stage builds what it gated.
test('a stage from the gate tree builds the commit it holds, refuses another or one whose full set runs, and is judged by the main checkout\'s verdicts', () => {
  const fx = makeFixture('publish-gate-tree');
  const repo = join(fx.root, 'repo');
  mkdirSync(repo, { recursive: true });
  const env = {
    ...process.env, GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
    GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
  };
  const git = (...args: string[]): string => {
    const result = spawnSync('git', args, { cwd: repo, encoding: 'utf8', env, windowsHide: true });
    assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
    return result.stdout.trim();
  };
  git('init', '--quiet', '--template=', '-b', 'main');
  assert.equal(git('rev-parse', '--show-toplevel').toLowerCase().replace(/\\/g, '/'), repo.toLowerCase().replace(/\\/g, '/'));
  writeFileSync(join(repo, '.gitignore'), 'local/\n');
  writeFileSync(join(repo, 'daoris.gates.json'), `${JSON.stringify({ gates: [{ name: 'first', run: 'node a.mjs' }] })}\n`);
  writeFileSync(join(repo, 'code.txt'), 'gated\n');
  git('add', '-A');
  git('commit', '--quiet', '-m', 'gated');
  const gated = git('rev-parse', 'HEAD');
  const to = join(fx.root, 'install');

  let source = gatedSource(repo, gated);
  // The CI gates a commit in the gate tree, running there only what its verdicts lack.
  assert.match(source.refusal!, new RegExp(`no gate tree at local/gate-tree: node tools/merge-branch\\.mjs --ci --stage ${gated.slice(0, 7)} makes one and gates it`));

  const tree = join(repo, 'local', 'gate-tree');
  git('worktree', 'add', '--quiet', '--detach', tree, gated);
  // Main moves on past what is gated: a merge committed while the full set runs.
  writeFileSync(join(repo, 'code.txt'), 'merged since\n');
  git('commit', '--quiet', '-am', 'merged since');
  const moved = git('rev-parse', 'HEAD');

  source = gatedSource(repo, 'main');
  assert.match(source.refusal!, new RegExp(`the gate tree holds ${gated.slice(0, 7)}, not ${moved.slice(0, 7)}: node tools/merge-branch\\.mjs --ci --stage ${moved.slice(0, 7)} gates that one there`));
  source = gatedSource(repo, gated.slice(0, 7));
  assert.equal(source.refusal, null, source.refusal ?? '');
  assert.equal(source.commit, gated);
  assert.equal(resolve(source.tree!).toLowerCase(), resolve(tree).toLowerCase());
  assert.equal(resolve(source.home!).toLowerCase(), resolve(repo).toLowerCase());
  // Never while its full set runs: what it would build is what is being gated.
  writeFileSync(join(repo, GATE_TREE_LOCK), `${JSON.stringify({ pid: process.pid, commit: gated, at: '2026-10-18T10:00:00Z' })}\n`);
  source = gatedSource(repo, gated, { alive: () => true });
  assert.match(source.refusal!, new RegExp(`its full set is still running \\(pid ${process.pid}, since 2026-10-18 10:00 UTC\\)`));
  assert.equal(gatedSource(repo, gated, { alive: () => false }).refusal, null, 'a lock whose process ended holds nothing');
  rmSync(join(repo, GATE_TREE_LOCK));

  // The verdicts are the main checkout's, keyed by tree: main's own pass does not stage the gate tree, and its gated tree's does.
  const record = (trees: string[]) => writeFileSync(join(repo, 'local', 'gate-verdicts.json'), `${JSON.stringify({
    schema: 1, verdicts: trees.map((at) => ({ gate: 'first', verdict: 'PASS', tree: at, at: '2026-10-18T11:00:00Z', commit: null, branch: null })),
  }, null, 2)}\n`);
  record([git('rev-parse', `${moved}^{tree}`)]);
  // A background run finished on each tree, as a stage needs.
  receipt(repo, git('rev-parse', `${moved}^{tree}`));
  receipt(repo, git('rev-parse', `${gated}^{tree}`));
  assert.equal(ungatedRefusal(to, repo).refusal, null, 'the main checkout passes on its own tree');
  const refused = ungatedRefusal(to, tree);
  assert.match(refused.refusal!, /the full set of gates has not passed this checkout/);
  assert.match(refused.refusal!, /first\s+stale\s+PASS .*code\.txt changed/);
  record([git('rev-parse', `${gated}^{tree}`)]);
  const passed = ungatedRefusal(to, tree);
  assert.equal(passed.refusal, null, passed.refusal ?? '');
  assert.match(passed.note!, /the full set has passed/);

  // The gate tree's own publish runs with the same arguments, but the commit, and the install as an absolute path.
  assert.deepEqual(childArgs(['--to', 'install', '--service', '--stage', '--from-gated', 'abc1234'], to), ['--to', to, '--service', '--stage']);
  // A stage done is the record's note, which the next merge's prune reads to remove the tree.
  writeFileSync(join(repo, GATE_TREE_RECORD), `${JSON.stringify({ schema: 1, commit: gated, ended: '2026-10-18T11:00:00Z', passed: true })}\n`);
  recordStaged(repo, { commit: gated, to, at: '2026-10-18T12:00:00Z' });
  const noted = JSON.parse(readFileSync(join(repo, GATE_TREE_RECORD), 'utf8'));
  assert.deepEqual(noted.staged, { commit: gated, to, at: '2026-10-18T12:00:00Z' });
  assert.equal(noted.passed, true, 'the rest of the record is kept');
  git('worktree', 'remove', tree);
  fx.cleanup();
});

// The stage history is the stage's own record, apart from the gate tree's, which every run rewrites.
test('the stage history survives a prune and another CI run, a later stage reads its baseline from it, and a first stage or a non-ancestor says the range is unknown', () => {
  const fx = makeFixture('publish-stage-history');
  const repo = join(fx.root, 'repo');
  mkdirSync(repo, { recursive: true });
  const env = {
    ...process.env, GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
    GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
  };
  const git = (...args: string[]): string => {
    const result = spawnSync('git', args, { cwd: repo, encoding: 'utf8', env, windowsHide: true });
    assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
    return result.stdout.trim();
  };
  git('init', '--quiet', '--template=', '-b', 'main');
  assert.equal(git('rev-parse', '--show-toplevel').toLowerCase().replace(/\\/g, '/'), repo.toLowerCase().replace(/\\/g, '/'));
  writeFileSync(join(repo, '.gitignore'), 'local/\n');
  const commit = (text: string): string => {
    writeFileSync(join(repo, 'code.txt'), `${text}\n`);
    git('add', '-A');
    git('commit', '--quiet', '-m', text);
    return git('rev-parse', 'HEAD');
  };
  const first = commit('first');
  const to = join(fx.root, 'install');

  // A first stage: nothing to compare with.
  assert.deepEqual(stageReport(repo, first), ['since the last stage: unknown (no stage is recorded yet)']);
  recordStage(repo, { commit: first, to, at: '2026-10-24T10:00:00Z' });
  const history = () => readFileSync(join(repo, 'local', 'stage-history.jsonl'), 'utf8').split('\n').filter(Boolean).map((line) => JSON.parse(line));
  assert.deepEqual(history(), [{ commit: first, tree: git('rev-parse', `${first}^{tree}`), to, at: '2026-10-24T10:00:00Z', forced: false }]);

  // A prune removes the gate tree, and another CI run rewrites the tree's record: the history is neither.
  git('worktree', 'add', '--quiet', '--detach', join(repo, 'local', 'gate-tree'), first);
  recordStaged(repo, { commit: first, to, at: '2026-10-24T10:00:00Z' });
  git('worktree', 'remove', join(repo, 'local', 'gate-tree'));
  writeFileSync(join(repo, GATE_TREE_RECORD), `${JSON.stringify({ schema: 1, commit: 'later', started: '2026-10-24T11:00:00Z', staged: null })}\n`);
  commit('second');
  const third = commit('third');
  const report = stageReport(repo, third);
  assert.match(report[0]!, new RegExp(`since the last stage \\(${first.slice(0, 7)}, 2026-10-24 10:00 UTC\\): 2 commits`));
  assert.deepEqual(report.slice(1).map((line: string) => line.replace(/^ {2}[0-9a-f]+ /, '')), ['third', 'second']);
  recordStage(repo, { commit: third, to, at: '2026-10-24T12:00:00Z', forced: true });
  assert.equal(history().at(-1).forced, true, 'a forced stage is recorded as forced');

  // A last staged commit this one does not descend from: the range is unknown, and said.
  git('checkout', '--quiet', '-b', 'other', first);
  const other = commit('other');
  assert.deepEqual(stageReport(repo, other), [`since the last stage: unknown (the last staged commit, ${third.slice(0, 7)}, is not an ancestor of this one)`]);
  fx.cleanup();
});

// A CI watch moves the gate tree to main's tip as main moves, so a stage that only looked at the lock first would build a
// tree moved under it. The pipeline design's §4.6 shape: the stage holds the gate tree's lock through its build, a holder of its own kind.
test('a stage from the gate tree holds its lock through the build, so no CI run moves the tree under it, and is refused while a run holds it (CI1)', async () => {
  const fx = makeFixture('publish-gated-hold');
  const repo = join(fx.root, 'repo');
  mkdirSync(repo, { recursive: true });
  const env = {
    ...process.env, GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.test',
    GIT_COMMITTER_NAME: 'Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.test',
  };
  const git = (...args: string[]): string => {
    const result = spawnSync('git', args, { cwd: repo, encoding: 'utf8', env, windowsHide: true });
    assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
    return result.stdout.trim();
  };
  git('init', '--quiet', '--template=', '-b', 'main');
  assert.equal(git('rev-parse', '--show-toplevel').toLowerCase().replace(/\\/g, '/'), repo.toLowerCase().replace(/\\/g, '/'));
  writeFileSync(join(repo, '.gitignore'), 'local/\n');
  writeFileSync(join(repo, 'code.txt'), 'gated\n');
  git('add', '-A');
  git('commit', '--quiet', '-m', 'gated');
  const gated = git('rev-parse', 'HEAD');
  const short = gated.slice(0, 7);
  git('worktree', 'add', '--quiet', '--detach', join(repo, 'local', 'gate-tree'), gated);
  const lock = join(repo, GATE_TREE_LOCK);

  // A CI run holds the tree: no stage, and the refusal says whose it is.
  writeFileSync(lock, `${JSON.stringify({ pid: process.pid, commit: gated, at: '2026-10-22T10:00:00Z', holder: 'ci' })}\n`);
  let source = gatedSource(repo, gated, { alive: () => true, hold: true });
  assert.match(source.refusal!, new RegExp(`a CI run is gating it \\(pid ${process.pid}, since 2026-10-22 10:00 UTC, on ${short}\\): its verdicts are not in yet`));
  // Another stage building from it.
  writeFileSync(lock, `${JSON.stringify({ pid: 4242, commit: gated, at: '2026-10-22T10:30:00Z', holder: 'stage' })}\n`);
  source = gatedSource(repo, gated, { alive: () => true, hold: true });
  assert.match(source.refusal!, new RegExp(`another stage of ${short} is building from it \\(pid 4242, since 2026-10-22 10:30 UTC\\)`));

  // One whose process ended holds nothing: the stage takes the lock as its own, until its build ends.
  source = gatedSource(repo, gated, { alive: () => false, hold: true, now: () => '2026-10-22T11:00:00Z' });
  assert.equal(source.refusal, null, source.refusal ?? '');
  assert.deepEqual(JSON.parse(readFileSync(lock, 'utf8')), { pid: process.pid, commit: gated, at: '2026-10-22T11:00:00Z', holder: 'stage' });
  // A CI run started meanwhile is refused by it, as the merge tool reads the lock.
  const mergeTool = await import(
    // @ts-expect-error — untyped workspace tooling; see above
    '../../../tools/merge-branch.mjs') as { takeGateLock: (home: string, options: { commit: string; holder?: string }) => unknown };
  assert.throws(() => mergeTool.takeGateLock(repo, { commit: gated, holder: 'ci' }), new RegExp(`a stage of ${short} is building from the gate tree`));
  source.release!();
  assert.equal(existsSync(lock), false, 'let go once its build has ended');
  source.release!();
  // A lock that does not read is no run's, as the merge tool reads it.
  writeFileSync(lock, 'not json');
  source = gatedSource(repo, gated, { hold: true });
  assert.equal(source.refusal, null, source.refusal ?? '');
  assert.equal(JSON.parse(readFileSync(lock, 'utf8')).holder, 'stage');
  source.release!();
  // Asked without holding, as its test above asks, nothing is taken.
  assert.equal(gatedSource(repo, gated).refusal, null);
  assert.equal(existsSync(lock), false);

  // A refused stage from the gate tree names the CI's command, which runs there what the commit's verdicts lack.
  const missing = 'merge-branch: the full set has NOT passed this checkout (tree 1a2b3c4, HEAD abc1234): 2 of 3 gates.\n  web  stale  PASS\n';
  const refused = ungatedRefusal(join(fx.root, 'install'), join(repo, 'local', 'gate-tree'), {
    ask: () => ({ code: 1, out: `${missing}  Run the stale gate on it: node tools/merge-branch.mjs --stale\n` }),
    remedy: { what: "what this commit's verdicts lack", where: 'in the gate tree', command: `node tools/merge-branch.mjs --ci --stage ${short}` },
  });
  assert.match(refused.refusal!, new RegExp(`run what this commit's verdicts lack in the gate tree: node tools/merge-branch\\.mjs --ci --stage ${short}\\n`));
  assert.doesNotMatch(refused.refusal!, /--stale/);
  git('worktree', 'remove', join(repo, 'local', 'gate-tree'));
  fx.cleanup();
});

test('a stage replaces what was staged whole, and a publish in place removes it', () => {
  const at = folder();
  markInstalled(at);
  mkdirSync(join(at, ...STAGED), { recursive: true });
  writeFileSync(join(at, ...STAGED, 'stale.dll'), 'old');
  const staging = join(at, ...STAGE, '.staging');
  mkdirSync(staging, { recursive: true });
  writeFileSync(join(staging, BUILD_MANIFEST), '{}');

  promoteStage(staging, at);

  assert.deepEqual(readdirSync(join(at, ...STAGED)), [BUILD_MANIFEST]);
  assert.ok(!existsSync(staging));

  writeFileSync(join(at, ...STAGE, SWAP_JOURNAL), '{ "phase": "installed" }');
  unstage(at);
  assert.ok(!existsSync(join(at, ...STAGED)));
  assert.ok(existsSync(join(at, ...STAGE, SWAP_JOURNAL)), 'the last swap’s record stays to read');
});

test('a stage whose rename stays refused is copied into place, its manifest last, so a half copy is nothing staged', () => {
  const at = folder();
  markInstalled(at);
  const staging = join(at, ...STAGE, '.staging');
  mkdirSync(join(staging, 'app'), { recursive: true });
  writeFileSync(join(staging, 'app', 'Daoris.Desktop.exe'), 'new application');
  writeFileSync(join(staging, BUILD_MANIFEST), '{ "id": "b2" }');
  const written: string[] = [];
  const held = () => { throw Object.assign(new Error('EPERM: operation not permitted, rename'), { code: 'EPERM' }); };

  promoteStage(staging, at, { rename: held, tries: 2, waitMs: 1, copied: (path: string) => written.push(path) });

  assert.equal(readFileSync(join(at, ...STAGED, 'app', 'Daoris.Desktop.exe'), 'utf8'), 'new application');
  assert.equal(readFileSync(join(at, ...STAGED, BUILD_MANIFEST), 'utf8'), '{ "id": "b2" }');
  assert.equal(written.at(-1), BUILD_MANIFEST, 'the manifest is the last file written');
  assert.ok(!existsSync(staging));
});

test('a folder that does not exist, or is empty, is fine', () => {
  const at = folder();
  assert.equal(refusal(join(at, 'not-yet'), { beside: false }), null);
  assert.equal(refusal(at, { beside: false }), null);
});

test('a folder this script installed to before is fine, whatever else it now holds', () => {
  const at = folder();
  markInstalled(at);
  writeFileSync(join(at, LAUNCHER), '');
  mkdirSync(join(at, 'app'));
  mkdirSync(join(at, 'a-repository'));
  assert.ok(isInstall(at));
  assert.equal(refusal(at, { beside: false }), null);
});

/**
 * The note a publish writes is also its marker, so it opens with the header `isInstall` reads, and it
 * names no machine path (`sensitive-info`): it is written into the install, never tracked. It also says
 * how to pin Daoris: from its running window, because the window names Daoris's taskbar id
 * and a pin made from it starts the launcher; a pin made on the launcher in Explorer carries no id, so
 * the running window shows as a second button beside it.
 */
// A staged build drains the install, which holds the presses that would start work, so the stage says so and how to
// stage it *not now*, in the install's note and in what the stage prints.
test('the stage says the install drains, what it holds, and that update --cancel stages it not now (DRAINHOLD1)', () => {
  for (const words of [installedNote(), stagedAdvice().join('\n')]) {
    assert.match(words, /drain/i);
    assert.match(words, /starts nothing new/);
    assert.match(words, /holds the presses that would start work/);
    assert.match(words, /daoris update --cancel/);
    assert.match(words, /not now/i);
    assert.match(words, /update --when-idle/);
  }
});

test('the install note is the marker, and says how to pin Daoris from its running window (D108)', () => {
  const note: string = installedNote();
  assert.ok(note.startsWith(`${MARKER_HEADER}\n`), 'the note opens with the header isInstall reads');
  const at = folder();
  writeFileSync(join(at, MARKER), note);
  assert.ok(isInstall(at));

  const pinning = note.slice(note.indexOf('## Pinning it to the taskbar'));
  assert.ok(note.includes('## Pinning it to the taskbar'), 'the note has no pinning section');
  const section = pinning.slice(0, pinning.indexOf('\n## ', 3) === -1 ? undefined : pinning.indexOf('\n## ', 3));
  assert.match(section, /right-click its button on the taskbar, then \*Pin to taskbar\*/);
  assert.match(section, new RegExp(`starts \`${LAUNCHER.replace('.', '\\.')}\` here`));
  // Why the other pin shows a second button: the launcher starts the application and exits.
  assert.match(section, new RegExp(`A pin made on \`${LAUNCHER.replace('.', '\\.')}\` itself, from Explorer, carries no id`));
  assert.match(section, /second button/);
  assert.match(section, new RegExp(`\`${[...SHELL_HOME, SHELL_EXE].join('/').replace(/\./g, '\\.')}\``));
  assert.match(section, /unpin/);

  assert.doesNotMatch(note, /[A-Za-z]:[\\/]|\/home\/|\/Users\//, 'a machine path in the note');
});

/**
 * The note names both service hosts `--service` lays under `app/`, the connector a session is handed
 * among them, and says what they outrank: whoever opens the folder to ask which connector runs reads it here.
 */
test('the install note says where both service hosts are, and that they come before any in the home’s bin/', () => {
  const note: string = installedNote();
  const row = note.split('\n').find((line) => line.startsWith(`| \`${SHELL_HOME[0]}/\``)) ?? '';
  assert.ok(row.includes(`\`${HOST_HOME.slice(1).join('/')}/\``), 'the HTTP host’s folder');
  assert.ok(row.includes(`\`${CONNECTOR_HOME.slice(1).join('/')}/\``), 'the connector’s folder');
  assert.match(row, /handed/, 'what the connector is for');
  assert.match(note, /ahead of any in `bin\/`/);
  // What it says of a re-publish is what `refusal` does, below.
  assert.match(note.replace(/\s+/g, ' '), /with `--service` while it carries the hosts \(a re-publish without it is refused/);
});

/**
 * The note says the install carries its doctrine tool, where, how each shell runs it,
 * on which Node, and that nothing changes the account's PATH, so whoever opens
 * the folder is not left wondering why a terminal of theirs still runs another `daoris`.
 */
test('the install note says where the doctrine tool is, how each shell runs it, and that your PATH is untouched', () => {
  const note: string = installedNote();
  assert.ok(note.includes('## The command'), 'the note has no section for the doctrine tool');
  const section = note.slice(note.indexOf('## The command')).split('\n## ')[0] ?? '';
  assert.ok(section.includes(`\`${CLI_BIN.join('/')}/\``), 'names app/bin/');
  assert.ok(section.includes(`\`${CLI_HOME.join('/')}/\``), 'names app/cli/');
  for (const launcher of CLI_LAUNCHERS) assert.ok(section.includes(`\`${launcher}\``), `names ${launcher}`);
  assert.match(section, /Git Bash/);
  assert.match(section, /Command Prompt/);
  assert.match(section, /PowerShell/);
  assert.match(section, /Node\.js 22 or later/);
  assert.match(section, /Nothing puts `app\/bin\/` on your account's PATH/);
  assert.doesNotMatch(note, /[A-Za-z]:[\\/]|\/home\/|\/Users\//, 'a machine path in the note');
});

/**
 * Every child Daoris starts is handed a PATH that begins with the install's `app/bin/`, found
 * beside the home (`Tools.InstallBin`), while the account's PATH stays the person's. The note says both, in one section, and names the condition the
 * driver's `ToolsChildrenTests` holds: the home, `data/`, sits beside `app/`.
 */
test('the install note says Daoris’s own children find the doctrine tool first on their PATH, and your account’s PATH is untouched', () => {
  const note: string = installedNote();
  const section = note.slice(note.indexOf('## The command')).split('\n## ')[0] ?? '';
  const prose = section.replace(/\s+/g, ' ');
  assert.match(prose, /Every program Daoris starts finds it by its bare name/);
  for (const child of ['a driven session', 'a conversation', 'an intake', 'a hook', 'a landing plugin', 'every shell of its terminal panel']) {
    assert.ok(prose.includes(child), `names ${child}`);
  }
  assert.ok(prose.includes(`begin their PATH with \`${CLI_BIN.join('/')}/\``), 'says app/bin/ comes first');
  assert.ok(prose.includes(`this install's home, \`${HOME}/\`, sits beside \`${CLI_BIN[0]}/\``), 'names why: the home beside app/');
  assert.match(prose, /Nothing puts `app\/bin\/` on your account's PATH, so a terminal of yours outside Daoris still runs whatever `daoris` you installed/);
});

/**
 * The note (the install layout design §2 and §4) says what each top folder of the Daoris folder is
 * and who may lose it, a row each, in the design's order; that `data/` is the one to back up and holds no key; and what a start
 * after an update moves out of an older `data/`, once, and what it never moves.
 */
test('the install note says what each top folder is, and what a start moves out of an older data/ (HOMELAYOUT4)', () => {
  const note: string = installedNote();
  const rows = note.split('\n').filter((line) => line.startsWith('| `'));
  const named = rows.map((line) => /^\| `([^`]+)`/.exec(line)?.[1]);
  assert.deepEqual(named, [LAUNCHER, `${SHELL_HOME[0]}/`, `${STAGE[0]}/`, `${HOME}/`, 'profiles/', 'work/', 'cache/', 'run/', 'logs/']);
  const row = (name: string) => rows.find((line) => line.startsWith(`| \`${name}\``)) ?? '';
  assert.match(row(`${HOME}/`), /back it up/i);
  assert.match(row(`${HOME}/`), /no key/);
  assert.match(row('profiles/'), /sign-ins/);
  assert.match(row('cache/'), /never back it up/i);
  assert.match(row('run/'), /while Daoris is closed/);
  const prose = note.replace(/\s+/g, ' ');
  assert.match(prose, /moves what an older version kept in `data\/` into the folders above, once/);
  assert.match(prose, /account homes, session trees, rooms, kept files and `data\/bin\/` stay where they are/i);
  assert.doesNotMatch(note, /[A-Za-z]:[\\/]|\/home\/|\/Users\//, 'a machine path in the note');
});

test('a marker without the header is not ours — a file with that name proves nothing', () => {
  const at = folder();
  writeFileSync(join(at, MARKER), '# something else\n');
  assert.ok(!isInstall(at));
  assert.match(refusal(at, { beside: false }) ?? '', /INSTALLED\.md/);
});

test('by default, a folder holding anything else is refused, naming what it holds', () => {
  const at = folder();
  writeFileSync(join(at, 'notes.txt'), 'someone else was here\n');
  const sentence = refusal(at, { beside: false });
  assert.match(sentence ?? '', /notes\.txt/);
  assert.match(sentence ?? '', /--beside/);
});

test('--beside installs next to what is there, when none of the names it writes is taken', () => {
  const at = folder();
  mkdirSync(join(at, 'testbed-core'));
  mkdirSync(join(at, 'testbed-ui'));
  writeFileSync(join(at, 'notes.txt'), 'a neighbour\n');
  assert.equal(refusal(at, { beside: true }), null);
});

test('--beside still refuses a folder where a name it would write is somebody else’s', () => {
  const at = folder();
  mkdirSync(join(at, 'testbed-core'));
  mkdirSync(join(at, 'app'));
  const sentence = refusal(at, { beside: true });
  assert.match(sentence ?? '', /\bapp\b/);
  assert.doesNotMatch(sentence ?? '', /testbed-core/);
});

test('--beside on a previous install is the ordinary re-publish', () => {
  const at = folder();
  markInstalled(at);
  mkdirSync(join(at, 'app'));
  mkdirSync(join(at, 'testbed-core'));
  assert.equal(refusal(at, { beside: true }), null);
});

/**
 * Both locators run a host the install carries under `app/` ahead of the home's `bin/`, so a publish in
 * place that left an older build's there would hand every session that build's binaries. A publish without `--service`
 * never touches the hosts' folders, so over an install that carries either host it is refused, naming each folder and
 * `--service`, as `--stage` refuses an install that carries its HTTP host.
 */
const carryHost = (at: string, home: readonly string[], exe: string) => {
  mkdirSync(join(at, ...home), { recursive: true });
  writeFileSync(join(at, ...home, exe), '');
};

const anInstall = (): string => {
  const at = folder();
  markInstalled(at);
  mkdirSync(join(at, ...SHELL_HOME), { recursive: true });
  writeFileSync(join(at, ...SHELL_HOME, SHELL_EXE), '');
  return at;
};

test('a publish in place without --service is refused while the install carries either service host, naming each and --service', () => {
  const cases: { name: string; homes: (readonly string[])[] }[] = [
    { name: 'the HTTP host', homes: [HOST_HOME] },
    { name: 'the connector', homes: [CONNECTOR_HOME] },
    { name: 'both', homes: [HOST_HOME, CONNECTOR_HOME] },
  ];
  for (const { name, homes } of cases) {
    const at = anInstall();
    for (const home of homes) carryHost(at, home, home === HOST_HOME ? HOST_EXE : CONNECTOR_EXE);
    const before = readdirSync(join(at, ...SHELL_HOME)).sort();

    for (const beside of [false, true]) {
      const sentence = refusal(at, { beside, service: false }) ?? '';
      assert.match(sentence, /Refusing/, `${name}, beside: ${beside}: it says it refused`);
      assert.match(sentence, /--service/, `${name}, beside: ${beside}`);
      for (const home of [HOST_HOME, CONNECTOR_HOME]) {
        const named = sentence.includes(`${home.join('/')}/`);
        assert.equal(named, homes.includes(home), `${name}: ${home.join('/')}/ named only when carried`);
      }
      assert.equal(refusal(at, { beside, service: true }), null, `${name}: --service carries them`);
    }
    assert.deepEqual(readdirSync(join(at, ...SHELL_HOME)).sort(), before, `${name}: the refusal touches nothing`);
  }
});

test('a publish in place without --service over an install that carries neither host is the ordinary re-publish', () => {
  const at = anInstall();
  assert.equal(refusal(at, { beside: false, service: false }), null);
  assert.equal(refusal(at, { beside: true, service: false }), null);

  // A host's folder without its executable is nothing either locator runs, so nothing to leave behind.
  mkdirSync(join(at, ...HOST_HOME, 'wwwroot'), { recursive: true });
  mkdirSync(join(at, ...CONNECTOR_HOME), { recursive: true });
  assert.equal(refusal(at, { beside: false, service: false }), null);
});

/**
 * The launcher and the publish are twins: the publish lays the application out where the
 * launcher looks, and the application's assembly names the file the launcher starts. Three spellings in
 * two languages, and the only thing that holds them together is this.
 */
test('the launcher starts the application the publish lays out, by the name its build gives it', () => {
  const desktop = join(here, '..', '..', 'Daoris.Desktop');
  const launcher = readFileSync(join(desktop, 'Daoris.Desktop.Launcher', 'Program.cs'), 'utf8');
  assert.ok(launcher.includes(`AppFolder = "${SHELL_HOME.join('/')}"`), 'the launcher looks in app/');
  assert.ok(launcher.includes(`ShellExe = "${SHELL_EXE}"`), 'the launcher starts the application by its name');

  const launcherProject = readFileSync(join(desktop, 'Daoris.Desktop.Launcher', 'Daoris.Desktop.Launcher.csproj'), 'utf8');
  assert.equal(`${/<AssemblyName>([^<]+)</.exec(launcherProject)?.[1]}.exe`, LAUNCHER);

  const app = readFileSync(join(desktop, 'Daoris.Desktop.App', 'Daoris.Desktop.App.csproj'), 'utf8');
  const assembly = /<AssemblyName>([^<]+)</.exec(app)?.[1] ?? '';
  assert.equal(`${assembly.replace(/\.App$/, '')}.exe`, SHELL_EXE);
});

/**
 * `--service` lays the knowledge connector beside the HTTP host under `app/`. An install that
 * carries only its HTTP host hands every session the home's `bin/` copy, which one `publish:service --install`
 * lays down and no republish refreshes, so a stale connector could rebuild the shared store at its own
 * older schema. Each host goes in the folder `service-publish` publishes it into, built by that script's
 * recipe, so there is one way to build a host and one name for its folder.
 */
test('--service lays the connector beside the HTTP host under app/, each built by service-publish’s one recipe', () => {
  const root = join(tmpdir(), 'an-install');
  const plan = servicePlan(root);

  assert.deepEqual(plan.map((step: { executable: string }) => step.executable),
    [join(root, ...CONNECTOR_HOME, CONNECTOR_EXE), join(root, ...HOST_HOME, HOST_EXE)]);
  // Every host the service publish makes, and none of its own.
  assert.deepEqual(plan.map((step: { host: unknown }) => step.host), [...HOSTS]);
  for (const step of plan) {
    assert.equal(step.folder, dirname(step.executable));
    assert.equal(step.command, publishCommand(step.host, 'win-x64', step.folder), `${step.host.binary} is built by another recipe`);
  }
  // Under the application's folder, where a swap carries them and the locators look.
  assert.deepEqual([CONNECTOR_HOME[0], HOST_HOME[0]], [SHELL_HOME[0], SHELL_HOME[0]]);
});

/**
 * The recipe itself: self-contained single-file, with the native libraries inside it. Without
 * `IncludeNativeLibrariesForSelfExtract`, `e_sqlite3` lands beside the executable, and a copy that took only the
 * executable died on its first store open (FIX-LOG). One recipe means one place this can be lost.
 */
test('a service host is published self-contained, single-file, its native libraries inside, into its own folder', () => {
  const [connector] = HOSTS;
  assert.equal(connector.binary, CONNECTOR_HOME.at(-1));
  const command: string = publishCommand(connector, 'win-x64', join(tmpdir(), 'an-install', ...CONNECTOR_HOME));
  assert.ok(command.startsWith(`dotnet publish "${connector.project}" -c Release -r win-x64 --self-contained `), command);
  assert.match(command, / -p:PublishSingleFile=true /);
  assert.match(command, / -p:IncludeNativeLibrariesForSelfExtract=true /);
  assert.ok(command.endsWith(` -o "${join(tmpdir(), 'an-install', ...CONNECTOR_HOME)}"`), command);
});

/**
 * The deployment rehearsal runs beside the driver's git half, which builds into and runs from the desktop
 * projects' own `bin/` and `obj/`. So, told an output of its own (`--artifacts-path`), every `dotnet publish` this script
 * runs builds there, by the SDK's own `--artifacts-path`, and the stages it lays an install out from go there too. Untold,
 * each recipe is the install's own. The merge test's own-output rule reads the rehearsal's half.
 */
test('told an output of its own, every publish builds and stages there; untold, each recipe is as it was', () => {
  const repo = join(tmpdir(), 'a-checkout');
  const own = join(tmpdir(), 'an-output');
  const folder = join(tmpdir(), 'an-install', 'x');
  const told: string[] = [
    launcherPublishCommand(folder, own), appPublishCommand(folder, own), driverPublishCommand(folder, 'win-x64', own),
    ...HOSTS.map((host: unknown) => publishCommand(host, 'win-x64', folder, own)),
  ];
  for (const command of told) {
    assert.ok(command.startsWith('dotnet publish "'), command);
    assert.ok(command.endsWith(` --artifacts-path "${own}"`), command);
    assert.equal(command.split('--artifacts-path').length, 2, command);
  }
  const untold: string[] = [
    launcherPublishCommand(folder), appPublishCommand(folder), driverPublishCommand(folder),
    ...HOSTS.map((host: unknown) => publishCommand(host, 'win-x64', folder)),
  ];
  for (const command of untold) assert.doesNotMatch(command, /--artifacts-path/, command);
  assert.ok(launcherPublishCommand(folder).startsWith(`dotnet publish "${LAUNCHER_PROJECT}" -c Release -r win-x64 --self-contained false `));
  assert.match(launcherPublishCommand(folder), / -p:PublishSingleFile=true /);
  assert.ok(appPublishCommand(folder).startsWith(`dotnet publish "${APP_PROJECT}" -c Release -r win-x64 --self-contained false `));
  for (const command of [launcherPublishCommand(folder), appPublishCommand(folder)]) {
    assert.match(command, / -p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none /, command);
    assert.match(command, new RegExp(` -o "${folder.replace(/[\\.]/g, '\\$&')}" --nologo$`), command);
  }
  assert.equal(publishStages(repo, own), join(own, 'publish-stage'));
  assert.equal(publishStages(repo), join(repo, ...APP_PROJECT.split('/'), 'bin', 'publish-stage'));

  // The flag: a folder, made absolute as `--to` is; given with none, a refusal before anything is built.
  assert.deepEqual(artifactsOf(['--to', 'x'], repo), { artifacts: null, refusal: null });
  assert.deepEqual(artifactsOf(['--artifacts-path', own, '--service'], repo), { artifacts: own, refusal: null });
  assert.deepEqual(artifactsOf(['--artifacts-path', 'out'], repo), { artifacts: join(repo, 'out'), refusal: null });
  for (const argv of [['--to', 'x', '--artifacts-path'], ['--artifacts-path', '--service']]) {
    const read = artifactsOf(argv, repo);
    assert.equal(read.artifacts, null);
    assert.match(read.refusal ?? '', /--artifacts-path <folder>/);
  }

  // And the publish itself runs every `dotnet publish` through those recipes, each told the output it is given.
  const text = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'tools', 'desktop-publish.mjs'), 'utf8');
  const main = text.slice(text.indexOf('async function main()'));
  assert.ok(main.length > 1000, 'the publish has no main any more: this test has stopped reading it');
  assert.doesNotMatch(main, /\b(?:run|execSync|spawnSync)\(\s*`dotnet\b/, 'the publish runs a dotnet command no recipe here builds');
  for (const recipe of ['launcherPublishCommand', 'appPublishCommand', 'driverPublishCommand', 'publishHost']) {
    const calls = [...main.matchAll(new RegExp(`\\b${recipe}\\(([^)]*)\\)`, 'g'))].map((call) => call[1]!);
    assert.ok(calls.length >= 1, `the publish no longer runs ${recipe}`);
    for (const args of calls) assert.match(args, /,\s*artifacts$/, `${recipe}(${args}) builds into the projects' own folders`);
  }
  assert.match(main, /\bpublishStages\(repoRoot, artifacts\)/);
});

/**
 * What an earlier publish wrote and this one no longer does: a republish into its own
 * install removes exactly those names — the single-file shell's launcher at the root, and
 * the browser's own folder under `app/`, with its executable and its second engine — and a folder
 * it never published is never asked to lose anything.
 */
test('a republish removes what an earlier publish wrote and this one no longer does, by name', () => {
  assert.deepEqual(RETIRED_LAUNCHERS, ['daoris-desktop.exe']);
  assert.deepEqual(RETIRED_IN_APP, ['daoris-browser']);

  const at = folder();
  mkdirSync(join(at, ...SHELL_HOME, 'daoris-browser'), { recursive: true });
  writeFileSync(join(at, 'daoris-desktop.exe'), '');
  assert.deepEqual(retiredPaths(at), [], 'not an install: nothing of it is this script’s to remove');

  markInstalled(at);
  assert.deepEqual(retiredPaths(at),
    [join(at, 'daoris-desktop.exe'), join(at, ...SHELL_HOME, 'daoris-browser')]);
  assert.ok(!retiredPaths(at).some((path: string) => path === join(at, ...SHELL_HOME)), 'never the application’s folder');
});

/**
 * The install's languages are the kit's to lay out, and there is one engine to lay them out
 * for: the engine's fallback, `en-US`, and what the app project names
 * (`ShenoraChromiumLocales`). The publish keeps `KEPT_LOCALES` of them and the deployment gate reads
 * them back off the install, so the list and the project must agree.
 */
test('the locales an install keeps are the kit’s fallback and the ones the app project names', () => {
  const app = readFileSync(join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.App', 'Daoris.Desktop.App.csproj'), 'utf8');
  const named = (/<ShenoraChromiumLocales>([^<]*)</.exec(app)?.[1] ?? '').split(';').map((name) => name.trim()).filter(Boolean);
  assert.deepEqual([...KEPT_LOCALES].sort(), ['en-US', ...named].map((name) => `${name}.pak`).sort());
});

/**
 * The install carries Daoris's own example plugins as OFFERS, in `app/plugin-offers/`,
 * never under `data/plugins/`, so none is installed until a press. The ones meant for people: the two that
 * land work, the install's own browser and the report a session files. Not the rehearsal fixture, and not
 * the browser a machine without the shell launches, which claims the same server name.
 */
test('the offers are the examples meant for people, never a rehearsal fixture', () => {
  assert.deepEqual([...OFFERED_PLUGINS], ['daoris-github', 'daoris-azure-devops', 'daoris-in-app-browser', 'daoris-dev-report']);
  assert.ok(!OFFERED_PLUGINS.includes('hold-by-title'), 'the family and deployment rehearsals install hold-by-title');
  assert.ok(!OFFERED_PLUGINS.includes('browser'), 'a second `browser` server would contribute nothing beside in-app-browser');
  // Each is a plugin's folder; that each reads sound, by the catalogue's rules, is the driver's `PluginOfferTests`'.
  const examples = join(here, '..', '..', '..', 'examples', 'plugins');
  for (const id of OFFERED_PLUGINS) assert.ok(existsSync(join(examples, id, 'plugin.json')), id);
});

/**
 * Where the offers are is a twin: the publish lays them out, and the driver's `PluginOffers.Layout` finds them beside the
 * application or the home. Two spellings, held here.
 */
test('the offers folder is where the driver looks for it', () => {
  assert.deepEqual([...PLUGIN_OFFERS], ['app', 'plugin-offers']);
  assert.equal(PLUGIN_OFFERS[0], SHELL_HOME[0], 'beside the application, in app/');
  const driver = readFileSync(driverFile('PluginOffers.cs'), 'utf8');
  assert.ok(driver.includes(`Layout = [${PLUGIN_OFFERS.map((part: string) => `"${part}"`).join(', ')}]`), 'PluginOffers.Layout');
});

test('laying out the offers copies each one whole into app/plugin-offers, and nothing into the home', () => {
  const examples = folder();
  for (const id of [...OFFERED_PLUGINS, 'hold-by-title']) {
    mkdirSync(join(examples, id, 'lib'), { recursive: true });
    writeFileSync(join(examples, id, 'plugin.json'), `{ "id": "${id}" }\n`);
    writeFileSync(join(examples, id, 'lib', 'part.mjs'), `// ${id}\n`);
  }
  const install = folder();
  mkdirSync(join(install, 'data'), { recursive: true });

  const laid = layOffers(examples, install);

  const offers = join(install, ...PLUGIN_OFFERS);
  assert.deepEqual(laid, [...OFFERED_PLUGINS]);
  assert.deepEqual(readdirSync(offers).sort(), [...OFFERED_PLUGINS].sort());
  assert.equal(readFileSync(join(offers, 'daoris-github', 'lib', 'part.mjs'), 'utf8'), '// daoris-github\n');
  assert.equal(existsSync(join(install, 'data', 'plugins')), false, '🔴 an offer is never installed by the publish');
});

test('a republish replaces the offers whole: a stale file and an offer dropped since both go', () => {
  const examples = folder();
  for (const id of OFFERED_PLUGINS) {
    mkdirSync(join(examples, id), { recursive: true });
    writeFileSync(join(examples, id, 'plugin.json'), `{ "id": "${id}", "version": "1.1.0" }\n`);
  }
  const install = folder();
  const offers = join(install, ...PLUGIN_OFFERS);
  mkdirSync(join(offers, 'daoris-github'), { recursive: true });
  writeFileSync(join(offers, 'daoris-github', 'stale.mjs'), '// from the publish before\n');
  mkdirSync(join(offers, 'retired-offer'), { recursive: true });

  layOffers(examples, install);

  assert.equal(existsSync(join(offers, 'daoris-github', 'stale.mjs')), false);
  assert.equal(existsSync(join(offers, 'retired-offer')), false);
  assert.match(readFileSync(join(offers, 'daoris-github', 'plugin.json'), 'utf8'), /1\.1\.0/);
  assert.deepEqual(readdirSync(join(install, SHELL_HOME[0])), [PLUGIN_OFFERS[1]], 'nothing left staged beside it');
});

test('laying out the offers waits for a held staging folder, as the other publish steps do (deployment rehearsal 2026-10-08)', () => {
  const examples = folder();
  for (const id of OFFERED_PLUGINS) {
    mkdirSync(join(examples, id), { recursive: true });
    writeFileSync(join(examples, id, 'plugin.json'), `{ "id": "${id}" }\n`);
  }
  const install = folder();
  let attempts = 0;

  const laid = layOffers(examples, install, {
    tries: 2,
    waitMs: 0,
    rename: (from: string, to: string) => {
      attempts += 1;
      if (attempts === 1) throw Object.assign(new Error('folder held'), { code: 'EPERM' });
      renameHeld(from, to);
    },
  });

  assert.equal(attempts, 2, 'the held swap is tried again, not thrown');
  assert.deepEqual(laid, [...OFFERED_PLUGINS]);
  assert.deepEqual(readdirSync(join(install, ...PLUGIN_OFFERS)).sort(), [...OFFERED_PLUGINS].sort());
});

test('an offer missing from the examples stops the publish, naming it, and the offers stand as they were', () => {
  const examples = folder();
  mkdirSync(join(examples, 'daoris-github'), { recursive: true });
  writeFileSync(join(examples, 'daoris-github', 'plugin.json'), '{ "id": "daoris-github" }\n');
  const install = folder();
  const offers = join(install, ...PLUGIN_OFFERS);
  mkdirSync(join(offers, 'daoris-in-app-browser'), { recursive: true });

  assert.throws(() => layOffers(examples, install), /daoris-azure-devops/);
  assert.ok(existsSync(join(offers, 'daoris-in-app-browser')), 'the last publish’s offers are untouched');
});

/**
 * The install carries the list built in at `app/resources.json`, beside the application,
 * where the driver reads it from its own folder. Two spellings, held here, and the source the publish copies is the
 * driver's own. Whether the list reads is the driver's `ToolResourcesTests`', which read the build's copy.
 */
const workspace = join(here, '..', '..', '..');

test('the list built in is where the driver looks for it', () => {
  assert.deepEqual([...RESOURCES], ['app', 'resources.json']);
  assert.equal(RESOURCES[0], SHELL_HOME[0], 'beside the application, in app/');
  const driver = readFileSync(driverFile('ToolResources.cs'), 'utf8');
  assert.ok(driver.includes(`Layout = [${RESOURCES.map((part: string) => `"${part}"`).join(', ')}]`), 'ToolResources.Layout');

  assert.deepEqual([...RESOURCES_SOURCE], ['src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'resources.json']);
  const project = readFileSync(join(workspace, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Daoris.Desktop.Driver.csproj'), 'utf8');
  assert.match(project, /<None Include="resources\.json" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="Never" \/>/,
    'every build carries it beside the driver, and the publish lays it out one way');
});

test('laying out the list copies the driver’s own whole into app/resources.json, and nothing into the home', () => {
  const install = folder();
  mkdirSync(join(install, 'data'), { recursive: true });
  const source = join(workspace, ...RESOURCES_SOURCE);

  const laid = layResources(source, install);

  assert.equal(laid, join(install, ...RESOURCES));
  assert.deepEqual(readFileSync(laid), readFileSync(source), 'the bytes as tracked, so its hash is the list’s own');
  assert.deepEqual(readdirSync(join(install, 'data')), [], 'nothing under the home');
  assert.deepEqual(readdirSync(join(install, SHELL_HOME[0])), [RESOURCES[1]], 'nothing left staged beside it');
});

test('a republish replaces the list whole', () => {
  const install = folder();
  mkdirSync(join(install, SHELL_HOME[0]), { recursive: true });
  writeFileSync(join(install, ...RESOURCES), '{"schema":1,"tools":{"gh":{}}}\n');
  const source = join(folder(), 'resources.json');
  writeFileSync(source, '{"schema":1,"tools":{}}\n');

  layResources(source, install);

  assert.equal(readFileSync(join(install, ...RESOURCES), 'utf8'), '{"schema":1,"tools":{}}\n');
  assert.deepEqual(readdirSync(join(install, SHELL_HOME[0])), [RESOURCES[1]]);
});

test('a list that is not schema 1 stops the publish, naming it, and the list stands as it was', () => {
  const install = folder();
  mkdirSync(join(install, SHELL_HOME[0]), { recursive: true });
  writeFileSync(join(install, ...RESOURCES), '{"schema":1,"tools":{}}\n');
  const at = folder();

  const sources: [string, string][] = [['not JSON', 'not json'], ['a newer schema', '{"schema":2,"tools":{}}'], ['a list', '[]']];
  for (const [name, text] of sources) {
    const source = join(at, `${name}.json`);
    writeFileSync(source, text);
    assert.throws(() => layResources(source, install), (error: Error) => error.message.includes(source), name);
    assert.equal(readFileSync(join(install, ...RESOURCES), 'utf8'), '{"schema":1,"tools":{}}\n', `${name}: the last list is untouched`);
  }
});

test('the last publish’s record is read from inside app/, and no record is no names', () => {
  const at = folder();
  assert.deepEqual(recordedShellFiles(at), []);
  mkdirSync(join(at, SHELL_FILES[0]));
  writeFileSync(join(at, ...SHELL_FILES), `${LAUNCHER}
libcef.dll

locales
`);
  assert.deepEqual(recordedShellFiles(at), [LAUNCHER, 'libcef.dll', 'locales']);
});

// ---------------------------------------------------------------------------------------------
// The install carries its doctrine tool. The publish packs `src/Daoris.Cli` as the
// release does, unpacks the tarball under `app/cli/` with the tar reader the CLI carries, and writes a
// launcher for each shell into `app/bin/`. The pack itself is a real `npm pack`, which only the deployment
// rehearsal runs; what is held here is where the package lands, what the launchers say, and what the
// layout refuses, over a tarball made in the test.

const workspaceCli = join(workspace, 'src', 'Daoris.Cli');

/** A package as `npm pack` writes one: every entry under `package/`, gzipped. */
function packed(at: string, name: string, files: Record<string, string>): string {
  const tarball = join(at, `${name}.tgz`);
  const entries = Object.entries(files).map(([path, body]) => tarEntry(path, body));
  writeFileSync(tarball, gzipSync(Buffer.concat([...entries, TAR_END])));
  return tarball;
}

/** A bin entry that says what it is handed, and exits with the code `--exit` names. */
const ECHO_BIN = [
  'const args = process.argv.slice(2);',
  'console.log(JSON.stringify(args));',
  "const at = args.indexOf('--exit');",
  'process.exit(at === -1 ? 0 : Number(args[at + 1]));',
  '',
].join('\n');

/** The release's package, in miniature: its manifest, its bin entry, its built dispatcher and its canon. */
function releasePackage(version = '0.4.2', overrides: Record<string, string | null> = {}): Record<string, string> {
  const files: Record<string, string | null> = {
    'package/package.json': JSON.stringify({ name: 'daoris', version, bin: { daoris: 'bin/daoris.mjs' } }),
    'package/bin/daoris.mjs': ECHO_BIN,
    'package/dist/cli.js': 'export async function runCli() { return 0; }\n',
    'package/canon/canon.json': JSON.stringify({ version }),
    'package/canon/core/rules/sensitive-info.md': '---\nname: sensitive-info\napplies_when: w\nenforces: e\n---\n\nBody.\n',
    ...overrides,
  };
  return Object.fromEntries(Object.entries(files).filter((pair): pair is [string, string] => pair[1] !== null));
}

test('the doctrine tool lands where the CLI reads the canon it ships, as npm lays a package out under a prefix', () => {
  assert.deepEqual([...CLI_HOME], ['app', 'cli']);
  assert.deepEqual([...CLI_PACKAGE], ['app', 'cli', 'node_modules', 'daoris']);
  assert.deepEqual([...CLI_PACKAGE.slice(0, CLI_HOME.length)], [...CLI_HOME]);
  assert.deepEqual([...CLI_BIN], ['app', 'bin']);
  assert.equal(CLI_HOME[0], SHELL_HOME[0], 'beside the application, in app/');
  assert.equal(CLI_BIN[0], SHELL_HOME[0], 'beside the application, in app/');

  // 🔴 The folder `node_modules` is what makes the unpacked package read its own canon: anywhere else
  // `resolveCanonRoot` takes it for a development checkout and reads `../../canon`, which in an install
  // is `<install>/canon`, a folder nothing publishes.
  const install = join(workspace, '_fixtures', 'an-install');
  const held = process.env.DAORIS_CANON;
  delete process.env.DAORIS_CANON;
  try {
    assert.equal(resolveCanonRoot(join(install, ...CLI_PACKAGE)), join(install, ...CLI_PACKAGE, 'canon'));
    assert.notEqual(resolveCanonRoot(join(install, ...CLI_HOME)), join(install, ...CLI_HOME, 'canon'),
      'unpacked straight into app/cli the package would read another tree');
  } finally {
    if (held !== undefined) process.env.DAORIS_CANON = held;
  }

  // The package is the one the release publishes, entered where its own manifest's `bin` says.
  const manifest = JSON.parse(readFileSync(join(workspaceCli, 'package.json'), 'utf8'));
  assert.equal(manifest.name, CLI_PACKAGE.at(-1));
  assert.equal(manifest.bin?.daoris, CLI_ENTRY.join('/'));
  assert.ok(manifest.files.includes(CLI_ENTRY[0]), 'the bin entry ships');
});

/**
 * A program the install carries finds its install, and the home in it, by the layout the publish
 * lays out: the marker at the root, the programs under `app/`, the home in `data/`. And the running desktop's address in that
 * home is one file name to the driver that writes it and the CLI that reads it. Three spellings each, held here.
 */
test('a terminal’s programs find the install and the running Daoris where the publish and the desktop put them', () => {
  assert.equal(INSTALL_MARKER, MARKER);
  assert.equal(INSTALL_APP, SHELL_HOME[0]);
  assert.equal(INSTALL_DATA, HOME);
  const home = readFileSync(driverFile('DaorisHome.cs'), 'utf8');
  assert.ok(home.includes(`InstallMarker = "${MARKER}"`), 'DaorisHome.InstallMarker');
  assert.ok(home.includes(`InstallApp = "${SHELL_HOME[0]}"`), 'DaorisHome.InstallApp');
  assert.ok(home.includes(`InstallData = "${HOME}"`), 'DaorisHome.InstallData');
  // The address file is the home's `running` entry, its place in the driver's table, under `run/`.
  assert.ok(home.includes(`new("running", HomeCategory.Run, "run/${RUNNING_FILE}", "${RUNNING_FILE}")`), 'HomeEntry.Running');
  // The service's hosts find it by the same three spellings, in the service's own copy.
  const serviceHome = readFileSync(join(here, '..', '..', 'Daoris.Service', 'Daoris.Service.Core', 'Platform', 'DaorisHome.cs'), 'utf8');
  assert.ok(serviceHome.includes(`InstallMarker = "${MARKER}"`), 'the service’s DaorisHome.InstallMarker');
  assert.ok(serviceHome.includes(`InstallApp = "${SHELL_HOME[0]}"`), 'the service’s DaorisHome.InstallApp');
  assert.ok(serviceHome.includes(`InstallData = "${HOME}"`), 'the service’s DaorisHome.InstallData');

  // The CLI as an install carries it, the driver host and the service's two hosts, each find the install above them.
  const install = folder();
  markInstalled(install);
  assert.equal(installOf(join(install, ...CLI_PACKAGE, 'src', 'platform')), install);
  assert.equal(installOf(join(install, ...DRIVER_HOME)), install);
  assert.equal(installOf(join(install, ...CONNECTOR_HOME)), install);
  assert.equal(installOf(join(install, ...HOST_HOME)), install);
});

/**
 * The launchers' folder is where every child's PATH begins. The publish lays it out, and the driver's
 * `DaorisHome.InstallBinLayout` (which `Tools.InstallBinLayout` reads) finds it beside the home, which in an install is the same
 * `app/`. Two spellings, held here.
 */
test('the launchers are where the tools’ environment puts them first on every child’s PATH', () => {
  const driver = readFileSync(driverFile('DaorisHome.cs'), 'utf8');
  assert.ok(driver.includes(`InstallBinLayout = [${CLI_BIN.map((part: string) => `"${part}"`).join(', ')}]`), 'DaorisHome.InstallBinLayout');
});

test('ONEWAY1ai-i daoris’s launcher for each shell runs the desktop’s program with no Node, and names no machine path', () => {
  const launchers: Record<string, string> = cliLaunchers();
  assert.deepEqual(Object.keys(launchers).sort(), [...CLI_LAUNCHERS].sort());
  assert.deepEqual([...CLI_LAUNCHERS].sort(), ['daoris', 'daoris.cmd']);
  const program = [...DRIVER_HOME, DRIVER_EXE].join('/');

  // Git Bash: a shell script, LF, finding its own folder from how it is called.
  const sh = launchers['daoris'] ?? '';
  assert.ok(sh.startsWith('#!/bin/sh\n'), 'Git Bash runs a file by its shebang');
  assert.ok(!sh.includes('\r'), 'a carriage return ends the shebang line in the wrong place');
  const shPath = /\nexec "\$basedir\/([^"]+)" "\$@"/.exec(sh)?.[1] ?? '';
  assert.equal(posix.normalize(posix.join(CLI_BIN.join('/'), shPath)), program, `the script runs ${shPath}`);

  // Command Prompt, and PowerShell by PATHEXT: a batch file, CRLF as cmd.exe reads one, every argument passed on.
  const cmd = launchers['daoris.cmd'] ?? '';
  assert.ok(cmd.split('\r\n').slice(0, -1).every((line) => !line.includes('\n')), 'every line ends CRLF');
  assert.ok(cmd.endsWith('\r\n'));
  const cmdPath = /\r\n"%~dp0([^"]+)" %\*/.exec(cmd)?.[1] ?? '';
  assert.equal(posix.normalize(posix.join(CLI_BIN.join('/'), cmdPath.replace(/\\/g, '/'))), program, `the batch file runs ${cmdPath}`);

  for (const [name, text] of Object.entries(launchers)) {
    // cmd.exe reads a batch file in the console's code page, so a non-ASCII byte prints as something else.
    assert.ok(/^[\x00-\x7F]*$/.test(text), `${name} is ASCII`);
    assert.doesNotMatch(text, /[A-Za-z]:[\\/]|\/home\/|\/Users\//, `a machine path in ${name}`);
    // A desktop group needs no Node: the program hands the doctrine commands to the CLI the install carries itself.
    assert.doesNotMatch(text, /\bnode\b/, `${name} runs the program alone`);
  }
});

/**
 * The desktop's program is published framework-dependent into `app/daoris-driver/`, which keeps its name
 * for good; `daoris`'s launchers run it.
 */
test('ONEWAY1ai-iv the program is its own folder under app/', () => {
  assert.deepEqual([...DRIVER_HOME], [SHELL_HOME[0], 'daoris-driver']);
  assert.equal(DRIVER_EXE, 'daoris-driver.exe');
  // The CLI finds the program its install carries where the publish lays it, two spellings held here.
  assert.deepEqual([...DRIVER_PROGRAM_HOME], [...DRIVER_HOME]);
  assert.equal(DRIVER_PROGRAM_FILE, process.platform === 'win32' ? DRIVER_EXE : DRIVER_EXE.replace(/\.exe$/, ''));
  const csproj = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', DRIVER_PROJECT, 'Daoris.Desktop.Driver.Host.csproj'), 'utf8');
  assert.ok(csproj.includes(`<AssemblyName>${DRIVER_HOME.at(-1)}</AssemblyName>`), 'the Host’s binary is the folder’s name');
  const command: string = driverPublishCommand(join(tmpdir(), 'an-install', ...DRIVER_HOME));
  assert.ok(command.startsWith(`dotnet publish "${DRIVER_PROJECT}" -c Release -r win-x64 --self-contained false`), command);
  assert.ok(command.endsWith(` -o "${join(tmpdir(), 'an-install', ...DRIVER_HOME)}"`), command);
  assert.doesNotMatch(command, /PublishSingleFile/, 'a folder of its own, framework-dependent');
});

test('ONEWAY1ai-iv the install note says daoris runs the desktop’s program', () => {
  const note: string = installedNote();
  const row = note.split('\n').find((line) => line.startsWith(`| \`${SHELL_HOME[0]}/\``)) ?? '';
  assert.ok(row.includes(`\`${DRIVER_HOME.slice(1).join('/')}/\``), 'the program’s folder in the app row');
  const section = note.slice(note.indexOf('## The command')).split('\n## ')[0] ?? '';
  const prose = section.replace(/\s+/g, ' ');
  assert.ok(prose.includes(`desktop's program this install carries in \`${DRIVER_HOME.join('/')}/\`, which needs no Node`), 'daoris runs the program');
  assert.doesNotMatch(note, /daoris driver |`daoris-driver [a-z]/, 'the note teaches no old form');
});

test('laying the doctrine tool out retries a held rename at each package and launcher step', async () => {
  const fx = makeFixture('publish-cli-held');
  const tarball = packed(fx.root, 'daoris-0.4.2', releasePackage());
  const install = join(fx.root, 'install');
  const attempts = new Map<string, number>();

  const laid = await layCli(tarball, install, {
    tries: 2,
    waitMs: 0,
    rename: (from: string, to: string) => {
      const attempt = (attempts.get(to) ?? 0) + 1;
      attempts.set(to, attempt);
      if (attempt === 1) throw Object.assign(new Error('file held'), { code: 'EPERM' });
      // The second attempt is a real move of files just unpacked, which the scanner may really hold: it waits as the
      // shipped default does, so only the injected refusal is counted.
      renameHeld(from, to);
    },
  });

  assert.equal(attempts.size, 3, 'unpacked package, installed package and launchers each use the held-file retry');
  assert.ok([...attempts.values()].every((attempt) => attempt === 2));
  assert.deepEqual(laid, { version: '0.4.2' });
  assert.equal(readFileSync(join(install, ...CLI_PACKAGE, ...CLI_ENTRY), 'utf8'), ECHO_BIN);
  for (const [name, text] of Object.entries(cliLaunchers())) {
    assert.equal(readFileSync(join(install, ...CLI_BIN, name), 'utf8'), text);
  }
});

test('laying the doctrine tool out unpacks the package under app/cli, writes both launchers, and nothing into the home', async () => {
  const fx = makeFixture('publish-cli-lay');
  const tarball = packed(fx.root, 'daoris-0.4.2', releasePackage());
  const install = join(fx.root, 'install');
  mkdirSync(join(install, 'data'), { recursive: true });

  const laid = await layCli(tarball, install);

  assert.deepEqual(laid, { version: '0.4.2' });
  const pkg = join(install, ...CLI_PACKAGE);
  assert.equal(readFileSync(join(pkg, ...CLI_ENTRY), 'utf8'), ECHO_BIN);
  assert.ok(existsSync(join(pkg, 'dist', 'cli.js')));
  assert.equal(JSON.parse(readFileSync(join(pkg, 'canon', 'canon.json'), 'utf8')).version, '0.4.2');
  assert.deepEqual(readdirSync(join(install, ...CLI_HOME)), [CLI_PACKAGE[2]], 'app/cli holds node_modules and nothing else');
  assert.deepEqual(readdirSync(join(install, ...CLI_PACKAGE.slice(0, -1))), [CLI_PACKAGE.at(-1)], 'the package, as daoris');

  const launchers: Record<string, string> = cliLaunchers();
  assert.deepEqual(readdirSync(join(install, ...CLI_BIN)).sort(), [...CLI_LAUNCHERS].sort());
  for (const name of CLI_LAUNCHERS) {
    assert.equal(readFileSync(join(install, ...CLI_BIN, name), 'utf8'), launchers[name], name);
  }
  assert.deepEqual(readdirSync(join(install, SHELL_HOME[0])).sort(), [CLI_BIN.at(-1), CLI_HOME.at(-1)].sort(), 'nothing left staged beside them');
  assert.deepEqual(readdirSync(join(install, 'data')), [], 'nothing under the home');
  fx.cleanup();
});

test('a republish replaces the doctrine tool whole: a stale file in either folder goes', async () => {
  const fx = makeFixture('publish-cli-replace');
  const install = join(fx.root, 'install');
  mkdirSync(join(install, ...CLI_PACKAGE), { recursive: true });
  writeFileSync(join(install, ...CLI_PACKAGE, 'stale.js'), '// from the publish before\n');
  mkdirSync(join(install, ...CLI_BIN), { recursive: true });
  writeFileSync(join(install, ...CLI_BIN, 'daoris.ps1'), '# a launcher the publish before wrote\n');

  await layCli(packed(fx.root, 'daoris-0.4.3', releasePackage('0.4.3')), install);

  assert.equal(existsSync(join(install, ...CLI_PACKAGE, 'stale.js')), false);
  assert.deepEqual(readdirSync(join(install, ...CLI_BIN)).sort(), [...CLI_LAUNCHERS].sort());
  assert.equal(JSON.parse(readFileSync(join(install, ...CLI_PACKAGE, 'package.json'), 'utf8')).version, '0.4.3');
  fx.cleanup();
});

/**
 * Refused before anything is replaced, naming what is wrong: a package that is not the release's `daoris`
 * would put a tool on every session's path that is not the one the release publishes.
 */
test('a package that is not the release’s daoris stops the publish, naming why, and the last one stands', async () => {
  const fx = makeFixture('publish-cli-refuse');
  const install = join(fx.root, 'install');
  await layCli(packed(fx.root, 'daoris-0.4.2', releasePackage()), install);

  const cases: Array<[string, Record<string, string>, RegExp]> = [
    ['no canon', releasePackage('0.5.0', { 'package/canon/canon.json': null }), /canon\/canon\.json/],
    ['a canon at another version', releasePackage('0.5.0', { 'package/canon/canon.json': '{"version":"0.4.9"}' }), /0\.4\.9.*0\.5\.0|0\.5\.0.*0\.4\.9/],
    ['no built dispatcher', releasePackage('0.5.0', { 'package/dist/cli.js': null }), /dist\/cli\.js/],
    ['no bin entry', releasePackage('0.5.0', { 'package/bin/daoris.mjs': null }), /bin\/daoris\.mjs/],
    ['the source tree', releasePackage('0.5.0', { 'package/src/cli.ts': 'export {};\n' }), /source/],
    ['another package', releasePackage('0.5.0', { 'package/package.json': JSON.stringify({ name: 'not-daoris', version: '0.5.0' }) }), /not-daoris/],
    ['not packed by npm', { ...releasePackage('0.5.0'), 'elsewhere/x.txt': 'x\n' }, /package\//],
  ];
  for (const [name, files, says] of cases) {
    const tarball = packed(fx.root, name.replace(/\W+/g, '-'), files);
    await assert.rejects(layCli(tarball, install), (error: Error) => says.test(error.message), name);
    assert.equal(JSON.parse(readFileSync(join(install, ...CLI_PACKAGE, 'package.json'), 'utf8')).version, '0.4.2',
      `${name}: the last publish’s tool stands`);
    assert.deepEqual(readdirSync(join(install, ...CLI_BIN)).sort(), [...CLI_LAUNCHERS].sort(), `${name}: its launchers stand`);
    assert.deepEqual(readdirSync(join(install, SHELL_HOME[0])).sort(), [CLI_BIN.at(-1), CLI_HOME.at(-1)].sort(),
      `${name}: nothing left staged`);
  }
  fx.cleanup();
});

