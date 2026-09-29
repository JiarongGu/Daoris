import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
// Untyped workspace tooling, suppressed at the one site — see desktop-tool.test.ts for why.
import {
  LAUNCHER, MARKER, MARKER_HEADER, OWN, RETIRED_LAUNCHERS, SHELL_EXE, SHELL_FILES, SHELL_HOME, isInstall, recordedShellFiles, refusal,
  // @ts-expect-error — untyped workspace tooling; see above
} from '../../../tools/desktop-publish.mjs';

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

test('four names make an install, and the marker is one of them', () => {
  assert.deepEqual([...OWN].sort(), ['Daoris.exe', 'INSTALLED.md', 'app', 'data']);
  assert.ok(OWN.includes(MARKER));
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
  assert.deepEqual(RETIRED_LAUNCHERS, ['daoris-desktop.exe']);
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
