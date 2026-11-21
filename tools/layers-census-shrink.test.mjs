import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { census } from './layers-census.mjs';
import { checkAmbient, scanAmbient } from './layers-census/ambient.mjs';
import { checkBoundaries } from './layers-census/boundary.mjs';
import { checkClock, scanClock } from './layers-census/clock.mjs';
import { counts } from './layers-census/counts.mjs';
import { checkGit, scanGit } from './layers-census/git.mjs';
import { checkProcesses, scanProcesses } from './layers-census/processes.mjs';
import { checkReferences } from './layers-census/references.mjs';
import { shrunk } from './layers-census/shrink.mjs';

const DESKTOP = 'src/Daoris.Desktop';
const PROJECT = (packages = []) => `<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>${packages.map((name) => `<PackageReference Include="${name}" />`).join('')}</ItemGroup></Project>`;
const A = `${DESKTOP}/Daoris.Desktop.Driver/Loop/A.cs`;
const B = `${DESKTOP}/Daoris.Desktop.Driver/Loop/B.cs`;
const T = `${DESKTOP}/Daoris.Desktop.Driver.Tests/Loop/CTests.cs`;

/** A tree of one application and its unit tests: A reads the clock once, T twice, B reads the ambient ports once. */
const sources = (overrides = {}) => ({
  'daoris.lanes.json': JSON.stringify({ lanes: [{ id: 'driver', paths: ['src/**'] }] }),
  [`${DESKTOP}/Daoris.Desktop.Driver/Daoris.Desktop.Driver.csproj`]: PROJECT(['Lyntai.Core']),
  [`${DESKTOP}/Daoris.Desktop.Driver.Tests/Daoris.Desktop.Driver.Tests.csproj`]: PROJECT(),
  [A]: 'var a = DateTime.UtcNow;',
  [B]: 'var p = AmbientPorts.Clock;',
  [T]: 'Thread.Sleep(5); Thread.Sleep(5);',
  ...overrides,
});
const tree = (files) => ({ files: Object.keys(files), read: (path) => files[path] });

/** A list that holds the tree as it stands, then a loosened one: every allowance above what the tree reads, and one on nothing. */
const loose = {
  packages: { 'Daoris.Desktop.Driver': ['Lyntai.Core', 'Microsoft.Data.Sqlite'], 'Daoris.Gone': ['Lyntai.Core'] },
  ambient: { 'Daoris.Desktop.Driver/Loop': 3, 'Daoris.Desktop.Driver/Store': 1 },
  clock: { [A]: 1, [T]: 5, [B]: 2 },
  processes: { [T]: 4 },
  git: {},
  slowTests: { 'Some.Test': 4000 },
  boundaries: {},
};

test('a shrink lowers each allowance the tree fell under to what it reads, drops one on nothing, and says each', () => {
  const { list, lowered } = shrunk(loose, tree(sources()));
  assert.deepEqual(list.ambient, { 'Daoris.Desktop.Driver/Loop': 1 });
  assert.deepEqual(list.clock, { [A]: 1, [T]: 2 });
  assert.deepEqual(list.processes, {});
  assert.deepEqual(list.packages, { 'Daoris.Desktop.Driver': ['Lyntai.Core'] });
  assert.deepEqual(lowered, [
    'packages Daoris.Desktop.Driver: Microsoft.Data.Sqlite is no longer referenced',
    'packages Daoris.Gone: no longer a project',
    'ambient Daoris.Desktop.Driver/Loop: 3 to 1',
    'ambient Daoris.Desktop.Driver/Store: 1 to none',
    `clock ${T}: 5 to 2`,
    `clock ${B}: 2 to none`,
    `processes ${T}: 4 to none`,
  ]);
});

test('a shrink changes nothing else: a slow test, an allowance the tree is over and a key the list lacks stay as they are', () => {
  const grown = { ...loose, clock: { [A]: 0 + 1, [T]: 1 }, ambient: { 'Daoris.Desktop.Driver/Loop': 3 } };
  const { list } = shrunk(grown, tree(sources({ [B]: 'var p = AmbientPorts.Clock; var q = AmbientPorts.Git;' })));
  assert.deepEqual(list.slowTests, { 'Some.Test': 4000 }, 'the machine\'s timings are not written');
  assert.equal(list.clock[T], 1, 'a file over its allowance is a breach for a person, never raised');
  assert.equal(list.ambient['Daoris.Desktop.Driver/Loop'], 2, 'lowered to what is read, and no lower');
  assert.equal(list.clock[B], undefined, 'a file that holds no allowance gets none');
  assert.deepEqual(Object.keys(list), Object.keys(grown), 'the sections keep their order');
});

