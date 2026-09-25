import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readText } from '../src/fsx.ts';
import {
  HOST_EXE, HOST_HOME, SHELL_EXE, hookLines, insideWorkspace, launchers, strays, transcriptHolds, utf8Of,
  // @ts-expect-error — untyped workspace tooling; the same seam desktop-tool.test.ts documents
} from '../../../tools/deployment-rehearsal.mjs';

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
    launchers(['daoris-desktop.exe', 'INSTALLED.md', 'app', 'data', 'WebView2Loader.dll']),
    ['daoris-desktop.exe']);
  // Two launchers is the failure being guarded, not a pass with a favourite.
  assert.deepEqual(launchers(['a.exe', 'b.cmd', 'c.bat', 'd.txt']), ['a.exe', 'b.cmd', 'c.bat']);
});

test('symbols and package doc files are strays, and the marker and the bundle are not', () => {
  assert.deepEqual(
    strays(['daoris-desktop.exe', 'daoris-desktop.pdb', 'WebView2Loader.xml', 'INSTALLED.md']),
    ['daoris-desktop.pdb', 'WebView2Loader.xml']);
  assert.deepEqual(strays(['daoris-desktop.exe', 'INSTALLED.md']), []);
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

test('the gate and the publish script agree on what the install is called', () => {
  const publish = readText(join(repoRoot, 'tools', 'desktop-publish.mjs'));
  assert.ok(publish.includes(`'${SHELL_EXE}'`), `the publish no longer names ${SHELL_EXE}`);
  assert.ok(publish.includes(`join(to, 'app', '${HOST_HOME[1]}')`),
    `the publish no longer puts the host in ${HOST_HOME.join('/')}`);
});
