import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import { readText } from '../src/fsx.ts';
import { makeFixture } from './_fixture.ts';
import {
  CLOSED_NOTE, SWEPT_NOTE, bridgeCall, cliProblems, concludedByTheClose, firstUnder, gitBashBeside, hookLines, insideWorkspace,
  invokeInPage, isMarkedProcess, launchers, loggedData, markedProcess, offerProblems, stageLiveBuild, strays, swapOutcome,
  transcriptHolds, utf8Of,
  // @ts-expect-error — untyped workspace tooling; the same seam desktop-tool.test.ts documents
} from '../../../tools/deployment-rehearsal.mjs';
// The install's layout, from the script that makes it: the gate reads the same constants.
import {
  CLI_BIN, CLI_ENTRY, CLI_LAUNCHERS, CLI_PACKAGE, HOST_EXE, HOST_HOME, OFFERED_PLUGINS, PLUGIN_OFFERS,
  // @ts-expect-error — untyped workspace tooling; the same seam desktop-tool.test.ts documents
} from '../../../tools/desktop-publish.mjs';
// The protocol stub both rehearsals run (DEPLOY5): one copy, where the family rehearsal's used to be.
// @ts-expect-error — untyped workspace tooling; the same seam desktop-tool.test.ts documents
import { ACP_STUB_AGENT } from '../../../tools/rehearsal-kit.mjs';

/**
 * The deployment gate (`tools/deployment-rehearsal.mjs`) is workspace tooling, tested from here for
 * the reason `desktop-tool.test.ts` states: this suite is what `npm run verify` and the release
 * workflow already run, and a seventh gate is a seventh row two lists would have to agree on.
 *
 * What is asserted here is the part of that gate which can be wrong while the gate stays green. Its
 * phases run a real publish and a real window and are their own evidence; its PREDICATES are not —
 * a transcript check that compared decoded strings would pass on exactly the bytes DEPLOY2 exists to
 * catch, and nothing downstream could tell.
 */

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = dirname(dirname(dirname(here)));

/**
 * PLUG9 (d), D103: the gate's check on the install's offers, watched failing both ways — an offer the
 * install does not carry, and one installed under a home, which a publish must never do.
 */
test('the offers check names an offer not carried and one installed, and passes on a sound install', () => {
  const fx = makeFixture('deploy-offers');
  const install = join(fx.root, 'install');
  const home = join(fx.root, 'home');
  for (const id of OFFERED_PLUGINS) fx.write(`install/${PLUGIN_OFFERS.join('/')}/${id}/plugin.json`, `{ "id": "${id}" }`);

  assert.deepEqual(offerProblems(install, [home]), []);

  fx.write(`home/plugins/${OFFERED_PLUGINS[0]}/plugin.json`, '{}');
  assert.deepEqual(offerProblems(install, [home]), [`${OFFERED_PLUGINS[0]} is installed under ${home}`]);

  const bare = join(fx.root, 'bare');
  assert.equal(offerProblems(bare, []).length, OFFERED_PLUGINS.length);
  assert.match(offerProblems(bare, [])[0], /plugin-offers\/github-pull-request is not carried/);
  fx.cleanup();
});

/**
 * 🔴 The check that must be watched failing, because it is the whole of defect 4c.
 *
 * A transcript mangled by the console codepage is still VALID UTF-8 — that is what made it invisible
 * — so a check that decodes before comparing sees a string with the wrong characters in it and a
 * check that compares bytes sees the wrong bytes. Only the second can tell.
 *
 * The mangling is reproduced by its shape rather than by its codepage: decode UTF-8 bytes as a
 * single-byte page, re-encode as UTF-8. That is what .NET did with CP936 on the machine this was
 * found on, and what it does with any ANSI codepage that is not 65001.
 */
test('a transcript check reads bytes, and mojibake is bytes it does not hold', () => {
  const line = 'stub: 道衍 — the unfolding of the way';
  assert.equal(transcriptHolds(utf8Of(`before\n${line}\nafter\n`), line), true);

  const mangled = Buffer.from(utf8Of(line).toString('latin1'), 'utf8');
  assert.equal(transcriptHolds(mangled, line), false, 'mojibake must not read as the line');

  // …and the trap in one line: the mangled bytes ARE valid UTF-8, so a decoded comparison of the
  // decoded text against itself succeeds while the transcript is wrong.
  assert.equal(mangled.toString('utf8').length > 0, true);
  assert.notEqual(mangled.toString('utf8'), line);
});

