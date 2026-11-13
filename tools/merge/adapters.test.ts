import { test } from 'vitest';
import assert from 'node:assert/strict';
import { emptyLock, fingerprintOf, judge, withRow, type Lock, type LockRow, type Sight } from './adapters.ts';

const MANIFEST = JSON.stringify({
  suite: 'adapters-store',
  tests: 'src/Daoris.Adapters/Daoris.Adapters.Store.Tests',
  contract: ['src/Daoris.Testing/Store'],
  ports: ['Store'],
  outside: ['nuget:Microsoft.Data.Sqlite'],
});

const PINS = '<PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.14" />\n<PackageVersion Include="Other" Version="1" />';

/** A commit as a map: a path to its tree or blob hash, and a few files to their text. */
function commit(changes: Record<string, string | null> = {}): Sight {
  const hashes: Record<string, string> = {
    'src/Daoris.Adapters/Daoris.Adapters.Store': 'tree-adapter',
    'src/Daoris.Adapters/Daoris.Adapters.Store.Tests': 'tree-suite',
    'src/Daoris.Testing/Store': 'tree-contract',
    'src/Daoris.Ports/Store': 'tree-port-store',
    'src/Daoris.Ports/Git': 'tree-port-git',
    'Directory.Build.props': 'blob-root',
    'src/Daoris.Desktop/Directory.Build.props': 'blob-desktop',
    'src/Layers.props': 'blob-layers',
    ...changes as Record<string, string>,
  };
  const texts: Record<string, string> = {
    'src/Daoris.Adapters/Daoris.Adapters.Store/adapter.json': MANIFEST,
    'src/Daoris.Desktop/Directory.Packages.props': PINS,
    ...Object.fromEntries(Object.entries(changes).filter(([path]) => path.endsWith('.json') || path.endsWith('.props'))),
  };
  return {
    tree: (path) => hashes[path] ?? null,
    blob: (path) => hashes[path] ?? null,
    text: (path) => texts[path] ?? null,
    dirty: () => [],
    adapters: () => ['Daoris.Adapters.Store'],
  };
}

const NAME = 'Daoris.Adapters.Store';
const at = (changes?: Record<string, string | null>) => fingerprintOf(NAME, commit(changes)).fingerprint;

test('a fingerprint is the same for the same inputs and moves with each input the package reads', () => {
  const base = at();
  assert.equal(at(), base);
  assert.notEqual(at({ 'src/Daoris.Adapters/Daoris.Adapters.Store': 'edited' }), base, 'its own folder');
  assert.notEqual(at({ 'src/Daoris.Adapters/Daoris.Adapters.Store.Tests': 'edited' }), base, 'its suite');
  assert.notEqual(at({ 'src/Daoris.Testing/Store': 'edited' }), base, 'the contract and the fake it is held to');
  assert.notEqual(at({ 'src/Daoris.Ports/Store': 'edited' }), base, 'a port it implements');
  assert.notEqual(at({ 'Directory.Build.props': 'edited' }), base, 'a build file that reaches every project');
  assert.notEqual(at({ 'src/Layers.props': 'edited' }), base, 'the layers\' settings, which every package takes through the ports');
  assert.notEqual(at({ 'src/Daoris.Desktop/Directory.Packages.props': PINS.replace('10.0.14', '10.0.15') }), base, 'the pin of what it drives');
});

