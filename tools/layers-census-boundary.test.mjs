import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { census } from './layers-census.mjs';
import { checkBoundaries, scanBoundary } from './layers-census/boundary.mjs';
import { counts } from './layers-census/counts.mjs';

const PROJECT = '<Project Sdk="Microsoft.NET.Sdk"></Project>';

/** A tree of two lanes carved out of one application, the way the assistant's is out of the driver, its tests and a test project. */
const sources = (overrides = {}) => ({
  'daoris.lanes.json': JSON.stringify({
    lanes: [
      { id: 'assistant', paths: ['src/App/Help/**', 'src/App/Roles/AskDaoris*.cs'] },
      { id: 'plugins', paths: ['src/App/Plugins/**'] },
      { id: 'app', paths: ['src/App/**', '!src/App/Help/**', '!src/App/Roles/AskDaoris*.cs', '!src/App/Plugins/**'] },
    ],
  }),
  'src/App/App.csproj': PROJECT,
  'src/App.Tests/App.Tests.csproj': PROJECT,
  'src/App/Help/HelpRoom.cs': 'public sealed class HelpRoom { Driver driver; RoleCatalog roles; public record struct Section(string Name); }',
  'src/App/Roles/AskDaorisReport.cs': 'public sealed record AskDaorisReport(RoleCatalog Roles); public enum Shared { A }',
  'src/App/Roles/RoleCatalog.cs': 'public sealed class RoleCatalog { }',
  'src/App/Loop/Driver.cs': [
    'public sealed class Driver {',
    '  HelpRoom room = new HelpRoom(); // a HelpRoom named in a comment is not a reference',
    '  string said = "HelpRoom in a string is not one either";',
    '  AskDaorisReport report; PluginCatalog plugins;',
    '}',
    'public interface Shared { }',
  ].join('\n'),
  'src/App/Plugins/PluginCatalog.cs': 'public sealed class PluginCatalog { RoleCatalog roles; }',
  'src/App.Tests/HelpRoomTests.cs': 'public sealed class HelpRoomTests { HelpRoom room; Driver driver; }',
  ...overrides,
});

const tree = (files) => ({ files: Object.keys(files), read: (path) => files[path] });

test('a lane\'s references across its boundary are counted both ways per type, and a comment, a string and a test project are not', () => {
  const found = scanBoundary(tree(sources()), 'assistant');
  assert.deepEqual(found.inbound, { AskDaorisReport: 1, HelpRoom: 2 }, 'the outside names the lane\'s types; its tests do not count');
  assert.deepEqual(found.outbound, { Driver: 1, RoleCatalog: 2 }, 'the lane names the outside\'s types, another lane\'s among them');
  assert.deepEqual(found.shared, ['Shared'], 'a name declared on both sides cannot say which is meant, so it is set aside');
  assert.deepEqual(found.files, { inside: 2, outside: 3 });
  assert.throws(() => scanBoundary(tree(sources()), 'nowhere'), /no lane 'nowhere'/);
});

test('the lane\'s own package, which its build holds to its ports, is neither side of the count', () => {
  const lanes = JSON.parse(sources()['daoris.lanes.json']);
  lanes.lanes[0].paths.push('src/Daoris.Assistant/**');
  const packaged = sources({
    'daoris.lanes.json': JSON.stringify(lanes),
    'src/Daoris.Assistant/Daoris.Assistant/Daoris.Assistant.csproj': PROJECT,
    'src/Daoris.Assistant/Daoris.Assistant/Ports/IRooms.cs': 'public interface IRooms { Driver Driver { get; } RoleCatalog Roles { get; } } public sealed record PluginCatalog();',
    'src/Daoris.Assistant/Daoris.Assistant.Tests/Daoris.Assistant.Tests.csproj': PROJECT,
    'src/Daoris.Assistant/Daoris.Assistant.Tests/RoomsTests.cs': 'public sealed class RoomsTests { Driver driver; IRooms rooms; }',
    'src/App/Loop/Rooms.cs': 'public sealed class Rooms { IRooms rooms; }',
  });
  const found = scanBoundary(tree(packaged), 'assistant');
  const before = scanBoundary(tree(sources({ 'src/App/Loop/Rooms.cs': 'public sealed class Rooms { IRooms rooms; }' })), 'assistant');
  assert.deepEqual({ ...found, files: found.files.inside }, { ...before, files: before.files.inside });
  assert.deepEqual(found.files, { inside: 2, outside: 4 });
});