test('the expected bytes are computed from the line, never spelled out', () => {
  // A literal byte sequence in the gate would be a second description of the same string, and the
  // way that fails is silent: someone edits the line and the check keeps asserting the old one.
  assert.deepEqual([...utf8Of('—')], [0xe2, 0x80, 0x94]);
  assert.deepEqual([...utf8Of('道')], [0xe9, 0x81, 0x93]);
});

/**
 * The publish makes two claims about what a person sees when they open the folder — one launcher,
 * and no build leftovers — and until this gate nothing read them back. Both are real regressions:
 * the first version of that script published 24 entries with the executable buried among them, and
 * the `.xml` doc files it also dropped are what made its own guard refuse its second run.
 */
test('a launcher is what a person could double-click, and nothing else at the root is one', () => {
  assert.deepEqual(
    launchers(['Daoris.exe', 'INSTALLED.md', 'app', 'data', 'WebView2Loader.dll']),
    ['Daoris.exe']);
  // Two launchers is the failure being guarded, not a pass with a favourite.
  assert.deepEqual(launchers(['a.exe', 'b.cmd', 'c.bat', 'd.txt']), ['a.exe', 'b.cmd', 'c.bat']);
});

test('symbols and package doc files are strays, and the marker and the bundle are not', () => {
  assert.deepEqual(
    strays(['Daoris.exe', 'Daoris.App.pdb', 'WebView2Loader.xml', 'INSTALLED.md']),
    ['Daoris.App.pdb', 'WebView2Loader.xml']);
  assert.deepEqual(strays(['Daoris.exe', 'INSTALLED.md']), []);
});

/**
 * WSSETUP2 (D124 §1.2): the gate reads the install's doctrine tool back off the published folder, and each
 * thing the publish claims is a sentence when it is not so: a launcher missing, the package without its
 * entry, its dispatcher or its canon, the source tree where the packed artefact belongs, or a version that
 * is not the canon's.
 */
test('the doctrine tool check names what an install lacks, and passes on a sound one', () => {
  const fx = makeFixture('deploy-cli');
  const pkg = `install/${CLI_PACKAGE.join('/')}`;
  const sound = () => {
    for (const name of CLI_LAUNCHERS) fx.write(`install/${CLI_BIN.join('/')}/${name}`, 'launcher\n');
    fx.write(`${pkg}/${CLI_ENTRY.join('/')}`, '// entry\n');
    fx.write(`${pkg}/dist/cli.js`, '// built\n');
    fx.write(`${pkg}/package.json`, '{"name":"daoris","version":"0.4.2"}');
    fx.write(`${pkg}/canon/canon.json`, '{"version":"0.4.2"}');
  };
  const install = join(fx.root, 'install');
  sound();
  assert.deepEqual(cliProblems(install, '0.4.2'), []);

  assert.deepEqual(cliProblems(install, '0.4.3'), [
    `${CLI_PACKAGE.join('/')}/package.json says 0.4.2, not 0.4.3`,
    `${CLI_PACKAGE.join('/')}/canon/canon.json says 0.4.2, not 0.4.3`,
  ]);

  fx.write(`${pkg}/src/cli.ts`, 'export {};\n');
  assert.match(cliProblems(install, '0.4.2').join('\n'), /src\/ is there: the source tree/);

  const bare = join(fx.root, 'bare');
  const problems: string[] = cliProblems(bare, '0.4.2');
  for (const name of CLI_LAUNCHERS) assert.ok(problems.includes(`${CLI_BIN.join('/')}/${name} is not there`), name);
  assert.ok(problems.includes(`${CLI_PACKAGE.join('/')}/dist/cli.js is not there`));
  assert.ok(problems.includes(`${CLI_PACKAGE.join('/')}/canon/canon.json is not there`));
  fx.cleanup();
});

/**
 * Which `daoris` a shell found is the first line its lookup prints (`where`, `Get-Command`, `command -v` through
 * `cygpath -w`), and it is the install's only when that file sits in the install's `app/bin/` itself:
 * compared case-blind and either separator, as Windows answers a path, and never as a prefix, so a folder
 * beside it whose name starts the same is not it.
 */