test('a pin is read from the versions file the manifest names, through the properties that file sets', () => {
  const versions = 'src/Daoris.Service/Directory.Packages.props';
  const manifest = JSON.stringify({ ...JSON.parse(MANIFEST), outside: ['nuget:Lyntai.Core'], pins: versions });
  const props = (released: string) => [
    `<DaorisLyntaiReleased>${released}</DaorisLyntaiReleased>`,
    '<DaorisLyntaiVersion Condition="\'$(DaorisLyntaiVersion)\' == \'\'">$(DaorisLyntaiReleased)</DaorisLyntaiVersion>',
    '<PackageVersion Include="Lyntai.Core" Version="$(DaorisLyntaiVersion)" />',
  ].join('\n');
  const read = (released: string) => fingerprintOf(NAME, commit({ 'src/Daoris.Adapters/Daoris.Adapters.Store/adapter.json': manifest, [versions]: props(released) }));

  assert.deepEqual(read('3.5.3').outside, { 'nuget:Lyntai.Core': '3.5.3' });
  assert.notEqual(read('3.5.4').fingerprint, read('3.5.3').fingerprint, 'a release of what it drives');
  const unset = commit({ 'src/Daoris.Adapters/Daoris.Adapters.Store/adapter.json': manifest, [versions]: '<PackageVersion Include="Lyntai.Core" Version="$(Nowhere)" />' });
  assert.throws(() => fingerprintOf(NAME, unset), /the pin of nuget:Lyntai\.Core/, 'a property the file never sets is no version');
});

test('a port it does not implement, a pin it does not read and a file no input names move nothing', () => {
  const base = at();
  assert.equal(at({ 'src/Daoris.Ports/Git': 'edited' }), base);
  assert.equal(at({ 'src/Daoris.Desktop/Directory.Packages.props': PINS.replace('"Other" Version="1"', '"Other" Version="2"') }), base);
  assert.equal(at({ 'src/Daoris.Desktop/Daoris.Desktop.Driver/Store/DesktopStore.cs': 'edited' }), base);
});

test('an adapter built over another moves with it, and a cycle is refused', () => {
  const over = JSON.stringify({ ...JSON.parse(MANIFEST), over: ['Daoris.Adapters.Base'] });
  const base = JSON.stringify({ suite: 's', tests: 'src/Daoris.Adapters/Daoris.Adapters.Base.Tests', contract: [], ports: [], outside: [] });
  const sight = (baseTree: string): Sight => {
    const inner = commit({
      'src/Daoris.Adapters/Daoris.Adapters.Store/adapter.json': over,
      'src/Daoris.Adapters/Daoris.Adapters.Base': baseTree,
      'src/Daoris.Adapters/Daoris.Adapters.Base.Tests': 'tree-base-suite',
    });
    return { ...inner, text: (path) => (path === 'src/Daoris.Adapters/Daoris.Adapters.Base/adapter.json' ? base : inner.text(path)) };
  };
  assert.notEqual(fingerprintOf(NAME, sight('one')).fingerprint, fingerprintOf(NAME, sight('two')).fingerprint);

  const cycle = { ...sight('one'), text: (path: string) => (path.endsWith('Base/adapter.json') ? JSON.stringify({ ...JSON.parse(base), over: [NAME] }) : sight('one').text(path)) };
  assert.throws(() => fingerprintOf(NAME, cycle), /built over itself/);
});

test('a package that declares no manifest fails its fingerprint, naming the declaration it lacks', () => {
  const sight: Sight = { ...commit(), text: () => null };
  assert.throws(() => fingerprintOf(NAME, sight), /no src\/Daoris\.Adapters\/Daoris\.Adapters\.Store\/adapter\.json/);
});

test('a row equal to the fingerprint is kept, a different one stale and none missing', () => {
  const sight = commit();
  const row = (fingerprint: string): LockRow => ({ fingerprint, tree: 't', ports: {}, outside: {}, suite: 's', toolchain: 'x', passed: 'now' });
  const lock = (fingerprint: string): Lock => withRow(emptyLock(), NAME, row(fingerprint));

  assert.equal(judge(lock(at()), sight)[0]?.state, 'kept');
  assert.equal(judge(lock('other'), sight)[0]?.state, 'stale');
  assert.equal(judge(emptyLock(), sight)[0]?.state, 'missing');
});

test('the lock keeps its rows in name order so the file is a stable diff', () => {
  const row: LockRow = { fingerprint: 'f', tree: 't', ports: {}, outside: {}, suite: 's', toolchain: 'x', passed: 'now' };
  const lock = withRow(withRow(emptyLock(), 'Daoris.Adapters.Zed', row), 'Daoris.Adapters.Alpha', row);

  assert.deepEqual(Object.keys(lock.adapters), ['Daoris.Adapters.Alpha', 'Daoris.Adapters.Zed']);
});
