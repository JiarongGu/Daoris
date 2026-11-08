import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { LOCK, OUTPUTS, sourceHash, staleness, writeContracts } from './contracts.mjs';

const REPO = dirname(dirname(fileURLToPath(import.meta.url)));
mkdirSync(join(REPO, '_fixtures'), { recursive: true });

const sources = {
  'src/Daoris.Contracts/Knowledge/KnowledgeContracts.cs': 'public sealed record Search(string Q);\n',
  'src/Daoris.Contracts/Daoris.Contracts.csproj': '<Project />\n',
  'src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs': '[DriverRoute("SET_CAP", PipeClass.Person)]\n',
  'tools/contracts/PageTypes.cs': 'public sealed class PageTypes;\n',
};
const hashOf = (files) => sourceHash(Object.keys(files), (path) => files[path]);

test('CODESHAPE1: the stamp is the declaring sources\' hash, the same with either line ending, and blind to the rest', () => {
  const hash = hashOf(sources);
  assert.match(hash, /^[0-9a-f]{64}$/);
  const crlf = Object.fromEntries(Object.entries(sources).map(([path, text]) => [path, text.replace(/\n/g, '\r\n')]));
  assert.equal(hashOf(crlf), hash);
  assert.equal(hashOf({
    ...sources, 'src/Daoris.Contracts/obj/Gen.cs': 'x', 'tools/contracts/bin/x.cs': 'x', 'src/Daoris.Web/src/a.ts': 'x', 'tools/contracts/notes.md': 'x',
    'src/Daoris.Desktop/Daoris.Desktop.Modules/obj/Gen.cs': 'x', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs': 'x',
  }), hash);
  assert.notEqual(hashOf({ ...sources, 'src/Daoris.Contracts/Knowledge/KnowledgeContracts.cs': 'public sealed record Search(string Q, int? Limit);\n' }), hash);
  assert.notEqual(hashOf({ ...sources, 'tools/contracts/PageTypes.cs': 'public sealed class PageTypes { }\n' }), hash);
  // A window route's mark moved is the page's routes moved.
  assert.notEqual(hashOf({ ...sources, 'src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs': '[DriverRoute("SET_CAPS", PipeClass.Person)]\n' }), hash);
});

test('NODEKIT1: a kind\'s own file stales the page, read by its name; its door and its tests stale nothing', () => {
  const kinds = 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Kinds/';
  const hash = hashOf(sources);
  const monitor = { ...sources, [`${kinds}Monitor/MonitorNode.cs`]: 'class MonitorNode;\n' };
  assert.notEqual(hashOf(monitor), hash);
  assert.notEqual(hashOf({ ...sources, [`${kinds}NodeGroup.cs`]: 'enum NodeGroup;\n' }), hash);
  assert.equal(hashOf({
    ...sources, [`${kinds}Monitor/MonitorReading.cs`]: 'x', [`${kinds}Tidy/TidyDoor.cs`]: 'x', [`${kinds}Monitor/obj/MonitorNode.cs`]: 'x',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeRuntime.cs': 'x',
  }), hash);
});

test('AGENTPROVIDER1t: a provider\'s own files and the registration list stale the page, whose vocabulary they declare; its layer and program stale nothing', () => {
  const agents = 'src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/';
  const hash = hashOf(sources);
  assert.notEqual(hashOf({ ...sources, [`${agents}Providers/Codex/CodexProvider.cs`]: 'class CodexProvider;\n' }), hash);
  assert.notEqual(hashOf({ ...sources, [`${agents}Providers/ClaudeCode/ClaudeTrust.cs`]: 'class ClaudeTrust;\n' }), hash);
  assert.notEqual(hashOf({ ...sources, [`${agents}Registry/Providers.cs`]: 'class Providers;\n' }), hash);
  assert.notEqual(hashOf({ ...sources, [`${agents}Registry/ProviderVocabulary.cs`]: 'class ProviderVocabulary;\n' }), hash);
  assert.equal(hashOf({
    ...sources, [`${agents}Providers/Codex/Layer/CodexLayerDoor.cs`]: 'x', [`${agents}Providers/Stub/Program/agent.mjs`]: 'x',
    [`${agents}Providers/Stub/pending.json`]: 'x', [`${agents}Registry/ProviderRegistry.cs`]: 'x', [`${agents}AcpProtocol.cs`]: 'x',
  }), hash);
});

test('CODESHAPE1: a file whose stamp is the hash is fresh; one missing, unstamped or stamped otherwise names itself and the command', () => {
  const hash = hashOf(sources);
  for (const output of Object.values(OUTPUTS)) {
    assert.equal(staleness(`// contracts: ${hash}\nexport type Search = { q: string };\n`, hash, output), null);
    assert.equal(staleness(`// contracts: ${hash}\r\nexport type Search = { q: string };\r\n`, hash, output), null);
    assert.match(staleness(null, hash, output), new RegExp(`${output.replace(/[.]/g, '\\.')} is not written: run \`node tools/contracts\\.mjs\``));
    assert.match(staleness('export type Search = { q: string };\n', hash, output), /is stale: .*Run `node tools\/contracts\.mjs`/);
    assert.match(staleness(`// contracts: ${'0'.repeat(64)}\n`, hash, output), /is stale/);
    assert.match(staleness('', hash, output), /is stale/);
  }
  assert.deepEqual(Object.values(OUTPUTS), ['src/Daoris.Web/src/data/generated/service.ts', 'src/Daoris.Web/src/bridge/generated/routes.ts']);
});