test('the daoris a shell found is the install’s only when the first one it lists is in app/bin itself', () => {
  const bin = join('C:', 'work', 'install', ...CLI_BIN);
  assert.equal(firstUnder(`${join(bin, 'daoris')}\r\n${join(bin, 'daoris.cmd')}\r\n`, bin), true);
  assert.equal(firstUnder(`${join(bin, 'daoris.cmd').toUpperCase()}\n`, bin), true);
  assert.equal(firstUnder(`${join(bin, 'daoris').replace(/\\/g, '/')}\n`, bin), true);
  // Another daoris listed first is the one the shell runs, whatever comes after it.
  assert.equal(firstUnder(`${join('C:', 'npm', 'daoris.cmd')}\n${join(bin, 'daoris.cmd')}\n`, bin), false);
  assert.equal(firstUnder(`${join(`${bin}-old`, 'daoris.cmd')}\n`, bin), false);
  assert.equal(firstUnder(`${join(bin, 'nested', 'daoris.cmd')}\n`, bin), false);
  assert.equal(firstUnder('', bin), false);
  assert.equal(firstUnder('INFO: Could not find files for the given pattern(s).\n', bin), false);
});

/**
 * Git Bash is the `bash.exe` beside git, never whatever `bash` PATH finds first, which on Windows is usually
 * WSL's: the rule the terminal's shell list keeps (`TerminalShells`, D96), read here so the gate measures the
 * shell a terminal and a session would open. Three folders up from git covers `cmd\`, `bin\` and `mingw64\bin\`.
 */
