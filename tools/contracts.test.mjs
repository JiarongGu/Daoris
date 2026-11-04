import { test } from 'node:test';
import assert from 'node:assert/strict';
import { OUTPUTS, sourceHash, staleness } from './contracts.mjs';

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
