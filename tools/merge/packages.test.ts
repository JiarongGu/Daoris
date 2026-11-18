import { test } from 'vitest';
import assert from 'node:assert/strict';
import { emptyLock, fingerprintOf, judge, withRow, type Lock, type LockRow, type Sight } from './packages.ts';

const MANIFEST = JSON.stringify({
  suite: 'adapters-store',
  tests: 'src/Daoris.Adapters/Daoris.Adapters.Store.Tests',
  contract: ['src/Daoris.Testing/Store'],
  ports: ['Store'],
  outside: ['nuget:Microsoft.Data.Sqlite'],
});

const PINS = '<PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.14" />\n<PackageVersion Include="Other" Version="1" />';
const FOLDER = 'src/Daoris.Adapters/Daoris.Adapters.Store';
const AT = `${FOLDER}/daoris.package.json`;

/** A commit as a map: a path to its tree or blob hash, and a few files to their text. */
function commit(changes: Record<string, string | null> = {}): Sight {
  const hashes: Record<string, string> = {
    [FOLDER]: 'tree-adapter',
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
    [AT]: MANIFEST,
    'src/Daoris.Desktop/Directory.Packages.props': PINS,
    ...Object.fromEntries(Object.entries(changes).filter(([path]) => path.endsWith('.json') || path.endsWith('.props'))) as Record<string, string>,
  };
  return {
    tree: (path) => hashes[path] ?? null,
    text: (path) => texts[path] ?? null,
    dirty: () => [],
    packages: () => [{ name: NAME, folder: FOLDER }],
  };
}

const NAME = 'Daoris.Adapters.Store';
const at = (changes?: Record<string, string | null>) => fingerprintOf(NAME, commit(changes)).fingerprint;

test('a fingerprint is the same for the same inputs and moves with each input the package reads', () => {
  const base = at();
  assert.equal(at(), base);
  assert.notEqual(at({ [FOLDER]: 'edited' }), base, 'its own folder');
  assert.notEqual(at({ 'src/Daoris.Adapters/Daoris.Adapters.Store.Tests': 'edited' }), base, 'its suite');
  assert.notEqual(at({ 'src/Daoris.Testing/Store': 'edited' }), base, 'the contract and the fake it is held to');
  assert.notEqual(at({ 'src/Daoris.Ports/Store': 'edited' }), base, 'a port it implements');
  assert.equal(at({ 'Directory.Build.props': 'edited' }), base, 'a shared build file never stales a completed package');
  assert.equal(at({ 'src/Daoris.Desktop/Directory.Build.props': 'edited' }), base);
  assert.equal(at({ 'src/Layers.props': 'edited' }), base);
  assert.equal(at({ 'daoris.gates.json': '{}' }), base);
  assert.notEqual(at({ 'src/Daoris.Desktop/Directory.Packages.props': PINS.replace('10.0.14', '10.0.15') }), base, 'the pin of what it drives');
});

