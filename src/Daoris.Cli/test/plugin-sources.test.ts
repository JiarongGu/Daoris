import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  MANIFEST, OFFERS_DIR, SOURCE_FILE, applyUpdate, commandPlugin, dataFolder, offersFolder, planUpdate, pluginsRoot,
  readNeeds, readOffers, readPluginSource,
} from '../src/plugins.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * PLUG9 (c) and (d), D103 — the CLI's half of two twins with the driver's `PluginInstall.cs` and
 * `PluginOffers.cs` (`PluginSourceTests.cs`, `PluginOfferTests.cs` hold the same tables):
 *
 * - **Where an installed plugin came from** is `.daoris-source.json` in its install folder, written into
 *   the staged copy before the swap: the folder it was added from, or the offer it was installed from.
 * - **Update** re-reads that source with the catalogue's own reader, refuses in the same words, says
 *   what changes, and swaps the install folder whole, `.data/` untouched.
 * - **The offers** are Daoris's own example plugins in the install's `app/plugin-offers/`, beside the
 *   home: listed, and installed by `add --offer`, never before.
 *
 * 🔴 Nothing here starts a plugin: an add copies, an update swaps, and the loop starts what runs later.
 */

/** A home under an install-shaped folder: `<root>/data` is the home, `<root>/app/plugin-offers` the offers. */
function machine(name: string) {
  const fx = makeFixture(name);
  const home = join(fx.root, 'data');
  mkdirSync(home, { recursive: true });
  const offers = join(fx.root, ...OFFERS_DIR);
  return { fx, home, offers };
}

function folder(at: string, manifest: string, files: Record<string, string> = {}): string {
  mkdirSync(at, { recursive: true });
  writeFileSync(join(at, MANIFEST), manifest, 'utf8');
  for (const [name, text] of Object.entries(files)) writeFileSync(join(at, name), text, 'utf8');
  return at;
}

