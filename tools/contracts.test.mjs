import { test } from 'node:test';
import assert from 'node:assert/strict';
import { OUTPUT, sourceHash, staleness } from './contracts.mjs';

const sources = {
  'src/Daoris.Contracts/Knowledge/KnowledgeContracts.cs': 'public sealed record Search(string Q);\n',
  'src/Daoris.Contracts/Daoris.Contracts.csproj': '<Project />\n',
  'tools/contracts/PageTypes.cs': 'public sealed class PageTypes;\n',
};
const hashOf = (files) => sourceHash(Object.keys(files), (path) => files[path]);

test('CODESHAPE1: the stamp is the declaring sources\' hash, the same with either line ending, and blind to the rest', () => {
  const hash = hashOf(sources);
  assert.match(hash, /^[0-9a-f]{64}$/);
  const crlf = Object.fromEntries(Object.entries(sources).map(([path, text]) => [path, text.replace(/\n/g, '\r\n')]));
  assert.equal(hashOf(crlf), hash);
  assert.equal(hashOf({ ...sources, 'src/Daoris.Contracts/obj/Gen.cs': 'x', 'tools/contracts/bin/x.cs': 'x', 'src/Daoris.Web/src/a.ts': 'x', 'tools/contracts/notes.md': 'x' }), hash);
  assert.notEqual(hashOf({ ...sources, 'src/Daoris.Contracts/Knowledge/KnowledgeContracts.cs': 'public sealed record Search(string Q, int? Limit);\n' }), hash);
  assert.notEqual(hashOf({ ...sources, 'tools/contracts/PageTypes.cs': 'public sealed class PageTypes { }\n' }), hash);
});

test('CODESHAPE1: a file whose stamp is the hash is fresh; one missing, unstamped or stamped otherwise names the command', () => {
  const hash = hashOf(sources);
  assert.equal(staleness(`// contracts: ${hash}\nexport type Search = { q: string };\n`, hash), null);
  assert.equal(staleness(`// contracts: ${hash}\r\nexport type Search = { q: string };\r\n`, hash), null);
  assert.match(staleness(null, hash), new RegExp(`${OUTPUT.replace(/[.]/g, '\\.')} is not written: run \`node tools/contracts\\.mjs\``));
  assert.match(staleness('export type Search = { q: string };\n', hash), /is stale: .*Run `node tools\/contracts\.mjs`/);
  assert.match(staleness(`// contracts: ${'0'.repeat(64)}\n`, hash), /is stale/);
  assert.match(staleness('', hash), /is stale/);
});