test('Git Bash is the bash beside git, up to three folders up, and none without git', () => {
  const root = join('C:', 'Program Files', 'Git');
  const bash = join(root, 'bin', 'bash.exe');
  const has = (path: string) => path === bash;
  assert.equal(gitBashBeside(join(root, 'cmd', 'git.exe'), has), bash);
  assert.equal(gitBashBeside(join(root, 'mingw64', 'bin', 'git.exe'), has), bash);
  assert.equal(gitBashBeside(join(root, 'bin', 'git.exe'), has), bash);
  assert.equal(gitBashBeside(join(root, 'a', 'b', 'c', 'git.exe'), has), null, 'further up is another program’s folder');
  assert.equal(gitBashBeside(null, has), null);
  assert.equal(gitBashBeside(join('C:', 'Windows', 'System32', 'git.exe'), () => false), null);

  const terminals = readText(join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Terminals.cs'));
  assert.match(terminals, /level < 3/, 'the terminal’s rule walks three folders up, as this does');
  assert.match(terminals, /Path\.Combine\(folder, "bin", "bash\.exe"\)/, 'and looks for bin\\bash.exe, as this does');
});

/**
 * The masking agent, named.
 *
 * `ServiceHostLocator` walks up from the running binary to a workspace manifest and falls through to
 * the HTTP host's own build — a candidate that exists on every machine this had ever run on, which
 * is exactly why 2a was invisible until a deployed machine had no workspace beneath it. A deployed
 * shell that ends up on that candidate has proven nothing about deployment, so the gate has to be
 * able to say which side of the line the host it found is on.
 */
test('a host under the workspace is named as the workspace’s, whatever the separators', () => {
  assert.equal(insideWorkspace(join(repoRoot, 'src', 'x', 'bin', HOST_EXE), repoRoot), true);
  assert.equal(insideWorkspace(`${repoRoot}/src/x/bin/${HOST_EXE}`, repoRoot), true);
  assert.equal(insideWorkspace(join('C:', 'install', 'app', HOST_EXE), repoRoot), false);
  // A sibling whose path merely STARTS with the workspace's is not inside it — the classic prefix
  // bug, and it would make the gate call a deployed host a workspace one and go red for nothing.
  assert.equal(insideWorkspace(`${repoRoot}-scratch/app/${HOST_EXE}`, repoRoot), false);
  // The gate's own scratch install lives under `_fixtures/`, which IS under the workspace — so the
  // question is about the host's project build, not about the folder the gate happens to use.
  assert.equal(
    insideWorkspace(join(repoRoot, '_fixtures', 'x', 'app', 'daoris-knowledge-http', HOST_EXE), repoRoot),
    false);
});

/**
 * The install layout and the locator's candidate list are a counterpart set, and nothing compared
 * them — which is defect 2a whole: the publish script printed the nested path on success and the
 * locator built only the flat one, so the two halves disagreed IN WRITING.
 *
 * This is the comparison, and it reads both sides rather than restating either.
 */
test('the host’s home in an install is a path the locator actually looks in', () => {
  const locator = readText(join(
    repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'ServiceHostLocator.cs'));
  assert.ok(
    locator.includes(`Path.Combine("${HOST_HOME[0]}", "${HOST_HOME[1]}")`),
    `the publish puts the host in ${HOST_HOME.join('/')} and ServiceHostLocator does not look there`);
});

/**
 * Phase 6 counts the example plugin's hook processes before and after the shell closes. Matched on
 * the script's name alone, it counted the same plugin running ANYWHERE on the machine (REV3) — another
 * rehearsal's, or the owner's installed Daoris — and called those this shell's orphans.
 */
test('a hook process is this install’s only when it was started from under its scratch', () => {
  const scratch = join('C:', 'work', 'daoris', '_fixtures', 'deployment-rehearsal');
  const ours = join(scratch, 'home', 'plugins', 'hold-by-title', 'hooks.mjs');
  const theirs = join('C:', 'Daoris', 'data', 'plugins', 'hold-by-title', 'hooks.mjs');
  const rows = [
    `101|"node.exe" "${ours}"`,
    `202|"node.exe" "${theirs}"`,
    `303|"node.exe" "${join(scratch, 'home', 'plugins', 'other', 'hooks.mjs')}"`,
    `404|"node.exe" "${ours.toUpperCase()}"`,
    '',
  ].join('\r\n');
  assert.deepEqual(hookLines(rows, scratch), [101, 404]);
});

// ---------------------------------------------------------------------------------------------
// DEPLOY5: a conversation open when the installed shell closes. A chat starts over the bridge, which
// is the page's alone, so the gate calls it from inside the page over the debug port; and the record
// it reads afterwards has two `stopped` notes it could hold, only one of which is the close's.

type Envelope = { id: string; module: string; type: string; payload: unknown };

/**
 * A page as the kit's Chromium transport leaves it: a marker naming the route the page posts to, and
 * a `receive` the page's own bridge set. `reply` decides what the shell pushes back for each post.
 */
function fakePage(reply: (request: Envelope, push: (message: unknown) => void) => void, { ready = true } = {}) {
  const posted: Envelope[] = [];
  const heard: string[] = [];
  const pageReceive = (message: string) => { heard.push(message); };
  const marker: { ipc: string; receive?: (message: string) => void } = { ipc: '/__shenora/ipc' };
  if (ready) marker.receive = pageReceive;
  const window = {
    __shenora_chromium: marker,
    fetch: async (url: string, init: { method: string; body: string }) => {
      assert.equal(url, marker.ipc);
      assert.equal(init.method, 'POST');
      const request = JSON.parse(init.body) as Envelope;
      posted.push(request);
      // Pushed later, as the shell pushes: by calling whatever `receive` the marker holds then.
      setTimeout(() => reply(request, (message) => marker.receive?.(JSON.stringify(message))), 0);
      return { ok: true };
    },
  };
  return { window, marker, posted, heard, pageReceive };
}

test('a call made inside the page answers its own reply, and every push still reaches the page', async () => {
  const page = fakePage((request, push) => {
    push({ category: 'notification', payload: [{ module: 'DAORIS', type: 'SESSION_ENDED' }] });
    push({ category: 'ipc', id: 'somebody-else', success: true, data: 'not ours' });
    push({ category: 'ipc', id: request.id, success: true, data: { sessionId: 'c0ffee', message: 'opened' } });
  });

  const answer = await invokeInPage(page.window, 'DAORIS.DRIVER', 'START_CHAT', { repository: 'newcomer', adapter: 'acp-stub' });

  assert.deepEqual(answer, { ok: true, data: { sessionId: 'c0ffee', message: 'opened' } });
  assert.equal(page.posted.length, 1);
  const [posted] = page.posted;
  assert.equal(posted?.module, 'DAORIS.DRIVER');
  assert.equal(posted?.type, 'START_CHAT');
  assert.deepEqual(posted?.payload, { repository: 'newcomer', adapter: 'acp-stub' });
  assert.equal(typeof posted?.id, 'string');
  // It listened BESIDE the page's bridge, never instead of it: all three pushes reached the page…
  assert.equal(page.heard.length, 3);
  // …and `receive` is the page's own again once the answer has come.
  assert.equal(page.marker.receive, page.pageReceive);
});

test('a refusal from the shell is an answer the check prints, not a throw', async () => {
  const page = fakePage((request, push) => push({
    category: 'ipc', id: request.id, success: false, error: { code: 'DRIVER_REFUSED', message: 'no checkout' },
  }));

  const answer = await invokeInPage(page.window, 'DAORIS.DRIVER', 'START_CHAT', { repository: 'nobody' });

  assert.deepEqual(answer, { ok: false, error: { code: 'DRIVER_REFUSED', message: 'no checkout' } });
  assert.equal(page.marker.receive, page.pageReceive);
});

test('a page whose bridge is not up yet is told so, and nothing is posted', async () => {
  // Posted then, the reply would be pushed to a `receive` the page's bridge replaces as it starts.
  const page = fakePage(() => assert.fail('nothing should have been posted'), { ready: false });

  const answer = await invokeInPage(page.window, 'DAORIS.DRIVER', 'START_CHAT', {});

  assert.equal(answer.ok, false);
  assert.equal(answer.error.code, 'NOT_READY');
  assert.equal(page.posted.length, 0);
});

test('a call nobody answers ends at its bound, and gives the page its receive back', async () => {
  const page = fakePage(() => {});

  const answer = await invokeInPage(page.window, 'DAORIS.DRIVER', 'START_CHAT', {}, 20);

  assert.equal(answer.ok, false);
  assert.equal(answer.error.code, 'TIMEOUT');
  assert.match(answer.error.message, /DAORIS\.DRIVER\.START_CHAT/);
  assert.equal(page.marker.receive, page.pageReceive);
});

test('the expression sent over the debug port closes over nothing of this module', async () => {
  // Evaluated in a realm of its own, as the page is: every name it reaches is a parameter or a global.
  const page = fakePage((request, push) => push({ category: 'ipc', id: request.id, success: true, data: { sessionId: 'b0a7' } }));
  const context = vm.createContext({ window: page.window, setTimeout, clearTimeout });

  const answer = await vm.runInContext(
    bridgeCall('DAORIS.DRIVER', 'START_CHAT', { repository: 'newcomer', adapter: 'acp-stub' }, 5000), context);

  // Across realms, so compared as JSON: the object came from the page's realm, not this one.
  assert.deepEqual(JSON.parse(JSON.stringify(answer)), { ok: true, data: { sessionId: 'b0a7' } });
  assert.deepEqual(page.posted[0]?.payload, { repository: 'newcomer', adapter: 'acp-stub' });
});

/**
 * 🔴 Two notes, one state. A conversation the close ended and one the next start's sweep found are
 * both `stopped`, so the state alone would pass on exactly the defect the FIX-LOG entry of 2026-09-25
 * found on the window: a chat that came back ended by the SWEEP's note, because the close wrote
 * nothing. Only the note tells them apart.
 */
test('a record is concluded by the close only when it carries the close’s note', () => {
  assert.equal(concludedByTheClose({ state: 'stopped', note: CLOSED_NOTE }), true);
  assert.equal(concludedByTheClose({ state: 'stopped', note: SWEPT_NOTE }), false, 'the sweep’s note is the defect');
  assert.equal(concludedByTheClose({ state: 'working', note: null }), false, 'left working is the defect too');
  assert.equal(concludedByTheClose({ state: 'stopped', note: 'the person ended the conversation.' }), false);
  assert.equal(concludedByTheClose({ state: 'completed', note: CLOSED_NOTE }), false);
  assert.equal(concludedByTheClose(null), false);
  assert.equal(concludedByTheClose(undefined), false);
});

/**
 * UPDATE1 (D139 §5, §6): the gate reads how the launcher's swap ended from its journal, and only a phase it knows; a journal
 * that is missing or torn is no outcome, so the check waits rather than passing on it.
 */
test('the swap’s journal is read for its phase, reason and confirmation, and nothing else counts as one', () => {
  assert.deepEqual(swapOutcome(JSON.stringify({ phase: 'installed', id: 'b1', confirmed: true })),
    { phase: 'installed', id: 'b1', reason: null, confirmed: true });
  assert.deepEqual(swapOutcome(JSON.stringify({ phase: 'rolled-back', id: 'b2', reason: 'exited' })),
    { phase: 'rolled-back', id: 'b2', reason: 'exited', confirmed: null });
  assert.equal(swapOutcome(''), null);
  assert.equal(swapOutcome('{ torn'), null);
  assert.equal(swapOutcome(JSON.stringify({ phase: 'finished' })), null);
  assert.equal(swapOutcome(JSON.stringify({ id: 'b1' })), null);
});

test('the machine log is read by event, for each line’s data, skipping what does not read', () => {
  const lines = [
    JSON.stringify({ event: 'update.applying', data: { by: 'idle', build: 'b1' } }),
    '{ torn',
    JSON.stringify({ event: 'update.installed', data: { build: 'b1', confirmed: true } }),
    JSON.stringify({ event: 'update.applying', data: { by: 'now', build: 'b2' } }),
    '',
  ].join('\n');

  assert.deepEqual(loggedData(lines, 'update.applying'), [{ by: 'idle', build: 'b1' }, { by: 'now', build: 'b2' }]);
  assert.deepEqual(loggedData(lines, 'update.rolled-back'), []);
});

/**
 * The rehearsal's roll-back stages the live build again with its application replaced by a program that exits at once,
 * and a manifest that agrees with it, so the check passes and the start fails: the case D139 §6 rolls back. Its refusal
 * stages the live build with one file changed after the manifest was written, so the check refuses it.
 */
test('a build staged to fail its start passes the check’s shape, and one staged to fail the check does not', () => {
  const fixture = makeFixture('deploy-update');
  const install = fixture.root;
  writeFileSync(join(install, 'INSTALLED.md'), '# Daoris — installed desktop\n');
  writeFileSync(join(install, 'Daoris.exe'), 'launcher');
  fixture.write('app/Daoris.Desktop.exe', 'application');
  fixture.write('app/Daoris.Desktop.App.dll', 'library');
  fixture.write('app/daoris-knowledge-http/daoris-knowledge-http.exe', 'host');
  fixture.write('data/driver.json', '{}');
  const exits = join(install, 'exits-at-once.exe');
  writeFileSync(exits, 'a program that exits');

  const staged = stageLiveBuild(install, { id: 'fails-to-start', version: '0.0.1', application: exits });

  const manifest = JSON.parse(readText(join(staged, 'build.json')));
  assert.equal(manifest.id, 'fails-to-start');
  assert.deepEqual(manifest.files.map((file: { path: string }) => file.path).sort(), [
    'Daoris.exe', 'INSTALLED.md', 'app/Daoris.Desktop.App.dll', 'app/Daoris.Desktop.exe', 'app/daoris-knowledge-http/daoris-knowledge-http.exe',
  ]);
  assert.equal(readText(join(staged, 'app', 'Daoris.Desktop.exe')), 'a program that exits');
  assert.equal(readText(join(install, 'app', 'Daoris.Desktop.exe')), 'application', 'the live build is untouched');
  assert.ok(!manifest.files.some((file: { path: string }) => file.path.startsWith('data/')), 'the home is never staged');

  const refused = stageLiveBuild(install, { id: 'fails-the-check', version: '0.0.1', tamper: 'app/Daoris.Desktop.App.dll' });
  const listed = JSON.parse(readText(join(refused, 'build.json'))).files
    .find((file: { path: string }) => file.path === 'app/Daoris.Desktop.App.dll');
  assert.notEqual(listed.size, readText(join(refused, 'app', 'Daoris.Desktop.App.dll')).length, 'changed after its manifest');
});

/** A C# `const string` whose value is one literal or several joined by `+`, as the source spells it. */
function csStringConst(source: string, name: string): string | null {
  const match = new RegExp(`const string ${name}\\s*=\\s*((?:"(?:[^"\\\\]|\\\\.)*"\\s*\\+?\\s*)+);`).exec(source);
  if (!match) return null;
  return [...(match[1] ?? '').matchAll(/"((?:[^"\\]|\\.)*)"/g)].map((literal) => literal[1]).join('');
}