test('a planted new reference across the boundary is red either way, and a fall is told to lower the list', () => {
  const held = scanBoundary(tree(sources()), 'assistant');
  const list = { assistant: { inbound: held.inbound, outbound: held.outbound } };
  assert.deepEqual(checkBoundaries(tree(sources()), list), []);

  const planted = sources({ 'src/App/Loop/Look.cs': 'public sealed class Look { HelpRoom room; }' });
  assert.match(checkBoundaries(tree(planted), list).join('\n'), /lane assistant: the outside names its type HelpRoom 3 times, over the ratchet's 2/);
  const reaching = sources({ 'src/App/Help/HelpStart.cs': 'public sealed class HelpStart { Driver driver; PluginCatalog plugins; }' });
  assert.deepEqual(checkBoundaries(tree(reaching), list), [
    'lane assistant: it names the outside type Driver 2 times, over the ratchet\'s 1: reach it through a port of the boundary instead',
    'lane assistant: it names the outside type PluginCatalog 1 times, over the ratchet\'s 0: reach it through a port of the boundary instead',
  ]);
  const fell = sources({ 'src/App/Help/HelpRoom.cs': 'public sealed class HelpRoom { RoleCatalog roles; }' });
  assert.match(checkBoundaries(tree(fell), list).join('\n'), /names the outside type Driver 0 times, under the ratchet's 1: lower the list/);
});

test('the census takes any lane as its argument, and holds each lane its list names on its own', async () => {
  mkdirSync('_fixtures', { recursive: true });
  const root = mkdtempSync(join('_fixtures', 'census-boundary-'));
  const said = [];
  const log = console.log;
  try {
    mkdirSync(join(root, 'tools'), { recursive: true });
    writeFileSync(join(root, 'tools', 'layers-census.json'), '{}');
    console.log = (line) => said.push(line);
    assert.equal(await census({ root, argv: ['--boundary', 'plugins'], tree: tree(sources()) }), 0, 'a lane the list does not hold is counted and said');
    assert.match(said.join('\n'), /lane plugins: 1 sources inside, 4 production sources outside/);
    assert.match(said.join('\n'), /inbound: 1 references to 1 of its types \(PluginCatalog 1\)/);

    assert.equal(await census({ root, argv: ['--write', '--boundary', 'plugins'], tree: tree(sources()) }), 0);
    assert.equal(await census({ root, argv: ['--write', '--boundary', 'assistant'], tree: tree(sources()) }), 0, 'a second lane joins the list beside the first');
    const list = JSON.parse(readFileSync(join(root, 'tools', 'layers-census.json'), 'utf8'));
    assert.deepEqual(Object.keys(list.boundaries), ['assistant', 'plugins']);
    assert.deepEqual(list.boundaries.plugins, { inbound: { PluginCatalog: 1 }, outbound: { RoleCatalog: 1 } });
    assert.equal(counts({ root, tree: tree(sources()) }), 0);

    const planted = sources({ 'src/App/Help/HelpPlugins.cs': 'public sealed class HelpPlugins { PluginCatalog catalog; }' });
    console.log = log;
    assert.equal(counts({ root, tree: tree(planted), warn: () => {} }), 1, 'the reference crosses both lanes\' boundaries, so both are red');
    console.log = (line) => said.push(line);
    assert.equal(await census({ root, argv: ['--boundary', 'plugins'], tree: tree(planted) }), 1, 'the argument judges the lane the list holds');
  } finally {
    console.log = log;
    rmSync(root, { recursive: true, force: true });
  }
});

test('this repository\'s list holds the assistant lane\'s boundary', () => {
  const list = JSON.parse(readFileSync(new URL('./layers-census.json', import.meta.url), 'utf8'));
  assert.ok(list.boundaries?.assistant, 'node tools/layers-census.mjs --write --boundary assistant writes it');
  assert.ok(Object.keys(list.boundaries.assistant.inbound).length > 0 && Object.keys(list.boundaries.assistant.outbound).length > 0);
});