test('a package of several projects is fingerprinted over the sources and evaluation cases its manifest names, each a planted input', () => {
  const root = 'src/Daoris.Assistant';
  const manifest = JSON.stringify({
    suite: 'assistant', tests: `${root}/Daoris.Assistant.Tests`, contract: [], ports: ['Clocks'], outside: [],
    sources: [`${root}/Daoris.Assistant`, `${root}/Daoris.Assistant.Testing`], evals: `${root}/evals`,
  });
  const sight = (changes: Record<string, string> = {}): Sight => {
    const hashes: Record<string, string> = {
      [root]: 'tree-root', [`${root}/Daoris.Assistant`]: 'core', [`${root}/Daoris.Assistant.Testing`]: 'fakes',
      [`${root}/Daoris.Assistant.Tests`]: 'suite', [`${root}/evals`]: 'cases', [`${root}/Daoris.Assistant.Desktop`]: 'adapter',
      'src/Daoris.Ports/Clocks': 'clocks', ...changes,
    };
    return {
      tree: (path) => hashes[path] ?? null, text: (path) => (path === `${root}/daoris.package.json` ? manifest : null), dirty: () => [],
      packages: () => [{ name: 'Daoris.Assistant', folder: root }],
    };
  };
  const base = fingerprintOf('Daoris.Assistant', sight()).fingerprint;
  assert.notEqual(fingerprintOf('Daoris.Assistant', sight({ [`${root}/Daoris.Assistant`]: 'edited' })).fingerprint, base, 'its core');
  assert.notEqual(fingerprintOf('Daoris.Assistant', sight({ [`${root}/Daoris.Assistant.Testing`]: 'edited' })).fingerprint, base, 'its fakes');
  assert.notEqual(fingerprintOf('Daoris.Assistant', sight({ [`${root}/Daoris.Assistant.Tests`]: 'edited' })).fingerprint, base, 'its suite');
  assert.notEqual(fingerprintOf('Daoris.Assistant', sight({ [`${root}/evals`]: 'edited' })).fingerprint, base, 'its evaluation cases');
  assert.notEqual(fingerprintOf('Daoris.Assistant', sight({ 'src/Daoris.Ports/Clocks': 'edited' })).fingerprint, base, 'a port it names');
  assert.equal(fingerprintOf('Daoris.Assistant', sight({ [`${root}/Daoris.Assistant.Desktop`]: 'edited' })).fingerprint, base, 'its desktop adapter is reached like any code');
  assert.equal(fingerprintOf('Daoris.Assistant', sight({ [root]: 'edited' })).fingerprint, base, 'the root\'s other folders are not its sources');
});

test('a pin is read from the versions file the manifest names, through the properties that file sets', () => {
  const versions = 'src/Daoris.Service/Directory.Packages.props';
  const manifest = JSON.stringify({ ...JSON.parse(MANIFEST), outside: ['nuget:Lyntai.Core'], pins: versions });
  const props = (released: string) => [
    `<DaorisLyntaiReleased>${released}</DaorisLyntaiReleased>`,
    '<DaorisLyntaiVersion Condition="\'$(DaorisLyntaiVersion)\' == \'\'">$(DaorisLyntaiReleased)</DaorisLyntaiVersion>',
    '<PackageVersion Include="Lyntai.Core" Version="$(DaorisLyntaiVersion)" />',
  ].join('\n');
  const read = (released: string) => fingerprintOf(NAME, commit({ [AT]: manifest, [versions]: props(released) }));

  assert.deepEqual(read('3.5.3').outside, { 'nuget:Lyntai.Core': '3.5.3' });
  assert.notEqual(read('3.5.4').fingerprint, read('3.5.3').fingerprint, 'a release of what it drives');
  const unset = commit({ [AT]: manifest, [versions]: '<PackageVersion Include="Lyntai.Core" Version="$(Nowhere)" />' });
  assert.throws(() => fingerprintOf(NAME, unset), /the pin of nuget:Lyntai\.Core/, 'a property the file never sets is no version');
});

test('a tool pin is the versions the tools table of resources.json holds, and moves when one is added or changed', () => {
  const manifest = JSON.stringify({ ...JSON.parse(MANIFEST), outside: ['tool:git'] });
  const table = (...versions: string[]) =>
    JSON.stringify({ tools: { git: { versions: Object.fromEntries(versions.map((version) => [version, { files: {} }])) }, node: { versions: { '24.21.0': {} } } } });
  const resources = 'src/Daoris.Desktop/Daoris.Desktop.Driver/resources.json';
  const read = (...versions: string[]) => fingerprintOf(NAME, commit({ [AT]: manifest, [resources]: table(...versions) }));

  assert.deepEqual(read('2.56.0').outside, { 'tool:git': '2.56.0' });
  assert.notEqual(read('2.57.0').fingerprint, read('2.56.0').fingerprint, 'the git floor moved');
  assert.notEqual(read('2.56.0', '2.57.0').fingerprint, read('2.56.0').fingerprint, 'a version added');
  const none = commit({ [AT]: manifest, [resources]: JSON.stringify({ tools: { node: {} } }) });
  assert.throws(() => fingerprintOf(NAME, none), /the pin of tool:git/, 'a tool the table does not hold is no version');
});