/**
 * The two notes are the driver's, spelled a second time in the gate on purpose (`twins.md`): the gate
 * shares no code with the driver. This reads both sides, so an edit to either note fails here rather
 * than turning the gate's check into one that no record can pass, or every record can.
 */
test('the gate’s two notes are the driver’s own, word for word', () => {
  const driver = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver');
  assert.equal(csStringConst(readText(join(driver, 'ChatRunner.cs')), 'ClosedNote'), CLOSED_NOTE);
  assert.equal(csStringConst(readText(join(driver, 'Orphans.cs')), 'Note'), SWEPT_NOTE);
  assert.notEqual(CLOSED_NOTE, SWEPT_NOTE);
});

/**
 * The marker a tracked process leaves under the home's `sessions/` (`<pid> <start ticks>`), read as
 * `SessionProcesses.AliveOnThisMachine` reads it: the pid alone could be a process the machine has
 * since handed that number to, so the start time has to agree too.
 */
test('a marker names a process by its pid and its start time, and only the same start is the same process', () => {
  const marked = markedProcess('4242 638950000000000000\n');
  assert.deepEqual(marked, { pid: 4242, started: 638950000000000000n });
  // Ticks outgrow a JavaScript number: read as a Number, this one would lose its last digits.
  assert.notEqual(BigInt(Number('638950000000000001')), 638950000000000001n);
  assert.deepEqual(markedProcess('4242 638950000000000001'), { pid: 4242, started: 638950000000000001n });

  for (const torn of ['', '4242', 'x 1', '4242 1 2', '-1 5', undefined]) {
    assert.equal(markedProcess(torn), null, `${JSON.stringify(torn)} is no marker`);
  }

  assert.equal(isMarkedProcess(marked, '638950000000000000'), true);
  assert.equal(isMarkedProcess(marked, ' 638950000005000000\r\n'), true, 'within a second, as PowerShell prints it');
  assert.equal(isMarkedProcess(marked, '638950000020000000'), false, 'two seconds on is someone else');
  assert.equal(isMarkedProcess(marked, ''), false, 'no process under that pid');
  assert.equal(isMarkedProcess(marked, 'not ticks'), false);
  assert.equal(isMarkedProcess(null, '638950000000000000'), false);
});