test('a list the tree already holds is returned as it is, and says nothing', () => {
  const held = { packages: { 'Daoris.Desktop.Driver': ['Lyntai.Core'] }, ambient: { 'Daoris.Desktop.Driver/Loop': 1 }, clock: { [A]: 1, [T]: 2 }, processes: {}, git: {}, slowTests: { x: 1 }, boundaries: {} };
  const { list, lowered } = shrunk(held, tree(sources()));
  assert.deepEqual(list, held);
  assert.deepEqual(lowered, []);
});

test('a lane\'s boundary counts are lowered per type, a type read no more leaves, and the lane stays', () => {
  const files = sources({
    'daoris.lanes.json': JSON.stringify({ lanes: [{ id: 'help', paths: ['src/Help/**'] }, { id: 'app', paths: ['src/Daoris.Desktop/**'] }] }),
    'src/Help/HelpRoom.cs': 'public sealed class HelpRoom { Driver driver; }',
    [`${DESKTOP}/Daoris.Desktop.Driver/Loop/Driver.cs`]: 'public sealed class Driver { HelpRoom room; }',
  });
  const list = { ...loose, boundaries: { help: { inbound: { HelpRoom: 3, Gone: 2 }, outbound: { Driver: 1 } }, nowhere: { inbound: { X: 1 }, outbound: {} } } };
  const { list: out, lowered } = shrunk(list, tree(files));
  assert.deepEqual(out.boundaries.help, { inbound: { HelpRoom: 1 }, outbound: { Driver: 1 } });
  assert.deepEqual(out.boundaries.nowhere, { inbound: { X: 1 }, outbound: {} }, 'a lane the lanes file no longer holds is for a person');
  assert.ok(lowered.includes('boundaries help inbound HelpRoom: 3 to 1'));
  assert.ok(lowered.includes('boundaries help inbound Gone: 2 to none'));
});

test('--shrink writes the list with only the allowances lowered, names them, and leaves the file untouched where none fell', async () => {
  mkdirSync('_fixtures', { recursive: true });
  const root = mkdtempSync(join('_fixtures', 'census-shrink-'));
  const said = [];
  const log = console.log;
  try {
    mkdirSync(join(root, 'tools'), { recursive: true });
    const file = join(root, 'tools', 'layers-census.json');
    writeFileSync(file, `${JSON.stringify(loose, null, 2)}\n`);
    console.log = (line) => said.push(line);
    assert.equal(await census({ root, argv: ['--shrink'], tree: tree(sources()) }), 0);
    const written = JSON.parse(readFileSync(file, 'utf8'));
    const expected = shrunk(loose, tree(sources())).list;
    assert.deepEqual(written, expected);
    assert.deepEqual(written.slowTests, loose.slowTests);
    assert.match(said.join('\n'), /layers-census: lowered 7 allowances/);
    assert.match(said.join('\n'), /clock .*CTests\.cs: 5 to 2/);
    console.log = log;
    assert.equal(counts({ root, tree: tree(sources()), warn: () => {} }), 0, 'the shrunk list is what the counts hold');

    const before = readFileSync(file, 'utf8');
    said.length = 0;
    console.log = (line) => said.push(line);
    assert.equal(await census({ root, argv: ['--shrink'], tree: tree(sources()) }), 0);
    assert.equal(readFileSync(file, 'utf8'), before);
    assert.match(said.join('\n'), /no allowance fell/);
  } finally {
    console.log = log;
    rmSync(root, { recursive: true, force: true });
  }
});

test('each red a fall causes names --shrink, which only lowers, and not --write, which also fills this machine\'s timings', () => {
  const found = tree(sources());
  const reds = [
    ...checkAmbient(scanAmbient(found), { 'Daoris.Desktop.Driver/Loop': 3 }),
    ...checkClock(scanClock(found), { [A]: 2, [T]: 2 }),
    ...checkProcesses(scanProcesses(found), { [T]: 1 }),
    ...checkGit(scanGit(found), { [T]: 1 }),
    ...checkReferences(found, { packages: { 'Daoris.Desktop.Driver': ['Lyntai.Core', 'Other'], Gone: ['x'] } }),
    ...checkBoundaries(tree(sources({ 'src/Help/HelpRoom.cs': 'public sealed class HelpRoom { }', 'daoris.lanes.json': JSON.stringify({ lanes: [{ id: 'help', paths: ['src/Help/**'] }, { id: 'app', paths: ['src/Daoris.Desktop/**'] }] }) })), { help: { inbound: { HelpRoom: 1 }, outbound: {} } }),
  ];
  assert.equal(reds.length, 7, reds.join(' | '));
  for (const red of reds) {
    assert.match(red, /node tools\/layers-census\.mjs --shrink/, red);
    assert.doesNotMatch(red, /--write/, red);
  }
});