function run(argv: string[], home: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = home;
  const lines: string[] = [];
  try {
    const code = commandPlugin({ root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd() }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }
}

function refused(argv: string[], home: string): Error {
  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = home;
  try {
    return captureError(() => commandPlugin({ root: process.cwd(), argv, write: () => {}, packageRoot: process.cwd() }));
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }
}

const GATE_V1 = JSON.stringify({
  id: 'acme.gate', name: 'Acme gate', version: '1.0.0',
  hooks: { command: ['node', '${plugin}/gate.mjs'], points: ['quest/consider'] },
});

// ——— (c) the source, recorded on every add door.

test('add records the folder it came from in the install, and the catalogue reads past it', () => {
  const { fx, home } = machine('plugsrc-add');
  const source = folder(join(fx.root, 'checkout', 'gate'), GATE_V1, { 'gate.mjs': '// v1' });

  assert.equal(run(['add', source], home).code, 0);

  const installed = join(pluginsRoot(home), 'acme.gate');
  assert.deepEqual(readPluginSource(installed), { source: { folder: source }, problem: null });
  assert.ok(existsSync(join(installed, SOURCE_FILE)));
  // Written into the staged copy, so nothing but the plugin and its record is under plugins/.
  assert.deepEqual(readdirSync(pluginsRoot(home)), ['acme.gate']);
  fx.cleanup();
});

test('a plugin copied in by hand, or added before Daoris kept a source, has none — never a guess', () => {
  const { fx, home } = machine('plugsrc-none');
  folder(join(pluginsRoot(home), 'acme.gate'), GATE_V1);

  assert.deepEqual(readPluginSource(join(pluginsRoot(home), 'acme.gate')), { source: null, problem: null });
  const { out } = run(['list'], home);
  assert.match(out, /no record of where it came from/);
  fx.cleanup();
});

/** The record's shape, the twin's table (`PluginSourceTests.The_record_reads_as_the_cli_reads_it`). */
test('the record reads a folder or an offer, and anything else is a named problem', () => {
  const { fx } = machine('plugsrc-shapes');
  const at = (text: string | null) => {
    const install = join(fx.root, 'install', String(Math.random()).slice(2));
    mkdirSync(install, { recursive: true });
    if (text !== null) writeFileSync(join(install, SOURCE_FILE), text);
    return readPluginSource(install);
  };
  const whole = join(fx.root, 'somewhere');

  assert.deepEqual(at(null), { source: null, problem: null });
  assert.deepEqual(at(JSON.stringify({ folder: whole })), { source: { folder: whole }, problem: null });
  assert.deepEqual(at(JSON.stringify({ offer: 'github-pull-request' })), { source: { offer: 'github-pull-request' }, problem: null });
  for (const bad of ['{ not json', '[]', '{}', JSON.stringify({ folder: 'relative/path' }),
    JSON.stringify({ offer: 'Not An Id' }), JSON.stringify({ folder: whole, offer: 'x' })]) {
    const read = at(bad);
    assert.equal(read.source, null, bad);
    assert.match(read.problem ?? '', /does not read/, bad);
  }
  fx.cleanup();
});

// ——— (c) update: what changes, then the swap.

test('update shows what changes and replaces nothing without --yes', () => {
  const { fx, home } = machine('plugsrc-plan');
  const source = folder(join(fx.root, 'checkout', 'gate'), GATE_V1, { 'gate.mjs': '// v1' });
  run(['add', source], home);
  folder(source, JSON.stringify({
    id: 'acme.gate', name: 'Acme gate', version: '1.1.0',
    hooks: { command: ['node', '${plugin}/gate2.mjs'], points: ['quest/consider', 'session/ended'] },
    harnesses: [{ name: 'acme-agent', command: ['acme-agent', '--acp'] }],
    servers: [{ name: 'browser', command: ['npx', '-y', '@playwright/mcp@0.0.82'] }],
  }), { 'gate.mjs': '// v2' });

  const { plan, refusal } = planUpdate(home, 'acme.gate');
  assert.equal(refusal, null);
  assert.deepEqual(plan!.changes, [
    { what: 'version', was: '1.0.0', now: '1.1.0' },
    { what: 'command', was: 'node ${plugin}/gate.mjs', now: 'node ${plugin}/gate2.mjs' },
    { what: 'points', was: 'quest/consider', now: 'quest/consider, session/ended' },
    { what: 'harnesses', was: '', now: 'acme-agent (acme-agent --acp)' },
    { what: 'servers', was: '', now: 'browser (npx -y @playwright/mcp@0.0.82)' },
  ]);

  const asked = run(['update', 'acme.gate'], home);
  assert.equal(asked.code, 1);
  assert.match(asked.out, /version\s+1\.0\.0 → 1\.1\.0/);
  assert.match(asked.out, /Not updated: this is what would change\. Run it again with --yes/);
  assert.equal(readFileSync(join(pluginsRoot(home), 'acme.gate', 'gate.mjs'), 'utf8'), '// v1');
  fx.cleanup();
});

test('update swaps the install folder whole, keeping .data and the record', () => {
  const { fx, home } = machine('plugsrc-swap');
  const source = folder(join(fx.root, 'checkout', 'gate'), GATE_V1, { 'gate.mjs': '// v1' });
  run(['add', source], home);
  const installed = join(pluginsRoot(home), 'acme.gate');
  writeFileSync(join(installed, 'stale.txt'), 'from the install before');
  mkdirSync(dataFolder(home, 'acme.gate'), { recursive: true });
  writeFileSync(join(dataFolder(home, 'acme.gate'), 'kept.json'), '{}');
  writeFileSync(join(source, 'gate.mjs'), '// v2');

  const done = run(['update', 'acme.gate', '--yes'], home);

  assert.equal(done.code, 0, done.out);
  assert.match(done.out, /updated plugin `acme\.gate`/);
  assert.match(done.out, /What it declares does not change/);
  assert.equal(readFileSync(join(installed, 'gate.mjs'), 'utf8'), '// v2');
  assert.equal(existsSync(join(installed, 'stale.txt')), false);
  assert.equal(existsSync(join(dataFolder(home, 'acme.gate'), 'kept.json')), true);
  assert.deepEqual(readPluginSource(installed).source, { folder: source });
  // Nothing staged or set aside is left: only the plugin and what it keeps.
  assert.deepEqual(readdirSync(pluginsRoot(home)).sort(), ['.data', 'acme.gate']);
  fx.cleanup();
});

/**
 * Update's refusals, in the same words as the driver's (`PluginSourceTests.Update_refuses…`, the same
 * rows in the same order). Each leaves the installed version exactly as it was.
 */
test('update refuses a source that is gone, unsound, another plugin, or one this build refuses', () => {
  const cases: [string, (m: ReturnType<typeof machine>, source: string) => void, RegExp][] = [
    ['not installed', (m) => { m.fx.write('data/plugins/.keep', ''); }, /no plugin `acme\.nobody` on this machine/],
    ['no source recorded', (m) => { folder(join(pluginsRoot(m.home), 'acme.bare'), GATE_V1.replace('acme.gate', 'acme.bare')); }, /`acme\.bare` has no record of where it came from/],
    ['record unreadable', (m) => { writeFileSync(join(pluginsRoot(m.home), 'acme.gate', SOURCE_FILE), '{ not json'); }, /record of where it came from does not read/],
    ['folder gone', (_m, source) => { renameAway(source); }, /is not there any more/],
    ['no manifest', (_m, source) => { rmManifest(source); }, /no `plugin\.json` in/],
    ['unsound manifest', (_m, source) => { writeFileSync(join(source, MANIFEST), '{ "id": "acme.gate", "apiVersion": 99 }'); }, /needs plugin API 99/],
    ['another plugin', (_m, source) => { writeFileSync(join(source, MANIFEST), '{ "id": "acme.other" }'); }, /now holds plugin `acme\.other`, not `acme\.gate`/],
    ['refused by this build', (_m, source) => { writeFileSync(join(source, MANIFEST), '{ "id": "acme.gate", "harnesses": [ { "name": "dsh", "command": ["x"] } ] }'); }, /`dsh`, which this build already carries/],
    ['offer no longer offered', (m) => { writeFileSync(join(pluginsRoot(m.home), 'acme.gate', SOURCE_FILE), JSON.stringify({ offer: 'acme.gate' })); }, /`acme\.gate` is not offered by this install any more/],
    ['source inside the home', (m) => {
      const inside = folder(join(m.home, 'elsewhere', 'gate'), GATE_V1);
      writeFileSync(join(pluginsRoot(m.home), 'acme.gate', SOURCE_FILE), JSON.stringify({ folder: inside }));
    }, /inside Daoris's home/],
  ];

  for (const [name, arrange, says] of cases) {
    const m = machine(`plugsrc-refuse-${cases.findIndex((row) => row[0] === name)}`);
    const source = folder(join(m.fx.root, 'checkout', 'gate'), GATE_V1, { 'gate.mjs': '// v1' });
    run(['add', source], m.home);
    arrange(m, source);
    const id = name === 'not installed' ? 'acme.nobody' : name === 'no source recorded' ? 'acme.bare' : 'acme.gate';

    const { refusal } = planUpdate(m.home, id);
    assert.match(refusal ?? '', says, name);
    const error = refused(['update', id, '--yes'], m.home);
    assert.match(error.message, says, name);
    assert.throws(() => applyUpdate(m.home, id), (thrown: Error) => says.test(thrown.message), name);
    if (name !== 'not installed' && name !== 'no source recorded') {
      assert.equal(readFileSync(join(pluginsRoot(m.home), 'acme.gate', 'gate.mjs'), 'utf8'), '// v1', name);
    }
    m.fx.cleanup();
  }
});

test('update names a plugin by its id, refused before it becomes a path', () => {
  const { fx, home } = machine('plugsrc-id');
  for (const id of ['..', '.data', 'acme/gate']) {
    assert.match(refused(['update', id], home).message, /not a plugin id/, id);
  }
  fx.cleanup();
});

test('a plugin whose folder a running process holds is not updated, and stays whole', {
  // A process's working directory holds its folder on Windows only; elsewhere the move succeeds.
  skip: process.platform !== 'win32',
}, async () => {
  const { fx, home } = machine('plugsrc-held');
  const source = folder(join(fx.root, 'checkout', 'gate'), GATE_V1, { 'gate.mjs': '// v1' });
  run(['add', source], home);
  writeFileSync(join(source, 'gate.mjs'), '// v2');
  const installed = join(pluginsRoot(home), 'acme.gate');
  const { spawn } = await import('node:child_process');
  const holder = spawn(process.execPath, ['-e', 'setTimeout(() => {}, 20000)'], { cwd: installed, stdio: 'ignore' });
  try {
    await new Promise((resolve) => setTimeout(resolve, 300));
    assert.match(refused(['update', 'acme.gate', '--yes'], home).message, /was not updated/);
    assert.equal(readFileSync(join(installed, 'gate.mjs'), 'utf8'), '// v1');
    assert.deepEqual(readdirSync(pluginsRoot(home)), ['acme.gate']);
  } finally {
    holder.kill();
    await new Promise((resolve) => setTimeout(resolve, 300));
    fx.cleanup();
  }
});

// ——— (d) the offers.

const OFFERED = JSON.stringify({
  id: 'github-pull-request', name: 'GitHub pull request', version: '1.0.0',
  hooks: { command: ['node', '${plugin}/land.mjs'], points: ['work/land'] },
});

const README = `# github-pull-request

Intro.

## What it needs

- **node** on the PATH
  (it is a \`node\` script).
- **gh**, signed in: \`gh auth login\`.

## When something fails

- not a need
`;

test('the offers are the install\'s app/plugin-offers, beside the home', () => {
  const { fx, home, offers } = machine('plugoffer-where');
  assert.equal(offersFolder(home), offers);
  assert.deepEqual(OFFERS_DIR, ['app', 'plugin-offers']);
  assert.deepEqual(readOffers(home), []);
  fx.cleanup();
});

/** The README's requirement lines, the twin's table (`PluginOfferTests.What_an_offer_needs…`). */
test('what an offer needs is its README\'s own section, emphasis dropped and code kept', () => {
  const { fx } = machine('plugoffer-needs');
  const at = (text: string | null) => {
    const plugin = join(fx.root, String(Math.random()).slice(2));
    mkdirSync(plugin, { recursive: true });
    if (text !== null) writeFileSync(join(plugin, 'README.md'), text);
    return readNeeds(plugin);
  };

  assert.deepEqual(at(null), []);
  assert.deepEqual(at('# x\n\nNothing about needs.\n'), []);
  assert.deepEqual(at(README), ['node on the PATH (it is a `node` script).', 'gh, signed in: `gh auth login`.']);
  assert.deepEqual(at(README.replace(/\n/g, '\r\n')), ['node on the PATH (it is a `node` script).', 'gh, signed in: `gh auth login`.']);
  fx.cleanup();
});

test('the offers are listed with what they declare and need; an unsound one says why', () => {
  const { fx, home, offers } = machine('plugoffer-list');
  folder(join(offers, 'github-pull-request'), OFFERED, { 'README.md': README });
  folder(join(offers, 'future'), '{ "id": "future", "apiVersion": 99 }');
  folder(join(offers, '.hidden'), OFFERED);
  mkdirSync(join(offers, 'notes'), { recursive: true });

  const listed = readOffers(home);
  assert.deepEqual(listed.map((offer) => offer.id), ['future', 'github-pull-request']);
  const github = listed.find((offer) => offer.id === 'github-pull-request')!;
  assert.equal(github.problem, null);
  assert.equal(github.installed, false);
  // As the manifest writes it: `${plugin}` stays, never a path on this machine.
  assert.deepEqual(github.manifest.hooks?.command, ['node', '${plugin}/land.mjs']);
  assert.deepEqual(github.needs, ['node on the PATH (it is a `node` script).', 'gh, signed in: `gh auth login`.']);
  assert.match(listed.find((offer) => offer.id === 'future')!.problem ?? '', /needs plugin API 99/);

  const { out } = run(['list'], home);
  assert.match(out, /offered by this install, not installed/);
  assert.match(out, /github-pull-request\s+GitHub pull request 1\.0\.0/);
  assert.match(out, /daoris plugin add --offer github-pull-request/);
  assert.match(out, /needs: node on the PATH/);
  fx.cleanup();
});

test('an offer is installed by add --offer, with the offer recorded, and then is not offered', () => {
  const { fx, home, offers } = machine('plugoffer-install');
  folder(join(offers, 'github-pull-request'), OFFERED, { 'README.md': README, 'land.mjs': '// v1' });

  const added = run(['add', '--offer', 'github-pull-request'], home);

  assert.equal(added.code, 0, added.out);
  const installed = join(pluginsRoot(home), 'github-pull-request');
  assert.equal(readFileSync(join(installed, 'land.mjs'), 'utf8'), '// v1');
  assert.deepEqual(readPluginSource(installed).source, { offer: 'github-pull-request' });
  assert.equal(readOffers(home)[0]!.installed, true);
  assert.doesNotMatch(run(['list'], home).out, /offered by this install, not installed/);
  assert.match(run(['list'], home).out, /from Daoris's own plugins, offered by this install/);

  // A republish brings a newer offer: update takes it, from the offer as the install has it now.
  folder(join(offers, 'github-pull-request'), OFFERED.replace('1.0.0', '1.1.0'), { 'land.mjs': '// v2' });
  const { plan } = planUpdate(home, 'github-pull-request');
  assert.deepEqual(plan!.changes, [{ what: 'version', was: '1.0.0', now: '1.1.0' }]);
  applyUpdate(home, 'github-pull-request');
  assert.equal(readFileSync(join(installed, 'land.mjs'), 'utf8'), '// v2');
  assert.deepEqual(readPluginSource(installed).source, { offer: 'github-pull-request' });
  fx.cleanup();
});

test('add --offer refuses an offer the install does not carry, naming what it offers', () => {
  const { fx, home, offers } = machine('plugoffer-unknown');
  folder(join(offers, 'github-pull-request'), OFFERED);

  const error = refused(['add', '--offer', 'acme.nothing'], home);
  assert.match(error.message, /this install offers no plugin `acme\.nothing`/);
  assert.match(error.message, /github-pull-request/);
  assert.match(refused(['add', '--offer'], home).message, /`--offer` names one of Daoris's own plugins/);
  assert.match(refused(['add', '--offer', '../x'], home).message, /not a plugin id/);
  assert.equal(existsSync(pluginsRoot(home)), false);
  fx.cleanup();
});

/** 🔴 Installing an offer runs nothing: its hook would leave a mark. */
test('installing an offer starts nothing it declares', () => {
  const { fx, home, offers } = machine('plugoffer-quiet');
  const mark = join(fx.root, 'ran.txt');
  folder(join(offers, 'acme.marks'), JSON.stringify({
    id: 'acme.marks', hooks: { command: ['node', '${plugin}/hook.mjs'], points: ['session/ended'] },
  }), { 'hook.mjs': `require('fs').writeFileSync(${JSON.stringify(mark)}, 'ran');` });

  run(['add', '--offer', 'acme.marks'], home);

  assert.equal(existsSync(mark), false);
  fx.cleanup();
});

function renameAway(path: string): void {
  renameSync(path, `${path}-moved`);
}

function rmManifest(path: string): void {
  rmSync(join(path, MANIFEST));
}
