import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readManifest } from '../src/config.ts';
import { isDeclared, registration } from '../src/connect.ts';
import { readLanes } from '../src/lanes.ts';
import { driverRows as csharpRows } from './_csharp.ts';
import { makeFixture } from './_fixture.ts';

/**
 * What `connect` would send from a manifest (WSSETUP5, D124 §3.3) — the CLI's half of a TWIN with the driver's
 * `LineRegistration`, which composes the same body from a repository's LINE and registers it with no `connect` run.
 * 🔴 `LineRegistrationTests.cs` holds the same table, row for row and in the same order, and the last test here
 * holds it to this one, cell for cell.
 *
 * Each row runs what `connect` runs before it sends: `readManifest`, then `isDeclared`, then `readLanes`, then
 * `registration()`. Its answer is the body (`@root` for the checkout's root), `declares nothing` where `connect`
 * refuses for want of a declaration, or `refused: manifest` or `refused: lanes` where it refuses a file. The
 * repository is `game` in every row.
 */

const LOCAL = 'http://localhost:5177';
const REMOTE = 'https://daoris.example.com';

const ROWS: [string, string, string | null, string, string][] = [
  ['a declared manifest, to this machine\'s service: the root travels', '{"source":"daoris@0.0.1","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"]}}', null, 'local', '{"repository":"game","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"]},"join":false,"shareKnowledge":false,"lanes":[],"root":"@root"}'],
  ['to a remote service: no root', '{"source":"daoris@0.0.1","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"]}}', null, 'remote', '{"repository":"game","packs":["web"],"domain":{"summary":"The game","owns":["play"],"accepts":["bugs"]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['no packs is none', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['uses by its rule: trimmed, a repeat in any case, a blank and its own name dropped', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":[" engine","ENGINE","Game",""]}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[],"uses":["engine"]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['uses that names nothing is no field at all', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":["game","  "]}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['uses that is not a list is none', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":"engine"}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['a field the domain carries beyond the four travels as written', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"notes":"kept"}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[],"notes":"kept"},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['join and knowledge, as explicit booleans', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]},"remote":{"join":true,"knowledge":true}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":true,"shareKnowledge":true,"lanes":[]}'],
  ['join alone shares no knowledge', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]},"remote":{"join":true}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":true,"shareKnowledge":false,"lanes":[]}'],
  ['a remote of null is local', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]},"remote":null}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['the lanes\' words, trimmed, never their paths, the steward\'s mark explicit', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', '{"lanes":[{"id":"cli","title":" CLI ","summary":"The package","paths":["src/**"]},{"id":"records","title":"Records","paths":["TASKS.md"],"steward":true}]}', 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[{"id":"cli","title":"CLI","summary":"The package","steward":false},{"id":"records","title":"Records","summary":"","steward":true}]}'],
  ['a lanes file that names none is an explicit empty list', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', '{"lanes":[]}', 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['a lanes file with no lanes field is none', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', '{}', 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['owns alone declares', '{"source":"daoris@0.0.1","domain":{"summary":"","owns":["play"],"accepts":[]}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"","owns":["play"],"accepts":[]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['no domain declares nothing', '{"source":"daoris@0.0.1","packs":[]}', null, 'local', 'declares nothing'],
  ['a domain of blanks declares nothing', '{"source":"daoris@0.0.1","domain":{"summary":"  ","owns":[],"accepts":[]}}', null, 'local', 'declares nothing'],
  ['a manifest that is not JSON is refused', '{"source":', null, 'local', 'refused: manifest'],
  ['JSON that is not an object is refused', '[]', null, 'local', 'refused: manifest'],
  ['no source is refused', '{"packs":[],"domain":{"summary":"The game","owns":[],"accepts":[]}}', null, 'local', 'refused: manifest'],
  ['knowledge without join is refused', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]},"remote":{"knowledge":true}}', null, 'local', 'refused: manifest'],
  ['a manifest refused and a lanes file refused: the manifest is said', '{"packs":[]}', '{"lanes":', 'local', 'refused: manifest'],
  ['a lanes file that is not JSON is refused whole', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', '{"lanes":', 'local', 'refused: lanes'],
  ['a lanes file that is not an object is refused whole', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', '[]', 'local', 'refused: lanes'],
  ['a lane with no paths is refused whole', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', '{"lanes":[{"id":"cli","title":"CLI"}]}', 'local', 'refused: lanes'],
  ['a lane id outside the alphabet is refused whole', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', '{"lanes":[{"id":"CLI","paths":["src/**"]}]}', 'local', 'refused: lanes'],
  ['two stewards are refused whole', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[]}}', '{"lanes":[{"id":"a","paths":["a/**"],"steward":true},{"id":"b","paths":["b/**"],"steward":true}]}', 'local', 'refused: lanes'],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":["straße","STRASSE"]}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[],"uses":["straße","STRASSE"]},"join":false,"shareKnowledge":false,"lanes":[]}'],
  ['a dotted capital I is not an i with a dot above', '{"source":"daoris@0.0.1","domain":{"summary":"The game","owns":[],"accepts":[],"uses":["İzmir","i\\u0307zmir"]}}', null, 'remote', '{"repository":"game","packs":[],"domain":{"summary":"The game","owns":[],"accepts":[],"uses":["İzmir","i\\u0307zmir"]},"join":false,"shareKnowledge":false,"lanes":[]}'],
];

/** What `connect` does with a checkout holding these files, up to what it would send. */
function connectWould(root: string, service: string): unknown {
  let manifest;
  try {
    manifest = readManifest(root);
  } catch {
    return 'refused: manifest';
  }
  if (!isDeclared(manifest.domain)) return 'declares nothing';
  let lanes;
  try {
    lanes = readLanes(root) ?? [];
  } catch {
    return 'refused: lanes';
  }
  // Through JSON, as it goes on the wire: an absent field is absent, not undefined.
  return JSON.parse(JSON.stringify(registration(root, manifest, 'game', service === 'local' ? LOCAL : REMOTE, undefined, lanes)));
}

test('a manifest registers as connect would send it, row for row as the driver composes it from a line', () => {
  for (const [index, [name, manifest, lanes, service, expected]] of ROWS.entries()) {
    const fx = makeFixture(`connect-twin-${index}`);
    fx.write('daoris.json', manifest);
    if (lanes !== null) fx.write('daoris.lanes.json', lanes);

    const answer = connectWould(fx.root, service);

    const wanted = expected.startsWith('{') ? JSON.parse(expected.replace('"@root"', JSON.stringify(fx.root))) : expected;
    assert.deepEqual(answer, wanted, name);
    fx.cleanup();
  }
});

// ——— The twin, held: the driver's table is this table, row for row and in this order.

const DRIVER_TABLE = join(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'LineRegistrationTests.cs');

test('the driver’s table is this table, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLE, 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(source, 'A_manifest_on_a_line_registers_as_connect_would_send_it', {}, 'LineRegistrationTests'), ROWS);
});
