import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdtempSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { delimiter, dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { gzipSync } from 'node:zlib';
// Untyped workspace tooling, suppressed at the one site — see desktop-tool.test.ts for why.
import {
  BUILD_MANIFEST, CLI_BIN, CLI_ENTRY, CLI_HOME, CLI_LAUNCHERS, CLI_PACKAGE, GATE_COMMAND, HOME, HOST_HOME, KEPT_LOCALES, LAUNCHER, MANIFEST_SCHEMA,
  MARKER, MARKER_HEADER, OFFERED_PLUGINS, OWN, PLUGIN_OFFERS, RESOURCES, RESOURCES_SOURCE, RETIRED_IN_APP, RETIRED_LAUNCHERS, SHELL_EXE,
  SHELL_FILES, SHELL_HOME, STAGE, STAGED, STAGED_REQUIRED, SWAP_JOURNAL, buildId, cliLaunchers, insideFixtures, installedNote, isInstall, layCli,
  layOffers, layResources, promoteStage, recordedShellFiles, refusal, retiredPaths, stageRefusal, stagedManifest, ungatedRefusal, unstage,
  writeManifest,
  // @ts-expect-error — untyped workspace tooling; see above
} from '../../../tools/desktop-publish.mjs';
// @ts-expect-error — untyped workspace tooling; see above
import { renameHeld } from '../../../tools/fsx.mjs';
import { resolveCanonRoot } from '../src/canon.ts';
import { OFFERS_DIR, readManifest } from '../src/plugins.ts';
import { BUILT_IN, BUILT_IN_LAYOUT, parseResources } from '../src/resources.ts';
import { INSTALL_BIN, installBin } from '../src/tools.ts';
import { makeFixture } from './_fixture.ts';
import { TAR_END, tarEntry } from './_tar.ts';

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

test('five names make an install, the marker and the update’s folder among them', () => {
  assert.deepEqual([...OWN].sort(), ['Daoris.exe', 'INSTALLED.md', 'app', 'data', 'update']);
  assert.ok(OWN.includes(MARKER));
  assert.ok(OWN.includes(STAGE[0]));
});

/**
 * UPDATE1 (D139 §1, §4): the publish's `--stage` writes the build where the application and the launcher look for it,
 * with the manifest their one check reads (`StagedBuild.cs`, which the launcher compiles). Two languages, one layout:
 * this reads the C# for its spellings and its required files.
 */
test('the staged build is laid out where StagedBuild looks, with the files it requires', () => {
  const source = readFileSync(join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'StagedBuild.cs'), 'utf8');
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
 * GATE3: a merge runs only the gates its lanes reach, so the full set runs before the install is built. A
 * "web only" batch once skipped the .NET suites and broke main unseen; the lane table could be wrong the
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

  // GATE6b: when a smaller command would pass it, the tool names that one, and so does the refusal, in place of the full set.
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
    const result = spawnSync('git', args, { cwd: repo, encoding: 'utf8', env });
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

  assert.match(gated.refusal!, /run every gate on this checkout: node tools\/merge-branch\.mjs --full/);

  record(['first']);
  gated = ungatedRefusal(to, repo);
  assert.match(gated.refusal!, /second\s+none/);
  assert.match(gated.refusal!, /first\s+PASS/);
  // GATE6b: one gate lacking is one gate to run, which the refusal names in place of the full set.
  assert.match(gated.refusal!, /run the gate it lacks on this checkout: node tools\/merge-branch\.mjs --rerun second\n/);
  assert.doesNotMatch(gated.refusal!, /--full/);

  record(['first', 'second']);
  gated = ungatedRefusal(to, repo);
  assert.equal(gated.refusal, null, gated.refusal ?? '');
  assert.match(gated.note!, /the full set has passed/);
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
 * names no machine path (`sensitive-info`): it is written into the install, never tracked. LEFT1 adds
 * how to pin Daoris (D108 §2): from its running window, because the window names Daoris's taskbar id
 * and a pin made from it starts the launcher; a pin made on the launcher in Explorer carries no id, so
 * the running window shows as a second button beside it.
 */
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
 * WSSETUP2 (D124 §1.2): the note says the install carries its doctrine tool, where, how each shell runs it,
 * on which Node, and that nothing changed the account's PATH (D124 §10 refused that), so whoever opens
 * the folder is not left wondering why a terminal of theirs still runs another `daoris`.
 */
test('the install note says where the doctrine tool is, how each shell runs it, and that your PATH is untouched', () => {
  const note: string = installedNote();
  assert.ok(note.includes('## The doctrine tool'), 'the note has no section for the doctrine tool');
  const section = note.slice(note.indexOf('## The doctrine tool')).split('\n## ')[0] ?? '';
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
 * WSSETUP3 (D124 §1.3): every child Daoris starts is handed a PATH that begins with the install's `app/bin/`, found
 * beside the home (`Tools.InstallBin`, the CLI's `installBin`), while the account's PATH stays the person's. The note
 * says both, in one section, and names the condition the code tests: the home, `data/`, sits beside `app/`.
 */
test('the install note says Daoris’s own children find the doctrine tool first on their PATH, and your account’s PATH is untouched', () => {
  const note: string = installedNote();
  const section = note.slice(note.indexOf('## The doctrine tool')).split('\n## ')[0] ?? '';
  const prose = section.replace(/\s+/g, ' ');
  assert.match(prose, /Every program Daoris starts finds it by its bare name/);
  for (const child of ['a driven session', 'a conversation', 'an intake', 'a hook', 'a landing plugin', 'every shell of its terminal panel']) {
    assert.ok(prose.includes(child), `names ${child}`);
  }
  assert.ok(prose.includes(`begin their PATH with \`${CLI_BIN.join('/')}/\``), 'says app/bin/ comes first');
  assert.ok(prose.includes(`this install's home, \`${HOME}/\`, sits beside \`${CLI_BIN[0]}/\``), 'names why: the home beside app/');
  assert.match(prose, /Nothing puts `app\/bin\/` on your account's PATH, so a terminal of yours outside Daoris still runs whatever `daoris` you installed/);
  // What the note says is what the code finds: the install's home finds its own launchers, a home elsewhere none.
  const install = folder();
  mkdirSync(join(install, HOME));
  mkdirSync(join(install, ...CLI_BIN), { recursive: true });
  assert.equal(installBin(join(install, HOME)), join(install, ...CLI_BIN));
  assert.equal(installBin(join(folder(), HOME)), null);
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
 * The launcher and the publish are twins (D93): the publish lays the application out where the
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
 * D93's rule, for what an earlier publish wrote and this one no longer does: a republish into its own
 * install removes exactly those names — the single-file shell's launcher at the root, and since CHR8
 * (D99) the browser's own folder under `app/`, with its executable and its second engine — and a folder
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
 * The install's languages are the kit's to lay out since SHEN1, and there is one engine to lay them out
 * for since CHR8: the engine's fallback, `en-US`, and what the app project names
 * (`ShenoraChromiumLocales`). The publish keeps `KEPT_LOCALES` of them and the deployment gate reads
 * them back off the install, so the list and the project must agree.
 */
test('the locales an install keeps are the kit’s fallback and the ones the app project names', () => {
  const app = readFileSync(join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.App', 'Daoris.Desktop.App.csproj'), 'utf8');
  const named = (/<ShenoraChromiumLocales>([^<]*)</.exec(app)?.[1] ?? '').split(';').map((name) => name.trim()).filter(Boolean);
  assert.deepEqual([...KEPT_LOCALES].sort(), ['en-US', ...named].map((name) => `${name}.pak`).sort());
});

/**
 * PLUG9 (d), D103: the install carries Daoris's own example plugins as OFFERS, in `app/plugin-offers/`,
 * never under `data/plugins/`, so none is installed until a press. The ones meant for people: the two that
 * land work, and the one that hands a session the install's own browser. Not the rehearsal fixture, and not
 * the browser a machine without the shell launches, which claims the same server name.
 */
test('the offers are the examples meant for people, never a rehearsal fixture', () => {
  assert.deepEqual([...OFFERED_PLUGINS], ['github-pull-request', 'azure-devops-pull-request', 'in-app-browser']);
  assert.ok(!OFFERED_PLUGINS.includes('hold-by-title'), 'the family and deployment rehearsals install hold-by-title');
  assert.ok(!OFFERED_PLUGINS.includes('browser'), 'a second `browser` server would contribute nothing beside in-app-browser');
  const examples = join(here, '..', '..', '..', 'examples', 'plugins');
  for (const id of OFFERED_PLUGINS) {
    assert.equal(readManifest(id, join(examples, id), true).problem, null, id);
  }
});

/**
 * Where the offers are is a twin (D103): the publish lays them out, the CLI finds them beside the home, and
 * the driver's `PluginOffers.Layout` beside the application or the home. Three spellings, held here.
 */
test('the offers folder is where the CLI and the driver look for it', () => {
  assert.deepEqual([...PLUGIN_OFFERS], ['app', 'plugin-offers']);
  assert.deepEqual([...PLUGIN_OFFERS], [...OFFERS_DIR]);
  assert.equal(PLUGIN_OFFERS[0], SHELL_HOME[0], 'beside the application, in app/');
  const driver = readFileSync(join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'PluginOffers.cs'), 'utf8');
  assert.ok(driver.includes(`Layout = [${PLUGIN_OFFERS.map((part: string) => `"${part}"`).join(', ')}]`), 'PluginOffers.Layout');
});

test('laying out the offers copies each one whole into app/plugin-offers, and nothing into the home', () => {
  const examples = folder();
  for (const id of ['github-pull-request', 'azure-devops-pull-request', 'in-app-browser', 'hold-by-title']) {
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
  assert.equal(readFileSync(join(offers, 'github-pull-request', 'lib', 'part.mjs'), 'utf8'), '// github-pull-request\n');
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
  mkdirSync(join(offers, 'github-pull-request'), { recursive: true });
  writeFileSync(join(offers, 'github-pull-request', 'stale.mjs'), '// from the publish before\n');
  mkdirSync(join(offers, 'retired-offer'), { recursive: true });

  layOffers(examples, install);

  assert.equal(existsSync(join(offers, 'github-pull-request', 'stale.mjs')), false);
  assert.equal(existsSync(join(offers, 'retired-offer')), false);
  assert.match(readFileSync(join(offers, 'github-pull-request', 'plugin.json'), 'utf8'), /1\.1\.0/);
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
  mkdirSync(join(examples, 'github-pull-request'), { recursive: true });
  writeFileSync(join(examples, 'github-pull-request', 'plugin.json'), '{ "id": "github-pull-request" }\n');
  const install = folder();
  const offers = join(install, ...PLUGIN_OFFERS);
  mkdirSync(join(offers, 'in-app-browser'), { recursive: true });

  assert.throws(() => layOffers(examples, install), /azure-devops-pull-request/);
  assert.ok(existsSync(join(offers, 'in-app-browser')), 'the last publish’s offers are untouched');
});

/**
 * TOOLS3, D121 §3.1: the install carries the list built in at `app/resources.json`, beside the application,
 * where the driver reads it from its own folder; the CLI finds it beside the home, which in an install is the
 * same folder. Three spellings, held here, and the source the publish copies is the driver's own.
 */
const workspace = join(here, '..', '..', '..');

test('the list built in is where the CLI and the driver look for it', () => {
  assert.deepEqual([...RESOURCES], ['app', 'resources.json']);
  assert.deepEqual([...RESOURCES], [...BUILT_IN_LAYOUT]);
  assert.equal(RESOURCES[0], SHELL_HOME[0], 'beside the application, in app/');
  const driver = readFileSync(join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'ToolResources.cs'), 'utf8');
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
  const list = parseResources(readFileSync(laid, 'utf8'), BUILT_IN);
  assert.equal(list.problem, null);
  assert.deepEqual(list.notes, []);
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
// WSSETUP2 (D124 §1.2): the install carries its doctrine tool. The publish packs `src/Daoris.Cli` as the
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

/** A bin entry that says what it was handed, and exits with the code `--exit` names. */
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
 * WSSETUP3 (D124 §1.3): the launchers' folder is where every child's PATH begins. The publish lays it out, and
 * the CLI's `INSTALL_BIN` and the driver's `Tools.InstallBinLayout` find it beside the home, which in an install is
 * the same `app/`. Three spellings, held here.
 */
test('the launchers are where the tools’ environment puts them first on every child’s PATH', () => {
  assert.deepEqual([...CLI_BIN], [...INSTALL_BIN]);
  const driver = readFileSync(join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Tools.Children.cs'), 'utf8');
  assert.ok(driver.includes(`InstallBinLayout = [${CLI_BIN.map((part: string) => `"${part}"`).join(', ')}]`), 'Tools.InstallBinLayout');

  const install = folder();
  mkdirSync(join(install, 'data'));
  mkdirSync(join(install, ...CLI_BIN), { recursive: true });
  assert.equal(installBin(join(install, 'data')), join(install, ...CLI_BIN), 'an install’s home finds its own launchers');
});

test('a launcher for each shell runs the package’s bin entry on the node PATH finds, and names no machine path', () => {
  const launchers: Record<string, string> = cliLaunchers();
  assert.deepEqual(Object.keys(launchers).sort(), [...CLI_LAUNCHERS].sort());
  assert.deepEqual([...CLI_LAUNCHERS].sort(), ['daoris', 'daoris.cmd']);
  const entry = [...CLI_PACKAGE, ...CLI_ENTRY].join('/');

  // Git Bash: a shell script, LF, finding its own folder from how it was called.
  const sh = launchers['daoris'] ?? '';
  assert.ok(sh.startsWith('#!/bin/sh\n'), 'Git Bash runs a file by its shebang');
  assert.ok(!sh.includes('\r'), 'a carriage return ends the shebang line in the wrong place');
  const shPath = /exec node "\$basedir\/([^"]+)" "\$@"/.exec(sh)?.[1] ?? '';
  assert.equal(posix.normalize(posix.join(CLI_BIN.join('/'), shPath)), entry, `the script runs ${shPath}`);

  // Command Prompt, and PowerShell by PATHEXT: a batch file, CRLF as cmd.exe reads one, every argument passed on.
  const cmd = launchers['daoris.cmd'] ?? '';
  assert.ok(cmd.split('\r\n').slice(0, -1).every((line) => !line.includes('\n')), 'every line ends CRLF');
  assert.ok(cmd.endsWith('\r\n'));
  const cmdPath = /node "%~dp0([^"]+)" %\*/.exec(cmd)?.[1] ?? '';
  assert.equal(posix.normalize(posix.join(CLI_BIN.join('/'), cmdPath.replace(/\\/g, '/'))), entry, `the batch file runs ${cmdPath}`);

  for (const [name, text] of Object.entries(launchers)) {
    // cmd.exe reads a batch file in the console's code page, so a non-ASCII byte prints as something else.
    assert.ok(/^[\x00-\x7F]*$/.test(text), `${name} is ASCII`);
    assert.doesNotMatch(text, /[A-Za-z]:[\\/]|\/home\/|\/Users\//, `a machine path in ${name}`);
    // Bare `node`: the one the child's PATH finds, which is the one Tools resolves (D124 §1.3).
    assert.match(text, /no node on PATH/, `${name} says so when there is no node`);
  }
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
      // shipped default does, so only the injected refusal is counted (FLAKE1, 2026-10-07).
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
 * would put a tool on every session's path that is not the one the release publishes (D124 §1.2).
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

/** The PATH a child gets, with `folder` first, under whichever spelling of the name this environment uses. */
function pathFirst(folder: string): NodeJS.ProcessEnv {
  const env = { ...process.env };
  const key = Object.keys(env).find((name) => name.toUpperCase() === 'PATH') ?? 'PATH';
  env[key] = `${folder}${delimiter}${env[key] ?? ''}`;
  return env;
}

/**
 * The launcher this platform's shell finds by the bare name, run for real: from another folder, an
 * argument with a space in it handed on whole, and the tool's exit code handed back, which is the
 * contract a gate reads (0 clean, 1 policy, 2 tool error). Command Prompt on Windows, `sh` elsewhere;
 * Git Bash and PowerShell are the deployment rehearsal's to measure, on the published install.
 */
test('the launcher this platform’s shell finds runs the package from any folder, passing arguments and the exit code through', async () => {
  const fx = makeFixture('publish-cli-run');
  const install = join(fx.root, 'install');
  await layCli(packed(fx.root, 'daoris-0.4.2', releasePackage()), install);
  const elsewhere = join(fx.root, 'a-repository');
  mkdirSync(elsewhere, { recursive: true });
  const env = pathFirst(join(install, ...CLI_BIN));

  const ran = process.platform === 'win32'
    ? spawnSync(process.env.ComSpec ?? 'cmd.exe', ['/d /s /c "daoris "two words" --exit 3"'],
      { cwd: elsewhere, env, encoding: 'utf8', windowsVerbatimArguments: true })
    : spawnSync('sh', ['-c', 'daoris "two words" --exit 3'], { cwd: elsewhere, env, encoding: 'utf8' });

  assert.equal(ran.stdout.trim(), JSON.stringify(['two words', '--exit', '3']), ran.stderr);
  assert.equal(ran.status, 3, 'the exit code is the tool’s');
  fx.cleanup();
});