/** A generator that is a test's: what it is asked to build, and the files as `look` reads them, stale or fresh. */
const stand = (why) => {
  const asked = { builds: [], looks: [] };
  return { asked, look: (root) => { asked.looks.push(existsSync(join(root, LOCK))); return { hash: 'h', why }; }, build: (_, hash) => asked.builds.push(hash) > 0 };
};

test('two web gates writing the page\'s types at once take turns: the one that waited reads the files again and builds nothing', async () => {
  const root = mkdtempSync(join(REPO, '_fixtures', 'contracts-lock-'));
  try {
    // The first run, in a process of its own, holds the lock through its build.
    const first = spawn(process.execPath, ['--input-type=module', '-e', [
      `import { withRunLock } from ${JSON.stringify(new URL('./run-lock.mjs', import.meta.url).href)};`,
      "withRunLock(process.argv[1], () => { console.log('held'); Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1500); });",
    ].join('\n'), join(root, LOCK)], { stdio: ['ignore', 'pipe', 'inherit'], windowsHide: true });
    const ended = new Promise((done) => first.once('exit', done));
    await new Promise((held) => first.stdout.once('data', held));

    const second = stand(null);
    const started = Date.now();
    assert.equal(writeContracts(root, '--if-stale', second), 0);
    assert.ok(Date.now() - started >= 1000, 'the second run built beside the first');
    assert.deepEqual(second.asked, { builds: [], looks: [true] }, 'it read the files again under the lock, found them written and built nothing');
    assert.equal(await ended, 0);
    assert.equal(existsSync(join(root, LOCK)), false, 'each run let the lock go');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a lock left by a run that died is taken at once, and the files are built', () => {
  const root = mkdtempSync(join(REPO, '_fixtures', 'contracts-lock-'));
  try {
    const dead = spawnSync(process.execPath, ['-e', ''], { windowsHide: true }).pid;
    mkdirSync(dirname(join(root, LOCK)), { recursive: true });
    writeFileSync(join(root, LOCK), `${JSON.stringify({ pid: dead, since: new Date().toISOString() })}\n`);
    const run = stand('stale');
    const started = Date.now();
    assert.equal(writeContracts(root, '--if-stale', run), 0);
    assert.ok(Date.now() - started < 5000, 'it waited on a holder that was gone');
    assert.deepEqual(run.asked, { builds: ['h'], looks: [true] });
    assert.equal(existsSync(join(root, LOCK)), false);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

const LOCAL_PACKS = [
  { props: 'lyntai-local.props', version: 'DaorisLyntaiVersion', feed: 'lyntai-feed', stand: '3.5.3-local.abc1234' },
  { props: 'shenora-local.props', version: 'DaorisShenoraVersion', feed: 'shenora-feed', stand: '0.25.0-local.abc1234' },
];

test('LYNTAILOCAL1: the generator restores against the local sibling packs the desktop imports, its Lyntai and its Shenora', () => {
  const fixtures = join(REPO, '_fixtures', 'contracts-local');
  const written = LOCAL_PACKS.map((pack) => join(REPO, 'local', pack.props));
  const had = written.map((file) => (existsSync(file) ? readFileSync(file) : null));
  const evaluate = () => {
    const run = spawnSync('dotnet', ['msbuild', join(REPO, 'tools', 'contracts', 'ContractsGen.csproj'), '-getProperty:RestoreAdditionalProjectSources',
      ...LOCAL_PACKS.map((pack) => `-getProperty:${pack.version}`)], { encoding: 'utf8', windowsHide: true });
    assert.equal(run.status, 0, run.stdout + run.stderr);
    return JSON.parse(run.stdout).Properties;
  };
  try {
    mkdirSync(join(REPO, 'local'), { recursive: true });
    LOCAL_PACKS.forEach((pack, at) => writeFileSync(written[at], `<Project><PropertyGroup><${pack.version}>${pack.stand}</${pack.version}>`
      + `<RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);${join(fixtures, pack.feed).replaceAll('\\', '/')}</RestoreAdditionalProjectSources></PropertyGroup></Project>`));
    const seen = evaluate();
    for (const pack of LOCAL_PACKS) {
      assert.equal(seen[pack.version], pack.stand, `${pack.version} is the local pack's`);
      assert.ok(String(seen.RestoreAdditionalProjectSources).includes(`contracts-local/${pack.feed}`), `${pack.feed} is a restore source`);
    }
  } finally {
    written.forEach((file, at) => (had[at] === null ? rmSync(file, { force: true }) : writeFileSync(file, had[at])));
  }
});