test('a port it does not implement, a pin it does not read and a file no input names move nothing', () => {
  const base = at();
  assert.equal(at({ 'src/Daoris.Ports/Git': 'edited' }), base);
  assert.equal(at({ 'src/Daoris.Desktop/Directory.Packages.props': PINS.replace('"Other" Version="1"', '"Other" Version="2"') }), base);
  assert.equal(at({ 'src/Daoris.Desktop/Daoris.Desktop.Driver/Store/DesktopStore.cs': 'edited' }), base);
});

test('a package built over another moves with it, and a cycle is refused', () => {
  const over = JSON.stringify({ ...JSON.parse(MANIFEST), over: ['Daoris.Adapters.Base'] });
  const base = JSON.stringify({ suite: 's', tests: 'src/Daoris.Adapters/Daoris.Adapters.Base.Tests', contract: [], ports: [], outside: [] });
  const sight = (baseTree: string): Sight => {
    const inner = commit({ [AT]: over, 'src/Daoris.Adapters/Daoris.Adapters.Base': baseTree, 'src/Daoris.Adapters/Daoris.Adapters.Base.Tests': 'tree-base-suite' });
    return {
      ...inner,
      text: (path) => (path === 'src/Daoris.Adapters/Daoris.Adapters.Base/daoris.package.json' ? base : inner.text(path)),
      packages: () => [{ name: NAME, folder: FOLDER }, { name: 'Daoris.Adapters.Base', folder: 'src/Daoris.Adapters/Daoris.Adapters.Base' }],
    };
  };
  assert.notEqual(fingerprintOf(NAME, sight('one')).fingerprint, fingerprintOf(NAME, sight('two')).fingerprint);

  const cycle = { ...sight('one'), text: (path: string) => (path.endsWith('Base/daoris.package.json') ? JSON.stringify({ ...JSON.parse(base), over: [NAME] }) : sight('one').text(path)) };
  assert.throws(() => fingerprintOf(NAME, cycle), /built over itself/);
});

test('a package that declares no manifest fails its fingerprint, naming the declaration it lacks', () => {
  const sight: Sight = { ...commit(), text: () => null };
  assert.throws(() => fingerprintOf(NAME, sight), /no src\/Daoris\.Adapters\/Daoris\.Adapters\.Store\/daoris\.package\.json/);
});

const row = (fingerprint: string, evals?: LockRow['evals']): LockRow => ({ fingerprint, tree: 't', ports: {}, outside: {}, suite: 's', toolchain: 'x', passed: 'now', ...evals ? { evals } : {} });

test('a row equal to the fingerprint is kept, a different one stale and none missing', () => {
  const sight = commit();
  const lock = (fingerprint: string): Lock => withRow(emptyLock(), NAME, row(fingerprint));

  assert.equal(judge(lock(at()), sight)[0]?.state, 'kept');
  assert.equal(judge(lock('other'), sight)[0]?.state, 'stale');
  assert.equal(judge(emptyLock(), sight)[0]?.state, 'missing');
  assert.equal(judge(lock(at()), commit({ 'Directory.Build.props': 'edited' }))[0]?.state, 'kept', 'a shared build change asks for no release');
  assert.equal(judge(lock(at()), commit({ [FOLDER]: 'edited' }))[0]?.state, 'stale', 'its own sources do');
});

test('the lock keeps its rows in name order, and a row its evaluations\' result', () => {
  const lock = withRow(withRow(emptyLock(), 'Daoris.Adapters.Zed', row('f')), 'Daoris.Assistant', row('g', { tier: 'room', cases: 30, passed: 30 }));
  assert.deepEqual(Object.keys(lock.packages), ['Daoris.Adapters.Zed', 'Daoris.Assistant']);
  assert.equal(lock.schema, 2);
  assert.deepEqual(lock.packages['Daoris.Assistant']?.evals, { tier: 'room', cases: 30, passed: 30 });
});