/**
 * The stub the gate opens its conversation on is the family rehearsal's protocol agent, moved beside
 * the rest of the rehearsal kit so both run one copy. What the deployment gate leans on is that a
 * conversation on it stays open, answering, until its input closes: a stub that ended after its first
 * turn would leave nothing `working` for the close to end.
 */
test('the protocol stub both rehearsals run holds a conversation open until its input closes', async () => {
  const fx = makeFixture('deployment-rehearsal-acp-stub');
  const agent = join(fx.root, 'acp-agent.mjs');
  writeFileSync(agent, ACP_STUB_AGENT);
  const env = { ...process.env };
  // A conversation serves no quest; the stub's chat branch is the one without one.
  delete env.DAORIS_QUEST_ID;
  const child = spawn(process.execPath, [agent], { cwd: fx.root, env, stdio: ['pipe', 'pipe', 'pipe'] });
  const exited = new Promise<number | null>((resolve) => child.on('exit', (code) => resolve(code)));

  const answers = new Map<number, { result?: { stopReason?: string; sessionId?: string } }>();
  const updates: string[] = [];
  let answered: () => void = () => {};
  createInterface({ input: child.stdout }).on('line', (line) => {
    const frame = JSON.parse(line);
    if (frame.method === 'session/update') updates.push(frame.params?.update?.content?.text ?? '');
    else if (frame.id !== undefined) {
      answers.set(frame.id, frame);
      if (answers.size === 3) answered();
    }
  });
  const allAnswered = new Promise<void>((resolve) => { answered = resolve; });

  const send = (frame: object) => child.stdin.write(`${JSON.stringify(frame)}\n`);
  send({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1 } });
  send({ jsonrpc: '2.0', id: 2, method: 'session/new', params: { cwd: fx.root, mcpServers: [] } });
  send({ jsonrpc: '2.0', id: 3, method: 'session/prompt', params: { sessionId: 'acp-session-1', prompt: [{ type: 'text', text: 'still there?' }] } });
  await allAnswered;

  assert.equal(answers.get(2)?.result?.sessionId, 'acp-session-1');
  assert.equal(answers.get(3)?.result?.stopReason, 'end_turn');
  assert.ok(updates.some((text) => text.includes('acp heard: still there?')), updates.join('\n'));
  // Answered, and still there: the conversation outlives its turn.
  assert.equal(child.exitCode, null);

  child.stdin.end();
  assert.equal(await exited, 0, 'end of input is the ending');
  fx.cleanup();
});
