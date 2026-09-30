import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
// Untyped workspace tooling, suppressed at the one site — see desktop-tool.test.ts for why.
import {
  KEPT_LOCALES, LAUNCHER, MARKER, MARKER_HEADER, OFFERED_PLUGINS, OWN, PLUGIN_OFFERS, RETIRED_IN_APP, RETIRED_LAUNCHERS,
  SHELL_EXE, SHELL_FILES, SHELL_HOME, isInstall, layOffers, recordedShellFiles, refusal, retiredPaths,
  // @ts-expect-error — untyped workspace tooling; see above
} from '../../../tools/desktop-publish.mjs';
import { OFFERS_DIR, readManifest } from '../src/plugins.ts';

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
